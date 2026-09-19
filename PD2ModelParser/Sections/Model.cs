using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Numerics;
using PD2ModelParser.Misc;

namespace PD2ModelParser.Sections
{
    [Flags]
    public enum ModelProperties : uint
    {
        CastShadows = 0x00000001,
        HasOpacity = 0x00000004
    }

    /// <summary>
    /// Represents a unit of geometry small enough to be one drawcall, like a GLTF Primitive
    /// </summary>
    public class RenderAtom
    {
        /// <summary>
        /// Where in the <see cref="DieselGeometry"/> indexes are relative to.
        /// </summary>
        public UInt32 BaseVertex { get; set; }

        /// <summary>
        /// Number of triangles to draw, ie, an IndexCount divided by three.
        /// </summary>
        public UInt32 TriangleCount { get; set; }

        /// <summary>
        /// Offset from the start of the Topology, measured in indexes, not triangles.
        /// </summary>
        public UInt32 BaseIndex { get; set; }

        /// <summary>
        /// Number of vertices after <see cref="BaseVertex"/> that are referenced by this atom.
        /// </summary>
        public UInt32 GeometrySliceLength { get; set; }

        public UInt32 MaterialId { get; set; }

        public override string ToString()
        {
            return "{BaseVertex=" + this.BaseVertex + " TriangleCount=" + this.TriangleCount + " BaseIndex=" + this.BaseIndex + " GeometrySliceLength=" + this.GeometrySliceLength + " MaterialId=" + this.MaterialId + "}";
        }

        public RenderAtom Clone()
        {
            return new RenderAtom
            {
                BaseIndex = this.BaseIndex,
                BaseVertex = this.BaseVertex,
                TriangleCount = this.TriangleCount,
                GeometrySliceLength = this.GeometrySliceLength,
                MaterialId = this.MaterialId

            };
        }
    }

    [ModelFileSection(Tags.model_data_tag,ShowInInspectorRoot=false)]
    internal class Model : Object3D, ISection, IPostLoadable, IHashContainer
    {
        [Category("Model")]
        [DisplayName("Version")]
        public UInt32 Version { get; set; }

        //Version 6
        [Category("Model")]
        public float DistanceRadius { get; set; }
        [Category("Model")]
        public UInt32 DistanceInt { get; set; }

        //Other versions
        [Category("Model")]
        public PassthroughGP PassthroughGP { get; set; }
        [Category("Model")]
        public TopologyIP TopologyIP { get; set; }

        [Category("Model")]
        public List<RenderAtom> RenderAtoms { get; set; }
        [Category("Model")]
        public MaterialGroup MaterialGroup { get; set; }
        [Category("Model")]
        public UInt32 Lightset_ID { get; set; }

        [Category("Model"), DisplayName("Bounds Min"), Description("Minimum corner of the bounding box.")]
        public Vector3 BoundsMin { get; set; } = new Vector3(0, 0, 0);

        [Category("Model"), DisplayName("Bounds Max"), Description("Maximum corner of the bounding box.")]
        public Vector3 BoundsMax { get; set; } = new Vector3(0, 0, 0);

        [Category("Model")]
        public UInt32 Properties_bitmap { get; set; }

        [Category("Model")]
        public float BoundingRadius { get; set; }

        [Category("Model")]
        public UInt32 BoundingInt { get; set; }

        [Category("Model")]
        public SkinBones SkinBones { get; set; }

        public Model(string object_name, uint triangleCount, uint vertexCount, PassthroughGP passGP, TopologyIP topoIP, MaterialGroup matg, Object3D parent)
            : base(object_name, parent)
        {
            this.size = 0;
            // TODO: Get rid of all referring to things by section ID outside of read/write of model files so we don't have to do this.
            SectionId = (uint)object_name.GetHashCode();

            this.Version = 3;
            this.PassthroughGP = passGP;
            this.TopologyIP = topoIP;
            this.RenderAtoms = [];
            RenderAtom nmi = new()
            {
                BaseVertex = 0,
                TriangleCount = triangleCount,
                BaseIndex = 0,
                GeometrySliceLength = vertexCount,
                MaterialId = 0
            };

            this.RenderAtoms.Add(nmi);

            //this.unknown9 = 0;
            this.MaterialGroup = matg;
            this.Lightset_ID = 0;
            this.Properties_bitmap = 0;
            this.BoundingRadius = 1;
            this.BoundingInt = 6;
            this.SkinBones = null;

        }

        public Model(string object_name, float DistanceRadius, System.Numerics.Vector3 bounds_min, System.Numerics.Vector3 bounds_max, Object3D parent)
            : base(object_name, parent)
        {
            this.size = 0;
            // TODO: Get rid of all referring to things by section ID outside of read/write of model files so we don't have to do this.
            SectionId = (uint)object_name.GetHashCode();

            this.Version = 6;
            this.BoundsMin = bounds_min;
            this.BoundsMax = bounds_max;
            this.DistanceRadius = DistanceRadius;
            this.DistanceInt = 0;
        }

        public Model(Obj_data obj, PassthroughGP passGP, TopologyIP topoIP, MaterialGroup matg, Object3D parent)
            : this(obj.Object_name, (uint)(obj.Faces.Count / 3), (uint)obj.Verts.Count, passGP, topoIP, matg, parent) { }

