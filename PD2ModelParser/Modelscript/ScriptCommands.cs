using PD2ModelParser.Importers;
using PD2ModelParser.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using System.Xml.Serialization;
using S = PD2ModelParser.Sections;

namespace PD2ModelParser.Modelscript
{
    public class CreateNewObjects : ScriptItem
    {
        [Required] public bool Create { get; set; }
        public override void Execute(ScriptState state)
        {
            ScriptState.Log.Status(Create ? "New objects will be created" : "New objects will NOT be created");
            state.CreateNewObjects = Create;
        }
    }

    public class SetRootPoint : ScriptItem
    {
        [Required] public string Name { get; set; }
        public override void Execute(ScriptState state)
        {
            if (Name == null)
            {
                ScriptState.Log.Status("Clearing default root point");
                state.DefaultRootPoint = null;
            }
            else
            {
                ScriptState.Log.Status($"Setting default root point to {Name}");
                var point = state.Data.SectionsOfType<S.Object3D>()
                    .FirstOrDefault(o => o.Name == Name);
                state.DefaultRootPoint = point ?? throw new Exception($"Root point {Name} not found!");
            }
        }
    }

    public class NewModel : ScriptItem
    {
        public override void Execute(ScriptState state)
        {
            ScriptState.Log.Status("Creating new model");
            state.Data = new FullModelData();
        }

    }

    public class LoadModel : ScriptItem
    {
        [Required] public string File { get; set; }
        public override void Execute(ScriptState state)
        {
            string resolvedPath = state.ResolvePath(File);
            ScriptState.Log.Status($"Loading model from {resolvedPath}");
            state.Data = ModelReader.Open(resolvedPath);
        }
    }

    public class SaveModel : ScriptItem
    {
        [Required] public string File { get; set; }
        public override void Execute(ScriptState state)
        {
            var resolvedPath = state.ResolvePath(File);
            ScriptState.Log.Status($"Saving model to {resolvedPath}");
            Exporters.DieselExporter.ExportFile(state.Data, resolvedPath);
        }
    }

    public class ValidateModel : ScriptItem
    {
        [Required] public string File { get; set; }

        public override void Execute(ScriptState state)
        {
            string path = state.ResolvePath(File);
            Console.WriteLine($"Validating {path}");
            try
            {
                var (original, repaired, repairs) = S.ModelValidator.CheckFile(path);
                original.Print("Original file");
                Console.WriteLine($"\nModel-level metadata repairs available: {repairs}");
                repaired.Print("After simulated metadata repair (not saved)");
                if (original.Errors > 0 || repaired.Errors > 0)
                    Environment.ExitCode = 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Cannot validate {path}: {ex.Message}");
                Environment.ExitCode = 2;
            }
        }
    }

    public class Resave : ScriptItem
    {
        [Required] public string File { get; set; }
        public bool Overwrite { get; set; }

        public override void Execute(ScriptState state)
        {
            string sourcePath = state.ResolvePath(File);
            string outputPath = ResaveFile(sourcePath, Overwrite);
            ScriptState.Log.Status($"Resaved model {sourcePath} to {outputPath}");
        }
        public static string ResaveFile(string inputPath, bool overwrite = false,
            Func<IReadOnlyList<string>, bool> confirmUnknownData = null)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                throw new ArgumentException("A model file path is required.", nameof(inputPath));

