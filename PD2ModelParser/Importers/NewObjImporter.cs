using PD2ModelParser.Sections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

namespace PD2ModelParser.Importers
{
    internal static class NewObjImporter
    {
        public static void ImportNewObj(FullModelData fmd, String filepath, bool addNew, Func<string, Object3D> root_point, Importers.IOptionReceiver _)
        {
            Log.Default.Info("Importing new obj with file: {0}", filepath);

            //Preload the .obj
            List<Obj_data> objects = [];
            List<Obj_data> toAddObjects = [];

            using (FileStream fs = new(filepath, FileMode.Open, FileAccess.Read))
            {
                using StreamReader sr = new(fs);
                string line;
                Obj_data obj_data = new();
                Obj_data obj = obj_data;
                bool reading_faces = false;
                int prevMaxVerts = 0;
                int prevMaxUvs = 0;
                int prevMaxNorms = 0;
                string current_shade_group = null;

                while ((line = sr.ReadLine()) != null)
                {

                    //preloading objects
                    if (line.StartsWith('#'))
                        continue;
                    else if (line.StartsWith("o ") || line.StartsWith("g "))
                    {

                        if (reading_faces)
                        {
                            reading_faces = false;
                            prevMaxVerts += obj.Verts.Count;
                            prevMaxUvs += obj.Uv.Count;
                            prevMaxNorms += obj.Normals.Count;

                            objects.Add(obj);
                            obj = new Obj_data();
                            current_shade_group = null;
                        }

                        if (String.IsNullOrEmpty(obj.Object_name))
                        {
                            obj.Object_name = line[2..];
                            Log.Default.Debug("Object {0} named: {1}", objects.Count + 1, obj.Object_name);
                        }
                    }
                    else if (line.StartsWith("usemtl "))
                    {
                        obj.Material_name = line[7..];
                    }
                    else if (line.StartsWith("v "))
                    {

                        if (reading_faces)
                        {
                            reading_faces = false;
                            prevMaxVerts += obj.Verts.Count;
                            prevMaxUvs += obj.Uv.Count;
                            prevMaxNorms += obj.Normals.Count;

                            objects.Add(obj);
                            obj = new Obj_data();
                        }

                        String[] verts = line.Replace("  ", " ").Split(' ');
                        Vector3 vert = new()
                        {
                            X = Convert.ToSingle(verts[1], CultureInfo.InvariantCulture),
                            Y = Convert.ToSingle(verts[2], CultureInfo.InvariantCulture),
                            Z = Convert.ToSingle(verts[3], CultureInfo.InvariantCulture)
                        };

                        obj.Verts.Add(vert);
                    }
                    else if (line.StartsWith("vt "))
                    {

                        if (reading_faces)
                        {
                            reading_faces = false;
                            prevMaxVerts += obj.Verts.Count;
                            prevMaxUvs += obj.Uv.Count;
                            prevMaxNorms += obj.Normals.Count;

                            objects.Add(obj);
                            obj = new Obj_data();
                        }

                        String[] uv0 = line.Split(' ');
                        Vector2 uv = new()
                        {
                            X = Convert.ToSingle(uv0[1], CultureInfo.InvariantCulture),
                            Y = Convert.ToSingle(uv0[2], CultureInfo.InvariantCulture)
                        };

                        obj.Uv.Add(uv);
                    }
                    else if (line.StartsWith("vn "))
                    {

                        if (reading_faces)
                        {
                            reading_faces = false;
                            prevMaxVerts += obj.Verts.Count;
                            prevMaxUvs += obj.Uv.Count;
                            prevMaxNorms += obj.Normals.Count;

                            objects.Add(obj);
                            obj = new Obj_data();
                        }

                        String[] norms = line.Split(' ');
                        Vector3 norm = new()
                        {
                            X = Convert.ToSingle(norms[1], CultureInfo.InvariantCulture),
                            Y = Convert.ToSingle(norms[2], CultureInfo.InvariantCulture),
                            Z = Convert.ToSingle(norms[3], CultureInfo.InvariantCulture)
                        };

                        obj.Normals.Add(norm);
                    }
                    else if (line.StartsWith("s "))
                    {
                        current_shade_group = line[2..];
                    }
                    else if (line.StartsWith("f "))
                    {
                        reading_faces = true;

                        if (current_shade_group != null)
                        {
                            if (obj.Shading_groups.TryGetValue(current_shade_group, out List<int> value))
                                value.Add(obj.Faces.Count);
                            else
                            {
                                List<int> newfaces = [obj.Faces.Count];
                                obj.Shading_groups.Add(current_shade_group, newfaces);
                            }
                        }

                        String[] faces = line[2..].Split(' ');
                        for (int x = 0; x < 3; x++)
                        {
                            ushort fa = 0, fb = 0, fc = 0;
                            if (obj.Verts.Count > 0)
                                fa = (ushort)(Convert.ToUInt16(faces[x].Split('/')[0]) - prevMaxVerts - 1);
                            if (obj.Uv.Count > 0)
                                fb = (ushort)(Convert.ToUInt16(faces[x].Split('/')[1]) - prevMaxUvs - 1);
                            if (obj.Normals.Count > 0)
                                fc = (ushort)(Convert.ToUInt16(faces[x].Split('/')[2]) - prevMaxNorms - 1);
                            if (fa < 0 || fb < 0 || fc < 0)
                                throw new Exception("What the actual flapjack, something is *VERY* wrong");
                            obj.Faces.Add(new Face(fa, fb, fc));
                        }
                    }
                }

                if (!objects.Contains(obj))
                    objects.Add(obj);
            }


            //Read each object
            foreach (Obj_data obj in objects)
            {
                //One would fix Tatsuto's broken shading here.

                //Locate the proper model
                var hashname = HashName.FromNumberOrString(obj.Object_name);
                Model modelSection = fmd.parsed_sections
                    .Where(i => i.Value is Model mod && hashname.Hash == mod.HashName.Hash)
                    .Select(i => i.Value as Model)
                    .FirstOrDefault();

                //Apply new changes
                if (modelSection == null)
                {
                    toAddObjects.Add(obj);
                    continue;
                }

                PassthroughGP passthrough_section = modelSection.PassthroughGP;
                DieselGeometry geometry_section = passthrough_section.DieselGeometry;
                Topology topology_section = passthrough_section.Topology;

                AddObject(obj, modelSection, geometry_section, topology_section);
            }


            //Add new objects
            if (addNew)
            {
                foreach (Obj_data obj in toAddObjects)
                {
                    //create new Model
                    Material newMat = new(obj.Material_name);
                    fmd.AddSection(newMat);
                    MaterialGroup newMatG = new(newMat);
                    fmd.AddSection(newMatG);
                    DieselGeometry newGeom = new(obj);
                    fmd.AddSection(newGeom);
                    Topology newTopo = new(obj);
                    fmd.AddSection(newTopo);

                    PassthroughGP newPassGP = new(newGeom, newTopo);
                    fmd.AddSection(newPassGP);
                    TopologyIP newTopoIP = new(newTopo);
                    fmd.AddSection(newTopoIP);

                    Object3D parent = root_point.Invoke(obj.Object_name);
                    Model newModel = new(obj, newPassGP, newTopoIP, newMatG, parent);
                    fmd.AddSection(newModel);

                    AddObject(obj, newModel, newGeom, newTopo);

                    //Add new sections
                }
            }
        }

