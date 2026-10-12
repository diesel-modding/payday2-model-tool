using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using PD2ModelParser.Importers;

namespace PD2ModelParser.Sections
{
    internal enum ValidationSeverity { Info, Warning, Error }

    internal sealed record ValidationIssue(ValidationSeverity Severity, string Code, uint SectionId, string Description)
    {
        public override string ToString() => $"{Severity,-7} {Code,-21} [{SectionId}] {Description}";
    }

    internal sealed class ValidationReport
    {
        public List<ValidationIssue> Issues { get; } = [];
        public int Sections { get; set; }
        public int GeometrySections { get; set; }
        public long Vertices { get; set; }
        public long Triangles { get; set; }
        public int Errors => Issues.Count(i => i.Severity == ValidationSeverity.Error);
        public int Warnings => Issues.Count(i => i.Severity == ValidationSeverity.Warning);
        public int Information => Issues.Count(i => i.Severity == ValidationSeverity.Info);

        public void Add(ValidationSeverity severity, string code, uint id, string description)
            => Issues.Add(new ValidationIssue(severity, code, id, description));

        public void Print(string title)
        {
            Console.WriteLine($"\n{title}: {Sections} sections, {GeometrySections} geometries, {Vertices} vertices, {Triangles} triangle records");
            Console.WriteLine($"{Errors} errors, {Warnings} warnings, {Information} informational findings");
            foreach (var issue in Issues.OrderByDescending(i => i.Severity).ThenBy(i => i.SectionId).ThenBy(i => i.Code))
                Console.WriteLine(issue);
        }
    }

    internal static class ModelValidator
    {
        public static (ValidationReport Original, ValidationReport Repaired, int Repairs) CheckFile(string path)
        {
            var model = ModelReader.Open(path, applyRepairs: false);
            long fileSize = new FileInfo(path).Length;
            var original = Check(model, fileSize);
            try
            {
                int fixes = ModelRepair.RepairAll(model);
                var repaired = Check(model, fileSize);
                return (original, repaired, fixes);
            }
            catch (Exception e)
            {
                var failed = Check(model, fileSize);
                failed.Add(ValidationSeverity.Error, "REPAIR_FAILED", 0, e.Message);
                return (original, failed, 0);
            }
        }

        public static ValidationReport Check(FullModelData data, long? fileSize = null)
        {
            var r = new ValidationReport { Sections = data.sections.Count };
            CheckSections(data, fileSize, r);
            CheckTopologies(data, r);
            r.Triangles = data.SectionsOfType<Topology>().Sum(t => (long)t.facelist.Count);
            foreach (var geom in data.SectionsOfType<DieselGeometry>())
                CheckGeometry(geom, r);
            foreach (var model in data.SectionsOfType<Model>())
                CheckModel(model, data, r);
            CheckObjects(data, r);
            CheckMaterials(data, r);
            CheckAnimations(data, r);
            CheckSkin(data, r);
            return r;
        }

        private static void CheckSections(FullModelData data, long? fileSize, ValidationReport r)
        {
            foreach (var group in data.sections.GroupBy(s => s.id).Where(g => g.Count() > 1))
                r.Add(ValidationSeverity.Error, "DUPLICATE_SECTION_ID", group.Key, $"{group.Count()} headers use this ID");
            long previousEnd = -1;
            foreach (var header in data.sections)
            {
                if (previousEnd > header.offset)
                    r.Add(ValidationSeverity.Error, "SECTION_OVERLAP", header.id, $"Starts at {header.offset}, previous ends at {previousEnd}");
                previousEnd = Math.Max(previousEnd, header.End);
                if (fileSize.HasValue && (header.offset < 0 || header.End > fileSize.Value))
                    r.Add(ValidationSeverity.Error, "SECTION_OUT_OF_FILE", header.id, $"Ends at {header.End}, file length is {fileSize.Value}");
                if (!data.parsed_sections.TryGetValue(header.id, out var section))
                    r.Add(ValidationSeverity.Error, "SECTION_NOT_PARSED", header.id, "Header has no parsed section");
                else if (section.TypeCode != header.type)
                    r.Add(ValidationSeverity.Error, "SECTION_TYPE_MISMATCH", header.id, $"Header 0x{header.type:X8}, parsed 0x{section.TypeCode:X8}");
            }
            foreach (var section in data.parsed_sections)
            {
                if (section.Key != section.Value.SectionId)
                    r.Add(ValidationSeverity.Error, "SECTION_ID_MISMATCH", section.Key, $"Object carries ID {section.Value.SectionId}");
                if (section.Value is Unknown)
                    r.Add(ValidationSeverity.Info, "UNKNOWN_SECTION", section.Key, "Unknown data is preserved; its contents cannot be validated");
            }
            if (fileSize.HasValue && previousEnd > 0 && previousEnd < fileSize.Value &&
                data.leftover_data?.Length > 0)
                r.Add(ValidationSeverity.Info, "TRAILING_BYTES", 0, $"{data.leftover_data.Length} bytes after last section");
        }

