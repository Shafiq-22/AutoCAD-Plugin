# Engineering disclaimer

**StructureGen is a drafting and documentation aid. It performs no structural engineering design,
analysis or verification.**

## What this means in practice

**Drawing a standard profile says nothing about whether that profile is adequate.** StructureGen
will happily draw an IPE 100 spanning thirty metres. It is a drawing tool; deciding what a member
must be is engineering, and it remains entirely yours.

**The dimensions and weights are only as good as the database you load.** StructureGen reads a data
file. It does not know whether that file is right. The dataset that ships is demonstration data,
transcribed from widely published figures for the purpose of exercising the software, and **every
record in it is marked unverified**. Verify every dimension and unit weight against your own
authoritative copy of the relevant standard before any output is used for procurement, fabrication
or construction.

**Generated geometry is not engineering approval.** A solid in a drawing is a representation. It
carries no implication that the member has been checked, that the connection works, or that the
arrangement is buildable.

**The bill of materials is a take-off, not a specification.** It totals what is in the drawing. It
does not account for fabrication allowances, cutting waste, weld metal, coatings, fittings, or
anything else not modelled. Where StructureGen cannot establish a weight it reports it as unknown
and excludes it from the totals, stating how many lines were excluded — read those notes.

**Simplifications are documented and affect the picture.** Tapered-flange channels are drawn with
parallel flanges; threads are not modelled. `docs/LIMITATIONS.md` lists every approximation with its
measured effect.

## Your responsibility

Anyone using StructureGen for real work is responsible for:

- Verifying the section data against a controlled copy of the standard.
- Checking that the generated geometry matches what was intended.
- Checking the schedule against the drawing before it is issued.
- All structural design decisions, in every respect.

## Licence

StructureGen is provided under the MIT Licence, **without warranty of any kind**, express or
implied, including but not limited to the warranties of merchantability, fitness for a particular
purpose and non-infringement. See `LICENSE`.

## Where this appears

This disclaimer is not buried in one file. It is shown on the palette's settings tab, written into
every exported workbook on its *Data Sources* sheet, printed by the `SGABOUT` command, and repeated
in the README.
