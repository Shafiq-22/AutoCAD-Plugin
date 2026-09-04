# Installing StructureGen

No source editing is required at any point.

## What you need

- Windows 64-bit
- AutoCAD 2025, 2026 or 2027, or a vertical built on one of them (AutoCAD Mechanical, AutoCAD Plant 3D)
- Nothing else. Microsoft Excel is **not** required — StructureGen writes `.xlsx` files itself.

## Getting the bundle

**Either** download `StructureGen.bundle` from the repository's Actions artifacts or Releases page,

**or** build it yourself (see `docs/BUILDING.md`):

```powershell
./build/build.ps1 -Zip
```

Either way you end up with a folder named `StructureGen.bundle` laid out like this:

```
StructureGen.bundle/
  PackageContents.xml
  Contents/
    2025/    StructureGen.Acad.dll and friends, built for AutoCAD 2025
    2026/    ... for AutoCAD 2026
    2027/    ... for AutoCAD 2027
    Data/    structural-sections.json and the rest of the data
    Docs/    this documentation
```

The `.bundle` suffix on the folder name matters. AutoCAD looks for it.

## Installing

Copy the whole `StructureGen.bundle` folder into **one** of these:

| Location | Path | Needs |
|---|---|---|
| Current user | `%APPDATA%\Autodesk\ApplicationPlugins\` | nothing |
| All users | `%PROGRAMDATA%\Autodesk\ApplicationPlugins\` | administrator rights |

Per-user is the easier choice. Paste `%APPDATA%\Autodesk\ApplicationPlugins` into Explorer's address
bar; create the folder if it is not there.

You should end up with, for example:

```
C:\Users\<you>\AppData\Roaming\Autodesk\ApplicationPlugins\StructureGen.bundle\PackageContents.xml
```

Then **restart AutoCAD** and type `STRUCTUREGEN`.

That is the whole installation. There is no installer to run, no registry key to add, and no
`NETLOAD` to perform.

## Why there is nothing to do about trusted paths

Both `ApplicationPlugins` locations are trusted by AutoCAD by default, so `SECURELOAD` does not
block a bundle installed there.

You only need to think about trust if you deliberately load the DLL from somewhere else with
`NETLOAD` — during development, say. In that case add the folder to the trusted list:

```
TRUSTEDPATHS
```

and append your build output folder, or set `SECURELOAD` to `0` temporarily. Prefer adding the
trusted path; turning security off is a blunt instrument.

## Uninstalling

Close AutoCAD and delete the `StructureGen.bundle` folder. Nothing else is written anywhere except:

- `%APPDATA%\StructureGen\settings.json` — your settings
- `%LOCALAPPDATA%\StructureGen\logs\` — log files, kept for 14 days

Delete those too if you want no trace left.

## First run

1. Type `STRUCTUREGEN`. The palette opens and docks like any other AutoCAD palette.
2. The status line at the bottom names the loaded database. Out of the box that is the
   European dataset, and it will tell you that all 1,038 of its records are unverified. **That is
   correct and deliberate** — see the note below.
3. Go to the **Settings** tab, point *Structural database* at your own data file, and press
   **Reload database**. The validation report appears immediately beneath.

### About the shipped data

Two datasets ship: `structural-sections.json`, 1,038 European and UK records in millimetres,
loaded by default; and `aisc-sections.json`, 1,660 American shapes in inches and pounds per foot. To use the
American one, point *Structural database* at it on the Settings tab and press **Reload database** —
the file declares its own units and StructureGen converts on load.

Almost all of both is compiled from published tables rather than typed, and each record names the
source, sheet or file it came from. **Every record is marked unverified.** StructureGen flags
them in the palette, on the schedule and in the exported workbook until you either verify them or
load your own data. See `docs/DATABASE.md` for what came from where, and for the approximations the
shipped data is known to contain.

## When it does not load

**The command is not recognised.**

- Is the folder named exactly `StructureGen.bundle`?
- Is `PackageContents.xml` directly inside it, not one level deeper? A common slip is copying a
  folder that *contains* the bundle.
- Did you restart AutoCAD? The manifest is read at start-up.
- Is your AutoCAD release supported? Type `ACADVER`. You need R25.0, R25.1 or R26.0.

**The palette opens but there are no categories.**

The database did not load. Look at the **Settings** tab: the validation report says why, naming the
record. `docs/DATABASE.md` explains every message.

**Something else went wrong.**

Type `SGABOUT`. It prints the plugin version, the host version, and the exact folders being used,
including where the log file is. The log has the technical detail; the messages on screen are
deliberately free of stack traces.

Log files live in `%LOCALAPPDATA%\StructureGen\logs\structuregen-<date>.log`.

## Acceptance checklist

**Please work through this on your AutoCAD.** The layers that do not need AutoCAD are covered by 233
automated tests, and continuous integration proves the AutoCAD layer compiles against all three
releases — but nothing can prove the plugin behaves correctly inside a running AutoCAD without a
licensed seat, and no continuous integration service provides one. This checklist is exactly what
has not been verified.

| # | Step | Expected |
|---|---|---|
| 1 | Install the bundle and restart AutoCAD | No error at start-up |
| 2 | Type `STRUCTUREGEN` | The palette opens and docks |
| 3 | Type `SGABOUT` | Version, host and folder paths are printed |
| 4 | Pick Beams, EN 10365, IPE 300 | Dimensions fill in and are greyed out |
| 5 | Try to edit the web thickness | The field is read-only |
| 6 | Tick custom mode, edit it | Now editable, and a warning appears |
| 7 | Untick, press Reset | The published values return |
| 8 | Set length 6000, quantity 4, Add to list | Row appears, running total shows 1012.8 kg |
| 9 | Press Generate all | Four beams appear on layer `SG-i-section`, and the view moves to show them |
| 9a | Type `SGSHOW` | It reports 4 elements and their extents, and frames them |
| 10 | Press Pick start/end, pick two points | The length is measured from the drawing |
| 11 | Type `SGINFO`, select a beam | Profile, dimensions and weight calculation are printed |
| 12 | Type `SGEDIT`, select a beam, change length, press Update in drawing | Geometry rebuilds, identity preserved |
| 13 | Copy a beam with AutoCAD's `COPY` | The copy keeps its metadata |
| 14 | Type `SGBOM`, choose Model | A native table appears; the copy is counted |
| 15 | Type `SGBOM`, choose Excel | A `.xlsx` file is written |
| 16 | Open it in Excel | Four sheets, frozen header, working auto-filter, no repair prompt |
| 17 | Add a plate with no material density | Its weight shows as a dash and the totals say one line is excluded |
| 18 | Add 500 M16 bolts and generate | Fast; `BLOCKS` shows one bolt definition, not 500 |
| 19 | Edit a data file and press Reload database | New data appears without restarting AutoCAD |
| 20 | Put a deliberate error in the data file and reload | It is refused, the record is named, and the old data stays loaded |

Please open an issue for anything that does not behave as described, quoting the log file.
