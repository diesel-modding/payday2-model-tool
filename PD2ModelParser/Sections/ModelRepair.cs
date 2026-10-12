using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PD2ModelParser.Sections
{
    internal static class ModelRepair
    {
        // Stuff to run to repair possible failures.
        public static int RepairAll(FullModelData data)
        {
            var models = data.SectionsOfType<Model>().ToList();
            int repaired = 0;
            foreach (var group in models.Where(m => m.Version != 6 &&
                         m.PassthroughGP?.Topology is Topology topology &&
                         (topology.Primitive_type == Topology.PrimitiveType.PointList ||
                          !Enum.IsDefined(typeof(Topology.PrimitiveType), topology.Primitive_type)))
                         .GroupBy(m => m.PassthroughGP.Topology))
            {
                if (!group.All(HasTriangleEvidence))
                    continue;

                var originalType = group.Key.Primitive_type;
                group.Key.Primitive_type = Topology.PrimitiveType.TriangleList;
                Log.Default.Warn("Repairing .model topology {0}: {1} -> TriangleList ({2} triangles)",
                    group.Key.SectionId, originalType, group.Key.facelist.Count);
                repaired++;
            }

            foreach (var model in models)
                repaired += RepairRenderAtomMetadata(model);

            return repaired;
        }

        // Legacy Tool wrote .model sections with PointList topology instead of TriangleList.
        // Check for valid mesh data and repair the topology.
        private static bool HasTriangleEvidence(Model model)
        {
            var gp = model.PassthroughGP;
            var topology = gp?.Topology;
            var geometry = gp?.DieselGeometry;
            if (topology == null || geometry == null || model.TopologyIP?.Topology != topology ||
                topology.DeclaredIndexCount == null ||
                (ulong)topology.DeclaredIndexCount.Value != (ulong)topology.facelist.Count * 3 ||
                topology.facelist.Count < 2 || geometry.verts == null || geometry.verts.Count < 3 ||
                model.RenderAtoms == null || model.RenderAtoms.Count == 0)
                return false;

            ulong totalIndices = (ulong)topology.facelist.Count * 3;
            int vertexCount = geometry.verts.Count;
            var spans = new List<(int Start, int End)>();
            var edges = new HashSet<(int A, int B)>();
            int validTriangles = 0;
            int sharedEdges = 0;

            foreach (var atom in model.RenderAtoms)
            {
                if (atom.TriangleCount == 0)
                    continue;

                ulong startIndex = atom.BaseIndex;
                ulong endIndex = startIndex + (ulong)atom.TriangleCount * 3;
                if (startIndex % 3 != 0 || endIndex > totalIndices || atom.GeometrySliceLength == 0 ||
                    atom.BaseVertex >= (ulong)vertexCount ||
                    (ulong)atom.BaseVertex + atom.GeometrySliceLength > (ulong)vertexCount)
                    return false;

                int start = (int)(startIndex / 3);
                int end = (int)(endIndex / 3);
                bool local = true;
                bool absolute = true;
                for (int i = start; i < end; i++)
                {
                    var face = topology.facelist[i];
                    foreach (int index in new[] { (int)face.a, (int)face.b, (int)face.c })
                    {
                        local &= index < atom.GeometrySliceLength &&
                                 (ulong)index + atom.BaseVertex < (ulong)vertexCount;
                        absolute &= index >= atom.BaseVertex &&
                                    (ulong)index - atom.BaseVertex < atom.GeometrySliceLength &&
                                    index < vertexCount;
                    }
                }

                if ((!local && !absolute) || (local && absolute && atom.BaseVertex != 0))
                    return false;

                int vertexOffset = local && !absolute ? (int)atom.BaseVertex : 0;
                for (int i = start; i < end; i++)
                {
                    var face = topology.facelist[i];
                    int ia = face.a + vertexOffset;
                    int ib = face.b + vertexOffset;
                    int ic = face.c + vertexOffset;
                    if (ia == ib || ib == ic || ic == ia)
                        continue;

                    var a = geometry.verts[ia];
                    var b = geometry.verts[ib];
                    var c = geometry.verts[ic];
                    if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(a.Z) ||
                        !float.IsFinite(b.X) || !float.IsFinite(b.Y) || !float.IsFinite(b.Z) ||
                        !float.IsFinite(c.X) || !float.IsFinite(c.Y) || !float.IsFinite(c.Z))
                        continue;

                    var ab = b - a;
                    var bc = c - b;
                    var ca = a - c;
                    float maxEdgeSquared = Math.Max(ab.LengthSquared(), Math.Max(bc.LengthSquared(), ca.LengthSquared()));
                    float areaSquared = Vector3.Cross(ab, c - a).LengthSquared();
                    if (!float.IsFinite(maxEdgeSquared) || !float.IsFinite(areaSquared) ||
                        maxEdgeSquared <= 1e-20f || areaSquared <= 1e-12f * maxEdgeSquared * maxEdgeSquared)
                        continue;

                    validTriangles++;
                    var edge = ia < ib ? (ia, ib) : (ib, ia);
                    if (!edges.Add(edge)) sharedEdges++;
                    edge = ib < ic ? (ib, ic) : (ic, ib);
                    if (!edges.Add(edge)) sharedEdges++;
                    edge = ic < ia ? (ic, ia) : (ia, ic);
                    if (!edges.Add(edge)) sharedEdges++;
                }

                spans.Add((start, end));
            }

            if (validTriangles < 2 || sharedEdges == 0 || spans.Count == 0 ||
                validTriangles * 4L < (long)topology.facelist.Count * 3)
                return false;

            if (topology.Primitive_type != Topology.PrimitiveType.PointList &&
                (validTriangles * 10L < (long)topology.facelist.Count * 9 ||
                 sharedEdges * 2L < validTriangles))
                return false;

            spans.Sort((a, b) => a.Start.CompareTo(b.Start));
            int covered = 0;
            foreach (var span in spans)
            {
                if (span.Start != covered)
                    return false;
                covered = span.End;
            }

            return covered == topology.facelist.Count;
        }

        // Model data was injected incorrectly leaving RenderAtom metadata inconsistent with the actual mesh data.
        // Check for valid mesh data and repair the meta data.
        private static int RepairRenderAtomMetadata(Model model)
        {
            if (model.Version == 6 || model.RenderAtoms == null || model.RenderAtoms.Count == 0 || model.PassthroughGP == null)
                return 0;

            var topology = model.PassthroughGP.Topology;
            var geometry = model.PassthroughGP.DieselGeometry;
            if (topology == null || geometry == null ||
                topology.Primitive_type != Topology.PrimitiveType.TriangleList)
                return 0;

            uint totalIndices = checked((uint)topology.facelist.Count * 3u);
            uint totalVertices = geometry.vert_count;
            int repaired = 0;

            for (int i = 0; i < model.RenderAtoms.Count; i++)
            {
                var atom = model.RenderAtoms[i];

                uint indexStart = Math.Min(atom.BaseIndex, totalIndices);
                uint indexEnd = totalIndices;
                for (int j = 0; j < model.RenderAtoms.Count; j++)
                {
                    uint candidate = model.RenderAtoms[j].BaseIndex;
                    if (candidate > indexStart && candidate < indexEnd)
                        indexEnd = candidate;
                }

                uint triangleCount = (indexEnd - indexStart) / 3u;

                uint vertexStart = Math.Min(atom.BaseVertex, totalVertices);
                uint vertexEnd = totalVertices;
                for (int j = 0; j < model.RenderAtoms.Count; j++)
                {
                    uint candidate = model.RenderAtoms[j].BaseVertex;
                    if (candidate > vertexStart && candidate < vertexEnd)
                        vertexEnd = candidate;
                }
                uint sliceLength = vertexEnd - vertexStart;

                if (atom.TriangleCount == triangleCount && atom.GeometrySliceLength == sliceLength)
                    continue;

                Log.Default.Warn(
                    "Repairing .model RenderAtom metadata: model={0}, atom={1}, TriangleCount {2}->{3}, GeometrySliceLength {4}->{5}",
                    model.Name, i, atom.TriangleCount, triangleCount, atom.GeometrySliceLength, sliceLength);

                atom.TriangleCount = triangleCount;
                atom.GeometrySliceLength = sliceLength;
                repaired++;
            }

            return repaired;
        }
    }
}
