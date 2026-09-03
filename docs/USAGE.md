# Using StructureGen

## The workflow

The palette's tabs follow the order the work happens in. Working left to right does the whole job.

```
  1 Create  ──►  2 List  ──►  generate  ──►  3 Bill of materials  ──►  export
```

## 1. Create an element

Type `STRUCTUREGEN`. The palette opens and docks like any other.

**Category → Standard → Profile.** Each choice narrows the next, so you are never shown a standard
that publishes nothing in the category you picked, or a profile that does not exist in that standard.

Choosing a profile fills in its dimensions and shows a summary line: designation, mass per metre,
material. If the record is not marked verified, a warning says so and names its provenance.

**Standard dimensions** appear in the grid, each with its status beside it:

| Status | Means |
|---|---|
| *fixed by standard* | Read-only. This is what makes an IPE 300 an IPE 300. |
| *adjustable* | The standard genuinely leaves it open — a bolt length within the published range, a plate's cut size. |
| *this occurrence* | Belongs to this particular member: length, quantity, layer, notes. |

To depart from the published profile, tick **Custom / non-standard mode**. The locked fields unlock,
a warning appears, and every item you add is marked non-standard on the schedule, in the workbook and
in the drawing's metadata. **Reset** puts everything back.

**This occurrence** — length, quantity, detail level, what to draw, layer, notes:

- *Detail*: `Simplified` (sharp corners), `Standard` (root fillets, the default), `Detailed` (also
  toe radii and, for bolts with the data, a stepped thread). Lower detail draws faster; use it when
  placing hundreds of members.
- *Draw as*: `Solid3D`, `Profile2D` (the true cross-section as closed polylines), or `Both`.
- *Layer*: leave empty for an automatic layer per category — `SG-i-section`, `SG-bolt` and so on.

**Placement:**

- **Pick point** — one insertion point; the member runs along X.
- **Pick start / end** — pick both ends. The direction *and the length* come from the picks, and the
  length box updates to the measured value. **The cross-section is untouched**: only the extrusion
  distance changes.
- **Clear** — back to the drawing origin.

For a quantity above one with a single placement, copies are laid out side by side rather than
stacked, so they can be told apart and selected.

Press **Add to list**.

## 2. Review the list

Nothing has touched the drawing yet. This is where a whole frame's worth of members is assembled and
checked before anything is committed.

Duplicate, delete, reorder or clear. The running total updates as you go, and any line that needs
attention is shown in red with the reason in the Note column — a weight that could not be
established, a non-standard profile, or unverified source data. Hovering a row shows how its weight
was calculated.

**Validate** checks everything without drawing anything. **Generate selected** does one item;
**Generate all** does the lot.

Generation reports what it made, including how many block definitions it created and how many it
reused. A high reuse count is the point: a thousand identical bolts share one definition.

## 3. Bill of materials

Two buttons, and the difference between them matters:

**Build from drawing** — reads the objects that actually exist. This is the authoritative schedule.
A member you erased by hand is gone from it; one you copied with `COPY` is on it, carrying its
parameters. Use this for anything that leaves your desk.

**Preview from list** — what the working list would weigh. Nothing is drawn. Useful for checking a
weight before committing, and labelled as a preview everywhere it appears.

Identical members collapse onto one line with a quantity. Members differing in profile, length,
standard, material or conformity stay separate — a non-standard member is never hidden inside the
count of a standard one.

**A weight that could not be established shows a dash, never a zero.** It is excluded from the
totals, and a note says how many lines were excluded. Read those notes before issuing anything.

Then:

- **Table in model** / **Table in layout** — draws a native AutoCAD table at a point you pick. It
  respects the drawing's table style and stays editable. It is a snapshot; re-run for a fresh one.
- **Export to Excel** — writes a `.xlsx`. Excel is not required to produce it.

## The exported workbook

| Sheet | Holds |
|---|---|
| **BOM** | The schedule: item, category, standard, profile, dimensions, length, quantity, unit weight, totals in kg and tonnes, material, weight basis, notes. Frozen header, auto-filter, grand total. |
| **Summary** | Totals by category. |
| **Element Details** | Every individual occurrence with its AutoCAD handle, so any figure traces back to the object that produced it. |
| **Data Sources** | Which database revision, when, which standards, every caveat, and the engineering disclaimer. |

The *Weight basis* column says how each figure was reached — *Tabulated kg/m*, *Tabulated kg/piece*,
*Volume × density* or *Not available* — and the Element Details sheet writes the arithmetic out in
full, so a checker can reproduce any number by hand.

## Editing an element that is already drawn

