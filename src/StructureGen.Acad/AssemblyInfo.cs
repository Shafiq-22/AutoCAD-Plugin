using Autodesk.AutoCAD.Runtime;
using StructureGen.Acad;

// AutoCAD looks for these attributes when the assembly is loaded. Declaring them explicitly is
// faster and more predictable than leaving AutoCAD to scan every type in the assembly, and it makes
// the plugin's entry points obvious to anyone reading the source.
[assembly: ExtensionApplication(typeof(StructureGenApplication))]
[assembly: CommandClass(typeof(StructureGenCommands))]