        private static void CheckTopologies(FullModelData data, ValidationReport r)
        {
            foreach (var topology in data.SectionsOfType<Topology>())
            {
                if (!Enum.IsDefined(typeof(Topology.PrimitiveType), topology.Primitive_type))
                    r.Add(ValidationSeverity.Error, "INVALID_PRIMITIVE_TYPE", topology.SectionId,
                        $"Unsupported primitive value 0x{(uint)topology.Primitive_type:X8}; {topology.facelist.Count} triangle records");
            }
        }

        private static void CheckGeometry(DieselGeometry g, ValidationReport r)
        {
            r.GeometrySections++;
            r.Vertices += g.vert_count;
            uint id = g.SectionId;
            int n = g.verts.Count;
            if (n != g.vert_count)
                r.Add(ValidationSeverity.Error, "VERTEX_COUNT", id, $"Header declares {g.vert_count}, position data has {n}");
            foreach (var repeat in g.Headers.GroupBy(h => h.ItemType).Where(h => h.Count() > 1))
                r.Add(ValidationSeverity.Warning, "REPEATED_CHANNEL", id, $"{repeat.Key} appears {repeat.Count()} times; decoding may overwrite data");
            if (!g.Headers.Any(h => h.ItemType == GeometryChannelTypes.POSITION0))
                r.Add(ValidationSeverity.Error, "MISSING_POSITION", id, "No POSITION0 channel");
            foreach (var head in g.Headers)
            {
                int count = GetChannelCount(g, head.ItemType);
                if (count >= 0 && count != n)
                    r.Add(ValidationSeverity.Error, "CHANNEL_LENGTH", id, $"{head.ItemType} contains {count} values for {n} vertices");
                if (head.ItemSize > 9 || head.ItemSize == 0)
                    r.Add(ValidationSeverity.Error, "CHANNEL_FORMAT", id, $"{head.ItemType} uses unsupported item size {head.ItemSize}");
                if (head.ItemType >= GeometryChannelTypes.TEXCOORD8 && head.ItemType <= GeometryChannelTypes.TEXCOORD9)
                    r.Add(ValidationSeverity.Error, "UV_CHANNEL_UNSUPPORTED", id, $"{head.ItemType} has no decoded storage in this importer");
            }
            CheckVectors(g.verts, id, "POSITION", r, checkLength: false);
            if (g.normals.Count > 0) CheckVectors(g.normals, id, "NORMAL", r, checkLength: true);
            if (g.uvDirectionU.Count > 0) CheckVectors(g.uvDirectionU, id, "UV_DIRECTION_U", r, checkLength: true);
            if (g.uvDirectionV.Count > 0) CheckVectors(g.uvDirectionV, id, "UV_DIRECTION_V", r, checkLength: true);
            foreach (var (uv, index) in g.UVs.Select((uv, i) => (uv, i)))
            {
                if (uv == null || uv.Count == 0) continue;
                int nonfinite = uv.Count(v => !float.IsFinite(v.X) || !float.IsFinite(v.Y));
                if (nonfinite > 0)
                    r.Add(ValidationSeverity.Error, "NONFINITE_UV", id, $"UV{index} contains {nonfinite} non-finite entries, first at {uv.FindIndex(v => !float.IsFinite(v.X) || !float.IsFinite(v.Y))}");
            }
            int badWeights = g.weights.Count(w => !Finite(w) || w.X < -0.001f || w.Y < -0.001f || w.Z < -0.001f);
            badWeights += g.fourth_weights.Count(w => !float.IsFinite(w) || w < -0.001f);
            if (badWeights > 0)
                r.Add(ValidationSeverity.Error, "INVALID_WEIGHTS", id, $"{badWeights} non-finite or negative blend weight entries");
            if (g.weight_groups.Count > 0 && g.weight_groups.Count != n)
                r.Add(ValidationSeverity.Error, "BLEND_INDICES_LENGTH", id, $"{g.weight_groups.Count} blend-index groups for {n} vertices");
            if (g.weights.Count > 0 && g.weights.Count != n)
                r.Add(ValidationSeverity.Error, "BLEND_WEIGHTS_LENGTH", id, $"{g.weights.Count} blend-weight entries for {n} vertices");
            if (g.Headers.Any(h => h.ItemType == GeometryChannelTypes.BLENDWEIGHT0 && h.ItemSize == 4)
                && g.fourth_weights.Count != n)
                r.Add(ValidationSeverity.Error, "FOURTH_WEIGHT_LENGTH", id, $"{g.fourth_weights.Count} fourth weights for {n} vertices");
        }

