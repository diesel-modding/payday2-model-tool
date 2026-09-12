using PD2Bundle;
using PD2ModelParser.Modelscript;
using PD2ModelParser.Sections;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

using static PD2ModelParser.Tags;

namespace PD2ModelParser.Exporters
{
    class ObjWriter
    {

        public static string ExportUV0File(FullModelData data, string filepath)
        {
            string output_file = filepath.Replace(".model", ".uv0.obj");

            ExportObj(data, output_file, 0);

            return output_file;
        }

        public static string ExportUV1File(FullModelData data, string filepath)
        {
            string output_file = filepath.Replace(".model", ".uv1.obj");

            ExportObj(data, output_file, 1);

            return output_file;
        }


        private static void ExportObj(FullModelData data, string path, int uvchannel)
        {
            List<SectionHeader> sections = data.sections;
            Dictionary<UInt32, ISection> parsed_sections = data.parsed_sections;
            byte[] leftover_data = data.leftover_data;

            //Generate obj
            ushort maxfaces = 0;
            UInt32 uvcount = 0;
            UInt32 normalcount = 0;

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    foreach (SectionHeader sectionheader in sections)
                    {
                        if (sectionheader.type == model_data_tag)
                        {
                            Model model_data = (Model)parsed_sections[sectionheader.id];
                            if (model_data.version == 6)
                                continue;
                            PassthroughGP passthrough_section = model_data.PassthroughGP;
                            Geometry geometry_section = passthrough_section.Geometry;
                            Log.Default.Debug("geometry_section {0}", geometry_section);
                            Topology topology_section = passthrough_section.Topology;
                            sw.WriteLine("#Diesel Model Tool");
                            sw.WriteLine("#OBJ Exporter");
                            sw.WriteLine("#Object " + model_data.Name);
                            sw.WriteLine("o " + model_data.Name);
                            foreach (Vector3 vert in geometry_section.verts)
                            {
                                sw.WriteLine("v " + vert.X.ToString("0.000000", CultureInfo.InvariantCulture) + " " + vert.Y.ToString("0.000000", CultureInfo.InvariantCulture) + " " + vert.Z.ToString("0.000000", CultureInfo.InvariantCulture));
                            }
                            sw.WriteLine("# " + geometry_section.verts.Count + " Vertices");

                            foreach (Vector2 uv in geometry_section.UVs[uvchannel])
                            {
                                sw.WriteLine("vt " + uv.X.ToString("0.000000", CultureInfo.InvariantCulture) + " " + uv.Y.ToString("0.000000", CultureInfo.InvariantCulture));
                            }

                            sw.WriteLine("# " + geometry_section.UVs[uvchannel].Count + " UVs");
                            foreach (Vector3 norm in geometry_section.normals)
                            {
                                sw.WriteLine("vn " + norm.X.ToString("0.000000", CultureInfo.InvariantCulture) + " " + norm.Y.ToString("0.000000", CultureInfo.InvariantCulture) + " " + norm.Z.ToString("0.000000", CultureInfo.InvariantCulture));
                            }
                            sw.WriteLine("# " + geometry_section.normals.Count + " Normals");
                            sw.WriteLine();

                            sw.WriteLine("g " + model_data.Name);
                            foreach (Face face in topology_section.facelist)
                            {
                                //x
                                sw.Write("f " + (maxfaces + face.a + 1));
                                sw.Write('/');
                                if (geometry_section.uv0.Count > 0)
                                    sw.Write((uvcount + face.a + 1));
                                sw.Write('/');
                                if (geometry_section.normals.Count > 0)
                                    sw.Write((normalcount + face.a + 1));

                                //y
                                sw.Write(" " + (maxfaces + face.b + 1));
                                sw.Write('/');
                                if (geometry_section.uv0.Count > 0)
                                    sw.Write((uvcount + face.b + 1));
                                sw.Write('/');
                                if (geometry_section.normals.Count > 0)
                                    sw.Write((normalcount + face.b + 1));

                                //z
                                sw.Write(" " + (maxfaces + face.c + 1));
                                sw.Write('/');
                                if (geometry_section.uv0.Count > 0)
                                    sw.Write((uvcount + face.c + 1));
                                sw.Write('/');
                                if (geometry_section.normals.Count > 0)
                                    sw.Write((normalcount + face.c + 1));

                                sw.WriteLine();
                            }
                            sw.WriteLine("# " + topology_section.facelist.Count + " Faces");
                            sw.WriteLine();

                            maxfaces += (ushort)geometry_section.verts.Count;
                            uvcount += (ushort)geometry_section.uv0.Count;
                            normalcount += (ushort)geometry_section.normals.Count;
                        }
                    }
                    sw.Close();
                }
                fs.Close();
            }
        }
    }
}
