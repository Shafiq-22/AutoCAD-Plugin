# Contributing to StructureGen

## Before you start

```bash
./build/build.sh
```

All 233 tests should pass. If they do not on a clean checkout, that is a bug worth an issue on its
own.

## The rule the architecture rests on

**Nothing that can be tested without AutoCAD may depend on AutoCAD.**

If you find yourself wanting `Autodesk.AutoCAD.*` in `Core`, `Data`, `Geometry`, `Bom`, `Excel` or
`Services`, the design has gone wrong somewhere. That boundary is why the test suite exists at all;
losing it loses the ability to verify any of the logic.

Practically: put the decision in a neutral project and the translation in `StructureGen.Acad`.

`tests/StructureGen.CompileCheck` compiles the AutoCAD and user-interface source against stand-in
AutoCAD and WPF types, so their calls into StructureGen's own code are checked even without Windows.
If you use an AutoCAD API the stubs do not cover, add it to `Stubs/AutodeskApi.cs` — but finding
yourself doing that often is a sign the logic belongs somewhere it can be tested properly.

## What good looks like here

**Tests come with the change.** Especially for anything touching geometry, weights or the schedule.
The strongest geometry test is comparing a computed area against a published one — it verifies the
whole outline in a single number.

**Errors are values, not exceptions.** A malformed database row, a negative length, a missing density
are all expected. Return `Result<T>` or add to a `ValidationReport` with a stable code, a message
written for a drafter, and the context identifying the record. Exceptions are for defects.

**Messages say what is wrong and what to do.** Not "Invalid input" but "'Web thickness' is fixed by
en-10365 for profile 'IPE 300' at 7.1 mm, but 12 mm was given. Switch the item to custom mode if you
intend to depart from the standard profile."

**Never invent a number.** If the data does not support a feature, leave the feature out and say so.
A weight that cannot be established is reported as unknown, not estimated and never zero.

**Comments explain why, not what.** The code says what it does. A comment earns its place by
explaining a decision, a constraint or a trap — why the hash is hand-written, why the fillet radius
is clamped, why extended entity data holds only a marker.

**No new runtime dependencies in the plugin.** It loads into AutoCAD's process alongside other
add-ins; every assembly it brings can collide with a different version of the same assembly. If you
believe a dependency is genuinely necessary, open an issue first and make the case.

## Adding an element type

See [docs/ADDING-ELEMENT-TYPES.md](docs/ADDING-ELEMENT-TYPES.md). The shortest route needs no code:
if the profile is a single closed outline, it goes in the database.

If it does need a generator, that is one class and one registration line. Two existing tests will
start covering it automatically.

## Adding section data

Data belongs in a data file, not in code, and `verified: true` is an assertion only you can make
about your own sources. The demonstration dataset is deliberately all unverified; please keep it that
way, and record `provenance` on anything you add.

## Style

`.editorconfig` covers the mechanics. Beyond that: the codebase is warnings-as-errors with XML
documentation required on public members, so a build that passes is most of the way there.

Name things the way a detailer would say them. `LengthIsUserParameter` beats `HasLen`.

## Commits and pull requests

Explain the change and why it was needed. If it affects generated geometry, say what you compared it
against.

Pull requests run the full test suite on Linux and compile the AutoCAD layer against all three
supported releases on Windows. Both must pass.

## Reporting a bug

Please include:

- Your AutoCAD release, from `ACADVER`, and whether it is a vertical
- What `SGABOUT` prints
- The relevant part of `%LOCALAPPDATA%\StructureGen\logs\`
- The database record involved, if the problem is data-related

A failing test is the most useful bug report there is.
