# StructureGen

**Parametric structural element generation, bills of materials and Excel export for AutoCAD 2025+.**

Pick a standard profile, set its length and quantity, place it, and StructureGen draws it as
parametric geometry that stays identifiable and editable afterwards. When the frame is drawn, it
reads the drawing back and produces a weight schedule you can put in the drawing or export to a
spreadsheet.

Built for AutoCAD 2025, 2026 and 2027, and for the verticals built on them — AutoCAD Mechanical and
AutoCAD Plant 3D — through the API they all share.

> **StructureGen is a drafting and documentation aid. It performs no structural engineering design,
> analysis or verification.** Drawing a standard profile says nothing about whether that profile is
> adequate. See [docs/DISCLAIMER.md](docs/DISCLAIMER.md).

---

## Contents

- [What it does](#what-it-does)
- [Quick start](#quick-start)
- [The section database is yours](#the-section-database-is-yours)
- [Supported AutoCAD releases](#supported-autocad-releases)
- [Commands](#commands)
- [How it works](#how-it-works)
- [Honest limitations](#honest-limitations)
- [Building](#building)
- [Documentation](#documentation)
- [Licence and dependencies](#licence-and-dependencies)

---

## What it does

**Generates elements from a standard.** I and H sections, channels, angles, T sections, pipes,
rectangular and square hollow sections, plates, flat and round bars, bolts, nuts, washers, and
anything else your database describes with an outline. Choose a category, a standard and a profile;
the dimensions fill in automatically.

**Keeps the standard standard.** Dimensions fixed by the standard are read-only. Changing the length
of a beam never touches its cross-section. If you genuinely need a non-standard size there is an
explicit custom mode, and everything made in it is marked non-standard on the schedule, in the
workbook and in the drawing's own metadata.

**Builds a list before it draws anything.** Assemble a whole frame's worth of members, watch the
running weight, fix the one that is wrong, then generate the lot in a single operation.

**Stays editable.** Every generated object carries its full parameter set. Select one months later
and `SGINFO` tells you what it is; `SGEDIT` loads it back into the palette, and updating it rebuilds
the geometry while keeping the element's identity.

**Schedules from the drawing, not from a list.** The bill of materials reads the objects that
actually exist. Erase a beam by hand and it leaves the schedule. Copy one with `COPY` and it appears
on it, carrying its parameters. Identical members collapse onto one line with a quantity; members
that differ in any way that matters do not.

**Never invents a weight.** Where a weight cannot be established it is reported as unknown, shown as
a dash, excluded from the totals, and the number of excluded lines is stated. Every figure records
how it was reached and writes the arithmetic out in full.

**Exports a real `.xlsx`.** Four sheets, header formatting, freeze panes, auto-filter, number
formats, totals, and a sheet recording exactly which data revision the numbers came from. **Microsoft
Excel is not required** — StructureGen writes the format itself.

**Draws thousands of elements without bogging down.** Identical geometry shares one block definition,
so a thousand identical bolts cost one definition and a thousand lightweight references.

---

## Quick start

1. Download `StructureGen.bundle`, or build it with `./build/build.ps1`.
2. Copy the folder to `%APPDATA%\Autodesk\ApplicationPlugins\`.
3. Restart AutoCAD.
4. Type `STRUCTUREGEN`.

No installer, no `NETLOAD`, no registry keys, no source editing.
Full detail in [docs/INSTALLATION.md](docs/INSTALLATION.md).

Then:

| | |
|---|---|
| **1 Create** | Category → Standard → Profile. Dimensions fill in. Set length and quantity. Pick a point, or pick both ends and let the length be measured from the drawing. **Add to list.** |
| **2 List** | Review, duplicate, reorder. Watch the running total. **Generate all.** |
| **3 Bill of materials** | **Build from drawing**, then draw it as a table or **Export to Excel**. |
| **4 Settings** | Point it at your own section data and press **Reload database**. |

Walkthrough in [docs/USAGE.md](docs/USAGE.md).

---

## The section database is yours

**No section data is compiled into StructureGen.** Everything it can draw comes from a JSON file you
can replace, and reloading it is a button press rather than a rebuild.

```json
{
  "id": "en10365-ipe-300",
  "standardId": "en-10365",
  "category": "i-section",
  "designation": "IPE 300",
  "unitWeight": 42.2,
  "dimensions": { "h": 300, "b": 150, "tw": 7.1, "tf": 10.7, "r": 15 },
  "verified": true,
  "provenance": "EN 10365:2017 Table 3, controlled copy, checked 2026-04-01."
}
```

The schema is designed around a few principles worth knowing about:

- **A file declares its own units, once.** An imperial table in inches and pounds per foot is
  dropped in unchanged and converted on load.
- **Every record says whether it has been verified**, and unverified records are flagged in the
  palette, on the schedule and in the exported workbook until you say otherwise.
- **A record can carry its own outline**, so a fabricated or proprietary section the code has never
  heard of works with no code change at all.
- **Density is optional, and leaving it out means something.** Without a tabulated weight and without
  a density, StructureGen reports the weight as unknown rather than guessing.

A command line tool converts your existing spreadsheets through an editable column-mapping profile:

```bash
dotnet run --project src/StructureGen.Tools.DataImport -- \
    import --in catalogue.xlsx --map my-mapping.json --out my-sections.json
```

It is forgiving about how real tables are written — quoted fields with commas, thousands separators,
comma decimals, stray unit suffixes, blank markers — and unforgiving about anything it cannot be sure
of, telling you which row and column rather than guessing.

Everything about the format, the vocabulary and the import: [docs/DATABASE.md](docs/DATABASE.md).

### About the data that ships

The included dataset — 120 records across 14 categories — exists so every feature works the moment
you install the plugin. Its figures are widely published nominal values transcribed for that purpose,
and **every record is marked unverified**. Verify them against your own authoritative source, or
replace the file, before using any output for procurement, fabrication or construction.

---

## Supported AutoCAD releases

One bundle contains three builds. AutoCAD picks the right one.

| Release | Series | .NET | Built against |
|---|---|---|---|
| AutoCAD 2025 | R25.0 | 8 | `AutoCAD.NET` 25.0.1 |
| AutoCAD 2026 | R25.1 | 8 | `AutoCAD.NET` 25.1.0 |
| AutoCAD 2027 | R26.0 | 10 | `AutoCAD.NET` 26.0.0 |

**There is no single binary that works everywhere, and this project does not pretend otherwise.** The
API assemblies are versioned per release, and the .NET runtime AutoCAD hosts changed at 2027. Each
profile is compiled against the *lowest* runtime present in its release series, so one binary covers
every update of that series — which matters for 2026, a series that began on .NET 8 and later moved
to .NET 10.

**AutoCAD Mechanical and AutoCAD Plant 3D** load the same binaries, because StructureGen uses only
the AutoCAD Managed API they all share. It does **not** create Mechanical structural members or Plant
3D piping components in those products' own object models — that needs product-specific APIs, and is
not claimed here.

Adding a future release is normally three lines of configuration and no source change.
Detail, and an honest account of what has and has not been verified:
[docs/COMPATIBILITY.md](docs/COMPATIBILITY.md).

---

## Commands

| Command | Does |
|---|---|
| `STRUCTUREGEN` | Opens the palette |
| `SGEDIT` | Loads a drawn element back into the palette |
| `SGINFO` | Prints everything known about a selected element |
| `SGSHOW` | Brings every StructureGen element in the drawing into view |
| `SGBOM` | Builds the schedule; offers a table or an export |
| `SGBOMEXPORT` | Builds the schedule and exports it straight to a workbook |
| `SGUPDATE` | Rebuilds selected elements from their stored parameters |
| `SGRELOADDB` | Reloads the structural database |
| `SGSETTINGS` | Opens the settings |
| `SGABOUT` | Version, host, folders and the disclaimer |

---

## How it works

**The design decision everything follows from:** nothing that can be tested without AutoCAD is
allowed to depend on AutoCAD.

```
    Acad  ──────►  UI  ──────►  Services  ──────►  Bom, Excel, Geometry, Data  ──────►  Core
  (AutoCAD)      (WPF)                                      ── testable anywhere ──
```

The user interface reaches AutoCAD only through a single interface, `ICadService`, in which no
Autodesk type appears. The result is that **233 tests** cover the geometry, the units, the database,
the weight calculations, the schedule and the spreadsheet writer, and run in under a second on any
machine — while the two projects that need Windows contain translation and presentation and almost
no logic. Even those two are not unchecked: a compile-check project puts their source through the
compiler against stand-in AutoCAD and WPF types, verifying every call they make into StructureGen's
own code.

That testing is not decorative. It caught three real defects during development, all fixed rather
than shipped: a shared-string reader that would have made every spreadsheet import come back empty,
a custom outline that lived only in the database so elements stopped regenerating when the data file
changed, and a comment claiming an accuracy that decimal arithmetic cannot actually provide. The
compile check found two more, both of which would have failed the Windows build.

Some things worth knowing:

- **Areas are exact, not approximated.** Cross-sections are stored as contours with AutoCAD's own
  bulge encoding, so arcs contribute their true circular-segment area. The computed area of an
  IPE 300 comes out at 5381.2 mm² against a published 5380 — which is a far better check on the
  generated shape than looking at a picture of it.
- **Block definitions are shared deterministically.** The name is derived from everything that
  affects the shape and nothing that does not, using a stable hash rather than the framework's
  per-process one — which would otherwise duplicate every definition each time AutoCAD restarted.
- **Parameters travel on the object.** An extension dictionary holds the full payload; extended
  entity data holds only a marker, which is what lets the schedule find every generated object in a
  single pass rather than opening every entity in model space.
- **The plugin has no third-party runtime dependencies at all.** It loads into AutoCAD's process
  alongside whatever else you have installed, and every assembly it brings is a chance of colliding
  with a different version loaded by something else. So the spreadsheet writer is written directly
  over `System.IO.Compression`, and dependency injection is constructor injection through a
  hand-written composition root rather than a container.

More: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## Honest limitations

Every one of these is a decision with a reason, documented in full with its measured effect in
[docs/LIMITATIONS.md](docs/LIMITATIONS.md).

- **Elements are blocks with metadata, not custom entities.** The AutoCAD Managed .NET API cannot
  create custom graphical entities — that needs ObjectARX in C++. Nor can it author dynamic block
  parameters and actions. What is done instead gives full parameter persistence and rebuild-on-demand
  editing, but not live grip editing of the section.
- **Tapered-flange channels are drawn with parallel flanges.** The drawn area is 2.0–2.9% high — a
  bound asserted by a test. Scheduled weights are unaffected, since they come from the tabulated
  mass. Give the record an explicit outline if you need it exact.
- **Threads are represented by a change of diameter, never modelled.** A drawing with a few thousand
  bolts has to stay usable.
- **Elements are extruded prisms.** No copes, holes, welds or curved members; use AutoCAD's own solid
  editing for those.
- **Nothing has been verified inside a running AutoCAD.** The neutral layers have 233 passing tests
  and CI proves the AutoCAD layer compiles against all three releases — but no CI service has an
  AutoCAD licence, so the in-application behaviour is unverified. There is a 20-step acceptance
  checklist at the end of [docs/INSTALLATION.md](docs/INSTALLATION.md); please work through it and
  open an issue for anything that misbehaves.

---

## Building

Without AutoCAD, on any platform:

```bash
./build/build.sh
```

Builds and tests everything that does not need Windows, and validates the shipped datasets.

The installable bundle, on Windows:

```powershell
./build/build.ps1 -Zip
```

You do **not** need AutoCAD installed to build. The Autodesk assemblies restore from nuget.org and
are compile-only: `ExcludeAssets="runtime"` keeps them out of the output, which is both a technical
requirement (a second copy stops the plugin loading) and a licensing one. Both the build script and
CI assert it afterwards.

Full detail: [docs/BUILDING.md](docs/BUILDING.md).

---

## Documentation

| | |
|---|---|
| [USAGE.md](docs/USAGE.md) | Using the palette, generating, editing, scheduling, exporting |
| [INSTALLATION.md](docs/INSTALLATION.md) | Installing, troubleshooting, and the acceptance checklist |
| [DATABASE.md](docs/DATABASE.md) | The schema, the dimension vocabulary, custom outlines, importing your data |
| [ADDING-ELEMENT-TYPES.md](docs/ADDING-ELEMENT-TYPES.md) | A worked example, from database-only to a full generator |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | How it is put together and why |
| [COMPATIBILITY.md](docs/COMPATIBILITY.md) | Releases, verticals, and what is and is not verified |
| [LIMITATIONS.md](docs/LIMITATIONS.md) | Every approximation, with its reason and its measured effect |
| [BUILDING.md](docs/BUILDING.md) | Building, developing, repository layout |
| [DISCLAIMER.md](docs/DISCLAIMER.md) | The engineering disclaimer, in full |

---

## Licence and dependencies

StructureGen is **MIT** licensed. See [LICENSE](LICENSE).

**Runtime dependencies in the plugin: none.**

Build and test time only:

| Package | Licence | Why |
|---|---|---|
| `AutoCAD.NET`, `.Core`, `.Model` | Autodesk, compile-only | The AutoCAD Managed API |
| `xunit`, `xunit.runner.visualstudio` | Apache-2.0 | Tests |
| `Microsoft.NET.Test.Sdk` | MIT | Test host |

No Autodesk binary is committed to this repository or produced by any build of it.

---

## Contributing

Issues and pull requests welcome. Please run `./build/build.sh` first — all 233 tests should pass.

If you are adding an element type, [docs/ADDING-ELEMENT-TYPES.md](docs/ADDING-ELEMENT-TYPES.md) walks
through it, and the shortest route often needs no code at all.