        private static int GetChannelCount(DieselGeometry g, GeometryChannelTypes type) => type switch
        {
            GeometryChannelTypes.POSITION0 => g.verts.Count,
            GeometryChannelTypes.NORMAL0 => g.normals.Count,
            GeometryChannelTypes.COLOR0 => g.vertex_colors.Count,
            GeometryChannelTypes.BLENDINDICES0 => g.weight_groups.Count,
            GeometryChannelTypes.BLENDWEIGHT0 => g.Format == DieselGeometry.GeometryFormat.RaidLegacy &&
                g.Headers.Any(h => h.ItemType == GeometryChannelTypes.BLENDWEIGHT0 && h.ItemSize == 7) ? g.weight_groups.Count : g.weights.Count,
            GeometryChannelTypes.UV_DIRECTION_U0 or GeometryChannelTypes.UV_DIRECTION_U1 => g.uvDirectionU.Count,
            GeometryChannelTypes.UV_DIRECTION_V0 or GeometryChannelTypes.UV_DIRECTION_V1 => g.uvDirectionV.Count,
            >= GeometryChannelTypes.TEXCOORD0 and <= GeometryChannelTypes.TEXCOORD7 => g.UVs[(int)(type - GeometryChannelTypes.TEXCOORD0)].Count,
            _ => -1
        };

        private static void CheckVectors(IReadOnlyList<Vector3> values, uint id, string channel, ValidationReport r, bool checkLength)
        {
            int invalid = 0, zero = 0, firstInvalid = -1, firstZero = -1;
            for (int i = 0; i < values.Count; i++)
            {
                if (!Finite(values[i]))
                {
                    invalid++;
                    if (firstInvalid < 0) firstInvalid = i;
                }
                else if (checkLength && values[i].LengthSquared() < 1e-12f)
                {
                    zero++;
                    if (firstZero < 0) firstZero = i;
                }
            }
            if (invalid > 0)
                r.Add(ValidationSeverity.Error, "NONFINITE_" + channel, id, $"{invalid} vectors, first vertex {firstInvalid}");
            if (zero > 0)
                r.Add(ValidationSeverity.Warning, "ZERO_" + channel, id, $"{zero} zero-length vectors, first vertex {firstZero}");
        }

