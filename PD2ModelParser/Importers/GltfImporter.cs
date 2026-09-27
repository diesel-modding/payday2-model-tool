using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GLTF = SharpGLTF.Schema2;
using DM = PD2ModelParser.Sections;

namespace PD2ModelParser.Importers
{
    internal class GltfImporter(FullModelData data)
    {
        public static void Import(FullModelData fmd, string path, bool createModels, Func<string, DM.Object3D> parentFinder, IOptionReceiver opts)
        {
            GLTF.ModelRoot gltf = GLTF.ModelRoot.Load(path);
            var importer = new GltfImporter(fmd);

            string preserveSkinsOpt = opts.GetOption("overwrite-rigging");
            if (preserveSkinsOpt != null)
            {
                importer.overwriteRigging = bool.Parse(preserveSkinsOpt);
            }

            string importTransforms = opts.GetOption("import-transforms");
            if (importTransforms != null)
            {
                _ = bool.TryParse(importTransforms, out importer.importTransforms);
            }

            importer.ImportTree(gltf, createModels, parentFinder);
        }

        public static bool ReuseExistingObjects = false;

        private readonly FullModelData data = data;
        private readonly Dictionary<GLTF.Node, DM.Object3D> objectsByNode = [];
        private bool createModels;
        private bool overwriteRigging;
        private bool importTransforms = true;
        private readonly List<(GLTF.Node node, DM.Model model)> toSkin = [];
        private readonly List<(GLTF.Skin skin, DM.Model model)> toRemap = [];

        private readonly float scaleFactor = 100;
        private Matrix4x4 axisCorrection = Matrix4x4.CreateRotationX(MathF.PI / 2);

        public void ImportTree(GLTF.ModelRoot root, bool createModels, Func<string, DM.Object3D> parentFinder)
        {
            this.createModels = createModels;

            foreach (var node in root.DefaultScene.VisualChildren)
            {
                DM.Object3D parent = null;
                try
                {
                    parent = parentFinder(node.Name);
                }
                catch
                { }

                ImportNode(node, parent, axisCorrection);
            }

            foreach (var (node, model) in toSkin)
            {
                ImportSkin(node, model);
            }

            foreach (var (skin, model) in toRemap)
            {
                RemapBoneIds(skin, model);
            }

            ImportAnimations(root);
        }

        private void UpdatePrimitiveModelFromMesh(GLTF.Mesh gmesh, DM.Model model)
        {
            var md = MeshData.FromGltfMesh(gmesh);

            if (md.verts == null || md.verts.Count == 0)
            {
                throw new Exception($"Primitive model {model.Name} has no vertices.");
            }

            Vector3 boundsMin;
            Vector3 boundsMax;
            float DistanceRadius;

            if (model.Name.StartsWith("c_capsule_", StringComparison.OrdinalIgnoreCase))
            {
                (boundsMin, boundsMax, DistanceRadius) = ReconstructCapsuleBounds(md);
            }
            else
            {
                boundsMin = md.verts.Aggregate(MathUtil.Min) * scaleFactor;
                boundsMax = md.verts.Aggregate(MathUtil.Max) * scaleFactor;
                DistanceRadius = CalculateDistanceRadius(boundsMin, boundsMax);
            }

            model.BoundsMin = boundsMin;
            model.BoundsMax = boundsMax;
            model.DistanceRadius = DistanceRadius;
        }

        private void ImportNode(GLTF.Node node, DM.Object3D parent, Matrix4x4 parentCorrection)
        {
            var hashname = HashName.FromNumberOrString(node.Name);
            DM.Object3D obj = null;

            if (ReuseExistingObjects)
            {
                obj = data.parsed_sections
                    .Select(i => i.Value as DM.Object3D)
                    .Where(i => i != null)
                    .FirstOrDefault(i => i.HashName.Hash == hashname.Hash);
            }

            bool shouldSetParent = true;

            if (obj == null)
            {
                if (createModels && node.Mesh == null)
                {
                    obj = new DM.Object3D(hashname.String, parent);
                }
                else if (createModels && node.Mesh != null)
                {
                    if (IsPrimitiveModelName(node.Name))
                    {
                        obj = CreateNewPrimitiveModel(node.Mesh, node.Name, parent);
                    }
                    else
                    {
                        obj = CreateNewModel(node.Mesh, node.Name);

                        if (node.Skin != null)
                        {
                            toSkin.Add((node, obj as DM.Model));
                            toRemap.Add((node.Skin, obj as DM.Model));
                        }
                    }
                }
                else if (createModels && node.PunctualLight != null)
                {
                    obj = CreateNewLamp(node.PunctualLight, node.Name);
                }
                else
                {
                    throw new Exception(
                        $"Object {node.Name} does not already exist " +
                        "and object creation is disabled.");
                }

                data.AddSection(obj);
            }
            else
            {
                if (node.Mesh != null && obj is not DM.Model)
                {
                    if (!createModels)
                    {
                        throw new Exception(
                            $"Object {node.Name} already exists, " +
                            "isn't a model, and object creation is disabled.");
                    }

                    var oldObj = obj;
                    obj = CreateNewModel(node.Mesh, node.Name);

                    foreach (var i in oldObj.children.ToList())
                    {
                        i.SetParent(obj);
                    }

                    data.AddSection(obj);

                    if (node.Skin != null)
                    {
                        toSkin.Add((node, obj as DM.Model));
                        toRemap.Add((node.Skin, obj as DM.Model));
                    }
                }
                else if (node.Mesh != null && obj is DM.Model mod)
                {
                    if (IsPrimitiveModelName(node.Name))
                    {
                        if (mod.Version != 6)
                        {
                            throw new Exception(
                                $"Primitive {node.Name} already exists " +
                                $"as model version {mod.Version}.");
                        }

                        UpdatePrimitiveModelFromMesh(node.Mesh, mod);
                    }
                    else
                    {
                        OverwriteModel(node.Mesh, mod);

                        if (node.Skin != null)
                        {
                            if (overwriteRigging)
                            {
                                toSkin.Add((node, mod));
                            }

                            toRemap.Add((node.Skin, mod));
                        }
                    }
                }
                else if (node.PunctualLight != null)
                {
                    throw new Exception("Can't overwrite lights yet.");
                }

                shouldSetParent = false;
            }

            objectsByNode.Add(node, obj);

            if (parent != null && shouldSetParent)
            {
                obj.SetParent(parent);
            }

            if (importTransforms)
            {
                var lt = node.LocalTransform;
                var m = lt.Matrix;

                m.M41 *= scaleFactor;
                m.M42 *= scaleFactor;
                m.M43 *= scaleFactor;

                if (parentCorrection != Matrix4x4.Identity)
                {
                    m = parentCorrection * m;
                }

                obj.Transform = m;
            }

            (obj as DM.Model)?.UpdateBounds();

            foreach (var child in node.VisualChildren)
            {
                ImportNode(child, obj, Matrix4x4.Identity);
            }
        }

