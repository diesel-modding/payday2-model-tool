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