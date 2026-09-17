# Diesel Model Tool - Give Me More Diesel Editon

This is a successor of IAmNotASpy and PoueT's model tool.

* You need .net 10 to run this
* Supports Export for Payday the Heist, Payday 2 Legacy, Payday 2, Raid WW II Legacy, Raid WW II U20

# glTF export/import

Both the importer and exporter treat the material name `Material: Default Material` specially: it becomes no
material on export, and a lack of material on import is replaced with that. Otherwise, the exporter creates
a dummy material for each material name in the Diesel model. The importer doesn't care about the precise
definition of materials, only their names.

Because GLTF dictates a 1m scale, and Payday 2 uses a 1cm scale, the exporter accounts for this (this does have
the downside that if you're importing into Blender bones and empties are drawn much too big).

# Feature Matrix

| Data             | GLTF In  | GLTF Out |
|------------------|----------|----------|
| Triangles        | ✓        | ✓        |
| UV channels      | ✓        | ✓        |
| Vertex colors    | ✓        | ✓        |
| Vertex weights   | ✓        | ✓        |
| Material slots   | ✓        | ✓        |
| Object hierarchy | ✓        | ✓        |
| Bones            | ✓        | ✓        |
| Skinning         | ✓        | ✓        |
| Collision        | ✓        | ✓        |
| Object Parents   | ✓        | ✓        |

# Hashlists
Diesel very rarely stores actual names of things if it can store a hash of the name instead, so a list of
names is needed. It can be downloaded in the hashlist tab.

# Licence:

This program is Free Software under the terms of the GNU General Public Licence, version 3. A copy of
this licence is distributed with the program's source files.
