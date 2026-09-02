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

`data/structural-sections-imperial-demo.json` is a three-record file in inches and pounds per foot,
included precisely to demonstrate this.

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
| `A` | Cross-sectional area, informational | Any |

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

## Verification

Every record has a `verified` flag, defaulting to `false`.

Unverified records are flagged on the palette when selected, marked on the schedule, counted in the
exported workbook, and reported by `SGRELOADDB`. **Every record in the shipped demonstration dataset
is unverified**, because the figures were transcribed from widely published tables for demonstration
and not checked against controlled copies.

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