        public Model(BinaryReader instream, SectionHeader section)
            : base(instream)
        {
            this.RenderAtoms = [];

            this.size = section.size;
            SectionId = section.id;

            this.Version = instream.ReadUInt32();

            if (this.Version == 6)
            {
                this.BoundsMin = instream.ReadVector3();
                this.BoundsMax = instream.ReadVector3();

                this.DistanceRadius = instream.ReadSingle();
                this.DistanceInt = instream.ReadUInt32();
            }
            else
            {
                PostLoadRef<PassthroughGP>(instream.ReadUInt32(), i => PassthroughGP = i);
                PostLoadRef<TopologyIP>(instream.ReadUInt32(), i => TopologyIP = i);
                var renderAtomCount = instream.ReadUInt32();

                for (int x = 0; x < renderAtomCount; x++)
                {
                    RenderAtom item = new()
                    {
                        BaseVertex = instream.ReadUInt32(),
                        TriangleCount = instream.ReadUInt32(),
                        BaseIndex = instream.ReadUInt32(),
                        GeometrySliceLength = instream.ReadUInt32(),
                        MaterialId = instream.ReadUInt32()
                    };
                    this.RenderAtoms.Add(item);
                }

                //this.unknown9 = instream.ReadUInt32();
                PostLoadRef<MaterialGroup>(instream.ReadUInt32(), i => MaterialGroup = i);
                this.Lightset_ID = instream.ReadUInt32(); // this is a section id afaik

                // Bitmap that stores properties about the model
                // Bits:
                // 1: cast_shadows
                // 3: has_opacity
                this.Properties_bitmap = instream.ReadUInt32();

                this.BoundsMin = instream.ReadVector3();
                this.BoundsMax = instream.ReadVector3();

                this.BoundingRadius = instream.ReadSingle();
                this.BoundingInt = instream.ReadUInt32();
                PostLoadRef<SkinBones>(instream.ReadUInt32(), i => SkinBones = i);
            }
            this.remaining_data = null;
            if ((section.offset + 12 + section.size) > instream.BaseStream.Position)
                remaining_data = instream.ReadBytes((int)((section.offset + 12 + section.size) - instream.BaseStream.Position));

        }

        public override void StreamWriteData(BinaryWriter outstream)
        {
            base.StreamWriteData(outstream);
            outstream.Write(this.Version);
            if (this.Version == 6)
            {
                outstream.Write(this.BoundsMin);
                outstream.Write(this.BoundsMax);
                outstream.Write(this.DistanceRadius);
                outstream.Write(this.DistanceInt);
            }
            else
            {
                outstream.Write(this.PassthroughGP.SectionId);
                outstream.Write(this.TopologyIP.SectionId);
                outstream.Write((uint)this.RenderAtoms.Count);
                foreach (RenderAtom modelitem in this.RenderAtoms)
                {
                    outstream.Write(modelitem.BaseVertex);
                    outstream.Write(modelitem.TriangleCount);
                    outstream.Write(modelitem.BaseIndex);
                    outstream.Write(modelitem.GeometrySliceLength);
                    outstream.Write(modelitem.MaterialId);
                }

                //outstream.Write(this.unknown9);
                outstream.Write(this.MaterialGroup?.SectionId ?? 0);
                outstream.Write(this.Lightset_ID);

                outstream.Write(this.Properties_bitmap);

                outstream.Write(this.BoundsMin);
                outstream.Write(this.BoundsMax);

                outstream.Write(this.BoundingRadius);
                outstream.Write(this.BoundingInt);
                outstream.Write(this.SkinBones?.SectionId ?? 0);

            }

            if (this.remaining_data != null)
                outstream.Write(this.remaining_data);
        }

        public override string ToString()
        {
            if (this.Version == 6)
                return "[Model_v6] " + base.ToString() + " version: " + this.Version + " unknown5: " + this.BoundsMin + " unknown6: " + this.BoundsMax + " DistanceRadius: " + this.DistanceRadius + " unknown8: " + this.DistanceInt + (this.remaining_data != null ? " REMAINING DATA! " + this.remaining_data.Length + " bytes" : "");
            else
            {
                var atoms_string = string.Join(",", RenderAtoms.Select(i => i.ToString()));
                return $"{base.ToString()} version: {this.Version} passthroughGP_ID: {this.PassthroughGP?.SectionId} topologyIP_ID: {this.TopologyIP?.SectionId} RenderAtoms: {this.RenderAtoms.Count} items: [{atoms_string}] MaterialGroup: {this.MaterialGroup.SectionId} unknown10: {this.Lightset_ID} bounds_min: {this.BoundsMin} bounds_max: {this.BoundsMax} unknown11: {this.Properties_bitmap} BoundingRadius: {this.BoundingRadius} BoundingInt: {this.BoundingInt} skinbones_ID: {this.SkinBones?.SectionId ?? 0}{(this.remaining_data != null ? " REMAINING DATA! " + this.remaining_data.Length + " bytes" : "")}";
            }
        }

        public void UpdateBounds()
        {
            if (Version != 3) { return; }

            var gp = this.PassthroughGP;
            if (gp == null) { return; }

            var geo = gp.DieselGeometry;
            if (geo == null) { return; }

            if (geo.verts.Count == 0) { return; }

            Matrix4x4.Decompose(Transform, out Vector3 scale, out _, out _);

            var scaled = geo.verts.Select(i => i * scale).ToList();

            BoundsMax = geo.verts.Aggregate(MathUtil.Max);
            BoundsMin = geo.verts.Aggregate(MathUtil.Min);
            BoundingRadius = scaled.Select(i => i.Length()).Max();
        }
    }
}
