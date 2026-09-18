using System;
using System.Collections.Generic;
using System.IO;

namespace PD2ModelParser.Sections
{
    [ModelFileSection(Tags.animation_data_tag)]
    public class Animation : AbstractSection, ISection, IHashNamed
    {
        public UInt32 size;

        public HashName HashName { get; set; }
        public UInt32 Unknown2 { get; set; }
        public float Keyframe_length { get; set; }
        public UInt32 Count { get; set; }
        public List<float> Items { get; set; } = [];

        public byte[] Remaining_data { get; set; } = null;

        public Animation(BinaryReader instream, SectionHeader section)
        {
            this.SectionId = section.id;
            this.size = section.size;
            this.HashName = new HashName(instream.ReadUInt64());
            this.Unknown2 = instream.ReadUInt32();
            this.Keyframe_length = instream.ReadSingle();
            this.Count = instream.ReadUInt32();
            for (int x = 0; x < this.Count; x++)
                this.Items.Add(instream.ReadSingle());

            this.Remaining_data = null;
            if ((section.offset + 12 + section.size) > instream.BaseStream.Position)
                this.Remaining_data = instream.ReadBytes((int)((section.offset + 12 + section.size) - instream.BaseStream.Position));
        }

        public override void StreamWriteData(BinaryWriter outstream)
        {
            outstream.Write(this.HashName.Hash);
            outstream.Write(this.Unknown2);
            outstream.Write(this.Keyframe_length);
            outstream.Write(this.Count);
            foreach (float item in this.Items)
            {
                outstream.Write(item);
            }

            if (this.Remaining_data != null)
                outstream.Write(this.Remaining_data);
        }

        public override string ToString()
        {
            return $"{base.ToString()} size: {this.size} Name: {this.HashName} unknown2: {this.Unknown2} keyframe_length: {this.Keyframe_length} count: {this.Count} items: (count={this.Items.Count}){(Remaining_data != null ? " REMAINING DATA! " + Remaining_data.Length + " bytes" : "")}";
        }
    }
}