            string sourcePath = System.IO.Path.GetFullPath(inputPath);
            if (!string.Equals(System.IO.Path.GetExtension(sourcePath), ".model", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The resave input must be a .model file.", nameof(inputPath));

            string outputPath = overwrite ? sourcePath : System.IO.Path.ChangeExtension(sourcePath, ".new.model");
            FullModelData model = ModelReader.Open(sourcePath);

            List<string> warnings = [];
            foreach (var pair in model.parsed_sections)
            {
                if (pair.Value is S.Unknown unknown)
                    warnings.Add($"Section {pair.Key}: unknown type 0x{unknown.TypeCode:X8} ({unknown.data?.Length ?? 0} bytes)");
            }

            foreach (string warning in warnings)
                Log.Default.Warn("Resave: {0}", warning);

            if (warnings.Count > 0 && confirmUnknownData != null && !confirmUnknownData(warnings))
                return null;

            RebuildGeometricUvDirections(model);

            string directory = System.IO.Path.GetDirectoryName(outputPath);
            string temporaryPath = System.IO.Path.Combine(directory,
                "." + System.IO.Path.GetFileName(outputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                Exporters.DieselExporter.ExportFile(model, temporaryPath);
                System.IO.File.Move(temporaryPath, outputPath, overwrite: true);
                return outputPath;
            }
            finally
            {
                if (System.IO.File.Exists(temporaryPath))
                    System.IO.File.Delete(temporaryPath);
            }
        }

        private static void RebuildGeometricUvDirections(FullModelData data)
        {
            S.ModelRepair.RepairAll(data);

            var visited = new HashSet<S.DieselGeometry>();
            foreach (var model in data.SectionsOfType<S.Model>())
            {
                var gp = model.PassthroughGP;
                var geometry = gp?.DieselGeometry;
                var topology = gp?.Topology;
                if (geometry == null || topology == null || visited.Contains(geometry))
                    continue;

                List<S.Face> faces = ResolveResaveFaces(model, geometry, topology);
                if (faces.Count == 0)
                {
                    Log.Default.Warn("Resave: skipping geometric UV recalculation on geometry {0}: no usable triangle faces", geometry.SectionId);
                    continue;
                }

                if (geometry.RebuildUvDirections(faces, out int fallbackVertices, out int repairedNormals))
                {
                    visited.Add(geometry);
                    Log.Default.Info("Resave: recalculated geometric UV on geometry {0} ({1} vertices; {2} fallback tangents; {3} repaired normals)",
                        geometry.SectionId, geometry.vert_count, fallbackVertices, repairedNormals);
                }
            }
        }

        private static List<S.Face> ResolveResaveFaces(S.Model model, S.DieselGeometry geometry, S.Topology topology)
        {
            var faces = new List<S.Face>();
            int vertexCount = geometry.verts.Count;
            var sourceFaces = topology.facelist;

            if (topology.Primitive_type != S.Topology.PrimitiveType.TriangleList)
                return faces;

            foreach (var atom in model.RenderAtoms)
            {
                if (atom.BaseIndex % 3 != 0 || atom.BaseIndex / 3 > sourceFaces.Count)
                    continue;
                int start = checked((int)(atom.BaseIndex / 3));
                int count = (int)Math.Min(atom.TriangleCount, (uint)(sourceFaces.Count - start));
                if (count == 0) continue;

                var slice = sourceFaces.GetRange(start, count);
                bool local = atom.GeometrySliceLength > 0;
                bool absolute = atom.GeometrySliceLength > 0;
                foreach (var f in slice)
                {
                    foreach (int index in new[] { (int)f.a, (int)f.b, (int)f.c })
                    {
                        local &= index < atom.GeometrySliceLength && index + (long)atom.BaseVertex < vertexCount;
                        absolute &= index >= atom.BaseVertex && (long)index - atom.BaseVertex < atom.GeometrySliceLength && index < vertexCount;
                    }
                }

                if (!local && !absolute)
                {
                    Log.Default.Warn("Resave: cannot resolve vertex indices for geometry {0}, atom at index {1}; skipping atom", geometry.SectionId, atom.BaseIndex);
                    continue;
                }

                if (local && absolute && atom.BaseVertex != 0)
                {
                    Log.Default.Warn("Resave: ambiguous local/absolute indices for geometry {0}, atom at index {1}; skipping atom", geometry.SectionId, atom.BaseIndex);
                    continue;
                }
                int offset = local && !absolute ? checked((int)atom.BaseVertex) : 0;
                if (slice.Any(f => (long)f.a + offset > ushort.MaxValue ||
                                   (long)f.b + offset > ushort.MaxValue ||
                                   (long)f.c + offset > ushort.MaxValue))
                {
                    Log.Default.Warn("Resave: geometry {0} uses vertex indices above 65535; skipping atom at index {1}", geometry.SectionId, atom.BaseIndex);
                    continue;
                }
                foreach (var f in slice)
                    faces.Add(new S.Face((ushort)(f.a + offset),
                        (ushort)(f.b + offset), (ushort)(f.c + offset)));
            }
            return faces;
        }
    }

    public class Import : ScriptItem
    {
        public string File { get; set; }
        [XmlAttribute("type")] public FileTypeInfo ForceType { get; set; }
        public bool? CreateNewObjects { get; set; }

        [NotAttribute] public string DefaultRootPoint { get; set; }
        [NotAttribute] public Dictionary<string, string> Parents { get; set; } = [];
        [NotAttribute] public Dictionary<string, string> ImporterOptions { get; set; } = [];

        public override void ParseXml(XElement element)
        {
            base.ParseXml(element);

            foreach (var child in element.Elements())
            {
                switch (child.Name.ToString())
                {
                    case "rootpoint":
                        var targetname = ScriptXml.RequiredAttr(child, "name");
                        foreach (var rpitem in child.Elements())
                        {
                            switch (rpitem.Name.ToString())
                            {
                                case "object":
                                    var childName = ScriptXml.RequiredAttr(rpitem, "name");
                                    if (this.Parents.ContainsKey(childName))
                                        throw new Exception($"Cannot redefine rootpoint for object {childName}");
                                    this.Parents.Add(childName, targetname);
                                    break;
                                case "default":
                                    if (this.DefaultRootPoint == null)
                                        this.DefaultRootPoint = targetname;
                                    else
                                        throw new Exception($"Cannot redefine default rootpoint to {targetname}");
                                    break;
                                default:
                                    throw new Exception($"Invalid <rootpoint> child: {rpitem.Name}");
                            }
                        }
                        break;
                    case "option":
                        this.ImporterOptions.Add(ScriptXml.RequiredAttr(child, "name"), child.Value.Trim());
                        break;
                }
            }
        }

        public override void Execute(ScriptState state)
        {
            var filepath = state.ResolvePath(File);
            ScriptState.Log.Status($"Importing from {filepath}");
            FileTypeInfo effectiveType = null;
            if (ForceType != null)
            {
                effectiveType = ForceType;
            }
            else
            {
                var ext = System.IO.Path.GetExtension(filepath);
                if (!FileTypeInfo.TryParseName(ext, out effectiveType))
                {
                    throw new Exception($"Unrecognised file extension \"{ext}\". Use a conventional extension or specify the type explicitly.");
                }
            }

            if (!effectiveType.CanImport)
            {
                throw new Exception($"No import support for {effectiveType}");
            }

            var parentObjects = new Dictionary<string, S.Object3D>();
            foreach (var kv in Parents)
            {
                var parent = state.Data.SectionsOfType<S.Object3D>().FirstOrDefault(i => i.Name == kv.Value) ?? throw new Exception($"Cannot find rootpoint element {kv.Value}");
                parentObjects.Add(kv.Key, parent);
            }

            S.Object3D defaultRootObject;
            if (DefaultRootPoint != null)
            {
                defaultRootObject = state.Data.SectionsOfType<S.Object3D>().FirstOrDefault(i => i.Name == DefaultRootPoint);
            }
            else
            {
                defaultRootObject = state.DefaultRootPoint;
            }

            S.Object3D ParentFinder(string name)
            {
                if (parentObjects.TryGetValue(name, out S.Object3D value)) return value;
                {
                    if (defaultRootObject == null)
                    {
                        ScriptState.Log.Status("No root point. Creating 'root_point' at origin");
                        var root = new S.Object3D("1439828811075370459", null);
                        state.Data.AddSection(root);
                        defaultRootObject = root;
                    }
                    return defaultRootObject;
                }
            }

            Importers.IOptionReceiver opts = effectiveType.CreateOptionReceiver();

            foreach (var kv in ImporterOptions)
            {
                opts.AddOption(kv.Key, kv.Value);
            }

            ScriptState.Log.Status("CreateNewObjects: {0} ?? {1}", CreateNewObjects, state.CreateNewObjects);
            bool createObjects = CreateNewObjects ?? state.CreateNewObjects;

            effectiveType.Import(state.Data, filepath, createObjects, ParentFinder, opts);
        }
    }

    public class PatternUV : ScriptItem
    {
        [Required] public string File { get; set; }

        public override void Execute(ScriptState state)
        {
            string path = state.ResolvePath(File);
            ScriptState.Log.Status($"Reading pattern UVs from {path}");
            if (!path.EndsWith(".obj"))
            {
                throw new Exception($"Using \"{0}\" for pattern UV import requires it be OBJ format");
            }
            bool result = Importers.NewObjImporter.ImportNewObjPatternUV(state.Data, path);
            if (!result)
            {
                throw new Exception("There was an error importing Pattern UV OBJ - see console");
            }
        }
    }

    public class Export : ScriptItem
    {
        [Required] public string File { get; set; }
        [XmlAttribute("type")] public FileTypeInfo ForceType { get; set; }
        public override void Execute(ScriptState state)
        {
            string path = state.ResolvePath(File);
            ScriptState.Log.Status($"Exporting to {path}");
            FileTypeInfo fti = ForceType;
            if (ForceType == null)
            {
                var ext = System.IO.Path.GetExtension(path);
                if (!FileTypeInfo.TryParseName(ext, out fti))
                {
                    throw new Exception($"Unrecognised file extension \"{ext}\". Use a conventional extension or specify the type explicitly.");
                }
            }

            if (!fti.CanExport)
                throw new Exception($"Cannot export {fti.FormatName}");

            fti.Export(state.Data, path);
        }
    }

    public class BatchExport : ScriptItem
    {
        [XmlAttribute("type")] public FileTypeInfo FileType { get; set; }
        [Required, XmlAttribute("sourcedir")] public string Directory { get; set; }

        public override void Execute(ScriptState state)
        {
            var dir = state.ResolvePath(Directory);
            ScriptState.Log.Status($"Batch exporting in {dir}");
            var actualType = FileType ?? state.DefaultExportType;
            foreach(var (path, _, fmd) in BulkFunctions.EveryModel(dir)) {
                state.Data = fmd;
                new Export
                {
                    // TODO: Better extension handling
                    File = System.IO.Path.ChangeExtension(path, actualType.Extension),
                    ForceType = actualType
                }.Execute(state);
            }
        }
    }

    public class SetDefaultExportType : ScriptItem
    {
        [Required, XmlAttribute("type")] public FileTypeInfo FileType { get; set; }
        public override void Execute(ScriptState state)
        {
            ScriptState.Log.Status($"Default batch export type is {FileType}");
            state.DefaultExportType = FileType;
        }
    }

    public class RunScript : ScriptItem
    {
        [Required] public string File { get; set; }
        public override void Execute(ScriptState state)
        {
            string path = state.ResolvePath(File);
            ScriptState.Log.Status($"Running other modelscript {path}");
            string oldBaseDir = state.WorkDir;
            state.WorkDir = System.IO.Path.GetDirectoryName(path);
            var script = Script.ParseXml(path);
            state.ExecuteItems(script);
            state.WorkDir = oldBaseDir;
            ScriptState.Log.Status($"Finished running {File}");
        }
    }

    public class CreateObject3d : ScriptItem
    {
        public string Name { get; set; }
        public string Parent { get; set; }
        public Vector3 Position { get; set; } = new Vector3(0, 0, 0);
        public Quaternion Rotation { get; set; } = new Quaternion(0, 0, 0, 1);
        public Vector3 Scale { get; set; } = new Vector3(1, 1, 1);

        public override void Execute(ScriptState state)
        {
            ScriptState.Log.Status($"Creating object {Name}");
            var extant = state.Data.SectionsOfType<S.Object3D>()
                .Where(i => i.Name == Name)
                .FirstOrDefault();
            if (extant != null)
            {
                throw new Exception($"Cannot create object {Name}: Already exists.");
            }

            S.Object3D parent = null;
            if (Parent != null)
            {
                parent = state.Data.SectionsOfType<S.Object3D>()
                    .Where(i => i.Name == Parent)
                    .FirstOrDefault() ?? throw new Exception($"Cannot find Object3D named \"{Parent}\" to use as a parent for {Name}");
            }

            var obj = new S.Object3D(Name, null);
            obj.SetParent(parent); // Add obj to parent's list of children
            var tf = Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation);
            tf.Translation = Position;
            obj.Transform = tf;
            state.Data.AddSection(obj);
        }
    }

