using PD2ModelParser.Misc;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;

namespace PD2ModelParser.Sections
{
    [ModelFileSection(Tags.skinbones_tag, ShowInInspectorRoot=false)]
    internal class SkinBones : Bones, ISection, IPostLoadable
    {
        private List<uint> ObjectsID { get; set; } = []; // of Object3D by SectionID

        [TypeConverter(typeof(Inspector.Object3DReferenceConverter))]
        public Object3D ProbablyRootBone { get; set; }
        public int Count => Objects.Count;
        public List<Object3D> Objects { get; private set; } = [];
        public List<Matrix4x4> Rotations { get; private set; } = [];
        public Matrix4x4 Global_skin_transform { get; set; }

        // Post-loaded
        public List<Matrix4x4> SkinPositions { get; private set; }

        public SkinBones() : base() { }

        public SkinBones(BinaryReader instream, SectionHeader section) : base(instream)
        {
            this.SectionId = section.id;
            this.size = section.size;

            PostLoadRef<Object3D>(instream.ReadUInt32(), i => ProbablyRootBone = i);
            uint count = instream.ReadUInt32();
            for (int x = 0; x < count; x++)
                this.ObjectsID.Add(instream.ReadUInt32());
            for (int x = 0; x < count; x++)
            {
                this.Rotations.Add(instream.ReadMatrix());
            }

            this.Global_skin_transform = instream.ReadMatrix();

            this.remaining_data = null;

            long end_pos = section.offset + 12 + section.size;
            if (end_pos > instream.BaseStream.Position)
            {
                // If exists, this contains hashed name for this geometry (see hashlist.txt)
                remaining_data = instream.ReadBytes((int) (end_pos - instream.BaseStream.Position));
            }
        }

        public override void StreamWriteData(BinaryWriter outstream)
        {
            base.StreamWriteData(outstream);
            outstream.Write(this.ProbablyRootBone != null ? this.ProbablyRootBone.SectionId : 0u);
            outstream.Write(this.Count);

            SectionUtils.CheckLength(Count, Objects);
            SectionUtils.CheckLength(Count, Rotations);

            foreach (var item in this.Objects)
                outstream.Write(item != null ? item.SectionId : 0u);
            foreach (Matrix4x4 matrix in this.Rotations)
            {
                outstream.Write(matrix);
            }

            outstream.Write(Global_skin_transform);

            if (this.remaining_data != null)
                outstream.Write(this.remaining_data);
        }

        public override string ToString()
        {
            string objects_string = (this.Objects.Count == 0 ? "none" : "");

            objects_string += string.Join(", ", this.Objects.Select(i => i.SectionId));

            string rotations_string = (this.Rotations.Count == 0 ? "none" : "");

            foreach (Matrix4x4 rotation in this.Rotations)
            {
                rotations_string += rotation + ", ";
            }

            return base.ToString() +
                   " object3D_section_id: " + (this.ProbablyRootBone != null ? this.ProbablyRootBone.SectionId.ToString() : "0") +
                   " count: " + this.Objects.Count + " objects:[ " + objects_string + " ]" +
                   " rotations count: " + this.Rotations.Count + " rotations:[ " + rotations_string + " ]" +
                   " global_skin_transform: " + this.Global_skin_transform +
                   (this.remaining_data != null ? " REMAINING DATA! " + this.remaining_data.Length + " bytes" : "");
        }

        public override void PostLoad(uint id, Dictionary<uint, ISection> parsed_sections)
        {
            base.PostLoad(id, parsed_sections);
            SkinPositions = [with(Count)];

            for (int i = 0; i < ObjectsID.Count; i++)
            {
                Object3D obj = (Object3D) parsed_sections[ObjectsID[i]];
                Objects.Add(obj);

                Matrix4x4 inter = Rotations[i].MultDiesel(obj.WorldTransform);
                Matrix4x4 skin_node = inter.MultDiesel(Global_skin_transform);

                SkinPositions.Add(skin_node);
            }
            ObjectsID = null;
        }
    }
}