        private static void AddObject(Obj_data obj, Model model_data_section, DieselGeometry geometry_section, Topology topology_section)
        {
            List<Face> called_faces = [];
            List<int> duplicate_verts = [];
            Dictionary<int, Face> dup_faces = [];

            bool broken = false;
            for (int x_f = 0; x_f < obj.Faces.Count; x_f++)
            {
                Face f = obj.Faces[x_f];
                broken = false;

                foreach (Face called_f in called_faces)
                {
                    if (called_f.a == f.a && called_f.b != f.b)
                    {
                        duplicate_verts.Add(x_f);
                        broken = true;
                        break;
                    }
                }

                if (!broken)
                    called_faces.Add(f);
            }

            Dictionary<int, Face> done_faces = [];

            foreach (int dupe in duplicate_verts)
            {
                int replacedF = -1;
                foreach (KeyValuePair<int, Face> pair in done_faces)
                {
                    Face f = pair.Value;
                    if (f.a == obj.Faces[dupe].a && f.b == obj.Faces[dupe].b)
                    {
                        replacedF = pair.Key;
                    }
                }

                Face new_face;
                if (replacedF > -1)
                {
                    new_face = new Face(obj.Faces[replacedF].a, obj.Faces[replacedF].b, obj.Faces[dupe].c);

                }
                else
                {
                    new_face = new Face((ushort)obj.Verts.Count, obj.Faces[dupe].b, obj.Faces[dupe].c);
                    obj.Verts.Add(obj.Verts[obj.Faces[dupe].a]);

                    done_faces.Add(dupe, obj.Faces[dupe]);
                }

                obj.Faces[dupe] = new_face;
            }

            Vector3 new_Model_data_bounds_min = new();// Z (max), X (low), Y (low)
            Vector3 new_Model_data_bounds_max = new();// Z (low), X (max), Y (max)

            foreach (Vector3 vert in obj.Verts)
            {
                //Z
                // Note these were previously broken
                if (vert.Z < new_Model_data_bounds_min.Z)
                    new_Model_data_bounds_min.Z = vert.Z;

                if (vert.Z > new_Model_data_bounds_max.Z)
                    new_Model_data_bounds_max.Z = vert.Z;

                //X
                if (vert.X < new_Model_data_bounds_min.X)
                    new_Model_data_bounds_min.X = vert.X;
                if (vert.X > new_Model_data_bounds_max.X)
                    new_Model_data_bounds_max.X = vert.X;

                //Y
                if (vert.Y < new_Model_data_bounds_min.Y)
                    new_Model_data_bounds_min.Y = vert.Y;

                if (vert.Y > new_Model_data_bounds_max.Y)
                    new_Model_data_bounds_max.Y = vert.Y;
            }

            //Arrange UV and Normals
            List<Vector3> new_arranged_Geometry_normals = [];
            List<Vector3> new_arranged_Geometry_unknown20 = [];
            List<Vector3> new_arranged_Geometry_unknown21 = [];
            List<int> added_uvs = [];
            List<int> added_normals = [];

            Vector2[] new_arranged_UV = new Vector2[obj.Verts.Count];
            for (int x = 0; x < new_arranged_UV.Length; x++)
                new_arranged_UV[x] = new Vector2(100f, 100f);
            Vector2 sentinel = new(100f, 100f);
            Vector3[] new_arranged_Normals = new Vector3[obj.Verts.Count];
            for (int x = 0; x < new_arranged_Normals.Length; x++)
                new_arranged_Normals[x] = new Vector3(0f, 0f, 0f);
            Vector3[] new_arranged_unknown20 = new Vector3[obj.Verts.Count];
            Vector3[] new_arranged_unknown21 = new Vector3[obj.Verts.Count];

            List<Face> new_faces = [];

            for (int fcount = 0; fcount < obj.Faces.Count; fcount += 3)
            {
                Face f1 = obj.Faces[fcount + 0];
                Face f2 = obj.Faces[fcount + 1];
                Face f3 = obj.Faces[fcount + 2];

                //UV
                if (obj.Uv.Count > 0)
                {
                    if (new_arranged_UV[f1.a].Equals(sentinel))
                        new_arranged_UV[f1.a] = obj.Uv[f1.b];
                    if (new_arranged_UV[f2.a].Equals(sentinel))
                        new_arranged_UV[f2.a] = obj.Uv[f2.b];
                    if (new_arranged_UV[f3.a].Equals(sentinel))
                        new_arranged_UV[f3.a] = obj.Uv[f3.b];
                }

                //normal
                if (obj.Normals.Count > 0)
                {
                    new_arranged_Normals[f1.a] = obj.Normals[f1.c];
                    new_arranged_Normals[f2.a] = obj.Normals[f2.c];
                    new_arranged_Normals[f3.a] = obj.Normals[f3.c];
                }

                Face new_f = new(f1.a, f2.a, f3.a);

                new_faces.Add(new_f);
            }

            for (int x = 0; x < new_arranged_Normals.Length; x++)
                new_arranged_Normals[x] = Vector3.Normalize(new_arranged_Normals[x]);

            List<Vector3> obj_verts = obj.Verts;
            ComputeTangentBasis(ref new_faces, ref obj_verts, ref new_arranged_UV, ref new_arranged_Normals, ref new_arranged_unknown20, ref new_arranged_unknown21);

            List<RenderAtom> new_Model_items2 = [];

            foreach (RenderAtom modelitem in model_data_section.RenderAtoms)
            {
                RenderAtom new_model_item = new()
                {
                    BaseVertex = modelitem.BaseVertex,
                    TriangleCount = (uint)new_faces.Count,
                    BaseIndex = modelitem.BaseIndex,
                    GeometrySliceLength = (uint)obj.Verts.Count,
                    MaterialId = modelitem.MaterialId
                };

                new_Model_items2.Add(new_model_item);
            }

            model_data_section.RenderAtoms = new_Model_items2;

            if (model_data_section.Version != 6)
            {
                model_data_section.BoundsMin = new_Model_data_bounds_min;
                model_data_section.BoundsMax = new_Model_data_bounds_max;
                model_data_section.BoundingRadius = obj.Verts.Select(i => i.Length()).Max();
            }

            geometry_section.vert_count = (uint)obj.Verts.Count;
            geometry_section.verts = obj.Verts;
            geometry_section.normals = [.. new_arranged_Normals];
            geometry_section.UVs[0] = [.. new_arranged_UV];
            geometry_section.binormals = [.. new_arranged_unknown20];
            geometry_section.tangents = [.. new_arranged_unknown21];

            topology_section.facelist = new_faces;
        }

