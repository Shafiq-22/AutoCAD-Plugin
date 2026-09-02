# AutoCAD version compatibility

## The short version

StructureGen ships one bundle containing three builds. AutoCAD picks the right one automatically.

| AutoCAD release | Series | .NET runtime | Built against | Bundle folder |
|---|---|---|---|---|
| AutoCAD 2025 | R25.0 | .NET 8 | `AutoCAD.NET` 25.0.1 | `Contents/2025` |
| AutoCAD 2026 | R25.1 | .NET 8, later updates .NET 10 | `AutoCAD.NET` 25.1.0 | `Contents/2026` |
| AutoCAD 2027 | R26.0 | .NET 10 | `AutoCAD.NET` 26.0.0 | `Contents/2027` |

Verified from the package metadata Autodesk publishes on nuget.org; the descriptions on those
packages name the release each one targets.

## Why there is not one binary

There cannot be, and any project claiming otherwise is either shipping one build and hoping, or
loading assemblies by reflection.

Two things change between AutoCAD releases:

1. **The API assemblies.** `AcMgd`, `AcDbMgd` and `AcCoreMgd` are versioned per release. A plugin
   compiled against one release's assemblies is bound to those exact versions.
2. **The .NET runtime the host provides.** AutoCAD 2025 and 2026 launched on .NET 8; AutoCAD 2027
   moved to .NET 10.

A managed assembly can only load into a runtime at least as new as the one it was compiled for.
That gives the rule this project follows:

> **Compile each release profile against the lowest .NET version that occurs anywhere in that
> release's series.**

The 2026 series is the interesting case. It began on .NET 8 and a later update moved to .NET 10.
Building `Contents/2026` against .NET 8 means the same binary loads on both, because a .NET 8
assembly runs on a .NET 10 host through normal assembly roll-forward. Building it against .NET 10
would have broken every 2026 installation that had not taken that update.

This is standard .NET assembly loading behaviour rather than anything AutoCAD-specific, but it has
not been tested here against every 2026 update — see *What has and has not been verified* below.

## How AutoCAD chooses

`PackageContents.xml` declares one `<Components>` block per release, each with a
`<RuntimeRequirements>` naming the series it applies to:

```xml
<RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R25.1" SeriesMax="R25.1" />
```

AutoCAD reads the manifest at start-up, compares its own series against each block, and loads only
the `ModuleName` from the block that matches. A release with no matching block loads nothing at all,
so installing the bundle on an unsupported AutoCAD is harmless rather than an error.

## The verticals

`Platform="AutoCAD*"` matches AutoCAD and every vertical built on it, so **AutoCAD Mechanical** and
**AutoCAD Plant 3D** load the same binary as plain AutoCAD.

This works because StructureGen uses only the AutoCAD Managed API that every vertical shares:
`Autodesk.AutoCAD.DatabaseServices`, `.Geometry`, `.EditorInput`, `.ApplicationServices`,
`.Runtime` and `.Windows`. It references no product-specific assembly and calls no product-specific
API.

The honest limits of that claim:

- **What is claimed.** The plugin loads and its commands work, because the API it uses is common to
  all of them. Geometry, metadata, the schedule and the export behave identically.
- **What is not claimed.** StructureGen does not create AutoCAD Mechanical structural members, does
  not populate the Mechanical BOM, and does not create Plant 3D piping components in the Plant 3D
  project database. Those are product-specific APIs in product-specific assemblies. A pipe generated
  by StructureGen is a solid with StructureGen's own metadata on it, not a Plant 3D pipe.

Integrating with a vertical's own object model would mean referencing that vertical's assemblies and
producing a separate build per product. That is a coherent future direction, not something this
project quietly half-does.

## Adding a future release

When AutoCAD 2028 arrives:

1. Add a block to `build/AcadVersions.props` giving its target framework, its `AutoCAD.NET` package
   version, and its series.
2. Add a `<Components>` block to `build/PackageContents.xml` with the matching `SeriesMin` and
   `SeriesMax`.
3. Add the release to the matrix in `.github/workflows/ci.yml` and to the default `-Versions` list
   in `build/build.ps1`.

No source change should be needed unless Autodesk has changed an API the plugin uses. If they have,
the Windows CI job for that release fails and names the member.

## What has and has not been verified

Being straight about this matters more than sounding confident.

**Verified in this repository, by running it:**

- Every platform-neutral layer compiles and its 233 tests pass.
- The shipped section databases load, validate and generate geometry, and the generated areas agree
  with published section tables.
- The exported workbook is a well-formed Office Open XML package that third-party readers open.

**Verified by continuous integration, when it runs on Windows:**

- The AutoCAD and WPF layers compile against all three release profiles.
- No Autodesk assembly ends up in the build output.

**Not verified anywhere, and requiring a licensed seat:**

- That the bundle loads in a running AutoCAD.
- That the commands, the palette, the generated geometry, the native table and the round trip
  through `SGEDIT` behave as intended in the application.

There is no way to test those without AutoCAD installed, and no continuous integration service
provides one. `docs/INSTALLATION.md` ends with an acceptance checklist covering exactly these
points; please work through it on your AutoCAD Mechanical 2026 and open an issue for anything that
does not behave as described.

**Not claimed at all:** that any of this will work on an AutoCAD release that does not yet exist.
