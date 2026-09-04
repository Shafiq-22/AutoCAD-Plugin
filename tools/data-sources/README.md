# Where the section data comes from

The section databases StructureGen ships are **compiled, not typed**. `compile_sections.py` reads
published section tables and converts what they publish into StructureGen's own format, recording
per record exactly where each number came from.

```
python3 compile_sections.py --download   # fetch the pinned wheels into ./_cache
python3 compile_sections.py --check      # compile in memory, print the report, write nothing
python3 compile_sections.py              # compile and write data/*.json
```

It needs nothing but Python 3.9, and network access to PyPI on the first run to fetch the two
pinned wheels. The workbook is committed here, and the XLSX reader beside it (`minixlsx.py`) is
about a hundred lines of zipfile and ElementTree, so nothing has to be installed to read it.

## Why compile

A dataset this size cannot be reviewed if it was typed. Nobody is going to check two thousand rows
of numbers by eye, and every slip becomes a wrong weight on a drawing that nobody can trace back to
anything. Compiling from a pinned input makes the whole file reproducible: run the script again and
the output is byte-identical, so a reviewer diffs instead of trusting. CI does this on every push
and fails if the committed files are not what the compiler currently produces.

## The sources

| Source | Licence / copyright | Provides |
|---|---|---|
| `Steel_Section_Properties.xlsx` — verbatim exports of the [ArcelorMittal Orange Book](https://orangebook.arcelormittal.com) and the SCI [Blue Book](https://www.steelforlifebluebook.co.uk) | © 2023 SCI and ArcelorMittal Commercial Sections S.A.; © SCI. Supplied by the repository owner. | 547 European and UK I and H sections: IPE, HE, HD, HL, HLZ, HP, UB, UC, UBP |
| [eurocodepy](https://github.com/pcachim/eurocodepy) | MIT | 410 hollow sections: CHS, RHS, SHS |
| [steelpy](https://pypi.org/project/steelpy/) | Apache-2.0 | AISC Shapes Database v16.0: W, M, S, HP, C, MC, L, WT, MT, ST, HSS, Pipe |

All three are pinned by SHA-256 in `sources.json` and the checksum is verified on every run. A
changed checksum stops the compilation rather than quietly folding new numbers into the output.

The workbook is committed here rather than downloaded, because it was supplied rather than
published at a stable URL, and the compilation has to be reproducible from the repository alone.
Its Blue Book sheets are read but not compiled: they exist to check the Orange Book's numbers
against a second, independently produced publication.

Note the copyright column. The repository's MIT licence covers its own code, not these tables.

## What the compiler will not do

- **It never marks a record verified.** An upstream package is not a controlled copy of a standard.
  Every record it writes carries `"verified": false`, and a test fails the build if one does not.
- **It never invents a value.** Where a source publishes nothing, nothing is written and the profile
  is drawn without it.
- **It never ships an area its own outline cannot reproduce.** A published area is what lets the
  test suite check the profile StructureGen draws against the table it came from, so an area that
  misses by more than 2% is moved to `properties`, where it is informational, and the record says
  why. Whether a larger miss is a modelling limit or a mis-read column is settled by the other
  figure the table publishes for the same shape: a mis-read column produces dimensions that agree
  with nothing, so the outline misses the tabulated *mass* too. Missing both by more than 8% fails
  the compilation.

## Where the sources disagreed

Two publications of the same UK sections agree on every dimension and every mass except the root
radius, where the Orange Book prints a round 20.0 mm across a whole range of different sections and
the Blue Book prints 19.1 or 17.8. One has been standardised, and the difference is large enough to
change a drawn fillet.

It is not settled by preferring a publisher. It is settled by asking which radius accounts for the
cross-sectional area the *Orange Book itself* prints in the same row, since that area was computed
from the real profile. Across the 131 sections where they differ, the Blue Book's radius reproduces
the Orange Book's own area to 0.11% on average against 0.25%, and 0.52% at worst against 1.53%. So
that is the radius drawn, and the record says so. See `better_radii`.

## Two derivations, and why they are not guesses

`_fillet_from_k` reconstructs the fillet radius of AISC shapes as `k - tf`, which is AISC's own
definition of the design distance `k`. It is applied only to the families where measurement shows it
helps; the numbers, and the families where it hurts, are in the comment above `AISC_FAMILIES`.

The European hollow sections state an inside corner radius of `1.0t` rather than letting it be
derived as `ro - t`. Which convention applies was read off the source's own tabulated areas, not
assumed: `1.0t` reproduces all 248 of them to within 0.42%, a uniform wall misses by up to 2.87%.
That is also why those sections are filed under EN 10210 rather than EN 10219 — an inference from
the data, stated as one in every affected record's provenance.

## What is still hand-written

European channels (UPN), European angles, plates, flat and round bars, and the ISO fasteners. The
workbook does not cover them and no permissively licensed machine-readable source was found, so the
small hand-transcribed set from the original demonstration dataset is left alone; the compiler
merges around it rather than replacing it. It recognises its own past output by the phrase every
record it writes carries in its provenance, so replacing a source removes the records that source
produced instead of leaving them beside their replacements. If you have authoritative tables for those, importing them with
`StructureGen.DataImport` is the intended route — see `docs/DATABASE.md`.