        private static void ComputeTangentBasis(ref List<Face> faces, ref List<Vector3> verts, ref Vector2[] uv0, ref Vector3[] normals, ref Vector3[] tangents, ref Vector3[] binormals)
        {
            //Taken from various sources online. Search up Normal Vector Tangent calculation.

            List<ushort> parsed = [];

            foreach (Face f in faces)
            {
                float u02 = (uv0[f.c].X - uv0[f.a].X);
                float v02 = (uv0[f.c].Y - uv0[f.a].Y);
                float u01 = (uv0[f.b].X - uv0[f.a].X);
                float v01 = (uv0[f.b].Y - uv0[f.a].Y);
                float dot00 = u02 * u02 + v02 * v02;
                float dot01 = u02 * u01 + v02 * v01;
                float dot11 = u01 * u01 + v01 * v01;
                float d = dot00 * dot11 - dot01 * dot01;
                float u = 1.0f;
                float v = 1.0f;
                if (d != 0.0f)
                {
                    u = (dot11 * u02 - dot01 * u01) / d;
                    v = (dot00 * u01 - dot01 * u02) / d;
                }

                Vector3 tangent = verts[f.c] * u + verts[f.b] * v - verts[f.a] * (u + v);

                //vert1
                if (!parsed.Contains(f.a))
                {
                    binormals[f.a] = Vector3.Normalize(Vector3.Cross(tangent, normals[f.a]));
                    tangents[f.a] = Vector3.Normalize(Vector3.Cross(binormals[f.a], normals[f.a]));
                    parsed.Add(f.a);
                }

                //vert2
                if (!parsed.Contains(f.b))
                {
                    binormals[f.b] = Vector3.Normalize(Vector3.Cross(tangent, normals[f.b]));
                    tangents[f.b] = Vector3.Normalize(Vector3.Cross(binormals[f.b], normals[f.b]));
                    parsed.Add(f.b);
                }
                //vert3
                if (!parsed.Contains(f.c))
                {
                    binormals[f.c] = Vector3.Normalize(Vector3.Cross(tangent, normals[f.c]));
                    tangents[f.c] = Vector3.Normalize(Vector3.Cross(binormals[f.c], normals[f.c]));
                    parsed.Add(f.c);
                }

            }
        }

