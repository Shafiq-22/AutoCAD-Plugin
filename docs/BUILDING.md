# Building StructureGen

## The two halves

The repository splits along what each platform can build, which is also how the architecture is
organised.

| Half | Projects | Builds on |
|---|---|---|
| Platform-neutral | Core, Data, Geometry, Bom, Excel, Services, Tools.DataImport, Tests | Windows, Linux, macOS |
| AutoCAD-hosted | UI (WPF), Acad | Windows only |

The AutoCAD half needs Windows twice over: WPF cannot be compiled anywhere else, and the AutoCAD
reference assemblies are Windows-targeted. The neutral half is where all the logic worth testing
lives, which is why the whole 233-test suite runs anywhere.

`StructureGen.Portable.slnf` is a solution filter naming just the neutral projects. Open that if you
do not have Windows, or do not have AutoCAD.

### The compile check

The Windows-only projects are not left entirely unverified elsewhere.
`tests/StructureGen.CompileCheck` compiles their source against hand-written stand-ins for the
AutoCAD and WPF types they use, which verifies every call they make into StructureGen's own code.
It is in the portable filter, so `./build/build.sh` runs it.

It caught two errors on its first run that would have failed the Windows build. It cannot catch a
mistaken assumption about Autodesk's own API — only the real compile does that — and
`tests/StructureGen.CompileCheck/README.md` is explicit about the distinction.

If you add a call to an AutoCAD API the stubs do not cover, add the member to
`Stubs/AutodeskApi.cs`. A signature and a default return is enough; nothing there ever runs.

## Prerequisites

- **.NET 8 SDK** for everything, plus **.NET 10 SDK** if you are building the AutoCAD 2027 profile
- **Windows** for the AutoCAD half
- Internet access on first build, to restore packages from nuget.org

You do **not** need AutoCAD installed to build. The Autodesk assemblies come from the `AutoCAD.NET`
packages Autodesk publishes on nuget.org, and are used to compile against only.

## Building and testing without AutoCAD

```bash
git clone https://github.com/Shafiq-22/AutoCAD-Plugin.git
cd AutoCAD-Plugin

./build/build.sh
```

That restores, builds, runs all 233 tests, and validates the shipped section databases. It is what
continuous integration runs on Linux.

By hand:

```bash
dotnet restore StructureGen.Portable.slnf
dotnet build   StructureGen.Portable.slnf -c Release
dotnet test    tests/StructureGen.Tests -c Release
```

## Building the installable bundle

On Windows:

```powershell
./build/build.ps1
```

This runs the tests, builds all three AutoCAD profiles, assembles
`artifacts/StructureGen.bundle`, and checks that no Autodesk assembly ended up in the output.

Useful switches:

```powershell
./build/build.ps1 -Versions 2026            # just the release you run
./build/build.ps1 -SkipTests                # skip the tests
./build/build.ps1 -Zip                      # also produce a .zip for distribution
./build/build.ps1 -Configuration Debug      # a debug build
```

To install what you built, see `docs/INSTALLATION.md`.

## Building one profile by hand

```powershell
dotnet publish src/StructureGen.Acad/StructureGen.Acad.csproj -c Release -p:AcadVersion=2026 -o out
```

`AcadVersion` selects the target framework and the `AutoCAD.NET` package version. It is resolved in
`build/AcadVersions.props`, which is the single place the release-to-API mapping lives. Passing an
unrecognised value fails the build with a message naming the supported ones.

## Why no Autodesk assembly is redistributed

The AutoCAD packages are referenced like this:

```xml
<PackageReference Include="AutoCAD.NET" Version="$(AcadNetPackageVersion)"
                  ExcludeAssets="runtime" PrivateAssets="all" />
```

`ExcludeAssets="runtime"` means the assemblies are used to compile against but never copied into the
output. This matters twice over:

1. **Technically.** AutoCAD loads its own copies. A second copy beside the plugin makes the plugin
   fail to load.
2. **Legally.** This project has no licence to redistribute Autodesk binaries, and none appear in
   this repository or in any artifact it produces.

Both the PowerShell build script and the Windows CI job assert this afterwards, so a reference that
loses the setting fails the build rather than shipping.

## Developing

Open `StructureGen.sln` in Visual Studio 2022 or Rider on Windows, or `StructureGen.Portable.slnf`
anywhere else.

The fastest inner loop is to work in the neutral projects and lean on the tests: the geometry, the
weights, the schedule, the database and the exporter are all fully covered there, so most changes
never need AutoCAD to verify.

To debug inside AutoCAD, build a Debug bundle, install it, start AutoCAD and attach the debugger to
`acad.exe`. Debug symbols are produced as portable PDBs beside the assemblies.

To test a rebuild without reinstalling, `NETLOAD` the DLL directly — but add its folder to
`TRUSTEDPATHS` first, or AutoCAD's security settings will refuse it.

## Repository layout

```
src/
  StructureGen.Core/              domain model, units, validation, geometry primitives
  StructureGen.Data/              database loading, validation, spreadsheet import
  StructureGen.Geometry/          the element generators
  StructureGen.Bom/               schedule grouping and totals
  StructureGen.Excel/             SpreadsheetML reader and writer, workbook builder
  StructureGen.Services/          application services, the ICadService seam, composition root
  StructureGen.Tools.DataImport/  command line data conversion and validation
  StructureGen.UI/                WPF palette, no AutoCAD reference
  StructureGen.Acad/              AutoCAD integration
tests/StructureGen.Tests/         233 tests
tests/StructureGen.CompileCheck/  compiles the Windows-only layers against API stand-ins
data/                             the shipped databases, schema and import mappings
build/                            version mapping, bundle manifest, build scripts
docs/                             this documentation
```

Dependencies point one way only: `Acad` → `UI` → `Services` → `Bom`/`Excel`/`Geometry`/`Data` →
`Core`. Nothing below `Services` knows AutoCAD exists.

## Third-party dependencies

At run time, in the plugin: **none.**

That is deliberate. The plugin loads into AutoCAD's process alongside whatever else the user has
installed, and every third-party assembly it brings is a chance of colliding with a different version
of the same assembly loaded by something else. So the spreadsheet writer is written against
`System.IO.Compression` directly rather than taking a library, and dependency injection is
constructor injection through a hand-written composition root rather than a container.

Build and test time only:

| Package | Licence | Why |
|---|---|---|
| `AutoCAD.NET`, `.Core`, `.Model` | Autodesk, compile-only | The AutoCAD Managed API |
| `xunit`, `xunit.runner.visualstudio` | Apache-2.0 | Tests |
| `Microsoft.NET.Test.Sdk` | MIT | Test host |

StructureGen itself is MIT. See `LICENSE`.
