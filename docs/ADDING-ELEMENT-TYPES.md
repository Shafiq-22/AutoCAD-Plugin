# Adding a new structural element type

A worked example: adding a **crane rail**, a shape none of the built-in generators covers.

There are two ways to do it, and the first needs no code at all.

---

## Option 1: describe it in the database

If the profile is a single closed outline, write it into your database and stop. No code, no
rebuild, no redeploy.

```json
{
  "id": "din536-a45",
  "standardId": "din-536",
  "category": "custom",
  "designation": "A45 crane rail",
  "material": "s355jr",
  "unitWeight": 22.1,
  "outline": [
    { "x": -87.5, "y": 0    },
    { "x":  87.5, "y": 0    },
    { "x":  87.5, "y": 20   },
    { "x":  12,   "y": 33   },
    { "x":  12,   "y": 40   },
    { "x":  27.5, "y": 40   },
    { "x":  27.5, "y": 55, "bulge": -0.2 },
    { "x": -27.5, "y": 55   },
    { "x": -27.5, "y": 40   },
    { "x": -12,   "y": 40   },
    { "x": -12,   "y": 33   },
    { "x": -87.5, "y": 20   }
  ],
  "verified": false,
  "provenance": "Traced from the manufacturer's drawing; not yet checked."
}
```

Press **Reload database**. It appears under Custom, generates, schedules and weighs like anything
else — and the outline is copied onto every element made from it, so drawings keep regenerating even
if you later renumber the record.

Use this whenever it fits. It is faster, it needs no build, and it cannot break anything else.

## When it does not fit

Write a generator when you need:

- **Parameters the user can vary.** An outline is fixed; a generator computes its shape from
  dimensions, so one generator serves a whole family of sizes.
- **Response to the detail level.** Fillets at `Standard`, toe radii at `Detailed`.
- **More than one prism.** A bolt is a head over a shank.
- **Category-specific validation.** "A wall this thick leaves no bore."

---

## Option 2: write a generator

Say we want crane rails as a family, with head width, web thickness, foot width and height as
parameters.

### Step 1 — a category identifier

`src/StructureGen.Core/Model/ElementCategory.cs`:

```csharp
/// <summary>Crane rails.</summary>
public const string CraneRail = "crane-rail";
```

Add it to `CategoryCatalog.All` in `CategoryDescriptor.cs`, in the position you want it to appear:

```csharp
new(ElementCategory.CraneRail, "Crane Rail", CategoryKind.Linear, true),
```

`CategoryKind.Linear` means it is extruded along a length; `true` means the user supplies that
length.

### Step 2 — dimension keys, if you need new ones

Reuse the existing vocabulary where it fits — `h`, `b`, `tw` all suit a rail. For anything genuinely
new, add it to `DimensionKeys.cs` and document it in `docs/DATABASE.md`.

```csharp
/// <summary>Rail foot width, <c>bf</c>.</summary>
public const string FootWidth = "bf";
```

### Step 3 — the generator

`src/StructureGen.Geometry/Generators/CraneRailGenerator.cs`:

```csharp
public sealed class CraneRailGenerator : ElementGeneratorBase
{
    private static readonly string[] Required =
    {
        DimensionKeys.Height, DimensionKeys.Width, DimensionKeys.WebThickness, DimensionKeys.FootWidth
    };

    public override string CategoryId => ElementCategory.CraneRail;

    public override IReadOnlyList<string> RequiredDimensions => Required;

    public override ParameterSchema GetParameterSchema(SectionRecord record)
    {
        List<ParameterDescriptor> parameters = new()
        {
            SchemaFactory.Locked(DimensionKeys.Height,       "Height h"),
            SchemaFactory.Locked(DimensionKeys.Width,        "Head width b"),
            SchemaFactory.Locked(DimensionKeys.WebThickness, "Web thickness tw"),
            SchemaFactory.Locked(DimensionKeys.FootWidth,    "Foot width bf"),
            SchemaFactory.Locked(DimensionKeys.RootRadius,   "Fillet radius r", required: false)
        };

        SchemaFactory.AddInstanceParameters(parameters, Descriptor);
        return new ParameterSchema(CategoryId, parameters);
    }

    protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
    {
        double height   = Dimension(definition, DimensionKeys.Height);
        double head     = Dimension(definition, DimensionKeys.Width);
        double web      = Dimension(definition, DimensionKeys.WebThickness);
        double foot     = Dimension(definition, DimensionKeys.FootWidth);
        double fillet   = RadiusForDetail(definition, DimensionKeys.RootRadius, DetailLevel.Standard);

        double halfHead = head / 2d, halfWeb = web / 2d, halfFoot = foot / 2d;
        double footTop  = height * 0.25d, headBottom = height * 0.7d;

        // Anticlockwise from the bottom right of the foot. A fillet radius is simply the third
        // argument to Add; the builder works out the tangent points and the bulge, decides convex
        // from concave by the turn direction, and reduces a radius that will not fit.
        ProfileLoopBuilder builder = new();
        builder.Add(halfFoot,  0d)
               .Add(halfFoot,  footTop)
               .Add(halfWeb,   footTop,    fillet)
               .Add(halfWeb,   headBottom, fillet)
               .Add(halfHead,  headBottom)
               .Add(halfHead,  height)
               .Add(-halfHead, height)
               .Add(-halfHead, headBottom)
               .Add(-halfWeb,  headBottom, fillet)
               .Add(-halfWeb,  footTop,    fillet)
               .Add(-halfFoot, footTop)
               .Add(-halfFoot, 0d);

        ProfileGeometry profile = new ProfileGeometry(builder.Build()).Centred();
        return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
    }

    protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
    {
        decimal web  = definition.Dimension(DimensionKeys.WebThickness) ?? 0m;
        decimal foot = definition.Dimension(DimensionKeys.FootWidth)    ?? 0m;

        if (web > 0m && foot > 0m && web >= foot)
        {
            report.AddError(
                "CraneRail.WebWiderThanFoot",
                $"A web of {Format(web)} mm is not narrower than the foot width of {Format(foot)} mm.",
                definition.Designation);
        }
    }
}
```