        private void OverwriteModel(GLTF.Mesh gmesh, DM.Model model)
        {
            var md = MeshData.FromGltfMesh(gmesh);

            var mats = md.materials.Select(i =>
            {
                var hn = HashName.FromNumberOrString(i);
                var mat = data.SectionsOfType<DM.Material>()
                    .FirstOrDefault(j => j.HashName.Hash == hn.Hash);

                if (mat == null)
                {
                    mat = new DM.Material(i);
                    data.AddSection(mat);
                }

                return mat;
            }).ToList();

            var matGroup = new DM.MaterialGroup(mats);
            data.AddSection(matGroup);
            model.MaterialGroup = matGroup;

            var ms = new MeshSections
            {
                topoip = model.TopologyIP,
                passgp = model.PassthroughGP,
                geom = model.PassthroughGP.DieselGeometry,
                topo = model.PassthroughGP.Topology,
                atoms = md.renderAtoms
            };

            ms.PopulateFromMeshData(md);
            ms.Scale(scaleFactor);
            model.RenderAtoms = md.renderAtoms;
        }

        private DM.Light CreateNewLamp(GLTF.PunctualLight gl, string name)
        {
            throw new NotImplementedException("Lights are currently not implemented");
        }

        private static bool IsPrimitiveModelName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            return
                name.StartsWith("c_sphere_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("c_capsule_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("c_box_", StringComparison.OrdinalIgnoreCase);
        }

        private DM.Model CreateNewPrimitiveModel(GLTF.Mesh gmesh, string name, DM.Object3D parent)
        {
            var md = MeshData.FromGltfMesh(gmesh);

            if (md.verts == null || md.verts.Count == 0)
            {
                throw new Exception($"Primitive model {name} has no vertices.");
            }

            Vector3 boundsMin;
            Vector3 boundsMax;
            float DistanceRadius;

            if (name.StartsWith("c_capsule_", StringComparison.OrdinalIgnoreCase))
            {
                (boundsMin, boundsMax, DistanceRadius) = ReconstructCapsuleBounds(md);
            }
            else
            {
                boundsMin = md.verts.Aggregate(MathUtil.Min) * scaleFactor;
                boundsMax = md.verts.Aggregate(MathUtil.Max) * scaleFactor;
                DistanceRadius = CalculateDistanceRadius(boundsMin, boundsMax);
            }

            Log.Default.Warn(
                "IMPORT PRIMITIVE: Name={0}, BoundsMin={1}, BoundsMax={2}, DistanceRadius={3}",
                name, boundsMin, boundsMax, DistanceRadius);

            return new DM.Model(name, DistanceRadius, boundsMin, boundsMax, parent);
        }

        private (Vector3 boundsMin, Vector3 boundsMax, float DistanceRadius)
            ReconstructCapsuleBounds(MeshData md)
        {
            if (md.verts == null || md.verts.Count == 0)
            {
                throw new InvalidOperationException("Capsule mesh contains no vertices.");
            }

            Vector3 min = md.verts.Aggregate(MathUtil.Min);
            Vector3 max = md.verts.Aggregate(MathUtil.Max);
            Vector3 boundsMin = min * scaleFactor;
            Vector3 boundsMax = max * scaleFactor;
            float DistanceRadius = CalculateDistanceRadius(boundsMin, boundsMax);

            return (boundsMin, boundsMax, DistanceRadius);
        }

