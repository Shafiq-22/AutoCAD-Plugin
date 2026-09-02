# Limitations, and why they exist

Everything here is a deliberate decision with a reason, not an unfinished feature. Where a
limitation comes from the AutoCAD API rather than from a choice, that is stated plainly.

## 1. Generated elements are blocks with metadata, not custom entities

**The limitation.** A StructureGen element is a `BlockReference` pointing at a `BlockTableRecord`,
carrying its parameters in an extension dictionary. It is not a custom AutoCAD entity with its own
graphics, grips and property palette integration.

**Why.** The AutoCAD Managed .NET API cannot create custom graphical entities. Deriving a class from
`Entity` and overriding `WorldDraw` in .NET is not supported: custom entities require ObjectARX in
C++, which is a different toolchain, a different licence and a per-release binary.

**What is done instead.** The full parameter set travels on the object, so it can be selected,
interrogated with `SGINFO`, edited with `SGEDIT` and rebuilt with `SGUPDATE`. The identity survives
copying, block insertion and WBLOCK. This is the strongest parametric behaviour the supported
managed API offers.

**What you lose.** No live grip editing of the section, and no automatic regeneration when a
parameter changes — you run `SGUPDATE`, or press Update in the palette.

## 2. Dynamic blocks are consumed, not authored

**The limitation.** StructureGen does not create dynamic blocks with parameters and actions.

**Why.** The managed API can *read and set* dynamic block properties on an existing reference
(`BlockReference.DynamicBlockReferencePropertyCollection`), but there is no public API for the block
authoring environment. Parameters and actions can only be created in the Block Editor by hand.

**What is done instead.** Distinct geometry gets a distinct block definition, named deterministically
so that identical geometry always shares one. The effect a dynamic block would give — one definition
serving many instances — is achieved; the interactive grip editing is not.

## 3. Tapered-flange channels are drawn with parallel flanges

**The limitation.** UPN and similar channels are rolled with a tapered inner flange face.
StructureGen draws them with parallel flanges.

**Measured effect.** The drawn cross-sectional area is 2.0% to 2.9% larger than the published area.
This is asserted by a test, which also asserts the direction: the approximation over-estimates and
never under-estimates.

**Effect on weights: none.** The schedule uses the tabulated mass per metre from the database, not
the drawn area. Only the picture is approximate.

**If you need it exact.** Give the record an explicit `outline`, which is extruded exactly as
written. See `docs/DATABASE.md`.

## 4. Threads are represented by diameter, never modelled

**The limitation.** Bolts and nuts carry no helical thread geometry.

**Why.** A drawing with a few thousand bolts has to stay usable. A modelled thread form multiplies
the face count of every fastener for detail that no fabrication drawing uses.

**What is done instead.** At the `Detailed` level, when the database supplies a thread minor
diameter (`d3`) and an unthreaded shank length (`ls`), the shank is split and the threaded portion is
drawn at the minor diameter — the usual detailing convention. Without those values the shank is a
plain cylinder at full diameter and nothing is invented.

**Effect on weights.** A fastener with a published `pieceMass` uses it. One without has its weight
derived from the simplified solid, which is slightly conservative because the thread removes material
that the model retains. The shipped fastener records say so in their notes, and the schedule labels
the figure *Volume x density*.

## 5. Vertical products are supported through the common API only

See `docs/COMPATIBILITY.md`. In short: the plugin loads and works in AutoCAD Mechanical and Plant 3D,
but it does not create Mechanical structural members or Plant 3D piping components in those
products' own object models.

## 6. Profiles are extruded prisms

**The limitation.** Every element is one or more constant cross-sections extruded along a straight
axis. There are no curved members, no tapered members, no cope cuts, no bolt holes and no welds.

**Why.** Those are connection design and fabrication detailing, which is a much larger problem than
element generation, and a half-implementation would be worse than none.

**What is done instead.** The generated solids are ordinary AutoCAD solids. Cope them, cut them and
drill them with AutoCAD's own tools.

**What that costs.** An element edited with AutoCAD's solid editing tools keeps its metadata and
stays on the schedule, but running `SGUPDATE` on it rebuilds the profile and discards the edits.
Do the StructureGen edits first.

## 7. The two-dimensional representation is a section outline

`Profile2D` draws the true cross-section as closed polylines. It is not a plan or elevation view of
the member, and there is no automatic dimensioning, hatching or annotation.

## 8. The schedule table is a snapshot

`SGBOM` draws a native AutoCAD table reflecting the drawing at that moment. It does not update
itself when the drawing changes. Re-run the command for a fresh one.

## 9. Weights are only as good as the data

StructureGen never invents a number. If a profile has no tabulated mass and its material has no
density, the weight is reported as unavailable, shown as a dash, excluded from the totals, and the
number of excluded lines is stated on the schedule and in the exported workbook.

The dataset that ships is **demonstration data**, marked unverified in every record. Replace it with
your own. See `docs/DATABASE.md`.

## 10. StructureGen performs no engineering

It draws what you ask for and totals its weight. It does not check capacity, stability, deflection,
connections, fire, corrosion or fabrication feasibility, and matching a standard profile says
nothing about whether that profile is adequate. See `docs/DISCLAIMER.md`.
