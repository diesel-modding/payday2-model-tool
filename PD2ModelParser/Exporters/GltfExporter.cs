using PD2ModelParser.Sections;
using SharpGLTF.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GLTF = SharpGLTF.Schema2;
namespace PD2ModelParser.Exporters
{
    internal class GltfExporter
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
        private FullModelData data;
        private GLTF.ModelRoot root;
        private GLTF.Scene scene;
        private Dictionary<ISection, GLTF.Material> materialsBySection;
        private Dictionary<ISection, GLTF.Node> nodesBySection;
        private List<(Model, GLTF.Node)> toSkin;
        private readonly float scaleFactor = 0.01f;
        private static List<Object3D> GetSkeletonObjects(SkinBones skinBones)
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
        private GLTF.ModelRoot Convert(FullModelData data)
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
        private void CreateNodeFromObject3D(Object3D thing, GLTF.IVisualNodeContainer parent)
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
                if (mod.Version == 3)
                {
                    node.Mesh = GetMeshForModel(mod);
                    if (mod.SkinBones != null)
                    {
                        toSkin.Add((mod, node));
                    }
                }
                else if (mod.Version == 6)
                {
                    if (IsPrimitiveModel(mod))
                    {
                        node.Mesh = CreatePrimitiveMesh(mod);
                        Log.Default.Warn("EXPORT PRIMITIVE: Name={0}, BoundsMin={1}, BoundsMax={2}, DistanceRadius={3}", mod.Name, mod.BoundsMin, mod.BoundsMax, mod.DistanceRadius);
                    }
                }
                else
                {
                    throw new Exception($"Model {mod.Name} is of unknown version {mod.Version}");
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
        private void SkinModel(Model model, GLTF.Node node)
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
                    ibm = skinbones.Rotations[skinBoneIndex];
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
        private static bool IsPrimitiveModel(Model model)
        {
            if (model == null) return false;
            if (model.Version != 6) return false;
            if (model.PassthroughGP != null) return false;
            if (model.TopologyIP != null) return false;
            string name = model.HashName?.String ?? model.Name ?? "";
            return
            name.StartsWith("c_sphere_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("c_capsule_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("c_box_", StringComparison.OrdinalIgnoreCase);
        }
        private GLTF.Mesh CreatePrimitiveMesh(Model model)
        {
            string name = model.HashName?.String ?? model.Name ?? "";
            if (name.StartsWith("c_sphere_", StringComparison.OrdinalIgnoreCase)) return CreateSphereMesh(model);
            if (name.StartsWith("c_capsule_", StringComparison.OrdinalIgnoreCase)) return CreateCapsuleMesh(model);
            if (name.StartsWith("c_box_", StringComparison.OrdinalIgnoreCase)) return CreateBoxMesh(model);
            return null;
        }
        private GLTF.Mesh CreateBoxMesh(Model model)
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
        private GLTF.Mesh CreateSphereMesh(Model model)
        {
            Vector3 size = (model.BoundsMax - model.BoundsMin) * scaleFactor;
            float Radius = MathF.Min(MathF.Min(size.X, size.Y), size.Z) * 0.5f;
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
                    vertices.Add(center + new Vector3(sinPhi * cosTheta * Radius, sinPhi * sinTheta * Radius, cosPhi * Radius));
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
        private GLTF.Mesh CreateCapsuleMesh(Model model)
        {
            Vector3 size = (model.BoundsMax - model.BoundsMin) * scaleFactor;
            Vector3 center = (model.BoundsMin + model.BoundsMax) * 0.5f* scaleFactor;
            int axis;
            if (size.X >= size.Y && size.X >= size.Z) axis = 0;
            else if (size.Y >= size.X && size.Y >= size.Z) axis = 1;
            else axis = 2;
            float largest = axis == 0 ? size.X : axis == 1 ? size.Y : size.Z;
            float radiusX = axis == 0 ? size.Z * 0.5f : size.X * 0.5f;
            float radiusY = axis == 1 ? size.Z * 0.5f : size.Y * 0.5f;
            float capRadius = MathF.Min(radiusX, radiusY);
            float cylinderLength = MathF.Max(0, largest - capRadius * 2.0f);
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
                float z = -halfCylinder + MathF.Sin(phi) * capRadius;
                float ringScale = MathF.Cos(phi);
                AddCapsuleRing(vertices, center, radiusX * ringScale, radiusY * ringScale, z, segments);
            }
            for (int y = 1; y < cylinderRings; y++)
            {
                float t = (float)y / cylinderRings;
                float z = -halfCylinder + t * cylinderLength;
                AddCapsuleRing(vertices, center, radiusX, radiusY, z, segments);
            }
            for (int y = 0; y <= hemisphereRings; y++)
            {
                float t = (float)y / hemisphereRings;
                float phi = t * MathF.PI * 0.5f;
                float z = halfCylinder + MathF.Sin(phi) * capRadius;
                float ringScale = MathF.Cos(phi);
                AddCapsuleRing(vertices, center, radiusX * ringScale, radiusY * ringScale, z, segments);
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
        private static void AddCapsuleRing(List<Vector3> vertices, Vector3 center, float radiusX, float radiusY, float z, int segments)
        {
            for (int x = 0; x <= segments; x++)
            {
                float u = (float)x / segments;
                float theta = u * MathF.PI * 2.0f;
                vertices.Add(center + new Vector3(MathF.Cos(theta) * radiusX, MathF.Sin(theta) * radiusY, z));
            }
        }
        private GLTF.Mesh CreateGeneratedMesh(string name, IList<Vector3> vertices, ushort[] indices)
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
        private GLTF.Accessor CreateIndexAccessor(string name, ushort[] indices)
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
        private GLTF.Mesh GetMeshForModel(Model model)
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
            foreach (var (indexAccessor, material) in CreatePrimitiveIndices(topology, model.RenderAtoms, materialGroup, geometry))
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
        private IEnumerable<(GLTF.Accessor, GLTF.Material)> CreatePrimitiveIndices(Topology topo, IEnumerable<RenderAtom> atoms, MaterialGroup materialGroup, DieselGeometry geometry)
        {
            var rawIndices = new ushort[topo.facelist.Count * 3];
            for (int i = 0; i < topo.facelist.Count; i++)
            {
                rawIndices[i * 3 + 0] = topo.facelist[i].a;
                rawIndices[i * 3 + 1] = topo.facelist[i].b;
                rawIndices[i * 3 + 2] = topo.facelist[i].c;
            }

            var atomList = atoms.ToList();
            var localIndexModes = atomList
                .Select(ra => DetectLocalIndices(rawIndices, ra, geometry))
                .ToList();
            int localVotes = localIndexModes.Count(mode => mode == true);
            int absoluteVotes = localIndexModes.Count(mode => mode == false);
            bool? modelIndexMode = localVotes > absoluteVotes ? true :
                                   absoluteVotes > localVotes ? false : null;

            for (int atomIndex = 0; atomIndex < atomList.Count; atomIndex++)
            {
                var ra = atomList[atomIndex];
                int indexCount = (int)ra.TriangleCount * 3;
                int baseIndex = (int)ra.BaseIndex;
                bool useLocalIndices = localIndexModes[atomIndex] ??
                                       modelIndexMode ??
                                       ra.BaseVertex != 0;
                var resolvedIndices = new ushort[indexCount];

                for (int i = 0; i < indexCount; i++)
                {
                    uint index = rawIndices[baseIndex + i];
                    if (useLocalIndices) index += ra.BaseVertex;
                    resolvedIndices[i] = (ushort)index;
                }

                var accessor = CreateIndexAccessor($"indices_{topo.HashName}_{atomIndex}", resolvedIndices);
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

        private static bool? DetectLocalIndices(ushort[] indices, RenderAtom atom, DieselGeometry geometry)
        {
            uint vertexCount = geometry.vert_count;
            int start = (int)atom.BaseIndex;
            int count = (int)atom.TriangleCount * 3;
            bool localValid = atom.GeometrySliceLength > 0;
            bool absoluteSliceValid = atom.GeometrySliceLength > 0;
            bool absoluteGeometryValid = true;

            for (int i = 0; i < count; i++)
            {
                uint index = indices[start + i];
                localValid &= index < atom.GeometrySliceLength &&
                              index + atom.BaseVertex < vertexCount &&
                              index + atom.BaseVertex <= ushort.MaxValue;
                absoluteSliceValid &= index >= atom.BaseVertex &&
                                      index - atom.BaseVertex < atom.GeometrySliceLength &&
                                      index < vertexCount;
                absoluteGeometryValid &= index < vertexCount;
            }

            if (localValid && !absoluteSliceValid) return true;
            if (absoluteSliceValid && !localValid) return false;
            if (localValid && absoluteSliceValid)
            {
                float localScore = ScoreIndexInterpretation(indices, atom, geometry, true);
                float absoluteScore = ScoreIndexInterpretation(indices, atom, geometry, false);
                const float scoreTolerance = 0.05f;

                if (localScore > absoluteScore + scoreTolerance) return true;
                if (absoluteScore > localScore + scoreTolerance) return false;
            }
            if (!localValid && !absoluteSliceValid && absoluteGeometryValid) return false;
            return null;
        }

        private static float ScoreIndexInterpretation(ushort[] indices, RenderAtom atom, DieselGeometry geometry, bool localIndices)
        {
            if (geometry.normals.Count != geometry.verts.Count) return float.NegativeInfinity;

            int start = (int)atom.BaseIndex;
            int count = (int)atom.TriangleCount * 3;
            float score = 0;
            int samples = 0;

            for (int i = 0; i < count; i += 3)
            {
                int indexA = indices[start + i + 0] + (localIndices ? (int)atom.BaseVertex : 0);
                int indexB = indices[start + i + 1] + (localIndices ? (int)atom.BaseVertex : 0);
                int indexC = indices[start + i + 2] + (localIndices ? (int)atom.BaseVertex : 0);
                var faceNormal = Vector3.Cross(
                    geometry.verts[indexB] - geometry.verts[indexA],
                    geometry.verts[indexC] - geometry.verts[indexA]);

                if (!faceNormal.IsFinite() || faceNormal.LengthSquared() < 1e-20f) continue;
                faceNormal = Vector3.Normalize(faceNormal);

                foreach (int vertexIndex in new[] { indexA, indexB, indexC })
                {
                    var normal = geometry.normals[vertexIndex];
                    if (!normal.IsFinite() || normal.LengthSquared() < 1e-20f) continue;
                    score += MathF.Abs(Vector3.Dot(faceNormal, Vector3.Normalize(normal)));
                    samples++;
                }
            }

            return samples > 0 ? score / samples : float.NegativeInfinity;
        }
        private List<(string, GLTF.Accessor)> GetGeometryAttributes(DieselGeometry geometry, Dictionary<int, int> jointRemap)
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
            if (geometry.uvDirectionU.Count == geometry.vert_count &&
                geometry.uvDirectionV.Count == geometry.vert_count &&
                geometry.normals.Count == geometry.vert_count)
            {
                Vector4 MakeTangent(Vector3 directionU, int index)
                {
                    var normal = Vector3.Normalize(geometry.normals[index]);
                    var tangent = directionU - normal * Vector3.Dot(normal, directionU);

                    if (!normal.IsFinite() || !tangent.IsFinite() || tangent.LengthSquared() < 1e-20f)
                    {
                        Log.Default.Warn("Vertex {0} of geometry {1}|{2} has an unusable UV U direction ({3})", index, geometry.SectionId, geometry.HashName, directionU);
                        normal = normal.IsFinite() ? normal : Vector3.UnitZ;
                        var axis = MathF.Abs(normal.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
                        tangent = axis - normal * Vector3.Dot(normal, axis);
                    }

                    tangent = Vector3.Normalize(tangent);
                    var directionV = geometry.uvDirectionV[index];
                    var handedness = Vector3.Dot(Vector3.Cross(tangent, normal), directionV);

                    if (!float.IsFinite(handedness))
                    {
                        Log.Default.Warn("Vertex {0} of geometry {1}|{2} has an unusable UV V direction ({3})", index, geometry.SectionId, geometry.HashName, directionV);
                        return new Vector4(tangent, 1);
                    }

                    var sign = Math.Sign(handedness);
                    return new Vector4(tangent, sign != 0 ? sign : 1);
                }

                var a_tangent = MakeVertexAttributeAccessor("vtan", geometry.uvDirectionU, 16, GLTF.DimensionType.VEC4, MakeTangent, ma => ma.AsVector4Array());
                result.Add(("TANGENT", a_tangent));
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
        private Vector2 FixupUV(Vector2 input) => new(input.X, 1 - input.Y);
        private GLTF.Accessor MakeVertexAttributeAccessor<TSource, TResult>(string maiName, IList<TSource> source, int stride, GLTF.DimensionType dimtype, Func<TSource, TResult> conv, Func<MemoryAccessor, IList<TResult>> getcontainer, GLTF.EncodingType enc = GLTF.EncodingType.FLOAT, bool normalized = false)
        {
            return MakeVertexAttributeAccessor(maiName, source, stride, dimtype, (s, i) => conv(s), getcontainer, enc, normalized);
        }
        private GLTF.Accessor MakeVertexAttributeAccessor<TSource, TResult>(string maiName, IList<TSource> source, int stride, GLTF.DimensionType dimtype, Func<TSource, int, TResult> conv, Func<MemoryAccessor, IList<TResult>> getcontainer, GLTF.EncodingType enc = GLTF.EncodingType.FLOAT, bool normalized = false)
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
