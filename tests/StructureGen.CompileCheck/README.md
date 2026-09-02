# Compile check for the Windows-only layers

`StructureGen.Acad` and `StructureGen.UI` cannot be built without Windows: WPF compiles nowhere else,
and the AutoCAD reference assemblies are Windows-targeted. That leaves roughly fifteen hundred lines
with no local verification at all for anyone not developing on Windows.

This project closes most of that gap. It compiles those two projects' source against hand-written
stand-ins for the AutoCAD and WPF types they use.

## What it proves

Every call the AutoCAD and user-interface layers make **into StructureGen's own code** is correct:
right names, right signatures, right types, right nullability.

On its first run it found two errors that would have failed the Windows build:

- An unqualified `catch (Exception)` in a file importing `Autodesk.AutoCAD.Runtime`, which declares
  its own `Exception` type — ambiguous, and a compile error.
- A missing `using` for the namespace holding the logging extension methods.

## What it does not prove

That the AutoCAD API actually looks like the stubs. They were written from the same understanding as
the code they check, so a mistaken assumption about Autodesk's own surface appears in both and
cancels out. Only compiling against the real assemblies catches that, which is what the Windows
continuous integration job does.

## If it fails after you add an AutoCAD call

Add the member to `Stubs/AutodeskApi.cs`. Keep the stub as thin as possible — a signature and a
default return is enough; nothing here ever executes.

Finding yourself adding stubs frequently is worth a pause. This layer is meant to be translation
only; logic belongs in the projects that can be tested properly.
