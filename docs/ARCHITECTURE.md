# Architecture

## The one decision everything follows from

**Nothing that can be tested without AutoCAD is allowed to depend on AutoCAD.**

AutoCAD cannot be installed on a build agent, cannot be scripted in a test, and cannot be had at all
on the machine most of this was written on. A design that puts the section maths, the weight
calculations and the schedule logic inside classes that reference `Autodesk.AutoCAD.*` is a design
where none of it can ever be verified — and weight schedules are exactly the sort of thing that must
be verified.

So the dependency graph points one way, and AutoCAD sits at the far end of it:

```
    Acad  ──────►  UI  ──────►  Services  ──────►  Bom, Excel, Geometry, Data  ──────►  Core
  (AutoCAD)      (WPF)        (net8.0)                  (net8.0)                      (net8.0)
   Windows      Windows                     ── everything below here is testable anywhere ──
```

The payoff is concrete. 233 tests cover the geometry, the units, the database, the weights, the
schedule and the spreadsheet writer, and they run in under a second on any machine. The two projects
that need Windows contain translation and presentation, and almost no logic.

## The seam

`StructureGen.Services.ICadService` is the entire vocabulary the user interface has for talking to
AutoCAD:

```csharp
Result<Vec3>                              PickPoint(string prompt);
Result<(Placement, decimal LengthMm)>     PickSegment(string start, string end);
Result<(ElementDefinition, string Handle)> PickElement(string prompt);
Result<GenerationSummary>                 Generate(GenerationRequest request);
Result<string>                            UpdateElement(string handle, ElementDefinition definition);
Result<IReadOnlyList<BomEntry>>           HarvestElements(HarvestScope scope);
Result<string>                            InsertBomTable(BomDocument bom, BomTablePlacement placement);
```

Not one AutoCAD type appears in it. `Vec3` and `Placement` are StructureGen's own, and the AutoCAD
layer converts at the boundary.

## Element generators

Adding a new kind of structural element means writing one class and registering it. Nothing in the
user interface, the schedule, the exporter or the AutoCAD layer changes.

```csharp
public interface IElementGenerator
{
    string CategoryId { get; }
    IReadOnlyList<string> RequiredDimensions { get; }
    ParameterSchema GetParameterSchema(SectionRecord record);
    ValidationReport Validate(ElementDefinition definition, SectionRecord? record = null);
    Result<ElementGeometry> BuildGeometry(ElementDefinition definition);
    MassProperties ComputeMass(ElementDefinition definition, ElementGeometry? geometry);
}
```

`ElementGeneratorRegistry` resolves a generator by category, and also implements
`ISectionRequirementProvider`, which the database validator consults. That one connection is what
makes the extension genuinely complete: registering a generator immediately extends database
validation to its category, with no change to the validator.

`docs/ADDING-ELEMENT-TYPES.md` walks through a worked example.

## Geometry without a CAD kernel

Generators return `ElementGeometry`: a list of cross-sections and the distances they are extruded
over. A cross-section is contours of vertices, each carrying a **bulge** — the tangent of a quarter
of the following arc's included angle.

Bulge is exactly how an AutoCAD lightweight polyline stores an arc, so the conversion in the AutoCAD
layer is a copy rather than a re-derivation. A fillet computed and unit tested off-line is the same
fillet that reaches the drawing.

It also makes areas exact. Arc segments contribute their true circular-segment area rather than a
chord approximation, so the computed area of an IPE 300 comes out at 5381.2 mm² against a published
5380 — which is a far stronger check on the generated shape than any screenshot.

One general corner-rounding routine serves every generator. Given three points and a radius it
returns the tangent points and the signed bulge, working out convex from concave by the turn
direction and reducing a radius that will not fit rather than producing self-intersecting geometry.
Root fillets, toe radii and hollow-section corners all go through it.

## Parametric elements

The AutoCAD Managed API cannot create custom entities, and cannot author dynamic blocks. Both need
ObjectARX in C++. What it *can* do is what StructureGen does:

