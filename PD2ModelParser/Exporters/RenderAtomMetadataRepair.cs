using PD2ModelParser.Sections;
using System;
using System.Collections.Generic;
using System.Linq;

//We need this because of older malformed modded .model files, like LAS stuff.
namespace PD2ModelParser.Exporters
{
    internal static class RenderAtomMetadataRepair
    {
        internal readonly struct Repair
        {
            public readonly int AtomIndex;
            public readonly uint OldTriangleCount;
            public readonly uint NewTriangleCount;
            public readonly uint OldGeometrySliceLength;
            public readonly uint NewGeometrySliceLength;

            public Repair(int atomIndex, uint oldTriangleCount, uint newTriangleCount,
                uint oldGeometrySliceLength, uint newGeometrySliceLength)
            {
                AtomIndex = atomIndex;
                OldTriangleCount = oldTriangleCount;
                NewTriangleCount = newTriangleCount;
                OldGeometrySliceLength = oldGeometrySliceLength;
                NewGeometrySliceLength = newGeometrySliceLength;
            }
        }

        public static List<Repair> RepairModel(Model model)
        {
            var repairs = new List<Repair>();
            if (model?.RenderAtoms == null || model.RenderAtoms.Count == 0 || model.PassthroughGP == null)
                return repairs;

            var topology = model.PassthroughGP.Topology;
            var geometry = model.PassthroughGP.DieselGeometry;
            if (topology == null || geometry == null)
                return repairs;

            uint totalIndices = checked((uint)topology.facelist.Count * 3u);
            uint totalVertices = geometry.vert_count;
            var atoms = model.RenderAtoms;

            for (int i = 0; i < atoms.Count; i++)
            {
                var atom = atoms[i];

                uint indexStart = Math.Min(atom.BaseIndex, totalIndices);
                uint indexEnd = totalIndices;
                for (int j = 0; j < atoms.Count; j++)
                {
                    uint candidate = atoms[j].BaseIndex;
                    if (candidate > indexStart && candidate < indexEnd)
                        indexEnd = candidate;
                }

                // Only complete triangles are representable by RenderAtom.TriangleCount.
                uint actualIndexCount = indexEnd >= indexStart ? indexEnd - indexStart : 0;
                uint actualTriangleCount = actualIndexCount / 3u;

                uint vertexStart = Math.Min(atom.BaseVertex, totalVertices);
                uint vertexEnd = totalVertices;
                for (int j = 0; j < atoms.Count; j++)
                {
                    uint candidate = atoms[j].BaseVertex;
                    if (candidate > vertexStart && candidate < vertexEnd)
                        vertexEnd = candidate;
                }
                uint actualGeometrySliceLength = vertexEnd >= vertexStart ? vertexEnd - vertexStart : 0;

                if (atom.TriangleCount != actualTriangleCount ||
                    atom.GeometrySliceLength != actualGeometrySliceLength)
                {
                    repairs.Add(new Repair(
                        i,
                        atom.TriangleCount,
                        actualTriangleCount,
                        atom.GeometrySliceLength,
                        actualGeometrySliceLength));

                    Log.Default.Warn(
                        "Repairing RenderAtom metadata: model={0}, atom={1}, TriangleCount {2}->{3}, GeometrySliceLength {4}->{5}",
                        model.Name, i, atom.TriangleCount, actualTriangleCount,
                        atom.GeometrySliceLength, actualGeometrySliceLength);

                    atom.TriangleCount = actualTriangleCount;
                    atom.GeometrySliceLength = actualGeometrySliceLength;
                }
            }

            return repairs;
        }

        public static int RepairAll(FullModelData data)
        {
            if (data == null) return 0;
            int repaired = 0;
            foreach (var model in data.parsed_sections.Values.OfType<Model>())
                repaired += RepairModel(model).Count;
            return repaired;
        }
    }
}
