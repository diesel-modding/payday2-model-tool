using PD2ModelParser.Sections;
using SharpGLTF.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GLTF = SharpGLTF.Schema2;
namespace PD2ModelParser.Exporters
{
    class GltfExporter
    {
        public static string ExportFile(FullModelData data, string path, bool binary)
        {
            var exporter = new GltfExporter();
            var gltfmodel = exporter.Convert(data);
            if (binary)
            {
                gltfmodel.SaveGLB(path);
            }
            else
            {
                gltfmodel.SaveGLTF(path, new GLTF.WriteSettings
                {
                    JsonIndented = true
                });
            }
            return path;
        }
        FullModelData data;
        GLTF.ModelRoot root;
        GLTF.Scene scene;
        Dictionary<ISection, GLTF.Material> materialsBySection;
        Dictionary<ISection, GLTF.Node> nodesBySection;
        List<(Model, GLTF.Node)> toSkin;
        readonly float scaleFactor = 0.01f;
        static List<Object3D> GetSkeletonObjects(SkinBones skinBones)
        {
            var bones = new List<Object3D>();
            var rootBone = skinBones.ProbablyRootBone;
            if (rootBone == null) return bones;
            void AddChildren(Object3D obj)
            {
                if (obj == null) return;
                if (obj is not Model and not Light) bones.Add(obj);
                foreach (var child in obj.children)
                    AddChildren(child);
            }
            AddChildren(rootBone);
            return bones;
        }
        GLTF.ModelRoot Convert(FullModelData data)
        {
            materialsBySection = [];
            nodesBySection = [];
            toSkin = [];
            this.data = data;
            root = GLTF.ModelRoot.CreateModel();
            scene = root.UseScene(0);
            foreach (var ms in data.parsed_sections.Where(i => i.Value is Material).Select(i => i.Value as Material))
            {
                materialsBySection[ms] = root.CreateMaterial(ms.HashName.String);
            }
            foreach (var i in data.SectionsOfType<Object3D>().Where(i => i.Parent == null))
            {
                CreateNodeFromObject3D(i, scene);
            }
            var axisCorrection = Matrix4x4.CreateRotationX(-MathF.PI / 2);
            foreach (var node in scene.VisualChildren.ToList())
            {
                node.LocalMatrix = axisCorrection * node.LocalMatrix;
            }
            foreach (var (thing, node) in toSkin)
            {
                SkinModel(thing, node);
            }
            return root;
        }
        void CreateNodeFromObject3D(Object3D thing, GLTF.IVisualNodeContainer parent)
        {
            var node = parent.CreateNode(thing.Name);
            nodesBySection[thing] = node;
            if (thing != null)
            {
                var istrs = Matrix4x4.Decompose(thing.Transform, out _, out _, out _);
                if (!istrs)
                {
                    throw new Exception($"In object \"{thing.Name}\" ({thing.SectionId}), non-TRS matrix");
                }
                var mat = thing.Transform;
                mat.Translation *= scaleFactor;
                mat.M14 = 0;
                mat.M24 = 0;
                mat.M34 = 0;
                mat.M44 = 1;
                node.LocalMatrix = mat;
            }
            Log.Default.Warn("EXPORT OBJECT: Name={0}, Type={1}, SectionId={2}, Parent={3}, Children={4}", thing.Name, thing.GetType().FullName, thing.SectionId, thing.Parent?.Name ?? "<null>", thing.children.Count);
            foreach (var field in thing.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                object value;
                try
                {
                    value = field.GetValue(thing);
                }
                catch
                {
                    continue;
                }
                if (value == null) continue;
                Log.Default.Warn("  FIELD {0}: {1} = {2}", field.FieldType.FullName, field.Name, value);
            }
            foreach (var property in thing.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
                object value;
                try
                {
                    value = property.GetValue(thing);
                }
                catch
                {
                    continue;
                }
                if (value == null) continue;
                Log.Default.Warn("  PROPERTY {0}: {1} = {2}", property.PropertyType.FullName, property.Name, value);
            }
            if (thing is Model mod)
            {
                if (mod.version == 3)
                {
                    node.Mesh = GetMeshForModel(mod);
                    if (mod.SkinBones != null)
                    {
                        toSkin.Add((mod, node));
                    }
                }
                else if (mod.version == 6)
                {
                    if (IsPrimitiveModel(mod))
                    {
                        node.Mesh = CreatePrimitiveMesh(mod);
                        Log.Default.Warn("EXPORT PRIMITIVE: Name={0}, BoundsMin={1}, BoundsMax={2}, RadDistance={3}", mod.Name, mod.BoundsMin, mod.BoundsMax, mod.RadDistance);
                    }
                }
                else
                {
                    throw new Exception($"Model {mod.Name} is of unknown version {mod.version}");
                }
            }
            else if (thing is Light dl)
            {
                GLTF.PunctualLight lamp;
                lamp = dl.LightType switch
                {
                    1 => root.CreatePunctualLight(GLTF.PunctualLightType.Point),
                    2 => root.CreatePunctualLight(GLTF.PunctualLightType.Spot),
                    _ => throw new Exception($"{thing.Name} is an unknown light type {dl.LightType}")
                };
                lamp.Color = new Vector3(dl.Colour.R, dl.Colour.G, dl.Colour.B);
                lamp.Range = dl.FarRange * scaleFactor;
                node.PunctualLight = lamp;
            }
            foreach (var i in thing.children)
            {
                CreateNodeFromObject3D(i, node);
            }
        }
        void SkinModel(Model model, GLTF.Node node)
        {
            if (model.SkinBones == null) return;
            var skinbones = model.SkinBones;
            var skeletonObjects = GetSkeletonObjects(skinbones);
            if (skeletonObjects.Count == 0) return;
            var skin = root.CreateSkin(model.Name);
            var skeletonRootNode = nodesBySection[skinbones.ProbablyRootBone];
            skin.Skeleton = skeletonRootNode;
            node.LocalTransform = Matrix4x4.Identity;
            var joints = new List<(GLTF.Node, Matrix4x4)>();
            foreach (var bone in skeletonObjects)
            {
                if (!nodesBySection.TryGetValue(bone, out var jointNode))
                {
                    throw new Exception($"Skeleton object \"{bone.Name}\" ({bone.SectionId}) " + $"does not have a GLTF node.");
                }
                Matrix4x4 ibm;
                int skinBoneIndex = skinbones.Objects.IndexOf(bone);
                if (skinBoneIndex >= 0)
                {
                    ibm = skinbones.rotations[skinBoneIndex];
                }
                else
                {
                    if (!Matrix4x4.Invert(bone.WorldTransform, out ibm))
                    {
                        throw new Exception($"Cannot invert world transform for bone \"{bone.Name}\" " + $"({bone.SectionId}).");
                    }
                }
                ibm.Translation *= scaleFactor;
                const float matrixEpsilon = 1e-6f;
                if (MathF.Abs(ibm.M14) < matrixEpsilon) ibm.M14 = 0;
                if (MathF.Abs(ibm.M24) < matrixEpsilon) ibm.M24 = 0;
                if (MathF.Abs(ibm.M34) < matrixEpsilon) ibm.M34 = 0;
                if (MathF.Abs(ibm.M44 - 1.0f) < matrixEpsilon) ibm.M44 = 1.0f;
                joints.Add((jointNode, ibm));
            }
            skin.BindJoints(joints);
            node.Skin = skin;
        }
        static bool IsPrimitiveModel(Model model)
        {
            if (model == null) return false;
            if (model.version != 6) return false;
            if (model.PassthroughGP != null) return false;
            if (model.TopologyIP != null) return false;
            string name = model.HashName?.String ?? model.Name ?? "";
            return
            name.StartsWith("c_sphere_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("c_capsule_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("c_box_", StringComparison.OrdinalIgnoreCase);
        }
        GLTF.Mesh CreatePrimitiveMesh(Model model)
        {
            string name = model.HashName?.String ?? model.Name ?? "";
            if (name.StartsWith("c_sphere_", StringComparison.OrdinalIgnoreCase)) return CreateSphereMesh(model);
            if (name.StartsWith("c_capsule_", StringComparison.OrdinalIgnoreCase)) return CreateCapsuleMesh(model);
            if (name.StartsWith("c_box_", StringComparison.OrdinalIgnoreCase)) return CreateBoxMesh(model);
            return null;
        }
        GLTF.Mesh CreateBoxMesh(Model model)
        {
            Vector3 min = model.BoundsMin * scaleFactor;
            Vector3 max = model.BoundsMax * scaleFactor;
            var vertices = new List<Vector3>
            {
                new(min.X, min.Y, min.Z),
                new(max.X, min.Y, min.Z),
                new(max.X, max.Y, min.Z),
                new(min.X, max.Y, min.Z),
                new(min.X, min.Y, max.Z),
                new(max.X, min.Y, max.Z),
                new(max.X, max.Y, max.Z),
                new(min.X, max.Y, max.Z)
            };
            var indices = new ushort[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,2,3,7,2,7,6,0,4,7,0,7,3,1,2,6,1,6,5};
            return CreateGeneratedMesh(model.Name, vertices, indices);
        }
        GLTF.Mesh CreateSphereMesh(Model model)
        {
            Vector3 size = (model.BoundsMax - model.BoundsMin) * scaleFactor;
            float radius = MathF.Min(MathF.Min(size.X, size.Y), size.Z) * 0.5f;
            Vector3 center = (model.BoundsMin + model.BoundsMax) * 0.5f* scaleFactor;
            const int segments = 24;
            const int rings = 12;
            var vertices = new List<Vector3>();
            var indices = new List<ushort>();
            for (int y = 0; y <= rings; y++)
            {
                float v = (float)y / rings;
                float phi = v * MathF.PI;
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);
                for (int x = 0; x <= segments; x++)
                {
                    float u = (float)x / segments;
                    float theta = u * MathF.PI * 2.0f;
                    float sinTheta = MathF.Sin(theta);
                    float cosTheta = MathF.Cos(theta);
                    vertices.Add(center + new Vector3(sinPhi * cosTheta * radius, sinPhi * sinTheta * radius, cosPhi * radius));
                }
            }
            for (int y = 0; y < rings; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int a = y * (segments + 1) + x;
                    int b = a + 1;
                    int c = a + segments + 1;
                    int d = c + 1;
                    indices.Add((ushort)a);
                    indices.Add((ushort)c);
                    indices.Add((ushort)b);
                    indices.Add((ushort)b);
                    indices.Add((ushort)c);
                    indices.Add((ushort)d);
                }
            }
            return CreateGeneratedMesh(model.Name, vertices, [.. indices]);
        }
        GLTF.Mesh CreateCapsuleMesh(Model model)
        {
            Vector3 size = (model.BoundsMax - model.BoundsMin) * scaleFactor;
            Vector3 center = (model.BoundsMin + model.BoundsMax) * 0.5f* scaleFactor;
            int axis;
            if (size.X >= size.Y && size.X >= size.Z) axis = 0;
            else if (size.Y >= size.X && size.Y >= size.Z) axis = 1;
            else axis = 2;
            float largest = axis == 0 ? size.X : axis == 1 ? size.Y : size.Z;
            float radius = axis == 0 ? MathF.Min(size.Y, size.Z) * 0.5f : axis == 1 ? MathF.Min(size.X, size.Z) * 0.5f : MathF.Min(size.X, size.Y) * 0.5f;
            float cylinderLength = MathF.Max(0, largest - radius * 2.0f);
            const int segments = 24;
            const int hemisphereRings = 8;
            const int cylinderRings = 4;
            var vertices = new List<Vector3>();
            var indices = new List<ushort>();
            float halfCylinder = cylinderLength * 0.5f;
            for (int y = 0; y <= hemisphereRings; y++)
            {
                float t = (float)y / hemisphereRings;
                float phi = -MathF.PI * 0.5f + t * MathF.PI * 0.5f;
                float z = -halfCylinder + MathF.Sin(phi) * radius;
                float ringRadius = MathF.Cos(phi) * radius;
                AddCapsuleRing(vertices, center, ringRadius, z, segments);
            }
            for (int y = 1; y < cylinderRings; y++)
            {
                float t = (float)y / cylinderRings;
                float z = -halfCylinder + t * cylinderLength;
                AddCapsuleRing(vertices, center, radius, z, segments);
            }
            for (int y = 0; y <= hemisphereRings; y++)
            {
                float t = (float)y / hemisphereRings;
                float phi = t * MathF.PI * 0.5f;
                float z = halfCylinder + MathF.Sin(phi) * radius;
                float ringRadius = MathF.Cos(phi) * radius;
                AddCapsuleRing(vertices, center, ringRadius, z, segments);
            }
            int rings = vertices.Count / (segments + 1);
            for (int y = 0; y < rings - 1; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int a = y * (segments + 1) + x;
                    int b = a + 1;
                    int c = a + segments + 1;
                    int d = c + 1;
                    indices.Add((ushort)a);
                    indices.Add((ushort)c);
                    indices.Add((ushort)b);
                    indices.Add((ushort)b);
                    indices.Add((ushort)c);
                    indices.Add((ushort)d);
                }
            }
            if (axis != 2)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 p = vertices[i] - center;
                    if (axis == 0)
                    {
                        p = new Vector3(p.Z, p.Y, -p.X);
                    }
                    else
                    {
                        p = new Vector3(p.X, p.Z, -p.Y);
                    }
                    vertices[i] = center + p;
                }
            }
            return CreateGeneratedMesh(model.Name, vertices, [.. indices]);
        }
        static void AddCapsuleRing(List<Vector3> vertices, Vector3 center, float radius, float z, int segments)
        {
            for (int x = 0; x <= segments; x++)
            {
                float u = (float)x / segments;
                float theta = u * MathF.PI * 2.0f;
                vertices.Add(center + new Vector3(MathF.Cos(theta) * radius, MathF.Sin(theta) * radius, z));
            }
        }
        GLTF.Mesh CreateGeneratedMesh(string name, IList<Vector3> vertices, ushort[] indices)
        {
            var mesh = root.CreateMesh(name);
            var positionAccessor = MakeVertexAttributeAccessor($"{name}_position", vertices, 12, GLTF.DimensionType.VEC3, i => i, ma => ma.AsVector3Array());
            var indexAccessor = CreateIndexAccessor($"{name}_indices", indices);
            var primitive = mesh.CreatePrimitive();
            primitive.DrawPrimitiveType = GLTF.PrimitiveType.TRIANGLES;
            primitive.SetVertexAccessor("POSITION", positionAccessor);
            primitive.SetIndexAccessor(indexAccessor);
            return mesh;
        }
        GLTF.Accessor CreateIndexAccessor(string name, ushort[] indices)
        {
            var buf = new ArraySegment<byte>(new byte[indices.Length * sizeof(ushort)]);
            var mai = new MemoryAccessInfo(name, 0, indices.Length, 0, GLTF.DimensionType.SCALAR, GLTF.EncodingType.UNSIGNED_SHORT);
            var ma = new MemoryAccessor(buf, mai);
            var array = ma.AsIntegerArray();
            for (int i = 0; i < indices.Length; i++)
            {
                array[i] = indices[i];
            }
            var accessor = root.CreateAccessor();
            accessor.SetIndexData(ma);
            return accessor;
        }
        GLTF.Mesh GetMeshForModel(Model model)
        {
            if (model.PassthroughGP == null) return null;
            var mesh = root.CreateMesh(model.Name);
            var secPassthrough = model.PassthroughGP;
            var geometry = secPassthrough.DieselGeometry;
            var topology = secPassthrough.Topology;
            var materialGroup = model.MaterialGroup;
            var jointRemap = new Dictionary<int,int>();
            if (model.SkinBones != null)
            {
                var skeletonObjects = GetSkeletonObjects(model.SkinBones);
                var jointIndices = new Dictionary<Object3D,int>();
                for (int i = 0; i < skeletonObjects.Count; i++) jointIndices[skeletonObjects[i]] = i;
                for (int i = 0; i < model.SkinBones.Objects.Count; i++)
                {
                    var bone = model.SkinBones.Objects[i];
                    if (jointIndices.TryGetValue(bone, out var jointIndex))
                    {
                        jointRemap[i] = jointIndex;
                        continue;
                    }
                    var parent = bone?.Parent;
                    while (parent != null)
                    {
                        if (jointIndices.TryGetValue(parent, out jointIndex))
                        {
                            jointRemap[i] = jointIndex;
                            break;
                        }
                        parent = parent.Parent;
                    }
                }
            }
            var attribs = GetGeometryAttributes(geometry, jointRemap);
            foreach (var (indexAccessor, material) in CreatePrimitiveIndices(topology, model.RenderAtoms, materialGroup))
            {
                var prim = mesh.CreatePrimitive();
                prim.DrawPrimitiveType = GLTF.PrimitiveType.TRIANGLES;
                foreach (var att in attribs)
                    prim.SetVertexAccessor(att.Item1, att.Item2);
                prim.SetIndexAccessor(indexAccessor);
                if (material.Name != "Material: Default Material") prim.Material = material;
            }
            return mesh;
        }
        IEnumerable<(GLTF.Accessor, GLTF.Material)> CreatePrimitiveIndices(Topology topo, IEnumerable<RenderAtom> atoms, MaterialGroup materialGroup)
        {
            var buf = new ArraySegment<byte>(new byte[topo.facelist.Count * 3 * 2]);
            var mai = new MemoryAccessInfo($"indices_{topo.HashName}", 0, topo.facelist.Count * 3, 0, GLTF.DimensionType.SCALAR, GLTF.EncodingType.UNSIGNED_SHORT);
            var ma = new MemoryAccessor(buf, mai);
            var array = ma.AsIntegerArray();
            for (int i = 0; i < topo.facelist.Count; i++)
            {
                array[i * 3 + 0] = topo.facelist[i].a;
                array[i * 3 + 1] = topo.facelist[i].b;
                array[i * 3 + 2] = topo.facelist[i].c;
            }
            var atomcount = 0;
            foreach (var ra in atoms)
            {
                var atom_mai = new MemoryAccessInfo($"indices_{topo.HashName}_{atomcount++}", (int)ra.BaseIndex * 2, (int)ra.TriangleCount * 3, 0, GLTF.DimensionType.SCALAR, GLTF.EncodingType.UNSIGNED_SHORT);
                var atom_ma = new MemoryAccessor(buf, atom_mai);
                var accessor = root.CreateAccessor();
                accessor.SetIndexData(atom_ma);
                var materialSection = materialGroup.Items[(int)ra.MaterialId];
                if (!materialsBySection.TryGetValue(materialSection, out var material))
                {
                    Log.Default.Warn($"Missing material section: " + $"ID {materialSection.SectionId}, " + $"HashName {materialSection.HashName}");
                    continue;
                }
                yield
                return (accessor, material);
            }
        }
        List<(string, GLTF.Accessor)> GetGeometryAttributes(DieselGeometry geometry, Dictionary<int, int> jointRemap)
        {
            List<(string, GLTF.Accessor)> result;
            result = [];
            var a_pos = MakeVertexAttributeAccessor("vpos", geometry.verts.Select(i => i * scaleFactor).ToList(), 12, GLTF.DimensionType.VEC3, i => i, ma => ma.AsVector3Array());
            result.Add(("POSITION", a_pos));
            if (geometry.normals.Count > 0)
            {
                Vector3 MakeNormal(Vector3 norm, int idx)
                {
                    var normalized = Vector3.Normalize(norm);
                    if (!normalized.IsFinite())
                    {
                        Log.Default.Warn("Vertex {0} of geometry {1}|{2} is bogus ({3})", idx, geometry.SectionId, geometry.HashName, norm);
                        return new Vector3(1, 0, 0);
                    }
                    if (!normalized.IsUnitLength())
                    {
                        Log.Default.Warn("Vertex {0} of geometry {1}|{2} is bogus length {4} ({3})", idx, geometry.SectionId, geometry.HashName, norm, norm.Length());
                        return new Vector3(1, 0, 0);
                    }
                    return normalized;
                }
                var a_norm = MakeVertexAttributeAccessor("vnorm", geometry.normals, 12, GLTF.DimensionType.VEC3, MakeNormal, ma => ma.AsVector3Array());
                result.Add(("NORMAL", a_norm));
            }
            if (geometry.tangents.Count > 0)
            {
                Vector4 makeTangent(Vector3 input, int index)
                {
                    var tangent = Vector3.Normalize(input);
                    if (!tangent.IsFinite())
                    {
                        Log.Default.Warn("Vertex {0} of geometry {1}|{2} has bogus tangent ({3})", index, geometry.SectionId, geometry.HashName, tangent);
                        tangent = new Vector3(0, 1, 0);
                    }
                    if (!tangent.IsUnitLength())
                    {
                        Log.Default.Warn("Vertex {0} of geometry {1}|{2} has bogus tangent length {4} ({3})", index, geometry.SectionId, geometry.HashName, tangent, tangent.Length());
                        tangent = new Vector3(0, 1, 0);
                    }
                    var binorm = geometry.binormals[index];
                    var normal = geometry.normals[index];
                    var txn = Vector3.Cross(tangent, normal);
                    var dot = Vector3.Dot(txn, binorm);
                    if (float.IsNaN(dot))
                    {
                        Log.Default.Warn("Weird normals in vtx {3} of geom {4}|{5}, N={0}, T={1}, B={2}, (T cross N) dot B is NaN", normal, tangent, binorm, index, geometry.SectionId, geometry.HashName);
                        return new Vector4(tangent, 1);
                    }
                    var sgn = float.IsNaN(dot) ? 1 : Math.Sign(dot);
                    return new Vector4(tangent, sgn != 0 ? sgn : 1);
                };
                var a_binorm = MakeVertexAttributeAccessor("vtan", geometry.tangents, 16, GLTF.DimensionType.VEC4, makeTangent, ma => ma.AsVector4Array());
                result.Add(("TANGENT", a_binorm));
            }
            if (geometry.vertex_colors.Count > 0)
            {
                var a_col = MakeVertexAttributeAccessor("vcol", geometry.vertex_colors, 16, GLTF.DimensionType.VEC4, MathUtil.ToVector4, ma => ma.AsVector4Array());
                result.Add(("COLOR_0", a_col));
            }
            for (var i = 0; i < geometry.UVs.Length; i++)
            {
                var uv = geometry.UVs[i];
                if (uv.Count > 0)
                {
                    var a_uv = MakeVertexAttributeAccessor($"vuv_{i}", uv, 12, GLTF.DimensionType.VEC2, FixupUV, ma => ma.AsVector2Array());
                    result.Add(($"TEXCOORD_{i}", a_uv));
                }
            }
            if (geometry.weights.Count > 0)
            {
                Vector4 ConvertWeight(Vector3 weight)
                {
                    float nonZero = 0;
                    if (float.IsNaN(weight.X))
                    {
                        weight.X = 0;
                    }
                    if (float.IsNaN(weight.Y))
                    {
                        weight.Y = 0;
                    }
                    if (float.IsNaN(weight.Z))
                    {
                        weight.Z = 0;
                    }
                    weight = Vector3.Max(weight, Vector3.Zero);
                    if (weight.X > 0)
                    {
                        nonZero += 1;
                    }
                    if (weight.Y > 0)
                    {
                        nonZero += 1;
                    }
                    if (weight.Z > 0)
                    {
                        nonZero += 1;
                    }
                    var total = weight.X + weight.Y + weight.Z;
                    if (Math.Abs(total - 1) > (2e-7f * nonZero))
                    {
                        var fac = 1 / total;
                        weight *= fac;
                    }
                    weight = Vector3.Min(weight, Vector3.One);
                    return new Vector4(weight, 0);
                }
                var a_wght = MakeVertexAttributeAccessor("vweight", geometry.weights, 16, GLTF.DimensionType.VEC4, ConvertWeight, ma => ma.AsVector4Array());
                result.Add(("WEIGHTS_0", a_wght));
            }
            if (geometry.weight_groups.Count > 0)
            {
                Vector4 ConvertWeightGroup(GeometryWeightGroups groups)
                {
                    int Remap(ushort boneIndex)
                    {
                        if (jointRemap.TryGetValue(boneIndex, out var jointIndex)) return jointIndex;
                        return 0;
                    }
                    return new Vector4(Remap(groups.Bones1), Remap(groups.Bones2), Remap(groups.Bones3), Remap(groups.Bones4));
                };
                var a_joint = MakeVertexAttributeAccessor("vjoint", geometry.weight_groups, 8, GLTF.DimensionType.VEC4, ConvertWeightGroup, ma => ma.AsVector4Array(), GLTF.EncodingType.UNSIGNED_SHORT);
                result.Add(("JOINTS_0", a_joint));
            }
            return result;
        }
        Vector2 FixupUV(Vector2 input) => new(input.X, 1 - input.Y);
        GLTF.Accessor MakeVertexAttributeAccessor<TSource, TResult>(string maiName, IList<TSource> source, int stride, GLTF.DimensionType dimtype, Func<TSource, TResult> conv, Func<MemoryAccessor, IList<TResult>> getcontainer, GLTF.EncodingType enc = GLTF.EncodingType.FLOAT, bool normalized = false)
        {
            return MakeVertexAttributeAccessor(maiName, source, stride, dimtype, (s, i) => conv(s), getcontainer, enc, normalized);
        }
        GLTF.Accessor MakeVertexAttributeAccessor<TSource, TResult>(string maiName, IList<TSource> source, int stride, GLTF.DimensionType dimtype, Func<TSource, int, TResult> conv, Func<MemoryAccessor, IList<TResult>> getcontainer, GLTF.EncodingType enc = GLTF.EncodingType.FLOAT, bool normalized = false)
        {
            var mai = new MemoryAccessInfo(maiName, 0, source.Count, stride, dimtype, enc, normalized);
            var ma = new MemoryAccessor(new ArraySegment<byte>(new byte[source.Count * stride]), mai);
            var array = getcontainer(ma);
            for (int i = 0; i < source.Count; i++)
            {
                array[i] = conv(source[i], i);
            }
            var accessor = root.CreateAccessor();
            accessor.SetVertexData(ma);
            return accessor;
        }
    }
}