        private static float CalculateDistanceRadius(Vector3 boundsMin, Vector3 boundsMax)
        {
            float result = 0;

            foreach (float x in new[] { boundsMin.X, boundsMax.X })
            {
                foreach (float y in new[] { boundsMin.Y, boundsMax.Y })
                {
                    foreach (float z in new[] { boundsMin.Z, boundsMax.Z })
                    {
                        float distance = new Vector3(x, y, z).Length();
                        if (distance > result)
                        {
                            result = distance;
                        }
                    }
                }
            }

            return result;
        }

        private DM.Model CreateNewModel(GLTF.Mesh gmesh, string name)
        {
            var md = MeshData.FromGltfMesh(gmesh);

            var mats = md.materials.Select(i =>
            {
                var hn = HashName.FromNumberOrString(i);
                var mat = data.SectionsOfType<DM.Material>()
                    .FirstOrDefault(j => j.HashName.Hash == hn.Hash);

                if (mat == null)
                {
                    mat = new DM.Material(i);
                    data.AddSection(mat);
                }

                return mat;
            }).ToList();

            var matGroup = new DM.MaterialGroup(mats);
            data.AddSection(matGroup);

            var ms = new MeshSections
            {
                geom = new DM.DieselGeometry()
            };
            data.AddSection(ms.geom);
            ms.geom.HashName = new HashName(gmesh.Name + ".Geometry");

            ms.topo = new DM.Topology(gmesh.Name);
            data.AddSection(ms.topo);

            ms.topoip = new DM.TopologyIP(ms.topo);
            data.AddSection(ms.topoip);

            ms.passgp = new DM.PassthroughGP(ms.geom, ms.topo);
            data.AddSection(ms.passgp);

            ms.atoms = md.renderAtoms;

            ms.PopulateFromMeshData(md);
            ms.Scale(this.scaleFactor);

            var model = new DM.Model(
                name,
                (uint)ms.topo.facelist.Count,
                (uint)ms.geom.verts.Count,
                ms.passgp,
                ms.topoip,
                matGroup,
                null)
            {
                RenderAtoms = md.renderAtoms
            };

            return model;
        }

        private static bool IsAncestorOf(GLTF.Node ancestor, GLTF.Node node)
        {
            if (ancestor == null || node == null)
            {
                return false;
            }

            for (var current = node; current != null; current = current.VisualParent)
            {
                if (current == ancestor) return true;
            }

            return false;
        }

        private DM.Object3D FindCommonSkeletonRoot(GLTF.Skin skin, DM.Model model)
        {
            if (skin == null || skin.JointsCount == 0 || model == null)
            {
                return null;
            }

            var (Joint, _) = skin.GetJoint((ushort)0);
            GLTF.Node firstJoint = Joint;

            if (firstJoint == null) return null;

            GLTF.Node modelNode = null;

            foreach (var pair in objectsByNode)
            {
                if (pair.Value == model)
                {
                    modelNode = pair.Key;
                    break;
                }
            }

            if (modelNode != null)
            {
                var modelParentNode = modelNode.VisualParent;

                if (modelParentNode != null && IsAncestorOf(modelParentNode, firstJoint))
                {
                    bool commonAncestor = true;

                    for (ushort i = 1; i < skin.JointsCount; i++)
                    {
                        var jointResult = skin.GetJoint(i);
                        GLTF.Node joint = jointResult.Joint;

                        if (joint == null || !IsAncestorOf(modelParentNode, joint))
                        {
                            commonAncestor = false;
                            break;
                        }
                    }

                    if (commonAncestor &&
                        objectsByNode.TryGetValue(modelParentNode, out var parentObject))
                    {
                        return parentObject;
                    }
                }
            }

            var commonAncestors = new HashSet<GLTF.Node>();

            for (var current = firstJoint; current != null; current = current.VisualParent)
            {
                commonAncestors.Add(current);
            }

            for (ushort i = 1; i < skin.JointsCount; i++)
            {
                var jointResult = skin.GetJoint(i);
                GLTF.Node joint = jointResult.Joint;

                if (joint == null) continue;

                var ancestors = new HashSet<GLTF.Node>();

                for (var current = joint; current != null; current = current.VisualParent)
                {
                    ancestors.Add(current);
                }

                commonAncestors.IntersectWith(ancestors);

                if (commonAncestors.Count == 0) return null;
            }

            for (var current = firstJoint; current != null; current = current.VisualParent)
            {
                if (!commonAncestors.Contains(current)) continue;

                if (objectsByNode.TryGetValue(current, out var obj))
                {
                    return obj;
                }
            }

            return null;
        }

