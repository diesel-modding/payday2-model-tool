using System;
using System.Collections.Generic;
using System.Numerics;

using PD2ModelParser.Sections;

namespace PD2ModelParser
{
    internal class Obj_data
    {
        public List<Vector3> Verts { get; set; }

        public List<Vector2> Uv { get; set; }

        public List<Vector3> Normals { get; set; }

        public string Object_name { get; set; }

        public List<Face> Faces { get; set; }

        public string Material_name { get; set; }

        public Dictionary<String, List<Int32>> Shading_groups { get; set; }
        
        public Obj_data()
        {
            this.Verts = [];
            this.Uv = [];
            this.Normals = [];
            this.Object_name = "";
            this.Faces = [];
            this.Material_name = "";
            this.Shading_groups = [];
        }
    }
}