    public class ModifyObject3d : ScriptItem
    {
        public string Name { get; set; }
        public bool SetParent { get; set; }
        public string Parent { get; set; }
        public Vector3? Position { get; set; }
        public Quaternion? Rotation { get; set; }
        public Vector3? Scale { get; set; }

        public override void Execute(ScriptState state)
        {
            ScriptState.Log.Status($"Modifying object {Name}");
            var extant = state.Data.SectionsOfType<S.Object3D>()
                .Where(i => i.Name == Name)
                .FirstOrDefault() ?? throw new Exception($"Cannot modify object {Name} Does not exist.");
            if (SetParent && Parent != null)
            {
                var parent = state.Data.SectionsOfType<S.Object3D>()
                    .Where(i => i.Name == Parent)
                    .FirstOrDefault() ?? throw new Exception($"Cannot find Object3D named \"{Parent}\" to use as a parent for {Name}");
                extant.SetParent(parent);
            }
            else if (SetParent)
            {
                extant.SetParent(null);
            }

            var tf = extant.Transform;
            if (Rotation.HasValue || Scale.HasValue)
            {
                Matrix4x4.Decompose(tf, out var scale, out var rotation, out var translation);
                Rotation.WithValue(r => rotation = r);
                Scale.WithValue(s => scale = s);
                tf = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation);
            }
            Position.WithValue(p => tf.Translation = p);