1. Type `SGEDIT` and select it.
2. It loads into the Create tab with its stored parameters.
3. Change what you need.
4. Press **Update in drawing**.

The geometry rebuilds and the element keeps its identity, so it stays the same element on the
schedule and in the drawing's history.

Behind the scenes the update picks the safe option: it retargets the reference if the new shape
already exists in the drawing, rebuilds the definition in place if this reference is its only user,
and otherwise creates a new definition and moves only this reference — so editing one member never
silently reshapes its neighbours.

`SGINFO` prints everything known about a selected element without opening anything, including the
weight calculation and whether its source data was verified.

`SGUPDATE` rebuilds a selection from their stored parameters. Use it after changing the detail level
or reloading a corrected database.

## Replacing the section data

**Settings** tab → set *Structural database* → **Reload database**.

The validation report appears immediately below. A file that fails to load leaves the previous data
in place, so a typo cannot leave you with nothing mid-session. `SGRELOADDB` does the same from the
command line.

Relative paths resolve against the bundle's `Data` folder; absolute paths are used as given, so a
database on a network share works.

To convert your own spreadsheets, see `docs/DATABASE.md`.

## All the commands

| Command | Does |
|---|---|
| `STRUCTUREGEN` | Opens the palette |
| `SGEDIT` | Loads a drawn element into the palette for editing |
| `SGINFO` | Prints everything known about a selected element |
| `SGSHOW` | Brings every StructureGen element in the drawing into view |
| `SGBOM` | Builds the schedule and offers a table or an export |
| `SGBOMEXPORT` | Builds the schedule and exports it straight to a workbook |
| `SGUPDATE` | Rebuilds selected elements from their stored parameters |
| `SGRELOADDB` | Reloads the structural database |
| `SGSETTINGS` | Opens the settings tab |
| `SGABOUT` | Version, host, folders and the disclaimer |

## Keeping large drawings fast

- **Use `Simplified` detail for repetitive components.** Bolts do not need fillets.
- **Let quantities do the work.** One item with a quantity of 200 shares one block definition. Two
  hundred separate items with quantity one still share it, as long as the geometry is identical — but
  the list is harder to read.
- **Draw only what you need.** `Solid3D` alone is cheaper than `Both`.
- **Generate in batches.** One press of Generate all is a single transaction, and far faster than
  twenty separate ones.

## Settings

| Setting | Does |
|---|---|
| Structural database | Path to the data file |
| Entry length unit | The unit you type lengths in |
| Schedule length unit | The unit lengths are reported in |
| Mass unit | The unit masses are shown in |
| Default detail | Detail level for new items |
| Layer prefix | Prefix for automatic layers, default `SG-` |
| Zoom to show newly generated elements | Whether the view moves to the new geometry, default on |
| Table text height | Text height for drawn schedule tables |
| Collapse identical members | Whether identical members share one schedule line |
| Different layers, different lines | Whether the layer distinguishes two lines |
| Length grouping tolerance | How close two lengths must be to count as the same |
| Verbose logging | More detail in the log file |

Settings live in `%APPDATA%\StructureGen\settings.json` and survive upgrades.

## "I generated something and the drawing still looks empty"

This is the most common first surprise, and it usually is not a failure.

An element is placed at the **world origin** unless you pick a point for it, and it is drawn along
the positive X axis. A new metric drawing opens with a view a few hundred millimetres wide, so a six
metre beam starting at the origin is almost entirely outside it. The beam is there; the view is not
looking at it.

StructureGen zooms to what it just created, which normally means you never see this. If you have
switched that off in Settings, or the view has since moved:

1. Type **`SGSHOW`**. It reports how many StructureGen elements the drawing contains and moves the
   view to show them. If it reports **zero**, nothing was created and the problem is a real one -
   read the message the palette printed on the command line.
2. Or type **`ZOOM`** then **`E`** for the whole drawing extents.

Two further things worth checking if `SGSHOW` finds elements but you still cannot see them:

- **A frozen or switched-off layer.** Elements go on a layer named after their category, `SG-i-section`
  by default. Open the Layer Properties Manager and check the `SG-` layers are on and thawed.
- **A 3D solid in a plan view.** A member generated as `Solid3D` and viewed from the top looks like a
  rectangle, which in a 2D wireframe visual style can be easy to mistake for nothing. Try
  View > 3D Views > SW Isometric.

## When something goes wrong

Messages are written for drafters, not developers: no stack traces. The technical detail goes to
`%LOCALAPPDATA%\StructureGen\logs\`, kept for 14 days. `SGABOUT` prints the exact path.

`docs/INSTALLATION.md` covers the common problems.
