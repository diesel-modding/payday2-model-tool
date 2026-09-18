using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PD2ModelParser.Sections
{
    /** A triangular face */
    public readonly struct Face(ushort a, ushort b, ushort c)
    {
        /** The index of the first vertex in this face */
        public readonly ushort a = a;

        /** The index of the second vertex in this face */
        public readonly ushort b = b;

        /** The index of the third (last) vertex in this face */
        public readonly ushort c = c;

        public readonly Face OffsetBy(int offset)
        {
            return new Face(
                (ushort) (a + offset),
                (ushort) (b + offset),
                (ushort) (c + offset)
            );
        }

        public readonly bool BoundsCheck(int vertlen)
        {
            return a >= 0 && b >= 0 && c >= 0 && a < vertlen && b < vertlen && c < vertlen;
        }

        public override readonly string ToString()
        {
            return $"{a}, {b}, {c}";
        }
    }

    [ModelFileSection(Tags.topology_tag)]
    internal class Topology : AbstractSection, ISection, IHashNamed
    {
        public enum PrimitiveType : uint
        {
            PointList = 0,
            LineList = 1,
            LineStrip = 2,
            TriangleList = 3,
            TriangleStrip = 4,
            TriangleFan = 5
        }

        public PrimitiveType Primitive_type { get; set; }
        public List<Face> facelist = [];
        public UInt32 count2;
        public byte[] items2;
        public HashName HashName { get; set; }

        public byte[] remaining_data = null;

        public Topology Clone(string newName)
        {
            var dst = new Topology(newName)
            {
                Primitive_type = this.Primitive_type
            };
            dst.facelist.Capacity = this.facelist.Count;
            dst.facelist.AddRange(this.facelist.Select(f => new Face(f.a, f.b, f.c )));
            dst.count2 = this.count2;
            dst.items2 = (byte[])(this.items2.Clone());
            return dst;
        }

        public Topology(string objectName)
        {
            this.Primitive_type = PrimitiveType.TriangleList;

            this.count2 = 0;
            this.items2 = [];
            this.HashName = new HashName(objectName + ".Topology");
        }

        public Topology(Obj_data obj) : this(obj.Object_name)
        {
            this.facelist = obj.Faces;
        }

        public Topology(BinaryReader instream, SectionHeader section)
        {
            SectionId = section.id;
            this.Primitive_type = (PrimitiveType)instream.ReadUInt32();
            uint count1 = instream.ReadUInt32();
            for (int x = 0; x < count1 / 3; x++)
            {
                var a = instream.ReadUInt16();
                var b = instream.ReadUInt16();
                var c = instream.ReadUInt16();
                this.facelist.Add(new Face(a,b,c));
            }

            this.count2 = instream.ReadUInt32();
            this.items2 = instream.ReadBytes((int) this.count2);
            this.HashName = new HashName(instream.ReadUInt64());

            this.remaining_data = null;
            if ((section.offset + 12 + section.size) > instream.BaseStream.Position)
                remaining_data = instream.ReadBytes((int)((section.offset + 12 + section.size) - instream.BaseStream.Position));
        }

        public override void StreamWriteData(BinaryWriter outstream)
        {
            outstream.Write((uint)this.Primitive_type);
            outstream.Write(facelist.Count * 3);
            foreach (Face face in facelist)
            {
                outstream.Write(face.a);
                outstream.Write(face.b);
                outstream.Write(face.c);
            }

            outstream.Write(this.count2);
            outstream.Write(this.items2);
            outstream.Write(this.HashName.Hash);

            if (this.remaining_data != null)
                outstream.Write(this.remaining_data);
        }

        public override string ToString()
        {
            return base.ToString() +
                   $" primitive_type: {Primitive_type} facelist: {facelist.Count} count2: {count2}" +
                   $" items2: {items2.Length} HashName: {HashName}" +
                   (this.remaining_data != null ? " REMAINING DATA! " + this.remaining_data.Length + " bytes" : "");
        }
    }
}