        private void ImportSkin(GLTF.Node node, DM.Model model)
        {
            DM.SkinBones skinBones = new()
            {
                Global_skin_transform = Matrix4x4.Identity
            };

            GLTF.Skin gltfSkin = node.Skin;
            DM.Object3D skeletonRoot;

            if (gltfSkin.Skeleton != null)
            {
                if (!objectsByNode.TryGetValue(gltfSkin.Skeleton, out skeletonRoot))
                {
                    throw new Exception(
                        $"GLTF skeleton root \"{gltfSkin.Skeleton.Name}\" " +
                        "was not imported as an Object3D.");
                }
            }
            else
            {
                skeletonRoot = FindCommonSkeletonRoot(gltfSkin, model) ?? throw new Exception(
                        $"Skinned model \"{model.Name}\" has no GLTF skeleton root " +
                        "and its joint hierarchy has no common root.");
                Log.Default.Warn(
                    "GltfImporter.ImportSkin: " +
                    "Skeleton root missing from GLTF skin for \"{0}\". " +
                    "Recovered root from joint hierarchy: \"{1}\"",
                    model.Name,
                    skeletonRoot.Name);
            }

            skinBones.ProbablyRootBone = skeletonRoot;

            DM.DieselGeometry geom = model.PassthroughGP.DieselGeometry;

            if (geom.weight_groups.Count != geom.vert_count)
            {
                throw new Exception(
                    $"Model \"{model.Name}\" has {geom.vert_count} vertices but " +
                    $"{geom.weight_groups.Count} weight-group entries.");
            }

            if (geom.weights.Count != geom.vert_count)
            {
                throw new Exception(
                    $"Model \"{model.Name}\" has {geom.vert_count} vertices but " +
                    $"{geom.weights.Count} weight entries.");
            }

            HashSet<ushort> usedBones = [];
            const float threshold = 0.00001f;

            for (int i = 0; i < geom.vert_count; i++)
            {
                var group = geom.weight_groups[i];
                var weights = geom.weights[i];

                if (weights.X > threshold) usedBones.Add(group.Bones1);
                if (weights.Y > threshold) usedBones.Add(group.Bones2);
                if (weights.Z > threshold) usedBones.Add(group.Bones3);
            }

            var bmi = new DM.BoneMappingItem();

            foreach (ushort gltfId in usedBones.OrderBy(i => i))
            {
                if (gltfId >= node.Skin.JointsCount)
                {
                    throw new Exception(
                        $"Model \"{model.Name}\" references GLTF joint {gltfId}, " +
                        $"but the skin only contains {node.Skin.JointsCount} joints.");
                }

                var (jointNode, ibm) = node.Skin.GetJoint(gltfId);

                if (!objectsByNode.TryGetValue(jointNode, out var bone))
                {
                    throw new Exception(
                        $"GLTF joint {gltfId} \"{jointNode.Name}\" " +
                        "was not imported as an Object3D.");
                }

                ushort modelId = (ushort)skinBones.Objects.Count;

                ibm.Translation *= scaleFactor;
                skinBones.Rotations.Add(ibm);
                skinBones.Objects.Add(bone);
                bmi.bones.Add(modelId);
            }

            skinBones.Bone_mappings.Add(bmi);

            foreach (var ra in model.RenderAtoms)
            {
                skinBones.Bone_mappings.Add(bmi);
            }

            data.AddSection(skinBones);
            model.SkinBones = skinBones;
        }

        private void RemapBoneIds(GLTF.Skin src, DM.Model model)
        {
            DM.SkinBones skinBones = model.SkinBones;

            Dictionary<DM.Object3D, ushort> sbIds =
                [];

            for (ushort sbId = 0; sbId < skinBones.Count; sbId++)
            {
                DM.Object3D bone = skinBones.Objects[sbId];
                sbIds[bone] = sbId;
            }

            ushort? LookupNewBoneId(DM.Object3D bone)
            {
                bool found = sbIds.TryGetValue(bone, out ushort sbId);

                if (found) return sbId;

                return bone.Parent != null
                    ? LookupNewBoneId(bone.Parent)
                    : null;
            }

            Dictionary<ushort, ushort> idMapping =
                [];

            for (ushort gltfId = 0; gltfId < src.JointsCount; gltfId++)
            {
                (GLTF.Node jointNode, _) = src.GetJoint(gltfId);

                if (!objectsByNode.TryGetValue(jointNode, out var obj))
                {
                    throw new Exception(
                        $"GLTF joint {gltfId} \"{jointNode.Name}\" " +
                        "was not imported as an Object3D.");
                }

                ushort? id = LookupNewBoneId(obj);
                idMapping[gltfId] = id ?? 0;
            }

            DM.DieselGeometry geom = model.PassthroughGP.DieselGeometry;

            for (int i = 0; i < geom.vert_count; i++)
            {
                DM.GeometryWeightGroups group = geom.weight_groups[i];

                ushort id1 = idMapping[group.Bones1];
                ushort id2 = idMapping[group.Bones2];
                ushort id3 = idMapping[group.Bones3];
                ushort id4 = idMapping[group.Bones4];

                geom.weight_groups[i] =
                    new DM.GeometryWeightGroups(id1, id2, id3, id4);
            }
        }

        public class MeshSections
        {
            public DM.DieselGeometry geom;
            public DM.Topology topo;
            public DM.TopologyIP topoip;
            public DM.PassthroughGP passgp;
            public List<DM.RenderAtom> atoms = [];

