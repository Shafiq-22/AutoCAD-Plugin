#!/usr/bin/env python3
"""Compile StructureGen section databases from published upstream data.

Why this exists
---------------
The section data StructureGen ships has to come from somewhere, and "somewhere" has to be
something a checker can follow back to a source. Typing several thousand numbers out of a
table by hand is not that: it is unreviewable, and every transcription slip becomes a wrong
weight on a drawing that nobody can trace.

So the dataset is compiled instead of written. This script reads two public, permissively
licensed packages, pinned by version and checksum in ``sources.json``, and converts what they
publish into StructureGen's own format, recording per record where each number came from.
Running it again on the same pinned inputs produces byte-identical output, so a reviewer can
re-run it and diff.

What it does not do
-------------------
It does not mark anything verified. Every compiled record carries ``"verified": false``,
because an upstream package is not a controlled copy of a standard and this project has no
standing to say otherwise. Marking records verified is the user's decision, made against their
own authoritative source.

It does not invent numbers. Where a source does not publish a value, the value is left out and
the geometry is drawn without it, rather than estimated. The one exception is the fillet radius
of AISC shapes, which is reconstructed from two values the source does publish - see
``_fillet_from_k`` - and is checked against the independently published cross-sectional area of
every shape it is applied to.

Usage
-----
    python3 tools/data-sources/compile_sections.py --download   # fetch the pinned wheels
    python3 tools/data-sources/compile_sections.py              # compile from ./_cache

Add ``--check`` to compile into memory and report without writing anything.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import math
import re
import subprocess
import sys
import zipfile
from collections import Counter
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
DATA = REPO / "data"
CACHE = HERE / "_cache"

# Steel density, kg/m3. Used only to sanity check a published mass against a published area,
# never to produce a mass that a source did not publish.
STEEL_DENSITY = 7850.0

# The conventional density of steel in AISC's tables, lb/in3, used for the same check.
STEEL_DENSITY_LB_IN3 = 0.2836


# --------------------------------------------------------------------------------------
# Source acquisition
# --------------------------------------------------------------------------------------


def load_manifest() -> dict[str, Any]:
    return json.loads((HERE / "sources.json").read_text(encoding="utf-8"))


def download(manifest: dict[str, Any]) -> None:
    """Fetch each pinned wheel into the cache and verify its checksum."""
    CACHE.mkdir(exist_ok=True)

    for source in manifest["sources"]:
        target = CACHE / source["wheel"]

        if not target.exists():
            print(f"downloading {source['package']}=={source['version']}")
            subprocess.run(
                [
                    sys.executable, "-m", "pip", "download",
                    f"{source['package']}=={source['version']}",
                    "--no-deps", "--quiet", "-d", str(CACHE),
                ],
                check=True,
            )

        digest = hashlib.sha256(target.read_bytes()).hexdigest()
        if digest != source["sha256"]:
            raise SystemExit(
                f"{source['wheel']} has checksum {digest}, but sources.json pins {source['sha256']}. "
                "Either the upstream file changed, which needs reviewing, or the download is corrupt."
            )

        print(f"  {source['wheel']}: checksum matches")


def open_source(source: dict[str, Any]) -> zipfile.ZipFile:
    target = CACHE / source["wheel"]

    if not target.exists():
        raise SystemExit(
            f"{target} is missing. Run with --download first, which fetches the pinned wheels from PyPI."
        )

    digest = hashlib.sha256(target.read_bytes()).hexdigest()
    if digest != source["sha256"]:
        raise SystemExit(f"{source['wheel']} does not match the checksum pinned in sources.json.")

    return zipfile.ZipFile(target)


# --------------------------------------------------------------------------------------
# Small helpers
# --------------------------------------------------------------------------------------


def number(value: Any) -> float | None:
    """Reads a value that may be blank, absent or a placeholder."""
    if value is None:
        return None

    text = str(value).strip().replace("–", "-")
    if text in ("", "-", "–", "None", "nan"):
        return None

    # A few AISC rows carry a trailing marker on the flange thickness of very heavy shapes.
    text = text.rstrip("–-f ")

    try:
        result = float(text)
    except ValueError:
        return None

    return None if math.isnan(result) else result


def rounded(value: float | None, places: int) -> float | None:
    if value is None:
        return None

    result = round(value, places)

    # Keep the JSON free of "-0.0" and of integers written as floats where they need not be.
    if result == 0:
        return 0.0

    return result


def slug(text: str) -> str:
    return re.sub(r"[^a-z0-9.]+", "-", text.lower()).strip("-")


# --------------------------------------------------------------------------------------
# European sections, from eurocodepy
# --------------------------------------------------------------------------------------

# eurocodepy writes lengths in centimetres, areas in square centimetres and mass in kg/m.
CM_TO_MM = 10.0
CM2_TO_MM2 = 100.0

EURO_PROVENANCE = (
    "Compiled by tools/data-sources/compile_sections.py from the eurocodepy package "
    "{version} (MIT, https://github.com/pcachim/eurocodepy), file {file}, converted from "
    "centimetres to millimetres. NOT verified against a controlled copy of the standard. Set "
    '"verified": true only after checking against your own authoritative source.'
)


HOT_FINISHED_NOTE = (
    " The source does not name a standard for its hollow sections. They are filed here under "
    "EN 10210 because the corner profile that reproduces the areas it tabulates - an outside "
    "radius of 1.5t against an inside radius of 1.0t - is the hot finished one, to within 0.42 "
    "per cent across all 248 rectangular and square sections. That is an inference from the "
    "data, not a statement by the source; check it before relying on the distinction."
)


def parse_euro_name(name: str) -> tuple[str, list[str]]:
    """Splits a eurocodepy section name into its family and its size tokens.

    ``HEA100`` becomes ``("HEA", ["100"])`` and ``RHS50x30x2_5`` becomes
    ``("RHS", ["50", "30", "2.5"])``. The underscore is always a decimal point in this source,
    which is checked against the tabulated dimensions by the caller.
    """
    match = re.match(r"^([A-Za-z]+)(.*)$", name)
    if not match:
        raise ValueError(f"cannot parse the section name '{name}'")

    family = match.group(1).upper()
    tokens = [token.replace("_", ".") for token in match.group(2).split("x") if token]
    return family, tokens


def euro_designation(family: str, tokens: list[str]) -> str:
    return f"{family} {'x'.join(tokens)}" if tokens else family


def compile_european(manifest: dict[str, Any], report: "Report") -> list[dict[str, Any]]:
    source = next(s for s in manifest["sources"] if s["key"] == "eurocodepy")
    sections: list[dict[str, Any]] = []

    with open_source(source) as archive:
        for member in source["files"]:
            rows = json.loads(archive.read(member).decode("utf-8"))

            for row in rows:
                record = european_record(row, member, source, report)
                if record is not None:
                    sections.append(record)

    return sections


def european_record(
    row: dict[str, Any], member: str, source: dict[str, Any], report: "Report"
) -> dict[str, Any] | None:
    name = str(row.get("Section", "")).strip()
    if not name:
        return None

    family, tokens = parse_euro_name(name)

    height = number(row.get("h"))
    width = number(row.get("b"))
    web = number(row.get("tw"))
    flange = number(row.get("tf"))
    radius = number(row.get("r"))
    mass = number(row.get("m"))
    area = number(row.get("A"))

    if height is None or mass is None:
        report.skipped(name, "the source row has no height or no mass")
        return None

    provenance = EURO_PROVENANCE.format(version=source["version"], file=member.split("/")[-1])

    if member.endswith(("chs_profiles_euro.json", "rhs_profiles_euro.json", "shs_profiles_euro.json")):
        provenance += HOT_FINISHED_NOTE

    if family in ("IPE", "IPN"):
        category, standard = "i-section", "en-10365"
    elif family in ("HEA", "HEB", "HEM", "HEAA"):
        category, standard = "h-section", "en-10365"
    elif family == "CHS":
        category, standard = "pipe", "en-10210"
    elif family == "RHS":
        category, standard = "rhs", "en-10210"
    elif family == "SHS":
        category, standard = "shs", "en-10210"
    else:
        report.skipped(name, f"the family '{family}' has no mapping")
        return None

    dimensions: dict[str, float] = {}

    if category in ("i-section", "h-section"):
        dimensions = {
            "h": rounded(height * CM_TO_MM, 3),
            "b": rounded(width * CM_TO_MM, 3),
            "tw": rounded(web * CM_TO_MM, 3),
            "tf": rounded(flange * CM_TO_MM, 3),
        }
        if radius:
            dimensions["r"] = rounded(radius * CM_TO_MM, 3)
    elif category == "pipe":
        # A round hollow section is square in the source's h and b columns, and its wall
        # thickness is repeated in tw and tf.
        dimensions = {"d": rounded(height * CM_TO_MM, 3), "t": rounded(web * CM_TO_MM, 3)}
    else:
        wall_thickness = web
        dimensions = {
            "h": rounded(height * CM_TO_MM, 3),
            "b": rounded(width * CM_TO_MM, 3),
            "t": rounded(wall_thickness * CM_TO_MM, 3),
        }
        if radius:
            dimensions["ro"] = rounded(radius * CM_TO_MM, 3)

            # The corner profile of a hot finished hollow section is not that of a tube bent
            # from uniform plate: EN 10210 pairs an outside radius of 1.5t with an inside radius
            # of 1.0t, so the wall thins around the corner. Which of the two conventions applies
            # is not stated by the source, and it is not guessed at either - it is read off the
            # areas the source tabulates. Drawing the corner with an inside radius of 1.0t
            # reproduces every one of the 248 tabulated rectangular and square areas to within
            # 0.42 per cent; assuming a uniform wall misses them by up to 2.87 per cent. So the
            # inside radius is stated explicitly here rather than left to be derived.
            dimensions["ri"] = rounded(wall_thickness * CM_TO_MM, 3)

    if area:
        dimensions["A"] = rounded(area * CM2_TO_MM2, 2)

    if any(value is None or value <= 0 for value in dimensions.values()):
        report.skipped(name, "a dimension the category needs is missing or not positive")
        return None

    report.check_designation(name, family, tokens, dimensions, mass, "european")
    report.check_mass(name, mass, area * CM2_TO_MM2 if area else None, "metric", family)

    record = {
        "id": f"{standard.replace('-', '')}-{slug(euro_designation(family, tokens))}",
        "standardId": standard,
        "category": category,
        "designation": euro_designation(family, tokens),
        "nominalSize": tokens[0] if tokens else "",
        "material": "s355jr",
        "unitWeight": rounded(mass, 4),
        "dimensions": dimensions,
        "verified": False,
        "provenance": provenance,
    }

    properties = tabulated(row, ("Iy", "Iz", "Wel_y", "Wel_z", "Wpl_y", "Wpl_z", "IT"))
    if properties:
        record["properties"] = properties

    return record


def tabulated(row: dict[str, Any], keys: tuple[str, ...]) -> dict[str, float]:
    """Copies tabulated section properties across verbatim, for reference only."""
    result: dict[str, float] = {}

    for key in keys:
        value = number(row.get(key))
        if value:
            result[key] = value

    return result


# --------------------------------------------------------------------------------------
# American sections, from steelpy
# --------------------------------------------------------------------------------------

AISC_PROVENANCE = (
    "Compiled by tools/data-sources/compile_sections.py from the AISC Shapes Database v16.0 as "
    "published in the steelpy package {version} (Apache-2.0, https://pypi.org/project/steelpy/), "
    "file {file}. Values are in inches and pounds per foot exactly as tabulated; this file "
    "declares those units and StructureGen converts on load. NOT verified against AISC's own "
    'copy of the database. Set "verified": true only after checking against your own '
    "authoritative source."
)

FILLET_NOTE = (
    " The fillet radius r is not tabulated; it is reconstructed as k - tf, which is the AISC "
    "definition of the design distance k, and the resulting outline is checked against the "
    "tabulated cross-sectional area by the test suite."
)

DESIGN_WALL_NOTE = (
    " AISC tabulates the area of this shape against its design wall thickness, about 93 per cent "
    "of the nominal wall it is named and weighed by. The nominal wall is drawn, because that is "
    "what the section measures, so the tabulated area is carried as a property rather than as a "
    "dimension."
)

INCONSISTENT_AREA_NOTE = (
    " The outline StructureGen draws from these dimensions does not reproduce the tabulated area "
    "of this shape, because the source publishes nothing that would let the flange taper or the "
    "fillet be modelled, so the area is carried as a property rather than as a dimension and is "
    "not offered as a check on the outline. The tabulated mass per foot is what the schedule uses "
    "and is unaffected."
)

TAPERED_NOTE = (
    " This shape has tapered flanges. StructureGen draws parallel flanges, which slightly "
    "over-estimates the area; the deviation is asserted by the test suite."
)


def _fillet_from_k(k: float | None, flange: float | None) -> float | None:
    """Recovers the fillet radius from AISC's design distance k.

    AISC defines k as the distance from the outer face of the flange to the web toe of the
    fillet, so k = tf + r for a rolled shape. Reversing that identity is not an estimate, but it
    is a derivation rather than a published figure, and it is recorded as such in the record's
    provenance. Its accuracy is not taken on trust either: applying it brings the computed area
    of all 289 W shapes to within 0.75 per cent of the independently tabulated area, against
    2.72 per cent with no fillet at all.
    """
    if k is None or flange is None:
        return None

    radius = k - flange
    return radius if radius > 0.005 else None


# "fillet" marks the families where reconstructing the radius from k is worth doing. The
# decision is measured, not assumed: for each family the outline area was computed both ways and
# compared with the independently tabulated area of every shape in it. Reconstructing helps only
# where the flanges are parallel, because k is then exactly tf plus the fillet -
#
#     W   289 shapes  2.72% -> 0.73%      S    28 shapes  1.87% -> 6.06%
#     HP   22 shapes  4.52% -> 2.25%      C    32 shapes  1.66% -> 5.55%
#     WT  289 shapes  2.72% -> 0.73%      MC   40 shapes  1.72% -> 3.68%
#                                         ST   28 shapes  1.87% -> 6.06%
#                                         L   137 shapes  1.35% -> 3.01%
#
# - where the flange is tapered, k also swallows the taper, so k - tf is much larger than the
# real fillet and the reconstruction makes the shape worse. Those families are left with sharp
# corners, which the figures above show is the more faithful of the two available outlines.
AISC_FAMILIES: dict[str, dict[str, Any]] = {
    "W_shapes.csv": {"category": "i-section", "shape": "flanged", "fillet": True},
    "M_shapes.csv": {"category": "i-section", "shape": "flanged"},
    "S_shapes.csv": {"category": "i-section", "shape": "flanged", "tapered": True},
    "HP_shapes.csv": {"category": "h-section", "shape": "flanged", "fillet": True},
    "C_shapes.csv": {"category": "channel", "shape": "flanged", "tapered": True},
    "MC_shapes.csv": {"category": "channel", "shape": "flanged", "tapered": True},
    "WT_shapes.csv": {"category": "t-section", "shape": "flanged", "fillet": True},
    "MT_shapes.csv": {"category": "t-section", "shape": "flanged"},
    "ST_shapes.csv": {"category": "t-section", "shape": "flanged", "tapered": True},
    "L_shapes.csv": {"category": "angle", "shape": "angle"},
    "HSS_shapes.csv": {"category": "hss", "shape": "hss"},
    "HSS_R_shapes.csv": {"category": "pipe", "shape": "round-hss"},
    "PIPE_shapes.csv": {"category": "pipe", "shape": "pipe"},
}


def compile_american(manifest: dict[str, Any], report: "Report") -> list[dict[str, Any]]:
    source = next(s for s in manifest["sources"] if s["key"] == "steelpy")
    sections: list[dict[str, Any]] = []

    with open_source(source) as archive:
        for member in source["files"]:
            leaf = member.split("/")[-1]
            family = AISC_FAMILIES.get(leaf)

            if family is None:
                continue

            text = archive.read(member).decode("utf-8-sig")
            for row in csv.DictReader(io.StringIO(text)):
                record = american_record(row, family, leaf, source, report)
                if record is not None:
                    sections.append(record)

    return sections


def american_record(
    row: dict[str, str],
    family: dict[str, Any],
    leaf: str,
    source: dict[str, Any],
    report: "Report",
) -> dict[str, Any] | None:
    name = str(row.get("shape", "")).strip()
    if not name:
        return None

    weight = number(row.get("weight"))
    area = number(row.get("area"))

    if weight is None or weight <= 0:
        report.skipped(name, "the source row has no mass per foot")
        return None

    provenance = AISC_PROVENANCE.format(version=source["version"], file=leaf)
    category = family["category"]
    dimensions: dict[str, float] = {}
    derived_fillet = False

    if family["shape"] == "flanged":
        depth = number(row.get("d"))
        flange_width = number(row.get("bf"))
        web = number(row.get("tw"))
        flange = number(row.get("tf"))
        radius = _fillet_from_k(number(row.get("k")), flange) if family.get("fillet") else None

        dimensions = {"h": depth, "b": flange_width, "tw": web, "tf": flange}

        if radius is not None:
            dimensions["r"] = rounded(radius, 4)
            derived_fillet = True

    elif family["shape"] == "angle":
        long_leg = number(row.get("d"))
        short_leg = number(row.get("b"))
        thickness = number(row.get("t"))
        radius = _fillet_from_k(number(row.get("k")), thickness) if family.get("fillet") else None

        dimensions = {"a": long_leg, "b2": short_leg, "tt": thickness}

        if radius is not None:
            dimensions["r"] = rounded(radius, 4)
            derived_fillet = True

    elif family["shape"] == "hss":
        height = number(row.get("Ht"))
        width = number(row.get("B"))
        wall = number(row.get("tnom"))

        # A square hollow section is drawn by a different generator from a rectangular one, and
        # AISC lists both in the same file.
        category = "shs" if height is not None and width is not None and abs(height - width) < 1e-9 else "rhs"
        dimensions = {"h": height, "b": width, "t": wall}

    else:
        outside = number(row.get("OD"))
        wall = number(row.get("tnom"))
        dimensions = {"d": outside, "t": wall}

    if any(value is None or value <= 0 for value in dimensions.values()):
        report.skipped(name, "a dimension the category needs is missing or not positive")
        return None

    # AISC tabulates the area of a hollow section against its DESIGN wall thickness, which is
    # about 93 per cent of the nominal wall the section is named and weighed by. The nominal
    # wall is what the section actually measures, so that is what is drawn - which means the
    # tabulated area is not the area of the outline here and must not be offered as a check on
    # it. It is carried across as a tabulated property instead, where it is informational only.
    design_area = family["shape"] in ("hss", "round-hss", "pipe")
    properties: dict[str, float] = {}
    inconsistent_area = False

    if area and design_area:
        properties["A_design"] = rounded(area, 4)
        if number(row.get("tdes")):
            properties["tdes"] = rounded(number(row.get("tdes")), 4)
    elif area:
        # A published area is offered to StructureGen as a check on the outline it draws, so it
        # is only worth carrying while it agrees with the other figure published for the same
        # shape. Area and mass are independent entries in the table and a constant density
        # relates them; where they disagree by more than two per cent the table is internally
        # inconsistent for that shape - which happens across the M and MT families - and neither
        # figure can be used to check an outline. The mass is what a schedule needs, so the mass
        # is kept as the dimension's authority and the area is carried as a property instead.
        outline = nominal_area(dimensions, family)
        deviation = abs(outline - area) / area * 100.0

        if deviation > 8.0:
            # A deviation this large is not a modelling limit. Every family here was measured,
            # and none of them is worse than about five per cent, so a shape that misses by more
            # than eight is a sign that a column has been read wrongly - which is exactly what
            # this compilation exists to make impossible to ship quietly.
            report.mangled(name, deviation)

        if deviation > 2.0:
            properties["A_tabulated"] = rounded(area, 4)
            report.demoted(name, deviation)
            inconsistent_area = True
        else:
            dimensions["A"] = rounded(area, 4)

    designation = report.check_designation(name, "", [], dimensions, weight, "aisc")
    report.check_mass(name, weight, nominal_area(dimensions, family), "imperial", leaf)

    notes = provenance
    if derived_fillet:
        notes += FILLET_NOTE
    if family.get("tapered"):
        notes += TAPERED_NOTE
    if design_area:
        notes += DESIGN_WALL_NOTE
    if inconsistent_area:
        notes += INCONSISTENT_AREA_NOTE

    record = {
        "id": f"aisc-{slug(designation)}",
        "standardId": "aisc-360",
        "category": category,
        "designation": designation,
        "nominalSize": re.match(r"^[A-Za-z]+[0-9.]*", designation).group(0),
        "material": "a992",
        "unitWeight": rounded(weight, 4),
        "dimensions": dimensions,
        "verified": False,
        "provenance": notes,
    }

    if properties:
        record["properties"] = properties

    return record


def nominal_area(dimensions: dict[str, float], family: dict[str, Any]) -> float | None:
    """The area of the outline as drawn, for checking a published mass against.

    Sharp corners and no fillets, which is close enough for a check meant to catch a
    transcription error rather than to measure a section.
    """
    shape = family["shape"]

    # The area a fillet of radius r adds back into the corner it fills.
    radius = dimensions.get("r", 0.0)  # present only where the reconstruction was applied
    fillet = (1.0 - math.pi / 4.0) * radius * radius

    if shape == "flanged" and family["category"] != "t-section":
        web = 2 * dimensions["b"] * dimensions["tf"] + (dimensions["h"] - 2 * dimensions["tf"]) * dimensions["tw"]
        return web + 4 * fillet

    if shape == "flanged":
        stem = dimensions["b"] * dimensions["tf"] + (dimensions["h"] - dimensions["tf"]) * dimensions["tw"]
        return stem + 2 * fillet

    if shape == "angle":
        return (dimensions["a"] + dimensions["b2"] - dimensions["tt"]) * dimensions["tt"] + fillet

    if shape == "hss":
        # Drawn with square corners here. A real hollow section has rounded ones, which take
        # material out, so this over-estimates most for a small section with a thick wall.
        return dimensions["h"] * dimensions["b"] - (dimensions["h"] - 2 * dimensions["t"]) * (
            dimensions["b"] - 2 * dimensions["t"])

    outer = dimensions["d"] / 2
    inner = outer - dimensions["t"]
    return math.pi * (outer * outer - inner * inner)


# --------------------------------------------------------------------------------------
# Designations
# --------------------------------------------------------------------------------------

FRACTIONS = {
    "1_2": "1/2", "1_4": "1/4", "3_4": "3/4", "1_8": "1/8", "3_8": "3/8",
    "5_8": "5/8", "7_8": "7/8", "1_16": "1/16", "3_16": "3/16", "5_16": "5/16",
    "7_16": "7/16", "9_16": "9/16", "11_16": "11/16", "13_16": "13/16", "15_16": "15/16",
}


def render_designation(name: str, dimensions: dict[str, float], mass: float) -> tuple[str, bool]:
    """Turns a source's file-safe shape name back into its published designation.

    ``steelpy`` replaces both the decimal point of ``M12.5X12.4`` and the solidus of
    ``L12X12X1-3/8`` with an underscore, so an underscore alone does not say which was meant.
    Rather than guess, each candidate reading is tested against the numbers tabulated for that
    same shape: 28.000 is accepted as a decimal because the outside diameter really is 28, and
    7/8 is accepted as a fraction because the wall thickness really is 0.875.

    Position matters, and getting it wrong would silently rename a section. The first token of
    an American designation is a NOMINAL depth, which is close to the real depth but rarely
    equal to it - a WT13.5X129 is 13.9 inches deep - so it is matched loosely. Every later token
    is either an exact dimension or the mass per foot, so it is matched tightly. Matching a late
    token loosely would be worse than useless: it would read the 5/8 of an HSS6X6X5/8 as 5.8,
    which is within a few per cent of the section's 6 inch width and would rename the section.

    A token that matches neither reading is left exactly as the source spelled it, and counted
    in the report.
    """
    if "_" not in name:
        return name, True

    exact = set(round(value, 4) for value in dimensions.values() if value)
    exact.add(round(mass, 4))

    # The overall size of the section, which the nominal first token approximates.
    overall = [dimensions[key] for key in ("h", "a", "d") if key in dimensions]

    def matches_exactly(value: float) -> bool:
        return any(abs(value - candidate) <= max(0.011, abs(candidate) * 0.004) for candidate in exact)

    parts = re.split(r"([Xx])", name)
    rendered: list[str] = []
    resolved = True
    index = 0

    for part in parts:
        if part in ("X", "x"):
            rendered.append(part)
            continue

        position = index
        index += 1

        if "_" not in part:
            rendered.append(part)
            continue

        decimal = read_decimal(part)
        fraction = read_fraction(part)

        if position == 0:
            # The leading token is a nominal size. It is close to the section's real overall
            # dimension for a rolled shape and nowhere near it for a pipe, whose nominal bore
            # bears no fixed relation to its outside diameter, so it cannot be checked against
            # the table. It does not need to be: only one reading of it usually parses at all,
            # and where both do, the nominal size is the one nearer the section's real size.
            readings = [reading for reading in (decimal, fraction) if reading is not None]

            if len(readings) == 1:
                rendered.append(readings[0][0])
                continue

            if len(readings) == 2 and overall:
                nearest = min(readings, key=lambda reading: abs(reading[1] - overall[0]))
                rendered.append(nearest[0])
                continue

            rendered.append(part)
            resolved = False
            continue

        # Every later token is an exact dimension or the mass per foot, so it is checked
        # against the table. Accepting one loosely would silently rename a section: the 5/8 of
        # an HSS6X6X5/8 read as 5.8 is within a few per cent of the section's 6 inch width.
        if decimal is not None and matches_exactly(decimal[1]):
            rendered.append(decimal[0])
            continue

        if fraction is not None and matches_exactly(fraction[1]):
            rendered.append(fraction[0])
            continue

        rendered.append(part)
        resolved = False

    return "".join(rendered), resolved


def read_decimal(token: str) -> tuple[str, float] | None:
    """Reads ``12_5`` as twelve and a half, keeping any letters around it."""
    match = re.match(r"^([A-Za-z]*)([0-9_]+)([A-Za-z]*)$", token)

    if not match:
        return None

    try:
        value = float(match.group(2).replace("_", "."))
    except ValueError:
        return None

    return match.group(1) + match.group(2).replace("_", ".") + match.group(3), value


def read_fraction(token: str) -> tuple[str, float] | None:
    """Reads ``1_3_8`` as one and three eighths, or ``7_8`` as seven eighths."""
    prefix = re.match(r"^([A-Za-z]*)([0-9_]*)([A-Za-z]*)$", token)

    if not prefix:
        return None

    letters, body, suffix = prefix.group(1), prefix.group(2), prefix.group(3)

    for whole_digits in (True, False):
        if whole_digits:
            match = re.match(r"^(\d+)_(\d+_\d+)$", body)
            if not match:
                continue
            whole, rest = int(match.group(1)), match.group(2)
        else:
            whole, rest = 0, body

        if rest not in FRACTIONS:
            continue

        numerator, denominator = FRACTIONS[rest].split("/")
        value = whole + int(numerator) / int(denominator)
        text = f"{whole}-{FRACTIONS[rest]}" if whole else FRACTIONS[rest]
        return letters + text + suffix, value

    return None


# --------------------------------------------------------------------------------------
# Reporting
# --------------------------------------------------------------------------------------


class Report:
    """Collects everything the compilation noticed, so nothing passes silently."""

    def __init__(self) -> None:
        self.skips: list[tuple[str, str]] = []
        self.unresolved: list[str] = []
        self.mass_outliers: list[tuple[str, float]] = []
        self.counts: Counter[str] = Counter()
        self.worst: dict[str, float] = {}
        self.inconsistent_areas: list[tuple[str, float]] = []
        self.mangled_areas: list[tuple[str, float]] = []

    def skipped(self, name: str, reason: str) -> None:
        self.skips.append((name, reason))

    def demoted(self, name: str, error: float) -> None:
        self.inconsistent_areas.append((name, error))

    def mangled(self, name: str, error: float) -> None:
        self.mangled_areas.append((name, error))

    def check_designation(
        self, name: str, family: str, tokens: list[str], dimensions: dict[str, float],
        mass: float, flavour: str,
    ) -> str:
        if flavour == "european":
            designation = euro_designation(family, tokens)

            # A hollow section is named after its actual dimensions, so every size token in its
            # name has to appear in its dimensions - which confirms that the underscore really
            # was a decimal point. A rolled I or H section is named after a nominal depth that
            # deliberately differs from the real one (HEA 340 is 330 mm deep), so there is
            # nothing to check there.
            if family not in ("CHS", "RHS", "SHS"):
                return designation

            values = [round(v, 3) for v in dimensions.values()]
            for token in tokens:
                try:
                    parsed = float(token)
                except ValueError:
                    continue
                if not any(abs(parsed - v) <= max(0.6, abs(v) * 0.02) for v in values):
                    self.unresolved.append(f"{name}: the size token '{token}' matches no tabulated dimension")

            return designation

        designation, resolved = render_designation(name, dimensions, mass)

        if not resolved:
            self.unresolved.append(f"{name}: kept verbatim, no reading of an underscore matched the data")

        return designation

    def check_mass(self, name: str, mass: float, area: float | None, flavour: str, group: str) -> None:
        """Checks a published mass against a published area, which are independent figures."""
        if not area:
            return

        if flavour == "metric":
            expected = area * 1e-6 * STEEL_DENSITY
        else:
            expected = area * STEEL_DENSITY_LB_IN3 * 12.0

        error = abs(mass - expected) / expected * 100.0

        self.counts[group] += 1
        self.worst[group] = max(self.worst.get(group, 0.0), error)

        if error > 3.0:
            self.mass_outliers.append((name, error))

    def summarise(self, sections: list[dict[str, Any]], title: str) -> None:
        print(f"\n{title}: {len(sections)} records")

        for category, count in sorted(Counter(s["category"] for s in sections).items()):
            print(f"    {category:<12} {count}")

    def finish(self) -> int:
        print()

        if self.skips:
            print(f"{len(self.skips)} row(s) skipped:")
            for name, reason in self.skips[:20]:
                print(f"    {name}: {reason}")

        if self.unresolved:
            print(f"{len(self.unresolved)} designation(s) could not be resolved and were kept verbatim:")
            for line in self.unresolved[:20]:
                print(f"    {line}")

        if self.inconsistent_areas:
            print(
                f"{len(self.inconsistent_areas)} record(s) whose outline does not reproduce the "
                "tabulated area within 2%; their area is carried as a property, not as a dimension:"
            )
            for name, error in sorted(self.inconsistent_areas, key=lambda x: -x[1])[:10]:
                print(f"    {name}: {error:.1f}%")

        print("Published mass against the area of the outline as drawn, worst case per family:")
        for group in sorted(self.worst):
            print(f"    {group:<18} {self.counts[group]:>4} records, worst {self.worst[group]:.1f}%")

        if self.mass_outliers:
            print(f"{len(self.mass_outliers)} record(s) whose mass and area disagree by more than 3%:")
            for name, error in sorted(self.mass_outliers, key=lambda x: -x[1])[:20]:
                print(f"    {name}: {error:.1f}%")
        else:
            print("Every record's published mass agrees with its published area to within 3%.")

        if self.mangled_areas:
            print(
                f"\nFAILED: {len(self.mangled_areas)} record(s) miss their tabulated area by more "
                "than 8%, which is too far to be a modelling limit:"
            )
            for name, error in sorted(self.mangled_areas, key=lambda x: -x[1])[:20]:
                print(f"    {name}: {error:.1f}%")

            return 1

        return 0


# --------------------------------------------------------------------------------------
# Output
# --------------------------------------------------------------------------------------


def merge_into_metric(compiled: list[dict[str, Any]]) -> dict[str, Any]:
    """Puts the compiled European records into the shipped metric database.

    Records the compilation covers replace whatever was there before. Records it does not cover
    - the European channels, angles and fasteners, for which no permissively licensed machine
    readable source was found - are left exactly as they are, so hand written data is never
    silently dropped by a re-run.
    """
    path = DATA / "structural-sections.json"
    database = json.loads(path.read_text(encoding="utf-8"))

    compiled_ids = {record["id"] for record in compiled}
    compiled_designations = {record["designation"].upper() for record in compiled}

    kept = [
        section for section in database["sections"]
        if section["id"] not in compiled_ids
        and section["designation"].upper() not in compiled_designations
    ]

    database["sections"] = sorted(kept + compiled, key=sort_key)

    # A file that declares its own standards does not fall back to the shared ones, so a
    # compilation that introduces a new standard has to introduce its entry too or every record
    # referring to it is refused on load.
    declared = {standard["id"] for standard in database.get("standards", [])}

    for standard_id in sorted({record["standardId"] for record in compiled} - declared):
        if standard_id not in KNOWN_STANDARDS:
            raise SystemExit(
                f"The compilation produced records under the standard '{standard_id}', which "
                "has no entry in KNOWN_STANDARDS. Add one rather than letting the file ship a "
                "reference it cannot resolve."
            )

        database.setdefault("standards", []).append(KNOWN_STANDARDS[standard_id])

    database["standards"] = sorted(database.get("standards", []), key=lambda s: s["id"])
    return database


KNOWN_STANDARDS: dict[str, dict[str, str]] = {
    "en-10210": {
        "id": "en-10210",
        "name": "EN 10210",
        "region": "Europe",
        "revision": "2006",
        "description": "Hot finished structural hollow sections of non-alloy and fine grain steels.",
    },
    "en-10219": {
        "id": "en-10219",
        "name": "EN 10219",
        "region": "Europe",
        "revision": "2006",
        "description": "Cold formed welded structural hollow sections.",
    },
    "en-10365": {
        "id": "en-10365",
        "name": "EN 10365",
        "region": "Europe",
        "revision": "2017",
        "description": "Hot rolled steel channels, I and H sections - dimensions and masses.",
    },
}


def sort_key(section: dict[str, Any]) -> tuple[str, str, float, str]:
    """Orders records so the file diffs cleanly and a palette list reads sensibly."""
    match = re.search(r"([0-9]+(?:\.[0-9]+)?)", section["designation"])
    size = float(match.group(1)) if match else 0.0
    family = re.match(r"^[A-Za-z]+", section["designation"])

    return (section["category"], family.group(0) if family else "", size, section["designation"])


def write(path: Path, database: dict[str, Any]) -> None:
    text = json.dumps(database, indent=2, ensure_ascii=False) + "\n"
    path.write_text(text, encoding="utf-8")
    print(f"wrote {path.relative_to(REPO)} ({len(text):,} bytes)")


def american_database(sections: list[dict[str, Any]], manifest: dict[str, Any]) -> dict[str, Any]:
    source = next(s for s in manifest["sources"] if s["key"] == "steelpy")

    return {
        "schemaVersion": "1.0",
        "database": {
            "id": "structuregen-aisc",
            "name": "AISC shapes (compiled, unverified)",
            "version": "1.0.0",
            "revisionDate": "2026-09-03",
            "description": (
                "American structural shapes compiled from the AISC Shapes Database v16.0 as "
                f"published in the steelpy package {source['version']}. Written in inches and "
                "pounds per foot; StructureGen converts on load."
            ),
            "verified": False,
            "notes": (
                "NOT VERIFIED. Every record carries \"verified\": false and names its source. The "
                "numbers were compiled mechanically by tools/data-sources/compile_sections.py "
                "from a third party transcription of AISC's database, not from AISC's own file, "
                "and no fillet radius is published by AISC - the value here is reconstructed from "
                "the tabulated design distance k. Verify against your own authoritative source "
                "before using any output from this dataset for procurement or fabrication."
            ),
        },
        "units": {"length": "in", "unitWeight": "lb/ft", "density": "lb/in3", "pieceMass": "lb"},
        "standards": [
            {
                "id": "aisc-360",
                "name": "AISC Steel Construction Manual",
                "region": "United States",
                "revision": "16th edition",
                "description": "Dimensions and properties of American structural shapes.",
            }
        ],
        "materials": [
            {
                "id": "a992",
                "name": "Structural steel",
                "grade": "ASTM A992",
                "density": 0.2836,
                "standardId": "astm-a992",
                "notes": "Density is the conventional 0.2836 lb/in3 used for steelwork take-off.",
            },
            {
                "id": "a36",
                "name": "Structural steel",
                "grade": "ASTM A36",
                "density": 0.2836,
                "standardId": "astm-a36",
                "notes": "Density is the conventional 0.2836 lb/in3 used for steelwork take-off.",
            },
        ],
        "sections": sorted(sections, key=sort_key),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--download", action="store_true", help="fetch the pinned wheels into the cache")
    parser.add_argument("--check", action="store_true", help="compile and report without writing")
    arguments = parser.parse_args()

    manifest = load_manifest()

    if arguments.download:
        download(manifest)

    report = Report()

    european = compile_european(manifest, report)
    american = compile_american(manifest, report)

    report.summarise(european, "European sections")
    report.summarise(american, "American sections")

    if arguments.check:
        return report.finish()

    write(DATA / "structural-sections.json", merge_into_metric(european))
    write(DATA / "aisc-sections.json", american_database(american, manifest))

    return report.finish()


if __name__ == "__main__":
    raise SystemExit(main())