The base class already handles quantity, length, required dimensions, the guard that stops a
standardised dimension being changed outside custom mode, and the whole weight calculation. Override
`ValidateCore` only for rules specific to the shape.

### Step 4 — register it

`ElementGeneratorRegistry.CreateDefault`:

```csharp
new Generators.CraneRailGenerator(),
```

That single line is the whole wiring. The palette offers the category, the validator starts checking
that `crane-rail` records carry `h`, `b`, `tw` and `bf`, the schedule groups and totals it, and the
exporter includes it. Nothing else changes.

### Step 5 — test it

`tests/StructureGen.Tests/GeometryTests.cs`:

```csharp
[Fact]
public void A_crane_rail_has_the_area_its_table_publishes()
{
    ElementGeometry geometry = Build(ElementCategory.CraneRail, new Dictionary<string, decimal>
    {
        ["h"] = 55m, ["b"] = 45m, ["tw"] = 24m, ["bf"] = 175m, ["r"] = 4m
    });

    Assert.Equal(2820d, geometry.PrimaryProfile.AreaMm2, 0);
    Assert.True(geometry.PrimaryProfile.Outer.IsAnticlockwise);
}
```

Checking the computed area against the published area is the strongest test available: it verifies
the whole outline, fillets included, in one number.

```bash
dotnet test tests/StructureGen.Tests
```

Two existing tests will now also cover your generator automatically:
`Every_record_in_the_shipped_database_generates` and
`The_outer_contour_of_every_generated_profile_is_wound_anticlockwise`.

### Step 6 — add data

```json
{
  "id": "din536-a45", "standardId": "din-536", "category": "crane-rail",
  "designation": "A45", "material": "s355jr", "unitWeight": 22.1,
  "dimensions": { "h": 55, "b": 45, "tw": 24, "bf": 175, "r": 4, "A": 2820 },
  "verified": false,
  "provenance": "DIN 536-1, transcribed. Not yet checked against a controlled copy."
}
```

---

## Conventions worth following

**Centre the profile on its bounding box.** Every built-in generator does, so rotation and insertion
behave predictably. `ProfileGeometry.Centred()` does it for you.

**Anticlockwise outer contours, clockwise voids.** `ProfileLoopBuilder.Build()` enforces this;
`ProfileGeometry` assumes it.

**Honour the detail level.** Use `RadiusForDetail` so `Simplified` really is cheaper to draw.
Drawings with thousands of members depend on it.

**Never invent a number.** If the record lacks the data for a feature, leave the feature out. The
bolt generator draws a plain cylinder when no thread minor diameter is published, rather than
assuming one.

**Say what is wrong, not that something is wrong.** Validation messages name the profile, the
dimension and the value, and say what to do about it.

## Adding something other than an element

**A new standard** — data only. Add it to the `standards` array.

**A new dimension** — add it to `DimensionKeys`, use it in a generator, document it in
`docs/DATABASE.md`.

**A new tabulated property** — put it in a record's `properties`. It is stored verbatim and carried
through untouched.

**A new column on the schedule** — add the value to `BomLine`, then to `BomWorkbookBuilder` for the
workbook and `AcadTableWriter` for the drawn table.

**A new AutoCAD release** — see `docs/COMPATIBILITY.md`. Usually three lines of configuration and no
source change at all.