            public void PopulateFromMeshData(MeshData md)
            {
                geom.Headers.Clear();

                void AddToGeom<TD>(
                    ref List<TD> dest,
                    uint size,
                    DM.GeometryChannelTypes ct,
                    IList<TD> src)
                {
                    if (src.Count > 0)
                    {
                        geom.Headers.Add(new DM.GeometryHeader(size, ct));
                        dest = src.ToList();
                    }
                }

                AddToGeom(
                    ref geom.verts,
                    3,
                    DM.GeometryChannelTypes.POSITION0,
                    md.verts);

                AddToGeom(
                    ref geom.normals,
                    8,
                    DM.GeometryChannelTypes.NORMAL0,
                    md.normals);

                AddToGeom(
                    ref geom.uvDirectionV,
                    8,
                    DM.GeometryChannelTypes.UV_DIRECTION_V0,
                    md.uvDirectionV);

                AddToGeom(
                    ref geom.uvDirectionU,
                    8,
                    DM.GeometryChannelTypes.UV_DIRECTION_U0,
                    md.uvDirectionU);

                AddToGeom(
                    ref geom.vertex_colors,
                    5,
                    DM.GeometryChannelTypes.COLOR0,
                    md.vertex_colors);

                for (var i = 0; i < md.uv0.Length; i++)
                {
                    var ct = (DM.GeometryChannelTypes)
                        ((int)DM.GeometryChannelTypes.TEXCOORD0 + i);

                    AddToGeom(
                        ref geom.UVs[i],
                        9,
                        ct,
                        md.uv0[i]);
                }

                if (md.weights.Count > 0 && md.weights.Count != md.verts.Count)
                {
                    throw new Exception(
                        $"Mesh has {md.verts.Count} vertices but " +
                        $"{md.weights.Count} weights.");
                }

                if (md.weightGroups.Count > 0 &&
                    md.weightGroups.Count != md.verts.Count)
                {
                    throw new Exception(
                        $"Mesh has {md.verts.Count} vertices but " +
                        $"{md.weightGroups.Count} weight groups.");
                }

                if (md.weights.Count > 0 && md.weightGroups.Count == 0)
                {
                    throw new Exception("Mesh has weights but no weight groups.");
                }

                if (md.weightGroups.Count > 0 && md.weights.Count == 0)
                {
                    throw new Exception("Mesh has weight groups but no weights.");
                }

                AddToGeom(
                    ref geom.weights,
                    3,
                    DM.GeometryChannelTypes.BLENDWEIGHT0,
                    md.weights);

                AddToGeom(
                    ref geom.weight_groups,
                    7,
                    DM.GeometryChannelTypes.BLENDINDICES0,
                    md.weightGroups);

                geom.vert_count = (uint)geom.verts.Count;
                topo.facelist = md.faces;
            }

            public void Scale(float fac)
            {
                for (var i = 0; i < geom.verts.Count; i++)
                {
                    geom.verts[i] = geom.verts[i] * fac;
                }
            }
        }

        public class MeshData
        {
            public List<Vector3> verts = [];
            public List<Vector3> normals = [];
            public List<DM.GeometryColor> vertex_colors = [];
            public List<Vector3> uvDirectionV = [];
            public List<Vector3> uvDirectionU = [];
            public List<DM.Face> faces = [];
            public List<DM.RenderAtom> renderAtoms = [];
            public List<string> materials = [];

            public List<Vector2>[] uv0 = [ [],[],[],[],[],[],[],[] ];

            public List<Vector3> weights = [];
            public List<DM.GeometryWeightGroups> weightGroups =
                [];

            public List<float?> tangentHandedness = [];

            public int AppendVertex(Vertex vtx)
            {
                var idx = this.verts.Count;

                this.verts.Add(vtx.pos);
                vtx.vtx_col.WithValue(v => this.vertex_colors.Add(v.ToGeometryColor()));
                vtx.normal.WithValue(v => this.normals.Add(v));
                for (var i = 0; i < 8; i++)
                {
                    vtx.uv[i].WithValue(v => this.uv0[i].Add(v));
                }

                vtx.weight.WithValue(v => this.weights.Add(v));

                if (vtx.weightGroups != null)
                {
                    this.weightGroups.Add(vtx.weightGroups);
                }

                this.tangentHandedness.Add(vtx.tangentHandedness);

                return idx;
            }

