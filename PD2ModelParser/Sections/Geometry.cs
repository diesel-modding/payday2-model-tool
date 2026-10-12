using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static PD2ModelParser.Sections.DieselGeometry;
namespace PD2ModelParser.Sections
{
    public class GeometryWeightGroups
    {
        public readonly ushort Bones1;
        public readonly ushort Bones2;
        public readonly ushort Bones3;
        public readonly ushort Bones4;
        public GeometryWeightGroups(ushort b1, ushort b2, ushort b3, ushort b4)
        {
            Bones1 = b1;
            Bones2 = b2;
            Bones3 = b3;
            Bones4 = b4;
        }
        public GeometryWeightGroups(BinaryReader instream)
        {
            Bones1 = instream.ReadUInt16();
            Bones2 = instream.ReadUInt16();
            Bones3 = instream.ReadUInt16();
            Bones4 = instream.ReadUInt16();
        }
        public void StreamWrite(BinaryWriter outstream)
        {
            outstream.Write(Bones1);
            outstream.Write(Bones2);
            outstream.Write(Bones3);
            outstream.Write(Bones4);
        }
    }
    readonly public struct GeometryColor
    {
        public readonly byte red;
        public readonly byte green;
        public readonly byte blue;
        public readonly byte alpha;
        public GeometryColor(byte red, byte green, byte blue, byte alpha)
        {
            this.red = red;
            this.green = green;
            this.blue = blue;
            this.alpha = alpha;
        }
        public GeometryColor(BinaryReader instream)
        {
            blue = instream.ReadByte();
            green = instream.ReadByte();
            red = instream.ReadByte();
            alpha = instream.ReadByte();
        }
        public void StreamWrite(BinaryWriter outstream)
        {
            outstream.Write(blue);
            outstream.Write(green);
            outstream.Write(red);
            outstream.Write(alpha);
        }
    }
    public class GeometryHeader
    {
        public UInt32 ItemSize
        {
            get;
            set;
        }
        public GeometryChannelTypes ItemType
        {
            get;
            set;
        }
        public GeometryHeader()
        { }
        public GeometryHeader(UInt32 size, GeometryChannelTypes type)
        {
            ItemSize = size;
            ItemType = type;
        }
    }
    public enum GeometryChannelTypes
    {
        POSITION0 = 1,
        NORMAL0 = 2,
        POSITION1 = 3,
        NORMAL1 = 4,
        COLOR0 = 5,
        COLOR1 = 6,
        TEXCOORD0 = 7,
        TEXCOORD1 = 8,
        TEXCOORD2 = 9,
        TEXCOORD3 = 10,
        TEXCOORD4 = 11,
        TEXCOORD5 = 12,
        TEXCOORD6 = 13,
        TEXCOORD7 = 14,
        TEXCOORD8 = 15,
        TEXCOORD9 = 16,
        BLENDINDICES0 = 17,
        BLENDINDICES1 = 18,
        BLENDWEIGHT0 = 19,
        BLENDWEIGHT1 = 20,
        POINTSIZE0 = 21,
        UV_DIRECTION_V0 = 22,
        UV_DIRECTION_U0 = 23,
        UV_DIRECTION_V1 = 24,
        UV_DIRECTION_U1 = 25,
    }
    [ModelFileSection(Tags.geometry_tag)]
    internal class DieselGeometry : AbstractSection, ISection, IHashNamed
    {
        public uint vert_count;
        public List<Vector2>[] UVs = new List<Vector2>[8];
        public List<Vector2> Uv0 => UVs[0];
        public List<Vector2> Uv1 => UVs[1];
        public List<GeometryHeader> Headers{ get; private set;} = [];
        public List<Vector3> verts = [];
        public List<Vector3> position1 = [];
        public List<Vector3> normals = [];
        public List<Vector3> normal1 = [];
        public List<GeometryColor> vertex_colors = [];
        public List<GeometryColor> vertex_colors1 = [];
        public List<GeometryWeightGroups> weight_groups = [];
        public List<GeometryWeightGroups> weight_groups1 = [];
        public List<Vector3> weights = [];
        public List<float> fourth_weights = [];
        public List<Vector4> weights1 = [];
        public List<Vector3> uvDirectionV = [];
        public List<Vector3> uvDirectionU = [];
        public List<float> point_sizes = [];
        public enum GeometryFormat
        {
            Payday,
            Raid,
            RaidLegacy
        }
        public GeometryFormat Format
        {
            get;
            private set;
        }
        public HashName HashName
        {
            get;
            set;
        }
        public byte[] remaining_data = null;
        private static float UnpackSignedByte(byte value)
        {
            return (value / 255.0f) * 2.0f - 1.0f;
        }
        private static byte PackSignedByte(float value)
        {
            value = Math.Clamp(value, -1.0f, 1.0f);
            return (byte)((value + 1.0f) * 127.5f);
        }
        private static Vector3 ReadPackedVector3(BinaryReader instream)
        {
            byte z = instream.ReadByte();
            byte y = instream.ReadByte();
            byte x = instream.ReadByte();
            instream.ReadByte();
            return new Vector3(UnpackSignedByte(x), UnpackSignedByte(y), UnpackSignedByte(z));
        }
        private static void WritePackedVector3(BinaryWriter outstream, Vector3 value)
        {
            outstream.Write(PackSignedByte(value.Z));
            outstream.Write(PackSignedByte(value.Y));
            outstream.Write(PackSignedByte(value.X));
            outstream.Write((byte)0);
        }
        private static Vector3 ReadFloatVector3(BinaryReader instream)
        {
            return new Vector3(instream.ReadSingle(), instream.ReadSingle(), instream.ReadSingle());
        }
        private static void WriteFloatVector3(BinaryWriter outstream, Vector3 value)
        {
            outstream.Write(value.X);
            outstream.Write(value.Y);
            outstream.Write(value.Z);
        }
        private Vector3 ReadVector3ByType(BinaryReader instream, uint type)
        {
            if (type == 3) return ReadFloatVector3(instream);
            if (type == 8)
            {
                if (Format == GeometryFormat.Raid) return ReadFloatVector3(instream);
                return ReadPackedVector3(instream);
            }
            throw new Exception($"Unsupported Vector3 geometry type {type}");
        }
        private void WriteVector3ByType(BinaryWriter outstream, Vector3 value, uint type)
        {
            if (type == 3)
            {
                WriteFloatVector3(outstream, value);
                return;
            }
            if (type == 8)
            {
                if (Format == GeometryFormat.Raid) WriteFloatVector3(outstream, value);
                else WritePackedVector3(outstream, value);
                return;
            }
            throw new Exception($"Unsupported Vector3 geometry type {type}");
        }
        private static float ReadHalf(BinaryReader instream)
        {
            ushort raw = instream.ReadUInt16();
            return (float)BitConverter.UInt16BitsToHalf(raw);
        }
        private static void WriteHalf(BinaryWriter outstream, float value)
        {
            ushort raw = BitConverter.HalfToUInt16Bits((Half)value);
            outstream.Write(raw);
        }