**Geometry is shared.** Each distinct shape becomes one `BlockTableRecord`; each occurrence is a
lightweight `BlockReference`. A thousand identical bolts cost one definition and a thousand
references. The definition name is derived from everything affecting the shape — category,
designation, dimensions, length, detail level, representation — and nothing that does not — position,
rotation, quantity, layer. Moving a beam never creates a second definition; editing one in custom
mode always does, so its neighbours are not silently reshaped.

The name uses a hand-written FNV-1a hash rather than `string.GetHashCode`, because the framework's
hash is randomised per process: using it would produce different block names every time AutoCAD
restarted, duplicating every definition in the drawing.

**Parameters travel with the object.** Two mechanisms, each for what it is good at:

- An **extension dictionary** holding an `Xrecord` carries the full parameter payload as JSON. No
  practical size limit, survives copy, block insertion and WBLOCK, and is the sanctioned place for
  an application's structured data. Written on the reference (this occurrence) and on the definition
  (the profile it was built from).
- **Extended entity data** carries nothing but a marker naming the application. It exists solely
  because a `SelectionFilter` can match on it, which is what lets the schedule find every generated
  object in one pass instead of opening every entity in model space.

The payload records the format version that wrote it. Reading is forward-tolerant: unknown fields
are ignored, missing optional fields fall back to defaults, and only a newer *major* format is
refused. A drawing from a later version opens here degraded rather than broken.

**Editing preserves identity.** `SGEDIT` reads the parameters, the palette edits them, and the update
path chooses between three outcomes: retarget the reference if the new shape already exists in the
drawing; rebuild the definition in place if this reference is its only user; otherwise create a new
definition and move only this reference. The element identity is written back unchanged in every
case.

## The schedule comes from the drawing

The palette's list is a staging area. Once generated, the drawing is the authority, and `SGBOM`
harvests from the objects that actually exist. A member erased by hand disappears from the next
schedule; one copied with `COPY` appears on it, carrying its parameters.

Each reference is written with a quantity of one, so counting objects is counting pieces. Grouping
then collapses identical members back onto one line.

**What counts as identical** is the interesting part: category, standard, designation, source record,
length, material, grade, custom flag, and a signature of the actual dimensions. That last one matters
after an edit — two members may both still say "IPE 300" while one has been altered — and the custom
flag ensures a non-conforming member is never hidden inside the count of a conforming one.

**A weight that cannot be established is never zero.** It is reported as unknown, shown as a dash,
excluded from the totals, and counted in a caveat that travels with the schedule everywhere it is
shown, drawn or exported. Every line records how its weight was reached and writes the arithmetic
out in full.

## Spreadsheets without a library

`StructureGen.Excel` writes Office Open XML directly over `System.IO.Compression`.

The reason is the process it runs in. The plugin is loaded into AutoCAD alongside whatever other
add-ins the user has, and every third-party assembly it brings is a chance of colliding with a
different version of the same assembly loaded by something else — a failure mode that shows up as
someone else's plugin breaking, days later, on one machine. A schedule needs a fraction of the
format, so writing it directly costs less than the risk.

Output is a genuine package: content types, relationships, styles, shared strings, worksheets and
document properties, with header formatting, freeze panes, auto-filter, number formats, column
widths and totals. Tests unzip it, parse every part and assert the structure.

The same layer reads `.xlsx`, which is how a user's own section spreadsheet gets imported.

## Errors

Expected failures — a malformed database row, a negative length, a missing density — are values, not
exceptions: `Result<T>` and `ValidationReport` carry a stable code, a message written for a drafter,
and the context that identifies the offending record.

Exceptions are reserved for defects and platform failures. Every command method catches everything,
logs the technical detail to a rolling file under the user's local application data, and shows the
user a sentence they can act on. No stack trace ever reaches the command line.

## Dependency injection without a container

Constructor injection throughout; no container. `StructureGenContext` wires the graph in a fixed
order — the same thing a container would do, minus the reflection and minus shipping
`Microsoft.Extensions.DependencyInjection` into a process that already loads its own copy.

The only process-wide state is the single context the AutoCAD extension application creates, because
command methods are static entry points and there is no other way to reach an instance from them.
Everything below it takes its dependencies through its constructor.