            public static MeshData FromGltfMesh(GLTF.Mesh mesh)
            {
                var vcount = mesh.Primitives
                    .Select(prim => prim.VertexAccessors["POSITION"].Count)
                    .Sum();

                if (vcount >= ushort.MaxValue)
                {
                    throw new Exception(
                        $"Too many vertices ({vcount}) in mesh {mesh.Name}. " +
                        "Limit is 65535");
                }

                var attribsUsed = mesh.Primitives[0]
                    .VertexAccessors
                    .Select(i => i.Key)
                    .Where(i => i != "TANGENT")
                    .OrderBy(i => i);

                var ok = mesh.Primitives
                    .Select(i => i.VertexAccessors.Keys.Where(j => j != "TANGENT").OrderBy(j => j))
                    .Aggregate(
                        true,
                        (acc, curr) => acc && curr.SequenceEqual(attribsUsed));

                if (!ok)
                {
                    throw new Exception(
                        "Vertex attributes not consistent between " +
                        "Primitives. Diesel cannot represent this.");
                }

                var ms = new MeshData
                {
                    materials = [.. mesh.Primitives
                        .Select(i => i.Material?.Name ?? "Material: Default Material")
                        .Distinct()]
                };

                uint currentBaseVertex = 0;
                uint currentBaseIndex = 0;

                foreach (var prim in mesh.Primitives)
                {
                    var vertices = GetVerticesFromPrimitive(prim).ToList();
                    var primFaces = prim.GetTriangleIndices().ToList();
                    var matname = prim.Material?.Name ?? "Material: Default Material";

                    var ra = new DM.RenderAtom
                    {
                        BaseIndex = currentBaseIndex,
                        BaseVertex = currentBaseVertex,
                        MaterialId = (uint)ms.materials.IndexOf(matname),
                        TriangleCount = (uint)primFaces.Count
                    };

                    int primitiveVertexBase = ms.verts.Count;

                    foreach (var vertex in vertices)
                    {
                        ms.AppendVertex(vertex);
                    }

                    foreach (var (A, B, C) in primFaces)
                    {
                        var df = new DM.Face(
                            checked((ushort)(primitiveVertexBase + A)),
                            checked((ushort)(primitiveVertexBase + B)),
                            checked((ushort)(primitiveVertexBase + C)));

                        ms.faces.Add(df);
                    }

                    ra.GeometrySliceLength = (uint)vertices.Count;
                    ms.renderAtoms.Add(ra);

                    currentBaseIndex += ra.TriangleCount * 3;
                    currentBaseVertex += ra.GeometrySliceLength;
                }

                if (ms.uv0[0].Count == ms.verts.Count)
                {
                    DM.DieselGeometry.ComputeUvDirections(
                        ms.verts,
                        ms.uv0[0],
                        ms.faces,
                        out ms.uvDirectionU,
                        out ms.uvDirectionV);

                    if (ms.normals.Count == ms.verts.Count)
                    {
                        if (ms.tangentHandedness.Any(h => h.HasValue))
                        {
                            FillMissingUvDirections(
                                ms.faces, ms.normals, ms.tangentHandedness,
                                ms.uvDirectionU, ms.uvDirectionV);

                            for (int i = 0; i < ms.verts.Count; i++)
                            {
                                if (!ms.tangentHandedness[i].HasValue)
                                    continue;

                                Vector3 n = ms.normals[i];
                                Vector3 u = ms.uvDirectionU[i];
                                Vector3 v = ms.uvDirectionV[i];
                                float h = Vector3.Dot(Vector3.Cross(u, n), v);

                                if (float.IsFinite(h) && MathF.Abs(h) > 1e-8f &&
                                    (h < 0 ? -1.0f : 1.0f) != ms.tangentHandedness[i].Value)
                                {
                                    ms.uvDirectionV[i] = -v;
                                }
                            }
                        }
                        else
                        {
                            StabilizeUvOrientationRegions(
                                ms.uv0[0], ms.normals, ms.faces,
                                ms.uvDirectionU, ms.uvDirectionV);
                        }
                    }
                }

                return ms;
            }

            private static void FillMissingUvDirections(
                IReadOnlyList<DM.Face> faces,
                IReadOnlyList<Vector3> normals,
                IReadOnlyList<float?> handedness,
                IList<Vector3> directionU,
                IList<Vector3> directionV)
            {
                var neighbours = new HashSet<int>[directionU.Count];
                for (int i = 0; i < neighbours.Length; i++) neighbours[i] = [];
                foreach (var f in faces)
                {
                    neighbours[f.a].Add(f.b); neighbours[f.a].Add(f.c);
                    neighbours[f.b].Add(f.a); neighbours[f.b].Add(f.c);
                    neighbours[f.c].Add(f.a); neighbours[f.c].Add(f.b);
                }

                bool changed;
                do
                {
                    changed = false;
                    for (int i = 0; i < directionU.Count; i++)
                    {
                        if (!handedness[i].HasValue) continue;
                        if (directionU[i].LengthSquared() > 1e-20f &&
                            directionV[i].LengthSquared() > 1e-20f) continue;

                        Vector3 sumU = Vector3.Zero, sumV = Vector3.Zero;
                        int count = 0;
                        foreach (int n in neighbours[i])
                        {
                            if (handedness[n] != handedness[i]) continue;
                            if (directionU[n].LengthSquared() <= 1e-20f ||
                                directionV[n].LengthSquared() <= 1e-20f) continue;
                            sumU += directionU[n];
                            sumV += directionV[n];
                            count++;
                        }

                        if (count == 0) continue;
                        directionU[i] = Vector3.Normalize(sumU);
                        directionV[i] = Vector3.Normalize(sumV);

                        float h = Vector3.Dot(
                            Vector3.Cross(directionU[i], normals[i]), directionV[i]);
                        if (float.IsFinite(h) && MathF.Abs(h) > 1e-8f &&
                            (h < 0 ? -1.0f : 1.0f) != handedness[i].Value)
                        {
                            directionV[i] = -directionV[i];
                        }
                        changed = true;
                    }
                } while (changed);
            }