        private static readonly uint[] PaydayItemSizes = [0, 4, 8, 12, 16, 4, 4, 8, 4, 4];
        private static readonly uint[] RaidItemSizes = [0, 4, 8, 12, 16, 4, 4, 8, 12, 8];
        private uint GetItemSizeBytes(GeometryHeader head)
        {
            uint[] sizes = Format == GeometryFormat.Raid ? RaidItemSizes : PaydayItemSizes;
            return sizes[(int)head.ItemSize];
        }
        public DieselGeometry Clone()
        {
            var src = this;
            var dst = new DieselGeometry
            {
                Format = src.Format,
                vert_count = vert_count
            };
            dst.Headers.AddRange(src.Headers.Select(i => new GeometryHeader(i.ItemSize, i.ItemType)));
            dst.verts.AddRange(src.verts);
            dst.position1.AddRange(src.position1);
            for (int i = 0; i < src.UVs.Length; i++) dst.UVs[i].AddRange(src.UVs[i]);
            dst.normals.AddRange(src.normals);
            dst.normal1.AddRange(src.normal1);
            dst.vertex_colors.AddRange(src.vertex_colors);
            dst.vertex_colors1.AddRange(src.vertex_colors1);
            dst.weight_groups.AddRange(src.weight_groups);
            dst.weight_groups1.AddRange(src.weight_groups1);
            dst.weights.AddRange(src.weights);
            dst.fourth_weights.AddRange(src.fourth_weights);
            dst.weights1.AddRange(src.weights1);
            dst.uvDirectionV.AddRange(src.uvDirectionV);
            dst.uvDirectionU.AddRange(src.uvDirectionU);
            dst.point_sizes.AddRange(src.point_sizes);
            dst.HashName = src.HashName;
            dst.remaining_data = src.remaining_data == null ? null : (byte[])src.remaining_data.Clone();
            return dst;
        }
        public DieselGeometry()
        {
            SectionId = 0;
            for (int i = 0; i < UVs.Length; i++) UVs[i] = [];
        }
        public DieselGeometry(Obj_data newobject) : this()
        {
            vert_count = (uint)newobject.Verts.Count;
            Headers.Add(new GeometryHeader(3, GeometryChannelTypes.POSITION0));
            Headers.Add(new GeometryHeader(9, GeometryChannelTypes.TEXCOORD0));
            Headers.Add(new GeometryHeader(8, GeometryChannelTypes.NORMAL0));
            Headers.Add(new GeometryHeader(8, GeometryChannelTypes.UV_DIRECTION_V0));
            Headers.Add(new GeometryHeader(8, GeometryChannelTypes.UV_DIRECTION_U0));
            verts = newobject.Verts;
            UVs[0] = newobject.Uv;
            normals = newobject.Normals;
            HashName = new HashName(newobject.Object_name + ".DieselGeometry");
        }
        public DieselGeometry(BinaryReader instream, SectionHeader section) : this()
        {
            SectionId = section.id;
            vert_count = instream.ReadUInt32();
            uint header_count = instream.ReadUInt32();
            for (int x = 0; x < header_count; x++)
            {
                GeometryHeader header = new()
                {
                    ItemSize = instream.ReadUInt32()
                };
                uint itemType = instream.ReadUInt32();
                if (section.legacy && itemType > (uint)GeometryChannelTypes.TEXCOORD7) itemType += 2;
                header.ItemType = (GeometryChannelTypes)itemType;
                Headers.Add(header);
            }
            if (section.legacy)
            {
                Format = Headers.Any(h => h.ItemSize == 9) ? GeometryFormat.RaidLegacy : GeometryFormat.Payday;
            }
            else if (Headers.Any(h => h.ItemSize == 9))
            {
                uint paydayBytesPerVertex = 0;
                uint raidBytesPerVertex = 0;
                foreach (GeometryHeader head in Headers)
                {
                    paydayBytesPerVertex += PaydayItemSizes[(int)head.ItemSize];
                    raidBytesPerVertex += RaidItemSizes[(int)head.ItemSize];
                }
                long sectionEnd = section.offset + 12 + section.size;
                long availableBytes = sectionEnd - instream.BaseStream.Position;
                long paydayExpected = (long)paydayBytesPerVertex * vert_count + 8;
                long raidExpected = (long)raidBytesPerVertex * vert_count + 8;
                Format = availableBytes == raidExpected ? GeometryFormat.Raid : GeometryFormat.Payday;
            }
            else
            {
                Format = GeometryFormat.Payday;
            }
            foreach (GeometryHeader head in Headers)
            {
                if (head.ItemType == GeometryChannelTypes.POSITION0)
                {
                    verts.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < vert_count; x++)
                    {
                        verts.Add(new Vector3(instream.ReadSingle(), instream.ReadSingle(), instream.ReadSingle()));
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.NORMAL0)
                {
                    normals.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < vert_count; x++) normals.Add(ReadVector3ByType(instream, head.ItemSize));
                }
                else if (head.ItemType == GeometryChannelTypes.COLOR0)
                {
                    vertex_colors.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < vert_count; x++) vertex_colors.Add(new GeometryColor(instream));
                }
                else if (head.ItemType == GeometryChannelTypes.UV_DIRECTION_V0 || head.ItemType == GeometryChannelTypes.UV_DIRECTION_V1)
                {
                    uvDirectionV.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < vert_count; x++) uvDirectionV.Add(ReadVector3ByType(instream, head.ItemSize));
                }
                else if (head.ItemType == GeometryChannelTypes.UV_DIRECTION_U0 || head.ItemType == GeometryChannelTypes.UV_DIRECTION_U1)
                {
                    uvDirectionU.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < vert_count; x++) uvDirectionU.Add(ReadVector3ByType(instream, head.ItemSize));
                }
                else if (head.ItemType == GeometryChannelTypes.BLENDINDICES0)
                {
                    weight_groups.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < vert_count; x++) weight_groups.Add(new GeometryWeightGroups(instream));
                }
                else if (head.ItemType == GeometryChannelTypes.BLENDWEIGHT0)
                {
                    if (Format == GeometryFormat.RaidLegacy && head.ItemSize == 7)
                    {
                        weight_groups.Capacity = (int)vert_count + 1;
                        for (int x = 0; x < vert_count; x++) weight_groups.Add(new GeometryWeightGroups(instream));
                    }
                    else
                    {
                        if (head.ItemSize != 2 && head.ItemSize != 3 && head.ItemSize != 4)
                        {
                            throw new Exception($"Bad BLENDWEIGHT0 item size {head.ItemSize}");
                        }
                        weights.Capacity = (int)vert_count + 1;
                        for (int x = 0; x < vert_count; x++)
                        {
                            Vector3 weights_entry = new()
                            {
                                X = instream.ReadSingle(),
                                Y = instream.ReadSingle()
                            };
                            if (head.ItemSize >= 3)
                            {
                                weights_entry.Z = instream.ReadSingle();
                            }
                            if (head.ItemSize == 4)
                            {
                                fourth_weights.Add(instream.ReadSingle());
                            }
                            weights.Add(weights_entry);
                        }
                    }
                }
                else if (head.ItemType >= GeometryChannelTypes.TEXCOORD0 && head.ItemType <= GeometryChannelTypes.TEXCOORD9)
                {
                    int idx = head.ItemType - GeometryChannelTypes.TEXCOORD0;
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector2 uv;
                        if (head.ItemSize == 2)
                        {
                            uv = new Vector2(instream.ReadSingle(), -instream.ReadSingle());
                        }
                        else if (head.ItemSize == 9)
                        {
                            if (Format == GeometryFormat.Raid)
                            {
                                uv = new Vector2(instream.ReadSingle(), -instream.ReadSingle());
                            }
                            else
                            {
                                uv = new Vector2(ReadHalf(instream), -ReadHalf(instream));
                            }
                        }
                        else
                        {
                            throw new Exception($"Unsupported TEXCOORD type {head.ItemSize}");
                        }
                        UVs[idx].Add(uv);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.POINTSIZE0)
                {
                    if (Format == GeometryFormat.RaidLegacy && head.ItemSize == 3)
                    {
                        weights.Capacity = (int)vert_count + 1;
                        for (int x = 0; x < vert_count; x++) weights.Add(new Vector3(instream.ReadSingle(), instream.ReadSingle(), instream.ReadSingle()));
                    }
                    else
                    {
                        throw new InvalidDataException($"Unsupported POINTSIZE item size {head.ItemSize} for format {Format}");
                    }
                }
                else
                {
                    throw new InvalidDataException($"Unsupported geometry channel type: {head.ItemType} " + $"(type {(uint)head.ItemType}, item size {head.ItemSize})");
                }
            }
            HashName = new HashName(instream.ReadUInt64());
            remaining_data = null;
            long sect_end = section.offset + 12 + section.size;
            if (sect_end > instream.BaseStream.Position)
            {
                remaining_data = instream.ReadBytes(
                    (int)(sect_end - instream.BaseStream.Position));
            }
        }
        public override void StreamWriteData(BinaryWriter outstream)
        {
            List<Vector3> verts = this.verts;
            List<Vector3> normals = [.. this.normals];
            List<GeometryWeightGroups> weight_groups = this.weight_groups;
            List<Vector3> uvDirectionV = this.uvDirectionV;
            List<Vector3> uvDirectionU = this.uvDirectionU;
            if (vert_count != verts.Count)
            {
                throw new InvalidDataException($"DieselGeometry {HashName}: vert_count={vert_count}, verts={verts.Count}.");
            }
            foreach (var head in Headers)
            {
                if (head.ItemType == GeometryChannelTypes.BLENDWEIGHT0)
                {
                    if (Format == GeometryFormat.RaidLegacy && head.ItemSize == 7)
                    {
                        if (weight_groups.Count != vert_count)
                        {
                            throw new InvalidDataException($"DieselGeometry {HashName}: RaidLegacy BLENDWEIGHT0 expects {vert_count} weight groups, got {weight_groups.Count}.");
                        }
                    }
                    else
                    {
                        if (head.ItemSize != 2 && head.ItemSize != 3 && head.ItemSize != 4)
                        {
                            throw new InvalidDataException($"DieselGeometry {HashName}: invalid BLENDWEIGHT0 item size {head.ItemSize}.");
                        }
                        if (weights.Count != vert_count)
                        {
                            throw new InvalidDataException($"DieselGeometry {HashName}: BLENDWEIGHT0 expects {vert_count} weights, got {weights.Count}.");
                        }
                        if (head.ItemSize == 4 && fourth_weights.Count != vert_count)
                        {
                            throw new InvalidDataException($"DieselGeometry {HashName}: BLENDWEIGHT0 expects {vert_count} fourth weight components, got {fourth_weights.Count}.");
                        }
                    }
                }
                if (head.ItemType == GeometryChannelTypes.BLENDINDICES0 && weight_groups.Count != vert_count)
                {
                    throw new InvalidDataException($"DieselGeometry {HashName}: BLENDINDICES0 expects {vert_count} weight groups, got {weight_groups.Count}.");
                }
            }
            outstream.Write(vert_count);
            outstream.Write(Headers.Count);
            foreach (GeometryHeader head in Headers)
            {
                outstream.Write(head.ItemSize);
                outstream.Write((uint)head.ItemType);
            }
            int vert_pos = 0;
            int norm_pos = 0;
            foreach (GeometryHeader head in Headers)
            {
                if (head.ItemType == GeometryChannelTypes.POSITION0)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector3 vert = verts[vert_pos++];
                        outstream.Write(vert.X);
                        outstream.Write(vert.Y);
                        outstream.Write(vert.Z);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.NORMAL0)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector3 norm = normals[norm_pos++];
                        WriteVector3ByType(outstream, norm, head.ItemSize);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.COLOR0)
                {
                    for (int x = 0; x < vert_count; x++) vertex_colors[x].StreamWrite(outstream);
                }
                else if (head.ItemType == GeometryChannelTypes.UV_DIRECTION_V0 || head.ItemType == GeometryChannelTypes.UV_DIRECTION_V1)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector3 value = uvDirectionV.Count == vert_count ? uvDirectionV[x] : Vector3.Zero;
                        WriteVector3ByType(outstream, value, head.ItemSize);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.UV_DIRECTION_U0 || head.ItemType == GeometryChannelTypes.UV_DIRECTION_U1)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector3 value = uvDirectionU.Count == vert_count ? uvDirectionU[x] : Vector3.Zero;
                        WriteVector3ByType(outstream, value, head.ItemSize);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.BLENDINDICES0)
                {
                    for (int x = 0; x < vert_count; x++) weight_groups[x].StreamWrite(outstream);
                }
                else if (head.ItemType == GeometryChannelTypes.BLENDWEIGHT0)
                {
                    if (Format == GeometryFormat.RaidLegacy && head.ItemSize == 7)
                    {
                        for (int x = 0; x < vert_count; x++) weight_groups[x].StreamWrite(outstream);
                    }
                    else
                    {
                        for (int x = 0; x < vert_count; x++)
                        {
                            Vector3 weight = weights[x];
                            outstream.Write(weight.X);
                            outstream.Write(weight.Y);
                            if (head.ItemSize == 3)
                            {
                                outstream.Write(weight.Z);
                            }
                            else if (head.ItemSize == 4)
                            {
                                outstream.Write(weight.Z);
                                outstream.Write(fourth_weights[x]);
                            }
                        }
                    }
                }
                else if (head.ItemType >= GeometryChannelTypes.TEXCOORD0 && head.ItemType <= GeometryChannelTypes.TEXCOORD9)
                {
                    int idx = head.ItemType - GeometryChannelTypes.TEXCOORD0;
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector2 uv = UVs[idx][x];
                        if (head.ItemSize == 2)
                        {
                            outstream.Write(uv.X);
                            outstream.Write(-uv.Y);
                        }
                        else if (head.ItemSize == 9)
                        {
                            if (Format == GeometryFormat.Raid)
                            {
                                outstream.Write(uv.X);
                                outstream.Write(-uv.Y);
                            }
                            else
                            {
                                WriteHalf(outstream, uv.X);
                                WriteHalf(outstream, -uv.Y);
                            }
                        }
                        else
                        {
                            throw new Exception($"Unsupported TEXCOORD type {head.ItemSize}");
                        }
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.POINTSIZE0)
                {
                    if (Format == GeometryFormat.RaidLegacy && head.ItemSize == 3)
                    {
                        if (weights.Count != vert_count)
                        {
                            throw new InvalidDataException("RaidLegacy POINTSIZE0 -> BLENDWEIGHT0 data is missing.");
                        }
                        for (int x = 0; x < vert_count; x++)
                        {
                            Vector3 weight = weights[x];
                            outstream.Write(weight.X);
                            outstream.Write(weight.Y);
                            outstream.Write(weight.Z);
                        }
                    }
                    else
                    {
                        if (point_sizes.Count != vert_count)
                        {
                            throw new InvalidDataException("POINTSIZE0 data is missing.");
                        }
                        for (int x = 0; x < vert_count; x++) outstream.Write(point_sizes[x]);
                    }
                }
                else
                {
                    throw new InvalidDataException($"Unsupported geometry channel type: {(uint)head.ItemType} " + $"(item size {head.ItemSize}, " + $"{GetItemSizeBytes(head)} bytes/vertex, " + $"format {Format})");
                }
            }
            outstream.Write(HashName.Hash);
            if (remaining_data != null) outstream.Write(remaining_data);
        }
        public override string ToString()
        {
            return base.ToString() + " Count: " + vert_count + " Headers: " + Headers.Count + " Verts: " + verts.Count + " UV0: " + Uv0.Count + " UV1: " + Uv1.Count + " Normals: " + normals.Count + " Weight Groups: " + weight_groups.Count + " Weights: " + weights.Count + " UV Direction V: " + uvDirectionV.Count + " UV Direction U: " + uvDirectionU.Count;
        }

        public const float UvDeterminantEpsilon = 1e-6f;

        public static void ComputeUvDirections(
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<Vector2> uvs,
            IReadOnlyList<Face> faces,
            out List<Vector3> directionU,
            out List<Vector3> directionV)
        {
            var accumulatedU = new Vector3[positions.Count];
            var accumulatedV = new Vector3[positions.Count];

            foreach (Face face in faces)
            {
                Vector3 edge1 = positions[face.b] - positions[face.a];
                Vector3 edge2 = positions[face.c] - positions[face.a];
                Vector2 uv1 = uvs[face.b] - uvs[face.a];
                Vector2 uv2 = uvs[face.c] - uvs[face.a];

                float determinant = uv1.X * uv2.Y - uv2.X * uv1.Y;
                if (!float.IsFinite(determinant) || MathF.Abs(determinant) < UvDeterminantEpsilon)
                    continue;

                float inverse = 1.0f / determinant;
                Vector3 faceU = (edge1 * uv2.Y - edge2 * uv1.Y) * inverse;
                Vector3 faceV = (edge1 * uv2.X - edge2 * uv1.X) * inverse;

                accumulatedU[face.a] += faceU;
                accumulatedU[face.b] += faceU;
                accumulatedU[face.c] += faceU;
                accumulatedV[face.a] += faceV;
                accumulatedV[face.b] += faceV;
                accumulatedV[face.c] += faceV;
            }

            directionU = [.. accumulatedU.Select(NormalizeDirection)];
            directionV = [.. accumulatedV.Select(NormalizeDirection)];
        }

        public static void ComputeUvDirections(
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<Vector2> uvs,
            IReadOnlyList<Face> faces,
            IReadOnlyList<Vector3> normals,
            IReadOnlyList<float?> tangentHandedness,
            out List<Vector3> directionU,
            out List<Vector3> directionV)
        {
            ComputeUvDirections(positions, uvs, faces, out directionU, out directionV);

            if (normals == null || normals.Count != positions.Count)
                return;

            if (tangentHandedness != null && tangentHandedness.Any(h => h.HasValue))
            {
                FillMissingUvDirections(
                    faces, normals, tangentHandedness, directionU, directionV);

                for (int i = 0; i < positions.Count; i++)
                {
                    if (!tangentHandedness[i].HasValue)
                        continue;

                    Vector3 n = normals[i];
                    Vector3 u = directionU[i];
                    Vector3 v = directionV[i];
                    float h = Vector3.Dot(Vector3.Cross(u, n), v);

                    if (float.IsFinite(h) && MathF.Abs(h) > 1e-8f &&
                        (h < 0 ? -1.0f : 1.0f) != tangentHandedness[i].Value)
                    {
                        directionV[i] = -v;
                    }
                }
            }
            else
            {
                StabilizeUvOrientationRegions(
                    uvs, normals, faces, directionU, directionV);
            }
        }

        public static void ComputeObjUvDirections(
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<Vector2> uvs,
            IReadOnlyList<Face> faces,
            IReadOnlyList<Vector3> normals,
            IReadOnlyList<(ushort Position, ushort Uv)> sourceVertices,
            out List<Vector3> directionU,
            out List<Vector3> directionV)
        {
            ComputeUvDirections(positions, uvs, faces, out directionU, out directionV);

            if (normals == null || normals.Count != positions.Count ||
                sourceVertices == null || sourceVertices.Count != positions.Count)
                return;

            StabilizeUvOrientationRegions(
                sourceVertices, uvs, normals, faces, directionU, directionV);
        }

        private static void StabilizeUvOrientationRegions(
            IReadOnlyList<(ushort Position, ushort Uv)> sourceVertices,
            IReadOnlyList<Vector2> uvs,
            IReadOnlyList<Vector3> normals,
            IReadOnlyList<Face> faces,
            IList<Vector3> directionU,
            IList<Vector3> directionV)
        {
            const float minUvArea = UvDeterminantEpsilon;
            int triangleCount = faces.Count;
            var sign = new sbyte[triangleCount];
            var reliable = new bool[triangleCount];
            var neighbours = new List<int>[triangleCount];
            var edges = new Dictionary<((ushort, ushort), (ushort, ushort)), List<int>>();

            static int Compare((ushort Position, ushort Uv) a, (ushort Position, ushort Uv) b) =>
                a.Position != b.Position ? a.Position.CompareTo(b.Position) : a.Uv.CompareTo(b.Uv);

            static ((ushort, ushort), (ushort, ushort)) Edge(
                (ushort Position, ushort Uv) a,
                (ushort Position, ushort Uv) b) =>
                Compare(a, b) <= 0 ? (a, b) : (b, a);

            for (int i = 0; i < triangleCount; i++)
            {
                neighbours[i] = [];
                Face f = faces[i];
                Vector2 a = uvs[f.a], b = uvs[f.b], c = uvs[f.c];
                float det = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);

                if (float.IsFinite(det) && MathF.Abs(det) >= minUvArea)
                {
                    sign[i] = det < 0 ? (sbyte)-1 : (sbyte)1;
                    reliable[i] = true;
                }

                foreach (var edge in new[]
                {
                    Edge(sourceVertices[f.a], sourceVertices[f.b]),
                    Edge(sourceVertices[f.b], sourceVertices[f.c]),
                    Edge(sourceVertices[f.c], sourceVertices[f.a])
                })
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

            var ambiguousVisited = new bool[triangleCount];

            for (int start = 0; start < triangleCount; start++)
            {
                if (sign[start] != 0 || ambiguousVisited[start])
                    continue;

                var component = new List<int>();
                var queue = new Queue<int>();
                sbyte boundarySign = 0;
                bool conflictingBoundary = false;

                queue.Enqueue(start);
                ambiguousVisited[start] = true;

                while (queue.Count > 0)
                {
                    int t = queue.Dequeue();
                    component.Add(t);

                    foreach (int n in neighbours[t])
                    {
                        if (sign[n] == 0)
                        {
                            if (!ambiguousVisited[n])
                            {
                                ambiguousVisited[n] = true;
                                queue.Enqueue(n);
                            }
                            continue;
                        }

                        if (boundarySign == 0)
                            boundarySign = sign[n];
                        else if (boundarySign != sign[n])
                            conflictingBoundary = true;
                    }
                }

                if (boundarySign == 0 || conflictingBoundary)
                    continue;

                foreach (int t in component)
                    sign[t] = boundarySign;
            }
            var expected = new sbyte[uvs.Count];
            var hasReliableLocal = new bool[uvs.Count];
            var vertexNeighbours = new HashSet<int>[uvs.Count];
            for (int i = 0; i < vertexNeighbours.Length; i++)
                vertexNeighbours[i] = [];

            for (int t = 0; t < triangleCount; t++)
            {
                Face f = faces[t];
                vertexNeighbours[f.a].Add(f.b); vertexNeighbours[f.a].Add(f.c);
                vertexNeighbours[f.b].Add(f.a); vertexNeighbours[f.b].Add(f.c);
                vertexNeighbours[f.c].Add(f.a); vertexNeighbours[f.c].Add(f.b);

                foreach (int v in new[] { (int)f.a, (int)f.b, (int)f.c })
                {
                    if (reliable[t])
                        hasReliableLocal[v] = true;

                    if (sign[t] == 0)
                        continue;

                    if (expected[v] == 0)
                        expected[v] = sign[t];
                    else if (expected[v] != sign[t])
                        expected[v] = 2;
                }
            }

            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < uvs.Count; i++)
                {
                    if (hasReliableLocal[i] || expected[i] is not (1 or -1))
                        continue;

                    float currentULengthSq = directionU[i].LengthSquared();
                    float currentVLengthSq = directionV[i].LengthSquared();
                    if (float.IsFinite(currentULengthSq) &&
                        float.IsFinite(currentVLengthSq) &&
                        currentULengthSq > 1e-20f &&
                        currentVLengthSq > 1e-20f)
                        continue;

                    Vector3 sumU = Vector3.Zero, sumV = Vector3.Zero;
                    int count = 0;
                    foreach (int n in vertexNeighbours[i])
                    {
                        if (expected[n] != expected[i]) continue;

                        float neighbourULengthSq = directionU[n].LengthSquared();
                        float neighbourVLengthSq = directionV[n].LengthSquared();
                        if (!float.IsFinite(neighbourULengthSq) ||
                            !float.IsFinite(neighbourVLengthSq) ||
                            neighbourULengthSq <= 1e-20f ||
                            neighbourVLengthSq <= 1e-20f) continue;

                        sumU += directionU[n];
                        sumV += directionV[n];
                        count++;
                    }

                    if (count == 0) continue;

                    float sumULengthSq = sumU.LengthSquared();
                    float sumVLengthSq = sumV.LengthSquared();
                    if (!float.IsFinite(sumULengthSq) ||
                        !float.IsFinite(sumVLengthSq) ||
                        sumULengthSq <= 1e-20f ||
                        sumVLengthSq <= 1e-20f) continue;

                    directionU[i] = sumU / MathF.Sqrt(sumULengthSq);
                    directionV[i] = sumV / MathF.Sqrt(sumVLengthSq);
                    changed = true;
                }
            } while (changed);

            for (int i = 0; i < uvs.Count; i++)
            {
                if (hasReliableLocal[i] || expected[i] is not (1 or -1))
                    continue;

                Vector3 n = normals[i], u = directionU[i], v = directionV[i];
                if (n.LengthSquared() <= 1e-20f ||
                    u.LengthSquared() <= 1e-20f ||
                    v.LengthSquared() <= 1e-20f)
                    continue;

                float handedness = Vector3.Dot(Vector3.Cross(u, n), v);
                if (!float.IsFinite(handedness) || MathF.Abs(handedness) <= 1e-8f)
                    continue;

                sbyte actual = handedness < 0 ? (sbyte)-1 : (sbyte)1;
                if (actual != expected[i])
                    directionV[i] = -v;
            }
        }

        public bool RebuildUvDirections(IReadOnlyList<Face> faces,
            out int fallbackVertices, out int repairedNormals)
        {
            fallbackVertices = 0;
            repairedNormals = 0;
            int count = verts.Count;
            if (count == 0 || Uv0.Count != count || normals.Count != count ||
                !Headers.Any(h => h.ItemType == GeometryChannelTypes.UV_DIRECTION_U0 || h.ItemType == GeometryChannelTypes.UV_DIRECTION_U1) ||
                !Headers.Any(h => h.ItemType == GeometryChannelTypes.UV_DIRECTION_V0 || h.ItemType == GeometryChannelTypes.UV_DIRECTION_V1))
                return false;

            var validFaces = faces.Where(f => f.BoundsCheck(count)).ToList();
            if (validFaces.Count == 0) return false;

            var normalFromFaces = new Vector3[count];
            foreach (var f in validFaces)
            {
                Vector3 faceNormal = Vector3.Cross(verts[f.b] - verts[f.a], verts[f.c] - verts[f.a]);
                if (!ValidDirection(faceNormal)) continue;
                normalFromFaces[f.a] += faceNormal;
                normalFromFaces[f.b] += faceNormal;
                normalFromFaces[f.c] += faceNormal;
            }

            Vector3 sharedNormal = normals.FirstOrDefault(ValidDirection);
            if (!ValidDirection(sharedNormal)) sharedNormal = Vector3.UnitZ;
            for (int i = 0; i < count; i++)
            {
                if (ValidDirection(normals[i])) continue;
                Vector3 n = ValidDirection(normalFromFaces[i]) ? normalFromFaces[i] : sharedNormal;
                normals[i] = Vector3.Normalize(n);
                repairedNormals++;
            }

            ComputeUvDirections(verts, Uv0, validFaces, normals, null,
                out var newU, out var newV);

            for (int i = 0; i < count; i++)
            {
                if (ValidDirection(newU[i]) && ValidDirection(newV[i])) continue;
                fallbackVertices++;
                Vector3 n = Vector3.Normalize(normals[i]);

                Vector3 u = newU[i];
                if (!ValidDirection(u))
                {
                    u = uvDirectionU.Count == count ? ProjectOntoNormalPlane(uvDirectionU[i], n) : Vector3.Zero;
                    if (!ValidDirection(u))
                    {
                        Vector3 axis = MathF.Abs(n.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitY;
                        u = Vector3.Cross(axis, n);
                    }
                    u = Vector3.Normalize(u);
                }

                Vector3 v = newV[i];
                if (!ValidDirection(v))
                {
                    float sign = -1.0f;
                    if (uvDirectionU.Count == count && uvDirectionV.Count == count &&
                        ValidDirection(uvDirectionU[i]) && ValidDirection(uvDirectionV[i]))
                    {
                        float storedSign = Vector3.Dot(
                            Vector3.Cross(uvDirectionU[i], n), uvDirectionV[i]);
                        if (float.IsFinite(storedSign) && MathF.Abs(storedSign) > 1e-6f)
                            sign = storedSign;
                    }
                    Vector3 cross = Vector3.Cross(u, n);
                    if (!ValidDirection(cross))
                    {
                        Vector3 axis = MathF.Abs(n.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitY;
                        u = Vector3.Normalize(Vector3.Cross(axis, n));
                        cross = Vector3.Cross(u, n);
                    }
                    v = Vector3.Normalize(cross) * (sign < 0 ? -1.0f : 1.0f);
                }
                newU[i] = u;
                newV[i] = v;
            }

            uvDirectionU = newU;
            uvDirectionV = newV;
            return true;
        }

        private static void FillMissingUvDirections(
            IReadOnlyList<Face> faces,
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

                    float currentULengthSq = directionU[i].LengthSquared();
                    float currentVLengthSq = directionV[i].LengthSquared();
                    if (float.IsFinite(currentULengthSq) &&
                        float.IsFinite(currentVLengthSq) &&
                        currentULengthSq > 1e-20f &&
                        currentVLengthSq > 1e-20f) continue;

                    Vector3 sumU = Vector3.Zero, sumV = Vector3.Zero;
                    int count = 0;
                    foreach (int n in neighbours[i])
                    {
                        if (handedness[n] != handedness[i]) continue;

                        float neighbourULengthSq = directionU[n].LengthSquared();
                        float neighbourVLengthSq = directionV[n].LengthSquared();
                        if (!float.IsFinite(neighbourULengthSq) ||
                            !float.IsFinite(neighbourVLengthSq) ||
                            neighbourULengthSq <= 1e-20f ||
                            neighbourVLengthSq <= 1e-20f) continue;

                        sumU += directionU[n];
                        sumV += directionV[n];
                        count++;
                    }

                    if (count == 0) continue;

                    float sumULengthSq = sumU.LengthSquared();
                    float sumVLengthSq = sumV.LengthSquared();
                    if (!float.IsFinite(sumULengthSq) ||
                        !float.IsFinite(sumVLengthSq) ||
                        sumULengthSq <= 1e-20f ||
                        sumVLengthSq <= 1e-20f) continue;

                    directionU[i] = sumU / MathF.Sqrt(sumULengthSq);
                    directionV[i] = sumV / MathF.Sqrt(sumVLengthSq);

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
            IReadOnlyList<Face> faces,
            IList<Vector3> directionU,
            IList<Vector3> directionV)
        {
            const float minUvArea = UvDeterminantEpsilon;
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

        private static bool ValidDirection(Vector3 v)
        {
            float lengthSquared = v.LengthSquared();
            return float.IsFinite(lengthSquared) && lengthSquared > 1e-20f;
        }

        private static Vector3 ProjectOntoNormalPlane(Vector3 v, Vector3 n)
        {
            if (!ValidDirection(v)) return Vector3.Zero;
            return v - Vector3.Dot(v, n) * n;
        }

        private static Vector3 NormalizeDirection(Vector3 value)
        {
            float lengthSquared = value.LengthSquared();
            return float.IsFinite(lengthSquared) && lengthSquared > 1e-20f
                ? value / MathF.Sqrt(lengthSquared)
                : Vector3.Zero;
        }
    }
}