        private static void CheckModel(Model model, FullModelData data, ValidationReport r)
        {
            if (model.Version == 6) return;
            uint id = model.SectionId;
            var gp = model.PassthroughGP;
            var topo = gp?.Topology;
            var geom = gp?.DieselGeometry;
            if (gp == null || topo == null || geom == null || model.TopologyIP?.Topology == null)
            {
                r.Add(ValidationSeverity.Error, "BROKEN_MODEL_REFERENCE", id, "Missing geometry, topology, passthrough or topology-IP reference");
                return;
            }
            if (!ReferenceEquals(model.TopologyIP.Topology, topo))
                r.Add(ValidationSeverity.Error, "TOPOLOGY_MISMATCH", id, $"Passthrough and TopologyIP reference different topologies");
            if (!Contains(data, gp) || !Contains(data, topo) || !Contains(data, geom) || !Contains(data, model.TopologyIP))
                r.Add(ValidationSeverity.Error, "MISSING_SECTION_REFERENCE", id, "Mesh refers to a section not contained in the model");
            if (model.MaterialGroup != null && !Contains(data, model.MaterialGroup))
                r.Add(ValidationSeverity.Error, "MATERIAL_GROUP_REFERENCE", id, "Material group is not present in the model");
            if (model.SkinBones != null && !Contains(data, model.SkinBones))
                r.Add(ValidationSeverity.Error, "SKIN_REFERENCE", id, "SkinBones section is not present in the model");
            if (model.RenderAtoms == null)
            {
                r.Add(ValidationSeverity.Error, "MISSING_RENDERATOMS", id, "Null RenderAtom collection");
                return;
            }
            if (topo.Primitive_type != Topology.PrimitiveType.TriangleList)
            {
                if (topo.Primitive_type == Topology.PrimitiveType.PointList && model.RenderAtoms.Any(a => a.TriangleCount > 0) && topo.facelist.Count > 0)
                    r.Add(ValidationSeverity.Warning, "POINTLIST_WITH_TRIANGLES", topo.SectionId, $"Model {id} has {topo.facelist.Count} triangle records; inspect possible legacy mislabeling");
                else if (Enum.IsDefined(typeof(Topology.PrimitiveType), topo.Primitive_type))
                    r.Add(ValidationSeverity.Info, "NON_TRIANGLE_TOPOLOGY", topo.SectionId, $"{topo.Primitive_type}; triangle-specific checks skipped");
                return;
            }
            if (model.RenderAtoms.Count == 0 && topo.facelist.Count > 0)
                r.Add(ValidationSeverity.Warning, "NO_RENDERATOMS", id, $"Topology {topo.SectionId} contains triangles but has no RenderAtoms");
            var covered = new bool[topo.facelist.Count];
            int vertexCount = geom.verts.Count;
            for (int i = 0; i < model.RenderAtoms.Count; i++)
            {
                var a = model.RenderAtoms[i];
                if (a.TriangleCount == 0)
                {
                    if (a.BaseIndex > (ulong)topo.facelist.Count * 3 || a.BaseVertex > geom.vert_count)
                        r.Add(ValidationSeverity.Info, "EMPTY_RENDERATOM_OFFSET", id, $"Atom {i} is empty and has out-of-range unused offsets");
                    continue;
                }
                ulong start = a.BaseIndex;
                ulong end = start + (ulong)a.TriangleCount * 3;
                if (start % 3 != 0 || end > (ulong)topo.facelist.Count * 3)
                {
                    r.Add(ValidationSeverity.Error, "ATOM_INDEX_RANGE", id, $"Atom {i}: indices [{start}, {end}) exceed {topo.facelist.Count * 3} or are unaligned");
                    continue;
                }
                if (a.BaseVertex >= geom.vert_count || (ulong)a.BaseVertex + a.GeometrySliceLength > geom.vert_count)
                    r.Add(ValidationSeverity.Error, "ATOM_VERTEX_RANGE", id, $"Atom {i}: vertices [{a.BaseVertex}, {(ulong)a.BaseVertex + a.GeometrySliceLength}) exceed {geom.vert_count}");
                bool local = a.GeometrySliceLength > 0;
                bool absolute = a.GeometrySliceLength > 0;
                int badTriangles = 0, zeroArea = 0, degenerateUv = 0, overlaps = 0;
                int firstIndex = -1;
                for (int f = (int)(start / 3); f < (int)(end / 3); f++)
                {
                    if (covered[f]) overlaps++;
                    covered[f] = true;
                    var face = topo.facelist[f];
                    var indices = new[] { (int)face.a, (int)face.b, (int)face.c };
                    bool localFace = indices.All(index => (ulong)index + a.BaseVertex < (ulong)vertexCount && index < a.GeometrySliceLength);
                    bool absoluteFace = indices.All(index => index < vertexCount && index >= a.BaseVertex && (ulong)index - a.BaseVertex < a.GeometrySliceLength);
                    local &= localFace;
                    absolute &= absoluteFace;
                    if (!localFace && !absoluteFace)
                    {
                        badTriangles++;
                        if (firstIndex < 0) firstIndex = f;
                    }
                }
                if (overlaps > 0)
                    r.Add(ValidationSeverity.Error, "ATOM_OVERLAP", id, $"Atom {i}: {overlaps} overlapping triangle references");
                if (badTriangles > 0)
                    r.Add(ValidationSeverity.Error, "TRIANGLE_INDEX_RANGE", id, $"Atom {i}: {badTriangles} triangles cannot fit in the declared vertex slice, first triangle {firstIndex}");
                else if (local && absolute && a.BaseVertex != 0)
                    r.Add(ValidationSeverity.Warning, "AMBIGUOUS_INDEX_SPACE", id, $"Atom {i}: local and absolute indices both fit; resave cannot safely determine indexing");
                else
                {
                    int offset = local && !absolute ? (int)a.BaseVertex : 0;
                    for (int f = (int)(start / 3); f < (int)(end / 3); f++)
                    {
                        var face = topo.facelist[f];
                        int x = face.a + offset, y = face.b + offset, z = face.c + offset;
                        var p = geom.verts[x]; var q = geom.verts[y]; var t = geom.verts[z];
                        if (!Finite(p) || !Finite(q) || !Finite(t)) continue;
                        var cross = Vector3.Cross(q - p, t - p);
                        float edge = Math.Max(Vector3.DistanceSquared(p, q), Math.Max(Vector3.DistanceSquared(q, t), Vector3.DistanceSquared(t, p)));
                        if (!Finite(cross) || edge <= 1e-25f || cross.LengthSquared() <= 1e-12f * edge * edge)
                            zeroArea++;
                        if (geom.Uv0.Count == vertexCount)
                        {
                            var u = geom.Uv0[x]; var v = geom.Uv0[y]; var w = geom.Uv0[z];
                            if (Finite(u) && Finite(v) && Finite(w))
                            {
                                double determinant = (double)(v.X - u.X) * (w.Y - u.Y) - (double)(v.Y - u.Y) * (w.X - u.X);
                                if (Math.Abs(determinant) <= 1e-12)
                                    degenerateUv++;
                            }
                        }
                    }
                }
                if (zeroArea > 0)
                    r.Add(ValidationSeverity.Warning, "DEGENERATE_TRIANGLES", id, $"Atom {i}: {zeroArea} near-zero-area triangles (geometry {geom.SectionId})");
                if (degenerateUv > 0)
                    r.Add(ValidationSeverity.Warning, "DEGENERATE_UV", id, $"Atom {i}: {degenerateUv} triangles have collapsed primary UVs; geometric directions need fallback");
                if (a.MaterialId >= (model.MaterialGroup?.Items.Count ?? 0))
                    r.Add(ValidationSeverity.Error, "MATERIAL_SLOT_RANGE", id, $"Atom {i}: material {a.MaterialId}, available slots {model.MaterialGroup?.Items.Count ?? 0}");
            }
            int uncovered = covered.Count(v => !v);
            if (uncovered > 0)
                r.Add(ValidationSeverity.Warning, "UNREFERENCED_TRIANGLES", id, $"{uncovered} topology triangles are not referenced by any nonempty RenderAtom");
        }

