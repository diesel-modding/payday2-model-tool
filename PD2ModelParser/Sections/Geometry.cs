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
                if (!float.IsFinite(determinant) || MathF.Abs(determinant) < 1e-12f)
                    continue;

                float inverse = 1.0f / determinant;
                Vector3 faceU = (edge1 * uv2.Y - edge2 * uv1.Y) * inverse;
                Vector3 faceV = (edge2 * uv1.X - edge1 * uv2.X) * inverse;

                accumulatedU[face.a] += faceU;
                accumulatedU[face.b] += faceU;
                accumulatedU[face.c] += faceU;
                accumulatedV[face.a] += faceV;
                accumulatedV[face.b] += faceV;
                accumulatedV[face.c] += faceV;
            }

            directionU = accumulatedU.Select(NormalizeDirection).ToList();
            directionV = accumulatedV.Select(NormalizeDirection).ToList();
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
