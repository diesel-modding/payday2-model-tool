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
            bool hasUvs = obj.Uv.Count > 0;
            bool hasNormals = obj.Normals.Count > 0;

            var vertexMap = new Dictionary<(ushort Position, ushort Uv, ushort Normal), ushort>();
            var sourceUvVertices = new List<(ushort Position, ushort Uv)>();
            List<Vector3> verts = [];
            List<Vector2> uvs = [];
            List<Vector3> normals = [];
            List<Face> faces = [];

            ushort GetVertex(Face corner)
            {
                var key = (
                    corner.a,
                    hasUvs ? corner.b : ushort.MaxValue,
                    hasNormals ? corner.c : ushort.MaxValue);

                if (vertexMap.TryGetValue(key, out ushort index))
                    return index;

                if (verts.Count >= ushort.MaxValue)
                    throw new Exception("OBJ contains too many expanded vertices for Diesel.");

                index = (ushort)verts.Count;
                vertexMap.Add(key, index);
                verts.Add(obj.Verts[corner.a]);
                sourceUvVertices.Add((corner.a, hasUvs ? corner.b : ushort.MaxValue));

                if (hasUvs)
                    uvs.Add(obj.Uv[corner.b]);
                if (hasNormals)
                    normals.Add(obj.Normals[corner.c]);

                return index;
            }

            if (obj.Faces.Count % 3 != 0)
                throw new Exception("OBJ face corner count is not divisible by 3.");

            for (int i = 0; i < obj.Faces.Count; i += 3)
            {
                faces.Add(new Face(
                    GetVertex(obj.Faces[i]),
                    GetVertex(obj.Faces[i + 1]),
                    GetVertex(obj.Faces[i + 2])));
            }

            List<Vector3> uvDirectionU = [];
            List<Vector3> uvDirectionV = [];

            if (hasUvs && uvs.Count == verts.Count)
            {
                DieselGeometry.ComputeUvDirections(
                    verts,
                    uvs,
                    faces,
                    out uvDirectionU,
                    out uvDirectionV);

                if (hasNormals && normals.Count == verts.Count)
                {
                    StabilizeUvOrientationRegions(
                        sourceUvVertices,
                        uvs,
                        normals,
                        faces,
                        uvDirectionU,
                        uvDirectionV);
                }
            }

            if (verts.Count == 0)
                return;

            Vector3 boundsMin = verts.Aggregate(MathUtil.Min);
            Vector3 boundsMax = verts.Aggregate(MathUtil.Max);

            List<RenderAtom> renderAtoms = [];
            foreach (RenderAtom atom in model_data_section.RenderAtoms)
            {
                renderAtoms.Add(new RenderAtom
                {
                    BaseVertex = atom.BaseVertex,
                    TriangleCount = (uint)faces.Count,
                    BaseIndex = atom.BaseIndex,
                    GeometrySliceLength = (uint)verts.Count,
                    MaterialId = atom.MaterialId
                });
            }
            model_data_section.RenderAtoms = renderAtoms;

            if (model_data_section.Version != 6)
            {
                model_data_section.BoundsMin = boundsMin;
                model_data_section.BoundsMax = boundsMax;
                model_data_section.BoundingRadius = verts.Max(v => v.Length());
            }

            geometry_section.vert_count = (uint)verts.Count;
            geometry_section.verts = verts;
            geometry_section.normals = normals;
            geometry_section.UVs[0] = uvs;
            geometry_section.uvDirectionU = uvDirectionU;
            geometry_section.uvDirectionV = uvDirectionV;
            topology_section.facelist = faces;
        }

        private static void StabilizeUvOrientationRegions(
            IReadOnlyList<(ushort Position, ushort Uv)> sourceVertices,
            IReadOnlyList<Vector2> uvs,
            IReadOnlyList<Vector3> normals,
            IReadOnlyList<Face> faces,
            IList<Vector3> directionU,
            IList<Vector3> directionV)
        {
            const float minUvArea = DieselGeometry.UvDeterminantEpsilon;
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
