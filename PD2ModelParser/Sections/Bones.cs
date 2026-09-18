using System;
using System.Collections.Generic;
using System.IO;
namespace PD2ModelParser.Sections
{
    internal class BoneMappingItem
    {
        public readonly List<UInt32> bones = [];
        public override string ToString()
        {
            string verts_string = (bones.Count == 0 ? "none" : "");
            foreach (UInt32 vert in bones)
            {
                verts_string += vert + ", ";
            }
            return "count: " + bones.Count + " verts: [" + verts_string + "]";
        }
    }
    [ModelFileSection(Tags.bones_tag)]
    internal class Bones : AbstractSection, ISection
    {
        public UInt32 size;

        public List<BoneMappingItem> Bone_mappings { get; private set; } = [];

        public byte[] remaining_data = null;
        internal Bones(){ }
        public Bones(BinaryReader instream, SectionHeader section) : this(instream)
        {
            Log.Default.Warn("Model contains a Bone that isn't a SkinBones!");
            this.SectionId = section.id;
            this.size = section.size;
            if ((section.offset + 12 + section.size) > instream.BaseStream.Position) remaining_data = instream.ReadBytes((int)((section.offset + 12 + section.size) - instream.BaseStream.Position));
        }
        public Bones(BinaryReader instream)
        {
            uint count = instream.ReadUInt32();
            for (int x = 0; x < count; x++)
            {
                BoneMappingItem bone_mapping_item = new();
                uint bone_count = instream.ReadUInt32();
                for (int y = 0; y < bone_count; y++) bone_mapping_item.bones.Add(instream.ReadUInt32());
                Bone_mappings.Add(bone_mapping_item);
            }
            this.remaining_data = null;
        }
        public override void StreamWriteData(BinaryWriter outstream)
        {
            outstream.Write(Bone_mappings.Count);
            foreach (BoneMappingItem bone in this.Bone_mappings)
            {
                outstream.Write(bone.bones.Count);
                foreach (UInt32 vert in bone.bones)
                    outstream.Write(vert);
            }
            if (this.remaining_data != null) outstream.Write(this.remaining_data);
        }
        public override string ToString()
        {
            string bones_string = (Bone_mappings.Count == 0 ? "none" : "");
            foreach (BoneMappingItem bone in Bone_mappings)
            {
                bones_string += bone + ", ";
            }
            return base.ToString() + " size: " + this.size + " bones:[ " + bones_string + " ]" + (this.remaining_data != null ? " REMAINING DATA! " + this.remaining_data.Length + " bytes" : "");
        }
    }
}