        public static bool ImportNewObjPatternUV(FullModelData fm, string filepath)
        {
            Log.Default.Info("Importing new obj with file for UV patterns: {0}", filepath);

            //Preload the .obj
            List<Obj_data> objects = [];

            try
            {
                using (FileStream fs = new(filepath, FileMode.Open, FileAccess.Read))
                {
                    using StreamReader sr = new(fs);
                    string line;
                    Obj_data obj = new();
                    bool reading_faces = false;
                    int prevMaxVerts = 0;
                    int prevMaxUvs = 0;
                    int prevMaxNorms = 0;


                    while ((line = sr.ReadLine()) != null)
                    {

                        //preloading objects
                        if (!line.StartsWith('#'))

						{
                            if (line.StartsWith("o ") || line.StartsWith("g "))
                            {

                                if (reading_faces)
                                {
                                    reading_faces = false;
                                    prevMaxVerts += obj.Verts.Count;
                                    prevMaxUvs += obj.Uv.Count;
                                    prevMaxNorms += obj.Normals.Count;

                                    objects.Add(obj);
                                    obj = new Obj_data();
                                }

                                obj.Object_name = line[2..];
                            }
                            else if (line.StartsWith("usemtl "))
                            {
                                obj.Material_name = line[2..];
                            }
                            else if (line.StartsWith("v "))
                            {

                                if (reading_faces)
                                {
                                    reading_faces = false;
                                    prevMaxVerts += obj.Verts.Count;
                                    prevMaxUvs += obj.Uv.Count;
                                    prevMaxNorms += obj.Normals.Count;

                                    objects.Add(obj);
                                    obj = new Obj_data();
                                }

                                String[] verts = line.Replace("  ", " ").Split(' ');
                                Vector3 vert = new()
                                {
                                    X = Convert.ToSingle(verts[1], CultureInfo.InvariantCulture),
                                    Y = Convert.ToSingle(verts[2], CultureInfo.InvariantCulture),
                                    Z = Convert.ToSingle(verts[3], CultureInfo.InvariantCulture)
                                };

                                obj.Verts.Add(vert);
                            }
                            else if (line.StartsWith("vt "))
                            {

                                if (reading_faces)
                                {
                                    reading_faces = false;
                                    prevMaxVerts += obj.Verts.Count;
                                    prevMaxUvs += obj.Uv.Count;
                                    prevMaxNorms += obj.Normals.Count;

                                    objects.Add(obj);
                                    obj = new Obj_data();
                                }

                                String[] uv0 = line.Split(' ');
                                Vector2 uv = new()
                                {
                                    X = Convert.ToSingle(uv0[1], CultureInfo.InvariantCulture),
                                    Y = Convert.ToSingle(uv0[2], CultureInfo.InvariantCulture)
                                };

                                obj.Uv.Add(uv);
                            }
                            else if (line.StartsWith("vn "))
                            {

                                if (reading_faces)
                                {
                                    reading_faces = false;
                                    prevMaxVerts += obj.Verts.Count;
                                    prevMaxUvs += obj.Uv.Count;
                                    prevMaxNorms += obj.Normals.Count;

                                    objects.Add(obj);
                                    obj = new Obj_data();
                                }

                                String[] norms = line.Split(' ');
                                Vector3 norm = new()
                                {
                                    X = Convert.ToSingle(norms[1], CultureInfo.InvariantCulture),
                                    Y = Convert.ToSingle(norms[2], CultureInfo.InvariantCulture),
                                    Z = Convert.ToSingle(norms[3], CultureInfo.InvariantCulture)
                                };

                                obj.Normals.Add(norm);
                            }
                            else if (line.StartsWith("f "))
                            {
                                reading_faces = true;
                                String[] faces = line[2..].Split(' ');
                                for (int x = 0; x < 3; x++)
                                {
                                    ushort fa = 0, fb = 0, fc = 0;
                                    if (obj.Verts.Count > 0)
                                        fa = (ushort)(Convert.ToUInt16(faces[x].Split('/')[0]) - prevMaxVerts - 1);
                                    if (obj.Uv.Count > 0)
                                        fb = (ushort)(Convert.ToUInt16(faces[x].Split('/')[1]) - prevMaxUvs - 1);
                                    if (obj.Normals.Count > 0)
                                        fc = (ushort)(Convert.ToUInt16(faces[x].Split('/')[2]) - prevMaxNorms - 1);
                                    if (fa < 0 || fb < 0 || fc < 0)
                                        throw new Exception("What the actual flapjack, something is *VERY* wrong");
                                    obj.Faces.Add(new Face(fa, fb, fc));
                                }

                            }
                        }
                        else continue;
                    }

                    if (!objects.Contains(obj))
                        objects.Add(obj);
                }



                //Read each object
                foreach (Obj_data obj in objects)
                {

                    //Locate the proper model
                    uint modelSectionid = 0;
                    foreach (KeyValuePair<uint, ISection> pair in fm.parsed_sections)
                    {
                        if (modelSectionid != 0)
                            break;

                        if (pair.Value is Model model)
                        {
                            if (UInt64.TryParse(obj.Object_name, out ulong tryp))
                            {
                                if (tryp == model.HashName.Hash)
                                    modelSectionid = pair.Key;
                            }
                            else
                            {
                                if (Hash64.HashString(obj.Object_name) == model.HashName.Hash)
                                    modelSectionid = pair.Key;
                            }
                        }
                    }

                    //Apply new changes
                    if (modelSectionid == 0)
                        continue;

                    Model model_data_section = (Model)fm.parsed_sections[modelSectionid];
                    PassthroughGP passthrough_section = model_data_section.PassthroughGP;
                    DieselGeometry geometry_section = passthrough_section.DieselGeometry;
                    Topology topology_section = passthrough_section.Topology;

                    //Arrange UV and Normals
                    Vector2[] new_arranged_UV = new Vector2[geometry_section.verts.Count];
                    for (int x = 0; x < new_arranged_UV.Length; x++)
                        new_arranged_UV[x] = new Vector2(100f, 100f);
                    Vector2 sentinel = new(100f, 100f);

                    if (topology_section.facelist.Count != obj.Faces.Count / 3)
                        return false;

                    for (int fcount = 0; fcount < topology_section.facelist.Count; fcount += 3)
                    {
                        Face f1 = obj.Faces[fcount + 0];
                        Face f2 = obj.Faces[fcount + 1];
                        Face f3 = obj.Faces[fcount + 2];

                        //UV
                        if (obj.Uv.Count > 0)
                        {
                            if (new_arranged_UV[topology_section.facelist[fcount / 3 + 0].a].Equals(sentinel))
                                new_arranged_UV[topology_section.facelist[fcount / 3 + 0].a] = obj.Uv[f1.b];
                            if (new_arranged_UV[topology_section.facelist[fcount / 3 + 0].b].Equals(sentinel))
                                new_arranged_UV[topology_section.facelist[fcount / 3 + 0].b] = obj.Uv[f2.b];
                            if (new_arranged_UV[topology_section.facelist[fcount / 3 + 0].c].Equals(sentinel))
                                new_arranged_UV[topology_section.facelist[fcount / 3 + 0].c] = obj.Uv[f3.b];
                        }
                    }



                    geometry_section.UVs[1] = [.. new_arranged_UV];

                    passthrough_section.DieselGeometry.UVs[1] = [.. new_arranged_UV];
                }
            }
            catch (Exception exc)
            {
                System.Windows.Forms.MessageBox.Show(exc.ToString());
                return false;
            }
            return true;
        }
    }
}