            private static void StabilizeUvOrientationRegions(
                IReadOnlyList<Vector2> uvs,
                IReadOnlyList<Vector3> normals,
                IReadOnlyList<DM.Face> faces,
                IList<Vector3> directionU,
                IList<Vector3> directionV)
            {
                const float minUvArea = DM.DieselGeometry.UvDeterminantEpsilon;
                int count = faces.Count;
                var sign = new sbyte[count];
                var neighbours = new List<int>[count];
                var edges = new Dictionary<(ushort, ushort), List<int>>();

                static (ushort, ushort) Edge(ushort a, ushort b) => a <= b ? (a, b) : (b, a);

                for (int i = 0; i < count; i++)
                {
                    neighbours[i] = [];
                    var f = faces[i];
                    Vector2 a = uvs[f.a], b = uvs[f.b], c = uvs[f.c];
                    float det = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
                    if (float.IsFinite(det) && MathF.Abs(det) >= minUvArea)
                        sign[i] = det < 0 ? (sbyte)-1 : (sbyte)1;

                    foreach (var edge in new[] { Edge(f.a, f.b), Edge(f.b, f.c), Edge(f.c, f.a) })
                    {
                        if (!edges.TryGetValue(edge, out var owners))
                            edges[edge] = owners = [];
                        foreach (int other in owners)
                        {
                            neighbours[i].Add(other);
                            neighbours[other].Add(i);
                        }
                        owners.Add(i);
                    }
                }

                bool changed;
                do
                {
                    changed = false;
                    var pending = new sbyte[count];
                    for (int i = 0; i < count; i++)
                    {
                        if (sign[i] != 0) continue;
                        sbyte inherited = 0;
                        bool conflict = false;
                        foreach (int n in neighbours[i])
                        {
                            if (sign[n] == 0) continue;
                            if (inherited == 0) inherited = sign[n];
                            else if (inherited != sign[n]) { conflict = true; break; }
                        }
                        if (!conflict) pending[i] = inherited;
                    }
                    for (int i = 0; i < count; i++)
                    {
                        if (sign[i] == 0 && pending[i] != 0)
                        {
                            sign[i] = pending[i];
                            changed = true;
                        }
                    }
                }
                while (changed);

                var vertexSign = new sbyte[uvs.Count];
                for (int i = 0; i < count; i++)
                {
                    if (sign[i] == 0) continue;
                    var f = faces[i];
                    foreach (int v in new[] { (int)f.a, (int)f.b, (int)f.c })
                    {
                        if (vertexSign[v] == 0) vertexSign[v] = sign[i];
                        else if (vertexSign[v] != sign[i]) vertexSign[v] = 2;
                    }
                }

                for (int i = 0; i < vertexSign.Length; i++)
                {
                    if (vertexSign[i] is not (1 or -1)) continue;
                    Vector3 n = normals[i], u = directionU[i], v = directionV[i];
                    float h = Vector3.Dot(Vector3.Cross(u, n), v);
                    if (float.IsFinite(h) && MathF.Abs(h) > 1e-8f &&
                        (h < 0 ? -1 : 1) != vertexSign[i])
                    {
                        directionV[i] = -v;
                    }
                }
            }

            private static IEnumerable<Vertex> GetVerticesFromPrimitive(GLTF.MeshPrimitive prim)
            {
                var pos = prim.VertexAccessors["POSITION"];

                var result = pos.AsVector3Array().Select(
                    (p, idx) =>
                    {
                        return new Vertex
                        {
                            pos = p
                        };
                    });

                prim.VertexAccessors.TryGetValue("NORMAL", out var normal);

                if (normal != null && normal.Count > 0)
                {
                    var na = normal.AsVector3Array();

                    result = result.Select(
                        (vtx, idx) =>
                        {
                            vtx.normal = na[idx];
                            return vtx;
                        });
                }

                prim.VertexAccessors.TryGetValue("TANGENT", out var tangent);

                if (tangent != null && tangent.Count > 0)
                {
                    var ta = tangent.AsVector4Array();

                    result = result.Select(
                        (vtx, idx) =>
                        {
                            vtx.tangentHandedness =
                                ta[idx].W < 0.0f ? -1.0f : 1.0f;

                            return vtx;
                        });
                }

                prim.VertexAccessors.TryGetValue("COLOR_0", out var vcols);

                if (vcols != null && vcols.Count > 0)
                {
                    if (vcols.Dimensions == GLTF.DimensionType.VEC4)
                    {
                        var vca = vcols.AsVector4Array();

                        result = result.Select(
                            (vtx, idx) =>
                            {
                                vtx.vtx_col = vca[idx];
                                return vtx;
                            });
                    }
                    else
                    {
                        var vca = vcols.AsVector3Array();

                        result = result.Select(
                            (vtx, idx) =>
                            {
                                vtx.vtx_col = new Vector4(vca[idx], 1.0f);
                                return vtx;
                            });
                    }
                }

                for (int i = 0; i < 8; i++)
                {
                    var ii = i;

                    prim.VertexAccessors.TryGetValue(
                        $"TEXCOORD_{ii}",
                        out var uv0);

                    if (uv0 != null && uv0.Count > 0)
                    {
                        var uva = uv0.AsVector2Array();

                        result = result.Select(
                            (vtx, idx) =>
                            {
                                vtx.uv[ii] =
                                    new Vector2(uva[idx].X, 1 - uva[idx].Y);

                                return vtx;
                            });
                    }
                }

                prim.VertexAccessors.TryGetValue("JOINTS_0", out var joints);
                prim.VertexAccessors.TryGetValue("WEIGHTS_0", out var weights);

                if (joints != null && joints.Count > 0)
                {
                    if (weights == null || weights.Count != joints.Count)
                    {
                        throw new Exception(
                            $"{prim.LogicalParent.Name} has JOINTS_0 " +
                            "without matching WEIGHTS_0.");
                    }

                    var ja = joints.AsVector4Array();
                    var wa = weights.AsVector4Array();

                    result = result.Select(
                        (vtx, idx) =>
                        {
                            var gltfWeight = wa[idx];

                            vtx.weight = new Vector3(
                                gltfWeight.X,
                                gltfWeight.Y,
                                gltfWeight.Z);

                            if (gltfWeight.W > 0.00001f)
                            {
                                Log.Default.Warn(
                                    $"{prim.LogicalParent.Name} has a vertex " +
                                    "with a fourth non-zero weight at " +
                                    $"{vtx.pos}; Diesel only supports three.");
                            }

                            vtx.weightGroups =
                                new DM.GeometryWeightGroups(
                                    (ushort)ja[idx].X,
                                    (ushort)ja[idx].Y,
                                    (ushort)ja[idx].Z,
                                    (ushort)ja[idx].W);

                            return vtx;
                        });
                }
                else if (weights != null && weights.Count > 0)
                {
                    throw new Exception(
                        $"{prim.LogicalParent.Name} has WEIGHTS_0 " +
                        "without JOINTS_0.");
                }

                return result;
            }
        }

