using System;
using System.Collections.Generic;

namespace StructureGen.Data.Import
{
    /// <summary>
    /// Describes how the columns of a supplied spreadsheet map onto the StructureGen schema.
    /// </summary>
    /// <remarks>
    /// This is the seam that makes the database genuinely replaceable. A user hands over a section
    /// table in whatever column order and naming their source happens to use; a mapping profile —
    /// a small JSON file, editable without recompiling anything — states which column is the
    /// designation, which is the web thickness and what units the file is in. Adding support for a
    /// new source table means writing a new mapping profile, not new code.
    /// </remarks>
    public sealed class MappingProfile
    {
        /// <summary>Gets or sets the profile name, used in reports.</summary>
        public string Name { get; set; } = "Unnamed mapping";

        /// <summary>Gets or sets an optional description of the source table this profile targets.</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets the standard identifier applied to every imported row.</summary>
        /// <remarks>Ignored when <see cref="StandardIdColumn"/> names a column.</remarks>
        public string? StandardId { get; set; }

        /// <summary>Gets or sets the column holding a per-row standard identifier.</summary>
        public string? StandardIdColumn { get; set; }

        /// <summary>Gets or sets the category identifier applied to every imported row.</summary>
        /// <remarks>Ignored when <see cref="CategoryColumn"/> names a column.</remarks>
        public string? Category { get; set; }

        /// <summary>Gets or sets the column holding a per-row category identifier.</summary>
        public string? CategoryColumn { get; set; }

        /// <summary>Gets or sets the column holding the profile designation. Required.</summary>
        public string? DesignationColumn { get; set; }

        /// <summary>
        /// Gets or sets the column holding the record identifier. When absent an identifier is
        /// derived from the standard, category and designation.
        /// </summary>
        public string? IdColumn { get; set; }

        /// <summary>Gets or sets the column holding the nominal size label.</summary>
        public string? NominalSizeColumn { get; set; }

        /// <summary>Gets or sets the column holding the mass per length.</summary>
        public string? UnitWeightColumn { get; set; }

        /// <summary>Gets or sets the column holding the mass per piece.</summary>
        public string? PieceMassColumn { get; set; }

        /// <summary>Gets or sets the column holding the material name.</summary>
        public string? MaterialColumn { get; set; }

        /// <summary>Gets or sets the material applied to every imported row when no column supplies one.</summary>
        public string? Material { get; set; }

        /// <summary>Gets or sets the column holding the material grade.</summary>
        public string? GradeColumn { get; set; }

        /// <summary>Gets or sets the grade applied to every imported row when no column supplies one.</summary>
        public string? Grade { get; set; }

        /// <summary>
        /// Gets or sets the mapping from StructureGen dimension key to source column name,
        /// for example <c>{ "h": "Depth", "tw": "Web thickness" }</c>.
        /// </summary>
        public Dictionary<string, string> Dimensions { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Gets or sets the mapping from additional property key to source column name.</summary>
        public Dictionary<string, string> Properties { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Gets or sets the length unit the source columns are expressed in.</summary>
        public string LengthUnit { get; set; } = "mm";

        /// <summary>Gets or sets the mass-per-length unit the source column is expressed in.</summary>
        public string UnitWeightUnit { get; set; } = "kg/m";

        /// <summary>Gets or sets the per-piece mass unit the source column is expressed in.</summary>
        public string PieceMassUnit { get; set; } = "kg";

        /// <summary>
        /// Gets or sets a value indicating whether imported rows are marked verified.
        /// </summary>
        /// <remarks>
        /// Defaults to <see langword="false"/> on purpose. A user who has checked their source
        /// against a controlled copy of the standard sets this to <see langword="true"/>
        /// deliberately; the importer never asserts it on their behalf.
        /// </remarks>
        public bool MarkVerified { get; set; }

        /// <summary>Gets or sets the provenance text stamped onto every imported record.</summary>
        public string? Provenance { get; set; }

        /// <summary>Gets or sets rows to skip before the header row.</summary>
        public int SkipRows { get; set; }

        /// <summary>
        /// Gets or sets the text that marks a cell as intentionally empty, for example <c>-</c>,
        /// which is common in published section tables.
        /// </summary>
        public List<string> BlankMarkers { get; set; } = new() { "-", "--", "n/a", "N/A", "NA" };
    }
}