        private static void CheckObjects(FullModelData data, ValidationReport r)
        {
            var objects = data.SectionsOfType<Object3D>().ToList();
            var members = new HashSet<Object3D>(objects);
            foreach (var node in objects)
            {
                if (!Finite(node.Transform))
                    r.Add(ValidationSeverity.Error, "NONFINITE_TRANSFORM", node.SectionId, "Transform contains NaN or infinity");
                if (node.Parent != null && !members.Contains(node.Parent))
                    r.Add(ValidationSeverity.Error, "INVALID_PARENT", node.SectionId, $"Parent {node.Parent.SectionId} is not present in this model");
                var seen = new HashSet<Object3D>();
                var parent = node;
                while (parent != null && seen.Add(parent) && seen.Count <= objects.Count + 1)
                    parent = parent.Parent;
                if (parent != null)
                    r.Add(ValidationSeverity.Error, "PARENT_CYCLE", node.SectionId, "Parent chain contains a cycle");
                foreach (var controller in node.Animations.Where(c => c != null))
                    if (!data.parsed_sections.TryGetValue(controller.SectionId, out var section) || !ReferenceEquals(controller, section))
                        r.Add(ValidationSeverity.Error, "BROKEN_ANIMATION_REF", node.SectionId, "Non-null controller reference is missing or points to the wrong section");
            }
        }

        private static void CheckMaterials(FullModelData data, ValidationReport r)
        {
            foreach (var group in data.SectionsOfType<MaterialGroup>())
                for (int i = 0; i < group.Items.Count; i++)
                    if (group.Items[i] == null || !data.parsed_sections.TryGetValue(group.Items[i].SectionId, out var stored) || !ReferenceEquals(stored, group.Items[i]))
                        r.Add(ValidationSeverity.Error, "MATERIAL_REF", group.SectionId, $"Material slot {i} has a broken reference");
        }

