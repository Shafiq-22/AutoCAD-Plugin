"""A very small read-only XLSX reader, sufficient for reading published section tables.

Why not openpyxl
----------------
This runs in continuous integration on every push, where the compilation is re-run and its
output diffed against what is committed. Reaching for a third-party library to read four
columns out of a spreadsheet would mean either pinning and installing one on the runner, or
letting an unpinned parser sit between a published table and the numbers a drawing is built
from. Neither is worth it for what is a zip file containing XML.

What it handles
---------------
Values only: shared strings, inline strings, numbers, and the cell references that say which
column each value belongs to. Sparse rows are filled, because a blank cell in these tables is
meaningful - it is what separates a designation that starts a group from the rows under it.

What it does not handle
-----------------------
Formulas are read as their cached value, which is what the source workbooks store. Dates,
styles, merged cells and everything else are ignored: none of them carry information these
tables need. A file that needs more than this should be converted before it gets here.
"""

from __future__ import annotations

import re
import zipfile
from xml.etree import ElementTree

MAIN = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
RELS = "{http://schemas.openxmlformats.org/package/2006/relationships}"
DOC_RELS = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"


def column_index(reference: str) -> int:
    """Turns a cell reference such as ``AB12`` into a one-based column number."""
    letters = re.match(r"^([A-Z]+)", reference)

    if not letters:
        raise ValueError(f"'{reference}' is not a cell reference")

    index = 0
    for character in letters.group(1):
        index = index * 26 + (ord(character) - ord("A") + 1)

    return index


class Workbook:
    """An open workbook. Use it as a context manager."""

    def __init__(self, path: str) -> None:
        self._archive = zipfile.ZipFile(path)
        self._shared = self._read_shared_strings()
        self._sheets = self._read_sheet_index()

    def __enter__(self) -> "Workbook":
        return self

    def __exit__(self, *_: object) -> None:
        self.close()

    def close(self) -> None:
        self._archive.close()

    @property
    def sheet_names(self) -> list[str]:
        """The worksheet names, in the order the workbook lists them."""
        return list(self._sheets)

    def rows(self, sheet: str) -> list[list[str | float | None]]:
        """Reads a whole worksheet as a rectangular list of rows.

        Every row is padded to the width of the widest row, so a caller can index a column
        without checking the length of each row first.
        """
        if sheet not in self._sheets:
            raise KeyError(f"the workbook has no sheet named '{sheet}'")

        with self._archive.open(self._sheets[sheet]) as stream:
            tree = ElementTree.parse(stream)

        rows: list[list[str | float | None]] = []
        width = 0

        for element in tree.getroot().iter(f"{MAIN}row"):
            row: list[str | float | None] = []

            for cell in element.findall(f"{MAIN}c"):
                index = column_index(cell.get("r", "A1"))

                # A sparse row omits its empty cells entirely, and the reference is the only
                # thing that says where the next value actually belongs.
                while len(row) < index - 1:
                    row.append(None)

                row.append(self._value(cell))

            rows.append(row)
            width = max(width, len(row))

        for row in rows:
            row.extend([None] * (width - len(row)))

        return rows

    def _value(self, cell: ElementTree.Element) -> str | float | None:
        kind = cell.get("t")

        if kind == "inlineStr":
            text = "".join(node.text or "" for node in cell.iter(f"{MAIN}t"))
            return text or None

        node = cell.find(f"{MAIN}v")
        if node is None or node.text is None:
            return None

        if kind == "s":
            return self._shared[int(node.text)]

        if kind in ("str", "e"):
            return node.text

        try:
            return float(node.text)
        except ValueError:
            return node.text

    def _read_shared_strings(self) -> list[str]:
        if "xl/sharedStrings.xml" not in self._archive.namelist():
            return []

        with self._archive.open("xl/sharedStrings.xml") as stream:
            tree = ElementTree.parse(stream)

        # A shared string can be split across several runs, each with its own formatting, and
        # the text is the concatenation of all of them.
        return [
            "".join(node.text or "" for node in item.iter(f"{MAIN}t"))
            for item in tree.getroot().findall(f"{MAIN}si")
        ]

    def _read_sheet_index(self) -> dict[str, str]:
        with self._archive.open("xl/_rels/workbook.xml.rels") as stream:
            relationships = {
                relationship.get("Id"): relationship.get("Target", "")
                for relationship in ElementTree.parse(stream).getroot()
            }

        with self._archive.open("xl/workbook.xml") as stream:
            tree = ElementTree.parse(stream)

        sheets: dict[str, str] = {}

        for sheet in tree.getroot().iter(f"{MAIN}sheet"):
            target = relationships.get(sheet.get(f"{DOC_RELS}id"), "")
            target = target[1:] if target.startswith("/") else "xl/" + target.lstrip("/")
            sheets[sheet.get("name", "")] = target.replace("xl/xl/", "xl/")

        return sheets