            extant.Transform = tf;
        }
    }

    public class Duplicate : ScriptItem
    {
        [Required] public string Source { get; set; }
        [Required] public string Destination { get; set; }
        public bool Instance { get; set; } = false;
        [XmlAttribute("new-materials")] public string[] NewMaterials { get; set; }

        public override void Execute(ScriptState state)
        {
            var source = state.Data.SectionsOfType<S.Model>()
                    .Where(i => i.Name == Source)
                    .FirstOrDefault() ?? throw new Exception($"Source model {Source} not found");
            if (source.Version != 3)
            {
                throw new Exception($"Can't duplicate non-v3 model {Source}");
            }

            var destinationExists = state.Data.SectionsOfType<S.Model>()
                    .Any(i => i.Name == Destination);
            if (destinationExists)
            {
                throw new Exception($"Destination model {Destination} not found");
            }

            S.TopologyIP newTopoIp;
            S.PassthroughGP newPgp;
            if(Instance)
            {
                newPgp = source.PassthroughGP;
                newTopoIp = source.TopologyIP;
            }
            else
            {
                var newGeom = source.PassthroughGP.DieselGeometry.Clone();
                newGeom.HashName = new HashName($"{Destination}.DieselGeometry");

                var newTopology = source.TopologyIP.Topology.Clone(Destination);

                newPgp = new S.PassthroughGP(newGeom, newTopology);
                newTopoIp = new S.TopologyIP(newTopology);

                state.Data.AddSection(newGeom);
                state.Data.AddSection(newTopology);
                state.Data.AddSection(newPgp);
                state.Data.AddSection(newTopoIp);
            }

            S.MaterialGroup newMatGroup = source.MaterialGroup;
            if(NewMaterials != null)
            {
                newMatGroup = new S.MaterialGroup(NewMaterials.Select(i =>
                {
                    var nm = new S.Material(i);
                    state.Data.AddSection(nm);
                    return nm;
                }));
            }

            var newModel = new S.Model(Destination,
                (uint)newTopoIp.Topology.facelist.Count,
                newPgp.DieselGeometry.vert_count,
                newPgp, newTopoIp, newMatGroup, source.Parent)
            {
                Version = source.Version
            };
            newModel.RenderAtoms.AddRange(source.RenderAtoms.Select(i => i.Clone()));
            newModel.Lightset_ID = source.Lightset_ID;
            newModel.Properties_bitmap = source.Properties_bitmap;
            newModel.BoundingInt = source.BoundingInt;
            newModel.SkinBones = source.SkinBones;

            newModel.UpdateBounds();

            state.Data.AddSection(newModel);
        }
    }

    public class Delete : ScriptItem
    {
        [Required] public string Target { get; set; }
        public bool Recurse { get; set; } = false;

        public override void Execute(ScriptState state)
        {
            var target = state.Data.SectionsOfType<S.Object3D>()
                    .Where(i => i.Name == Target)
                    .FirstOrDefault() ?? throw new Exception($"Object to delete not found: {Target}");
            if (!Recurse && target.children.Count != 0)
            {
                throw new Exception($"Object {Target} has children but Recurse=\"false\" ");
            }

            void DoRecurse(S.Object3D curr)
            {
                foreach(var c in curr.children)
                {
                    DoRecurse(c);
                }
                curr.Parent = null;
                state.Data.RemoveSection(curr.SectionId);
            }

            if (Recurse) { DoRecurse(target); }
            else
            {
                target.Parent = null;
                state.Data.RemoveSection(target.SectionId);
            }
        }
    }
}