        private static void CheckAnimations(FullModelData data, ValidationReport r)
        {
            var linked = new HashSet<IAnimationController>(data.SectionsOfType<Object3D>().SelectMany(o => o.Animations).Where(a => a != null));
            foreach (var controller in data.SectionsOfType<IAnimationController>())
            {
                if (!linked.Contains(controller))
                    r.Add(ValidationSeverity.Info, "UNREFERENCED_CONTROLLER", controller.SectionId, "No Object3D directly references this controller");
                if (!float.IsFinite(controller.KeyframeLength) || controller.KeyframeLength < 0)
                    r.Add(ValidationSeverity.Error, "ANIMATION_DURATION", controller.SectionId, $"Invalid duration {controller.KeyframeLength}");
                switch (controller)
                {
                    case IAnimationController<Vector3> c:
                        CheckTrack(c.Keyframes, controller.SectionId, r, Finite);
                        break;
                    case IAnimationController<Quaternion> c:
                        CheckTrack(c.Keyframes, controller.SectionId, r, q => Finite(q) && q.LengthSquared() > 1e-12f);
                        int nonUnit = c.Keyframes.Count(k => Finite(k.Value) && Math.Abs(k.Value.LengthSquared() - 1.0f) > 0.1f);
                        if (nonUnit > 0)
                            r.Add(ValidationSeverity.Warning, "QUATERNION_LENGTH", controller.SectionId, $"{nonUnit} rotation keys have non-unit quaternions");
                        break;
                    case IAnimationController<float> c:
                        CheckTrack(c.Keyframes, controller.SectionId, r, float.IsFinite);
                        break;
                }
            }
            foreach (var animation in data.SectionsOfType<Animation>())
                if (!float.IsFinite(animation.Keyframe_length) || animation.Items.Any(v => !float.IsFinite(v)))
                    r.Add(ValidationSeverity.Error, "ANIMATION_DATA", animation.SectionId, "Non-finite duration or animation data");
        }

        private static void CheckTrack<T>(IList<Keyframe<T>> keys, uint id, ValidationReport r, Func<T, bool> finite)
        {
            int invalid = 0, outOfOrder = 0;
            float last = float.NegativeInfinity;
            foreach (var key in keys)
            {
                if (!float.IsFinite(key.Timestamp) || !finite(key.Value)) invalid++;
                if (key.Timestamp < last) outOfOrder++;
                last = key.Timestamp;
            }
            if (invalid > 0)
                r.Add(ValidationSeverity.Error, "INVALID_KEYFRAMES", id, $"{invalid} non-finite or invalid keyframes");
            if (outOfOrder > 0)
                r.Add(ValidationSeverity.Error, "UNSORTED_KEYFRAMES", id, $"{outOfOrder} timestamp reversals");
        }

        private static void CheckSkin(FullModelData data, ValidationReport r)
        {
            foreach (var skin in data.SectionsOfType<SkinBones>())
            {
                if (skin.Objects.Count != skin.Rotations.Count)
                    r.Add(ValidationSeverity.Error, "SKIN_LENGTH_MISMATCH", skin.SectionId, $"{skin.Objects.Count} objects, {skin.Rotations.Count} bind matrices");
                if (skin.Objects.Any(o => o == null || !data.parsed_sections.ContainsKey(o.SectionId)))
                    r.Add(ValidationSeverity.Error, "SKIN_BONE_REFERENCE", skin.SectionId, "Skin references a missing object");
                int bad = skin.Rotations.Count(m => !Finite(m));
                if (!Finite(skin.Global_skin_transform)) bad++;
                if (bad > 0)
                    r.Add(ValidationSeverity.Error, "SKIN_TRANSFORM", skin.SectionId, $"{bad} non-finite bind/global transforms");
            }
        }

        private static bool Contains(FullModelData data, ISection section) =>
            section != null && data.parsed_sections.TryGetValue(section.SectionId, out var entry) && ReferenceEquals(section, entry);

        private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
        private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        private static bool Finite(Quaternion q) => float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W);
        private static bool Finite(Matrix4x4 m) =>
            float.IsFinite(m.M11) && float.IsFinite(m.M12) && float.IsFinite(m.M13) && float.IsFinite(m.M14) &&
            float.IsFinite(m.M21) && float.IsFinite(m.M22) && float.IsFinite(m.M23) && float.IsFinite(m.M24) &&
            float.IsFinite(m.M31) && float.IsFinite(m.M32) && float.IsFinite(m.M33) && float.IsFinite(m.M34) &&
            float.IsFinite(m.M41) && float.IsFinite(m.M42) && float.IsFinite(m.M43) && float.IsFinite(m.M44);
    }
}
