using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

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
            this.Bones1 = instream.ReadUInt16();
            this.Bones2 = instream.ReadUInt16();
            this.Bones3 = instream.ReadUInt16();
            this.Bones4 = instream.ReadUInt16();
        }

        public void StreamWrite(BinaryWriter outstream)
        {
            outstream.Write(this.Bones1);
            outstream.Write(this.Bones2);
            outstream.Write(this.Bones3);
            outstream.Write(this.Bones4);
        }

        public override string ToString()
        {
            return "{ Bones1=" + this.Bones1 + ", Bones2=" + this.Bones2 + ", Bones3=" + this.Bones3 + ", Bones4=" +
                   this.Bones4 + " }";
        }
    }

    public struct GeometryColor
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
            this.blue = instream.ReadByte();
            this.green = instream.ReadByte();
            this.red = instream.ReadByte();
            this.alpha = instream.ReadByte();
        }

        public void StreamWrite(BinaryWriter outstream)
        {
            outstream.Write(this.blue);
            outstream.Write(this.green);
            outstream.Write(this.red);
            outstream.Write(this.alpha);
        }

        public override string ToString()
        {
            return "{Red=" + this.red + ", Green=" + this.green + ", Blue=" + this.blue + ", Alpha=" + this.alpha + "}";
        }
    }

    public class GeometryHeader
    {
        /// <summary>
        /// Byte sizes for various types of geometry buffer. Official data is consistent regarding
        /// which one goes with which <see cref="GeometryChannelTypes"/>.
        /// </summary>
       public static readonly IReadOnlyList<uint> ItemSizes = new List<uint> { 0, 4, 8, 12, 16, 4, 4, 8, 4, 4 };

        /// <summary>
        /// Index into <see cref="ItemSizes"/>
        /// </summary>
        public UInt32 ItemSize { get; set; }
        public GeometryChannelTypes ItemType { get; set; }

        public GeometryHeader()
        {
        }

        public GeometryHeader(UInt32 size, GeometryChannelTypes type)
        {
            ItemSize = size;
            ItemType = type;
        }

        public uint ItemSizeBytes { 
            get 
            { 
                //Console.WriteLine(ItemSize.ToString());
                return ItemSizes[(int)ItemSize]; 
            } 
        }
    }
    // Extracted from dsl::wd3d::D3DShaderProgram::compile
    // These are the actual names of each channel, as passed to the shader
    public enum GeometryChannelTypes
    {
        POSITION = 1,
        POSITION0 = 1,
        NORMAL = 2,
        NORMAL0 = 2,
        POSITION1 = 3,
        NORMAL1 = 4,
        COLOR = 5,
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
        BLENDINDICES = 17,
        BLENDINDICES0 = 17,
        BLENDINDICES1 = 18,
        BLENDWEIGHT = 19,
        BLENDWEIGHT0 = 19,
        BLENDWEIGHT1 = 20,
        POINTSIZE = 21,
        BINORMAL = 22,
        BINORMAL0 = 22,
        TANGENT = 23,
        TANGENT0 = 23,
    }

    [ModelFileSection(Tags.geometry_tag)]
    class Geometry : AbstractSection, ISection, IHashNamed
    {
        // Count of everysingle item in headers (Verts, Normals, UVs, UVs for normalmap, Colors, Unknown 20, Unknown 21, etc)
        public uint vert_count;

        public List<Vector2>[] UVs = new List<Vector2>[8];

        public List<GeometryHeader> Headers { get; private set; } = new List<GeometryHeader>();
        public List<Vector3> verts = new List<Vector3>();
        public List<Vector2> uvs => UVs[0];
        public List<Vector2> pattern_uvs => UVs[1];
        public List<Vector3> normals = new List<Vector3>();
        public List<GeometryColor> vertex_colors = new List<GeometryColor>();
        public List<GeometryWeightGroups> weight_groups = new List<GeometryWeightGroups>(); //4 - Weight Groups
        public List<Vector3> weights = new List<Vector3>(); //3 - Weights
        public List<Vector3> binormals = new List<Vector3>(); //3 - Tangent/Binormal
        public List<Vector3> tangents = new List<Vector3>(); //3 - Tangent/Binormal

        // Unknown items from this section. Includes colors and a few other items.
        public List<byte[]> unknown_item_data = new List<byte[]>();

        public HashName HashName { get; set; }

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

            instream.ReadByte(); // W / unused byte

            return new Vector3(
                UnpackSignedByte(x),
                UnpackSignedByte(y),
                UnpackSignedByte(z));
        }

        private static void WritePackedVector3(
            BinaryWriter outstream,
            Vector3 value)
        {
            outstream.Write(PackSignedByte(value.Z));
            outstream.Write(PackSignedByte(value.Y));
            outstream.Write(PackSignedByte(value.X));
            outstream.Write((byte)0);
        }

        private static Vector3 ReadFloatVector3(BinaryReader instream)
        {
            return new Vector3(
                instream.ReadSingle(),
                instream.ReadSingle(),
                instream.ReadSingle()
            );
        }

        private static void WriteFloatVector3(BinaryWriter outstream, Vector3 value)
        {
            outstream.Write(value.X);
            outstream.Write(value.Y);
            outstream.Write(value.Z);
        }

        private static Vector3 ReadVector3ByType(BinaryReader instream, uint type)
        {
            if (type == 3)
                return ReadFloatVector3(instream);

            if (type == 8)
                return ReadPackedVector3(instream);

            throw new Exception($"Unsupported Vector3 geometry type {type}");
        }

        private static void WriteVector3ByType(BinaryWriter outstream, Vector3 value, uint type)
        {
            if (type == 3)
            {
                WriteFloatVector3(outstream, value);
                return;
            }

            if (type == 8)
            {
                WritePackedVector3(outstream, value);
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

        public Geometry Clone()
        {
            var src = this;
            var dst = new Geometry();
            dst.vert_count = this.vert_count;
            for (int i = 0; i < UVs.Length; i++)
            {
                src.UVs[i].CopyTo(dst.UVs[i]);
            }
            dst.Headers.AddRange(src.Headers.Select(i => new GeometryHeader(i.ItemSize, i.ItemType)));
            src.verts.CopyTo(dst.verts);
            src.normals.CopyTo(dst.normals);
            src.vertex_colors.CopyTo(dst.vertex_colors);
            src.weight_groups.CopyTo(dst.weight_groups);
            src.weights.CopyTo(dst.weights);
            src.binormals.CopyTo(dst.binormals);
            src.tangents.CopyTo(dst.tangents);
            foreach(var ud in src.unknown_item_data)
            {
                dst.unknown_item_data.Add((byte[])(ud.Clone()));
            }
            dst.HashName = src.HashName;
            return dst;
        }

        public Geometry()
        {
            this.SectionId = 0;
            for (int i = 0; i < UVs.Length; i++)
            {
                UVs[i] = new List<Vector2>();
            }
        }

        public Geometry(obj_data newobject) : this()
        {
            this.vert_count = (uint) newobject.verts.Count;

            this.Headers.Add(new GeometryHeader(3, GeometryChannelTypes.POSITION));

            this.Headers.Add(new GeometryHeader(9, GeometryChannelTypes.TEXCOORD0));

            this.Headers.Add(new GeometryHeader(8, GeometryChannelTypes.NORMAL0));

            this.Headers.Add(new GeometryHeader(8, GeometryChannelTypes.BINORMAL0));

            this.Headers.Add(new GeometryHeader(8, GeometryChannelTypes.TANGENT0));
            
            this.verts = newobject.verts;
            this.UVs[0] = newobject.uv;
            this.normals = newobject.normals;
            //this.binormals;
            //this.tangents;

            this.HashName = new HashName(newobject.object_name + ".Geometry");
        }

        public Geometry(BinaryReader instream, SectionHeader section) : this()
        {
            this.SectionId = section.id;
            // Count of everysingle item in headers (Verts, Normals, UVs, UVs for normalmap, Colors, Unknown 20, Unknown 21, etc)
            this.vert_count = instream.ReadUInt32();
            //Count of all headers for items in this section
            uint header_count = instream.ReadUInt32();
            UInt32 calc_size = 0;
            for (int x = 0; x < header_count; x++)
            {
                GeometryHeader header = new GeometryHeader();
                header.ItemSize = instream.ReadUInt32();
                header.ItemType = (GeometryChannelTypes) instream.ReadUInt32();
                calc_size += header.ItemSizeBytes;
                this.Headers.Add(header);
            }

            foreach (GeometryHeader head in this.Headers)
            {
                //Console.WriteLine("Header type: " + head.ItemType + " Size: " + head.ItemSize);
                if (head.ItemType == GeometryChannelTypes.POSITION)
                {
                    verts.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        Vector3 vert = new Vector3();
                        vert.X = instream.ReadSingle();
                        vert.Y = instream.ReadSingle();
                        vert.Z = instream.ReadSingle();

                        this.verts.Add(vert);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.NORMAL)
                {
                    normals.Capacity = (int)vert_count + 1;

                    for (int x = 0; x < this.vert_count; x++)
                    {
                        this.normals.Add(ReadVector3ByType(instream, head.ItemSize));
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.COLOR0)
                {
                    vertex_colors.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        this.vertex_colors.Add(new GeometryColor(instream));
                    }
                }
                //Below is unknown data

                else if (head.ItemType == GeometryChannelTypes.BINORMAL0)
                {
                    binormals.Capacity = (int)vert_count + 1;

                    for (int x = 0; x < this.vert_count; x++)
                    {
                        this.binormals.Add(ReadVector3ByType(instream, head.ItemSize));
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.TANGENT0)
                {
                    tangents.Capacity = (int)vert_count + 1;

                    for (int x = 0; x < this.vert_count; x++)
                    {
                        this.tangents.Add(ReadVector3ByType(instream, head.ItemSize));
                    }
                }

                //Weight Groups
                else if (head.ItemType == GeometryChannelTypes.BLENDINDICES0)
                {
                    weight_groups.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        GeometryWeightGroups unknown_15_entry = new GeometryWeightGroups(instream);
                        this.weight_groups.Add(unknown_15_entry);
                    }
                }

                //Weights
                else if (head.ItemType == GeometryChannelTypes.BLENDWEIGHT0)
                {
                    if(head.ItemSize == 4)
                    {
                        Log.Default.Warn("Section {0} has four weights", this.SectionId);
                    }

                    weights.Capacity = (int)vert_count + 1;
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        Vector3 unknown_17_entry = new Vector3();
                        unknown_17_entry.X = instream.ReadSingle();
                        unknown_17_entry.Y = instream.ReadSingle();
                        if (head.ItemSize == 3)
                            unknown_17_entry.Z = instream.ReadSingle();
                        else if(head.ItemSize == 4)
                        {
                            unknown_17_entry.Z = instream.ReadSingle();
                            var tmp = instream.ReadSingle();
                            if (tmp != 0)
                                Log.Default.Warn("Nonzero fourth weight in {0} vtx {1} (is {2})", this.SectionId, x, tmp);
                        }
                        else if (head.ItemSize != 2)
                            throw new Exception("Bad BLENDWEIGHT0 item size " + head.ItemSize);
                        this.weights.Add(unknown_17_entry);
                    }
                }
                else if (head.ItemType >= GeometryChannelTypes.TEXCOORD0 &&
                        head.ItemType <= GeometryChannelTypes.TEXCOORD9)
                {
                    int idx = head.ItemType - GeometryChannelTypes.TEXCOORD0;

                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector2 uv;

                        if (head.ItemSize == 2)
                        {
                            uv = new Vector2(
                                instream.ReadSingle(),
                                -instream.ReadSingle());
                        }
                        else if (head.ItemSize == 9)
                        {
                            uv = new Vector2(
                                ReadHalf(instream),
                                -ReadHalf(instream));
                        }
                        else
                        {
                            throw new Exception($"Unsupported TEXCOORD type {head.ItemSize}");
                        }

                        UVs[idx].Add(uv);
                    }
                }
                else
                {
                    this.unknown_item_data.Add(
                        instream.ReadBytes((int) (head.ItemSizeBytes * this.vert_count)));
                }
            }

            this.HashName = new HashName(instream.ReadUInt64());

            this.remaining_data = null;
            long sect_end = section.offset + 12 + section.size;
            if (sect_end > instream.BaseStream.Position)
            {
                // If exists, this contains hashed name for this geometry (see hashlist.txt)
                remaining_data = instream.ReadBytes((int) (sect_end - instream.BaseStream.Position));
            }
        }

        public bool HasHeader(GeometryChannelTypes type)
        {
            return Headers.Any(h => h.ItemType == type);
        }

        public override void StreamWriteData(BinaryWriter outstream)
        {
            outstream.Write(this.vert_count);
            outstream.Write(Headers.Count);
            foreach (GeometryHeader head in this.Headers)
            {
                outstream.Write(head.ItemSize);
                outstream.Write((uint) head.ItemType);
            }

            List<Vector3> verts = this.verts;
            int vert_pos = 0;
            List<Vector3> normals = this.normals.ToList();
            int norm_pos = 0;

            List<GeometryWeightGroups> unknown_15s = this.weight_groups;
            int unknown_15s_pos = 0;
            List<Vector3> binormals = this.binormals;
            int binormals_pos = 0;
            List<Vector3> tangents = this.tangents;
            int tangents_pos = 0;

            List<byte[]> unknown_data = this.unknown_item_data;
            int unknown_data_pos = 0;

            foreach (GeometryHeader head in this.Headers)
            {
                if (head.ItemType == GeometryChannelTypes.POSITION)
                {
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        Vector3 vert = verts[vert_pos];
                        outstream.Write(vert.X);
                        outstream.Write(vert.Y);
                        outstream.Write(vert.Z);
                        vert_pos++;
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.NORMAL)
                {
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        Vector3 norm = normals[norm_pos];

                        WriteVector3ByType(outstream, norm, head.ItemSize);

                        norm_pos++;
                    }
                }

                else if (head.ItemType == GeometryChannelTypes.COLOR)
                {
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        this.vertex_colors[x].StreamWrite(outstream);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.BINORMAL)
                {
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        Vector3 binormal =
                            this.binormals.Count == this.vert_count
                                ? binormals[x]
                                : Vector3.Zero;

                        WriteVector3ByType(outstream, binormal, head.ItemSize);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.TANGENT)
                {
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        Vector3 tangent =
                            this.tangents.Count == this.vert_count
                                ? tangents[x]
                                : Vector3.Zero;

                        WriteVector3ByType(outstream, tangent, head.ItemSize);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.BLENDINDICES)
                {
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        if (this.weight_groups.Count != this.vert_count)
                        {
                            outstream.Write(0.0f);
                            outstream.Write(0.0f);
                        }
                        else
                        {
                            GeometryWeightGroups unknown_15_entry = unknown_15s[x];
                            unknown_15_entry.StreamWrite(outstream);
                            unknown_15s_pos++;
                        }
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.BLENDWEIGHT)
                {
                    if (head.ItemSize == 4)
                    {
                        Log.Default.Warn("Section {0} has four weights", this.HashName);
                    }
                    for (int x = 0; x < this.vert_count; x++)
                    {
                        Vector3 weight = this.weights.Count != this.vert_count ? Vector3.UnitX : weights[x];
                        outstream.Write(weight.X);
                        outstream.Write(weight.Y);

                        if (head.ItemSize == 3)
                            outstream.Write(weight.Z);
                        else if (head.ItemSize == 4)
                        {
                            outstream.Write(weight.Z);
                            outstream.Write(0.0f);
                        }
                        else if (head.ItemSize != 2)
                            throw new Exception("Cannot write bad header BLENDWEIGHT s=" + head.ItemSize);
                    }
                }
                else if (head.ItemType >= GeometryChannelTypes.TEXCOORD0 &&
                        head.ItemType <= GeometryChannelTypes.TEXCOORD9)
                {
                    int idx = head.ItemType - GeometryChannelTypes.TEXCOORD0;

                    for (int x = 0; x < this.vert_count; x++)
                    {
                        Vector2 uv = UVs[idx][x];

                        if (head.ItemSize == 2)
                        {
                            outstream.Write(uv.X);
                            outstream.Write(-uv.Y);
                        }
                        else if (head.ItemSize == 9)
                        {
                            WriteHalf(outstream, uv.X);
                            WriteHalf(outstream, -uv.Y);
                        }
                        else
                        {
                            throw new Exception($"Unsupported TEXCOORD type {head.ItemSize}");
                        }
                    }
                }
                else
                {
                    outstream.Write(unknown_data[unknown_data_pos]);
                    unknown_data_pos++;
                }
            }

            outstream.Write(this.HashName.Hash);

            if (this.remaining_data != null)
                outstream.Write(this.remaining_data);
        }

        public void PrintDetailedOutput(StreamWriter outstream)
        {
            //for debug purposes
            //following prints the suspected "weights" table

            if (this.weight_groups.Count > 0 && this.binormals.Count > 0 && this.tangents.Count > 0 &&
                this.weights.Count > 0)
            {
                outstream.WriteLine("Printing weights table for " + this.HashName);
                outstream.WriteLine("====================================================");
                outstream.WriteLine(
                    "unkn15_1\tunkn15_2\tunkn15_3\tunkn15_4\tunkn17_X\tunkn17_Y\tunk17_Z\ttotalsum\tunk_20_X\tunk_20_Y\tunk_20_Z\tunk21_X\tunk21_Y\tunk21_Z");


                for (int x = 0; x < this.weight_groups.Count; x++)
                {
                    outstream.WriteLine(this.weight_groups[x].Bones1.ToString() + "\t" +
                                        this.weight_groups[x].Bones2.ToString() + "\t" +
                                        this.weight_groups[x].Bones3.ToString() + "\t" +
                                        this.weight_groups[x].Bones4.ToString() + "\t" +
                                        this.weights[x].X.ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                        this.weights[x].Y.ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                        this.weights[x].Z.ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                        (this.weights[x].X + this.weights[x].Y + this.weights[x].Z).ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                        this.binormals[x].X.ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                        this.binormals[x].Y.ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                        this.binormals[x].Z.ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                        this.tangents[x].X.ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                        this.tangents[x].Y.ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                        this.tangents[x].Z.ToString("0.000000",
                                            System.Globalization.CultureInfo.InvariantCulture));
                }

                outstream.WriteLine("====================================================");
            }
        }

        public override string ToString()
        {
            return base.ToString() +
                   " Count: " + this.vert_count +
                   " Headers: " + this.Headers.Count +
                   " Verts: " + this.verts.Count +
                   " UVs: " + this.uvs.Count +
                   " Pattern UVs: " + this.pattern_uvs.Count +
                   " Normals: " + this.normals.Count +
                   " weight_groups: " + this.weight_groups.Count +
                   " weights: " + this.weights.Count +
                   " binormals: " + this.binormals.Count +
                   " tangents: " + this.tangents.Count +
                   " Geometry_unknown_item_data: " + this.unknown_item_data.Count +
                   " unknown_hash: " + this.HashName +
                   (this.remaining_data != null ? " REMAINING DATA! " + this.remaining_data.Length + " bytes" : "");
        }
    }
}