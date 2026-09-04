# The structural database

StructureGen hard-codes no section data. Everything it can draw comes from a JSON file you can
replace, and reloading it takes a button press rather than a rebuild.

- [The file](#the-file)
- [Units](#units)
- [Standards and materials](#standards-and-materials)
- [Sections](#sections)
- [The dimension vocabulary](#the-dimension-vocabulary)
- [Custom outlines](#custom-outlines)
- [Weights, and what happens without one](#weights-and-what-happens-without-one)
- [Verification](#verification)
- [Importing your own tables](#importing-your-own-tables)
- [Validation](#validation)

## The file

The default is `Contents/Data/structural-sections.json` inside the installed bundle. Change the path
on the palette's **Settings** tab; a relative path resolves against the bundle's `Data` folder, and an
absolute path is used as given, so a database on a network share works.

```json
{
  "schemaVersion": "1.0",
  "database": {
    "id": "acme-sections",
    "name": "Acme standard sections",
    "version": "2026.1",
    "revisionDate": "2026-04-01",
    "verified": true,
    "notes": "Checked against controlled copies on 2026-04-01 by A. Engineer."
  },
  "units":     { "length": "mm", "unitWeight": "kg/m", "density": "kg/m3", "pieceMass": "kg" },
  "standards": [ ... ],
  "materials": [ ... ],
  "sections":  [ ... ]
}
```

`database.version` is stamped onto every object generated from it and onto every schedule, so a
drawing opened in a year's time still says which data revision its weights came from.

`schemaVersion` is checked on load. A different **major** version is refused rather than misread; a
newer **minor** version is accepted, because minor revisions only ever add optional fields.

Point your editor at `data/schema/structural-database.schema.json` for completion and validation
while editing by hand.

## Units

Declared once, at the top of the file, and applied to every number in it. This is what lets an
imperial table be dropped in unchanged.

| Field | Accepts | Default |
|---|---|---|
| `length` | `mm`, `cm`, `m`, `in`, `ft` | `mm` |
| `unitWeight` | any mass per length: `kg/m`, `lb/ft`, `kg/mm` | `kg/m` |
| `density` | any mass per volume: `kg/m3`, `lb/in3`, `g/cm3` | `kg/m3` |
| `pieceMass` | `kg`, `g`, `t`, `lb` | `kg` |

Everything is converted to millimetres and kilograms on load, so nothing downstream ever has to ask
what unit a value is in. Longer spellings (`millimetres`, `inches`, `tonnes`) are understood too. An
unrecognised token falls back to the default and reports a warning rather than failing silently.

One special case: the dimension key `A` is an **area**, so it converts by the square of the length
factor. An `A` of 53.8 in a file declaring `cm` becomes 5380 mm².

`A` is not decoration. Where a record carries one, the test suite rebuilds the profile from the
record's other dimensions and compares the area of the outline it gets with the `A` the table
published — two figures that came from the table independently. Every shipped record passes that
within 2%. It is the check that would catch a column read into the wrong field, which is the
mistake that otherwise ships silently: the file still loads, the profile still looks plausible in
the palette, and only the drawing is wrong.

So do not put an `A` on a record whose outline cannot reproduce it. Where a standard tabulates an
area against something other than the shape as drawn — AISC quotes hollow sections against a design
wall about 93% of the nominal one — put it under `properties` instead, where it is carried verbatim
and treated as informational.

`data/structural-sections-imperial-demo.json` is a three-record file in inches and pounds per foot,
included precisely to demonstrate this. `data/aisc-sections.json` is the same idea at full size:
1,660 American shapes stored in the inches and pounds per foot their tables publish, so nothing was
rounded on the way in and every number can be compared with the source directly.

## Standards and materials

```json
"standards": [
  { "id": "en-10365", "name": "EN 10365", "region": "Europe", "revision": "2017",
    "description": "Hot rolled steel channels, I and H sections." }
],
"materials": [
  { "id": "s355jr", "name": "Structural steel", "grade": "S355JR",
    "density": 7850, "standardId": "en-10025" }
]
```

Both may be omitted, in which case StructureGen looks for a `standards.json` beside the section file
and uses the standards and materials from it. That lets several section files share one list without
repeating it. Shared materials are converted using **that** file's unit block, not the section
file's — so a shared density in kg/m³ stays correct when picked up by a file written in lb/in³.

`density` is optional, and leaving it out is a meaningful choice: see below.

## Sections

```json
{
  "id": "en10365-ipe-300",
  "standardId": "en-10365",
  "category": "i-section",
  "designation": "IPE 300",
  "nominalSize": "300",
  "material": "s355jr",
  "grade": "S355JR",
  "unitWeight": 42.2,
  "dimensions": { "h": 300, "b": 150, "tw": 7.1, "tf": 10.7, "r": 15, "A": 5380 },
  "properties": { "Iy": 8356, "Iz": 604 },
  "verified": true,
  "provenance": "EN 10365:2017 Table 3, controlled copy, checked 2026-04-01.",
  "notes": null
}
```

`id` is written onto every object generated from the record. **Changing it orphans existing
drawings** from their source record, so treat it as permanent.

`category` decides which generator draws the profile:

| Category | Draws |
|---|---|
| `i-section`, `h-section` | Doubly symmetric I and H with root fillets |
| `channel` | Channel, parallel flange approximation |
| `angle` | Equal and unequal leg angle |
| `t-section` | T with the flange on top |
| `pipe` | Circular hollow section |
| `rhs`, `shs` | Rectangular and square hollow section |
| `plate` | Rectangle, dimensions adjustable |
| `flat-bar` | Rectangle, dimensions fixed by the standard |
| `round-bar` | Solid circle |
| `bolt`, `nut`, `washer` | Fasteners |
| `custom` | Whatever the record's `outline` says |

A category with no registered generator is not an error: its records can still be scheduled and
weighed, they just cannot be drawn. The validator says so.

`properties` is stored verbatim, never converted, and is informational only.

## The dimension vocabulary

All in the file's length unit.

| Key | Meaning | Used by |
|---|---|---|
| `h` | Overall depth | I, H, channel, T, RHS, SHS |
| `b` | Overall width, flange width | I, H, channel, T, RHS |
| `tw` | Web or stem thickness | I, H, channel, T |
| `tf` | Flange thickness | I, H, channel, T |
| `r` | Root fillet radius | I, H, channel, T, angle |
| `r2` | Toe radius | Channel, angle |
| `d` | Outside diameter | Pipe, round bar |
| `t` | Wall thickness | Pipe, RHS, SHS |
| `ro` | Outside corner radius | RHS, SHS |
| `ri` | Inside corner radius; omit to take it as `ro` less the wall | RHS, SHS, optional |
| `a` | Leg A, the vertical leg | Angle |
| `b2` | Leg B; omit for an equal angle | Angle |
| `tt` | Leg or plate thickness | Angle, plate, flat bar |
| `w` | Plate or bar width | Plate, flat bar |
| `dth` | Nominal thread diameter | Bolt, nut |
| `s` | Width across flats | Bolt, nut |
| `k` | Head height | Bolt |
| `m` | Nut height | Nut |
| `l` | Bolt length under the head | Bolt |
| `lmin`, `lmax` | Published length range | Bolt, optional |
| `d3` | Thread minor diameter | Bolt, nut, optional |
| `ls` | Unthreaded shank length | Bolt, optional |
| `d1`, `d2`, `hw` | Washer inside, outside, thickness | Washer |
| `A` | Cross-sectional area; StructureGen checks its own outline against it | Any |

Which of these a user may edit is decided by the generator, not by the database:

- **Fixed by the standard** — locked in the palette and rejected by the validator, unless the item is
  explicitly switched to custom mode, which marks it non-standard everywhere it appears.
- **Adjustable** — the standard genuinely leaves it open: a bolt length within the published range,
  or the cut size of a plate.
- **This occurrence** — length, quantity, orientation, layer, notes.

## Custom outlines

For a profile no built-in generator covers — a fabricated box girder, a cold-rolled purlin, a
proprietary section — give the record an outline and StructureGen extrudes it exactly as written.
No code change, no rebuild.

```json
{
  "id": "acme-purlin-200",
  "standardId": "acme",
  "category": "custom",
  "designation": "Z 200x75x2.5",
  "outline": [
    { "x": 0,    "y": 0   },
    { "x": 75,   "y": 0   },
    { "x": 75,   "y": 20, "bulge": -0.4142 },
    { "x": 72.5, "y": 20  }
  ]
}
```

Points are in the file's length unit and the contour closes automatically. `bulge` is the tangent of
a quarter of the arc's included angle for the segment **leaving** that vertex: `0` is a straight
segment, `1` is a half circle, `0.4142` is a quarter turn anticlockwise, `-0.4142` a quarter turn
clockwise. This is exactly how an AutoCAD polyline stores an arc, so what you write is what is drawn.

Winding does not matter; StructureGen corrects it. At least three points are required.

The outline is **copied onto every element generated from the record**, so a drawing keeps
regenerating even after the database is replaced or the record renumbered.

## Weights, and what happens without one

In order of preference:

1. `unitWeight` — the tabulated mass per metre. Used for anything measured by length.
2. `pieceMass` — the tabulated mass per piece. Used for fasteners.
3. **Volume × density**, when the record has neither but its material has a `density`.
4. **Nothing.** The weight is reported as unknown.

The fourth case is the important one. StructureGen never guesses a density and never treats an
unknown weight as zero. Such a line shows a dash, is excluded from the totals, and the number of
excluded lines is stated on the schedule, in the exported workbook and on the palette.

Every line carries a **weight basis** column saying which route was taken, and the calculation is
written out in full — `42.2 kg/m x 6 m x 4 = 1012.8 kg` — so a checker can reproduce any figure by
hand.

## What ships, and where it came from

| File | Records | Units | Contents |
|---|---|---|---|
| `data/structural-sections.json` | 1,038 | mm, kg/m | European and UK sections: IPE, HE, HD, HL, HLZ, HP, UB, UC, UBP, CHS, RHS, SHS, plus hand-written channels, angles, plates, bars and fasteners |
| `data/aisc-sections.json` | 1,660 | in, lb/ft | American shapes: W, M, S, HP, C, MC, L, WT, MT, ST, HSS, Pipe |
| `data/structural-sections-imperial-demo.json` | 3 | in, lb/ft | A miniature file kept only to demonstrate the unit declaration |

Almost all of that is **compiled, not typed**. `tools/data-sources/compile_sections.py` reads three
sources, each pinned by SHA-256 in `tools/data-sources/sources.json`, and converts what they publish
into this format:

| Source | Provides |
|---|---|
| **`tools/data-sources/Steel_Section_Properties.xlsx`** — verbatim exports of the [ArcelorMittal Orange Book](https://orangebook.arcelormittal.com) (produced by SCI, dimensions to EN 10365:2017) and the SCI [Blue Book](https://www.steelforlifebluebook.co.uk) | 547 European and UK I and H sections |
| **[eurocodepy](https://github.com/pcachim/eurocodepy)** (MIT) | 410 hollow sections: CHS, RHS, SHS |
| **[steelpy](https://pypi.org/project/steelpy/)** (Apache-2.0), a transcription of the AISC Shapes Database v16.0 | 1,660 American shapes |

Compiling rather than transcribing is the point. Running the script again on the same pinned inputs
produces byte-identical files, so a reviewer can re-run it and diff instead of taking several
thousand numbers on trust — and CI does exactly that on every push. Every record names the source,
sheet or file it came from in its own `provenance`.

> **Copyright.** The Orange Book sheets are © 2023 SCI and ArcelorMittal Commercial Sections S.A.,
> the Blue Book sheets are © SCI, and the AISC data is © AISC with redistribution subject to AISC's
> terms. The repository's MIT licence covers its own code, not these tables. If you intend to publish
> this repository, check those terms.

### How the data is checked

Three independent checks, all run by the test suite or the compiler on every push:

1. **Every published area is compared against the outline StructureGen draws from that record's own
   dimensions.** Both figures come from the same table but were arrived at independently, so a
   column read into the wrong field breaks the agreement. All 2,698 records pass within 2%.
2. **Every published mass is compared against the area of that outline**, at a constant density.
3. **158 UK sections are compared against a second, independently produced publication.** The Orange
   Book and the Blue Book agree on every dimension and every mass — with one exception, below.

### The decisions that were not obvious

Nothing was invented to fill a gap: where a source publishes no value, none is written. Four
judgements were made, each stated in the provenance of every record it touches:

- **The root radius of 131 UK sections** is taken from the Blue Book rather than the Orange Book.
  The two agree on everything else, but the Orange Book prints a rounded figure — 20.0 mm across a
  whole range of different sections — where the Blue Book prints 19.1 or 17.8. The tie is broken by
  asking which radius accounts for the cross-sectional area the *Orange Book itself* prints in the
  same row: the Blue Book's does, to 0.11% on average against 0.25%, and 0.52% at worst against
  1.53%.
- **The fillet radius of AISC W, HP and WT shapes** is reconstructed as `k - tf`, AISC's own
  definition of the design distance `k`. It brings all 289 W shapes to within 0.73% of their
  tabulated area, against 2.72% with no fillet. It is deliberately *not* applied to the
  tapered-flange families, where `k` also swallows the taper and the reconstruction makes the shape
  worse — measured per family, with the figures recorded in the script.
- **The inside corner radius of the European hollow sections** is stated as `1.0t` rather than
  derived as `ro - t`. That is EN 10210's hot-finished corner, and it reproduces all 248 tabulated
  areas to within 0.42%, against 2.87% for a uniform wall. It is also why those records are filed
  under EN 10210 rather than EN 10219 — an inference from the data, stated as one.
- **Six shapes** carry their tabulated area under `properties` rather than `dimensions`, because the
  outline cannot reproduce it within 2%.

### Known approximations in the shipped data

- **Hollow section corners.** AISC publishes no corner radius for HSS, so they are drawn with square
  corners — up to about 13% over-stated in area on a small, thick-walled section. The schedule is
  unaffected: weight comes from the tabulated mass, not from the outline.
- **Tapered flanges.** UPN, and the AISC S, C, MC and ST families, are drawn with parallel flanges.
- **Aliased sections.** Some sections are published twice under different names — an `HD 260 x 172`
  and an `HE 260 M` are the same rolled section with identical dimensions and mass. Both ship,
  because a fabricator orders by whichever designation their market uses.

## Verification

Every record has a `verified` flag, defaulting to `false`.

Unverified records are flagged on the palette when selected, marked on the schedule, counted in the
exported workbook, and reported by `SGRELOADDB`. **Every record in every shipped dataset is
unverified**, and a test fails the build if one ever is not. A published export is not a controlled
copy of a standard, and this project has no standing to say otherwise on your behalf.

Set `"verified": true` only on records you personally have checked, and use `provenance` to record
against what.

## Importing your own tables

`StructureGen.DataImport` converts a CSV or XLSX section table using a **mapping profile**: a small
JSON file naming which of your columns is which. Adding support for a new source table means writing
a mapping profile, not writing code.

```json
{
  "name": "Acme catalogue",
  "standardId": "en-10365",
  "category": "i-section",
  "designationColumn": "Designation",
  "unitWeightColumn": "Mass",
  "dimensions": { "h": "Depth h", "b": "Width b", "tw": "Web tw", "tf": "Flange tf", "r": "Root radius r" },
  "lengthUnit": "mm",
  "unitWeightUnit": "kg/m",
  "material": "s355jr",
  "markVerified": false,
  "blankMarkers": ["-", "n/a"]
}
```

```bash
dotnet run --project src/StructureGen.Tools.DataImport -- \
    import --in catalogue.xlsx --sheet "I sections" \
           --map my-mapping.json --out acme-sections.json
```

The importer is deliberately forgiving of real-world tables: quoted fields containing commas,
thousands separators, comma decimal separators, stray unit suffixes and blank markers are all
handled. It is deliberately unforgiving about anything it cannot be sure of — an unparsable cell
leaves the field unset and says which row and column, rather than guessing.

It never marks a record verified unless the profile says to. That assertion is yours to make.

Worked examples live in `data/import-mappings/`, including a metric mapping, an imperial one and a
sample CSV, all exercised by the test suite.

Other commands:

```bash
# What worksheets does this workbook have?
dotnet run --project src/StructureGen.Tools.DataImport -- sheets --in catalogue.xlsx

# Check a database exactly as the plugin would on load.
dotnet run --project src/StructureGen.Tools.DataImport -- validate --in acme-sections.json
```

Exit codes: `0` success, `1` usage error, `2` the data was rejected.

## Validation

The same rules run in the plugin, in the import tool and in continuous integration, so a database
that passes the tool is guaranteed to load in AutoCAD.

**Errors** stop the file loading:

- Malformed JSON, or an unsupported major schema version
- A missing `id`, `category` or `designation`
- A duplicate id
- A section naming a standard that is not defined
- A dimension that is zero or negative
- A missing dimension the category's generator needs
- Geometry that cannot exist: flanges thicker than half the depth, a wall thicker than half the
  diameter, a web wider than the flange
- A unit weight or piece mass that is not greater than zero

**Warnings** load but are reported:

- A duplicate designation within one standard and category
- A material that is not defined
- A category with no generator — schedulable but not drawable
- A record with no route to a weight
- An implausible root radius, which is clamped when drawn

**Information:**

- How many records are unverified

A failed load leaves the previously loaded data in place, so a typo in an edited file cannot leave
the palette with nothing to work from mid-session.