        public class Vertex : IEquatable<Vertex>
        {
            public Vector3 pos;
            public Vector3? normal;
            public Vector4? vtx_col;
            public Vector2?[] uv = new Vector2?[10];
            public Vector3? weight;
            public DM.GeometryWeightGroups weightGroups;
            public float? tangentHandedness;

            public bool Equals(Vertex other)
            {
                if (other == null) return false;

                return
                    pos.Equals(other.pos) &&
                    EqualityComparer<Vector3?>.Default.Equals(normal, other.normal) &&
                    EqualityComparer<Vector4?>.Default.Equals(vtx_col, other.vtx_col) &&
                    EqualityComparer<Vector3?>.Default.Equals(weight, other.weight) &&
                    EqualityComparer<DM.GeometryWeightGroups>.Default.Equals(
                        weightGroups,
                        other.weightGroups) &&
                    uv.SequenceEqual(other.uv);
            }
        }

        private void ImportAnimations(GLTF.ModelRoot root)
        {
            foreach (var anim in root.LogicalAnimations)
            {
                foreach (var chan in anim.Channels)
                {
                    var node = chan.TargetNode;

                    if (!objectsByNode.TryGetValue(node, out var targetObject))
                    {
                        Log.Default.Warn(
                            "GltfImporter.ImportAnimations: Animation target " +
                            "\"{0}\" was not imported. Skipping channel.",
                            node?.Name ?? "<null>");
                        continue;
                    }

                    if (chan.TargetNodePath == GLTF.PropertyPath.rotation)
                    {
                        var sampler = chan.GetRotationSampler();
                        AddRotationAnimation(targetObject, sampler);
                    }
                    else if (chan.TargetNodePath == GLTF.PropertyPath.translation)
                    {
                        var sampler = chan.GetTranslationSampler();
                        AddTranslationAnimation(targetObject, sampler);
                    }
                }
            }
        }

        private void AddRotationAnimation(
            DM.Object3D targetObject,
            GLTF.IAnimationSampler<Quaternion> sampler)
        {
            var controller = new DM.QuatLinearRotationController();

            foreach (var (key, value) in sampler.GetLinearKeys())
            {
                controller.Keyframes.Add(
                    new DM.Keyframe<Quaternion>(key, value));
            }

            if (controller.Keyframes.Count > 0)
            {
                controller.KeyframeLength =
                    controller.Keyframes.Max(i => i.Timestamp);
            }

            if (targetObject.Animations.Count == 0)
            {
                targetObject.Animations.Add(controller);
                targetObject.Animations.Add(null);
                targetObject.Animations.Add(null);
            }
            else if (
                targetObject.Animations.Count == 1 &&
                targetObject.Animations[0].GetType() ==
                    typeof(DM.LinearVector3Controller))
            {
                targetObject.Animations.Insert(0, controller);
            }
            else
            {
                throw new Exception(
                    $"Failed to insert animation in {targetObject.Name}: " +
                    "unrecognised controller list shape");
            }

            data.AddSection(controller);
        }

        private void AddTranslationAnimation(
            DM.Object3D target,
            GLTF.IAnimationSampler<Vector3> sampler)
        {
            var controller = new DM.LinearVector3Controller();

            foreach (var (ts, v) in sampler.GetLinearKeys())
            {
                controller.Keyframes.Add(
                    new DM.Keyframe<Vector3>(ts, v * scaleFactor));
            }

            if (controller.Keyframes.Count > 0)
            {
                controller.KeyframeLength =
                    controller.Keyframes.Max(i => i.Timestamp);
            }

            if (target.Animations.Count == 0)
            {
                target.Animations.Add(controller);
            }
            else if (
                target.Animations.Count == 3 &&
                target.Animations[0].GetType() ==
                    typeof(DM.QuatLinearRotationController) &&
                target.Animations[1] == null &&
                target.Animations[2] == null)
            {
                target.Animations.RemoveAt(2);
                target.Animations[1] = controller;
            }
            else
            {
                throw new Exception(
                    $"Failed to insert animation in {target.Name}: " +
                    "unrecognised controller list shape");
            }

            data.AddSection(controller);
        }
    }
}
