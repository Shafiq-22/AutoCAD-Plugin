using System.Collections.Generic;

namespace StructureGen.Data.Json
{
    /// <summary>The root object of a structural database file.</summary>
    /// <remarks>
    /// These types mirror the JSON exactly and are deliberately tolerant: every member is nullable
    /// so that a malformed file produces a validation report naming the missing field, rather than
    /// a deserialisation exception the user cannot act on.
    /// </remarks>
    public sealed class DatabaseFileDto
    {
        /// <summary>Gets or sets the schema version of the file.</summary>
        public string? SchemaVersion { get; set; }

        /// <summary>Gets or sets the dataset identity block.</summary>
        public DatabaseInfoDto? Database { get; set; }

        /// <summary>Gets or sets the units the numbers in this file are expressed in.</summary>
        public UnitsDto? Units { get; set; }

        /// <summary>Gets or sets the standards defined in this file.</summary>
        public List<StandardDto>? Standards { get; set; }

        /// <summary>Gets or sets the materials defined in this file.</summary>
        public List<MaterialDto>? Materials { get; set; }

        /// <summary>Gets or sets the section records defined in this file.</summary>
        public List<SectionDto>? Sections { get; set; }
    }

    /// <summary>The dataset identity block.</summary>
    public sealed class DatabaseInfoDto
    {
        /// <summary>Gets or sets the stable dataset identifier.</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the display name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the dataset version.</summary>
        public string? Version { get; set; }

        /// <summary>Gets or sets the revision date.</summary>
        public string? RevisionDate { get; set; }

        /// <summary>Gets or sets the description.</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets a value indicating whether the publisher asserts the values are checked.</summary>
        public bool? Verified { get; set; }

        /// <summary>Gets or sets free text notes.</summary>
        public string? Notes { get; set; }
    }

    /// <summary>The units block.</summary>
    public sealed class UnitsDto
    {
        /// <summary>Gets or sets the token describing the unit of the dimension values.</summary>
        public string? Length { get; set; }

        /// <summary>Gets or sets the token describing the unit of the mass-per-length values.</summary>
        public string? UnitWeight { get; set; }

        /// <summary>Gets or sets the token describing the unit of the density values.</summary>
        public string? Density { get; set; }

        /// <summary>Gets or sets the token describing the unit of the per-piece mass values.</summary>
        public string? PieceMass { get; set; }
    }

    /// <summary>A standard as written in the file.</summary>
    public sealed class StandardDto
    {
        /// <summary>Gets or sets the identifier.</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the display name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the issuing region or body.</summary>
        public string? Region { get; set; }

        /// <summary>Gets or sets the revision or year.</summary>
        public string? Revision { get; set; }

        /// <summary>Gets or sets the description.</summary>
        public string? Description { get; set; }
    }

    /// <summary>A material as written in the file.</summary>
    public sealed class MaterialDto
    {
        /// <summary>Gets or sets the identifier.</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the display name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the grade designation.</summary>
        public string? Grade { get; set; }

        /// <summary>Gets or sets the density, expressed in the file's density unit.</summary>
        public decimal? Density { get; set; }

        /// <summary>Gets or sets the identifier of the standard the material comes from.</summary>
        public string? StandardId { get; set; }

        /// <summary>Gets or sets free text notes.</summary>
        public string? Notes { get; set; }
    }

    /// <summary>A section record as written in the file.</summary>
    public sealed class SectionDto
    {
        /// <summary>Gets or sets the identifier.</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the identifier of the owning standard.</summary>
        public string? StandardId { get; set; }

        /// <summary>Gets or sets the element category identifier.</summary>
        public string? Category { get; set; }

        /// <summary>Gets or sets the profile designation.</summary>
        public string? Designation { get; set; }

        /// <summary>Gets or sets the nominal size label.</summary>
        public string? NominalSize { get; set; }

        /// <summary>Gets or sets the default material name or identifier.</summary>
        public string? Material { get; set; }

        /// <summary>Gets or sets the default material grade.</summary>
        public string? Grade { get; set; }

        /// <summary>Gets or sets the mass per length, expressed in the file's unit-weight unit.</summary>
        public decimal? UnitWeight { get; set; }

        /// <summary>Gets or sets the mass per piece, expressed in the file's piece-mass unit.</summary>
        public decimal? PieceMass { get; set; }

        /// <summary>Gets or sets the cross-sectional dimensions, expressed in the file's length unit.</summary>
        public Dictionary<string, decimal>? Dimensions { get; set; }

        /// <summary>Gets or sets additional tabulated properties. Stored verbatim and never converted.</summary>
        public Dictionary<string, decimal>? Properties { get; set; }

        /// <summary>Gets or sets a value indicating whether the values were checked against an authoritative source.</summary>
        public bool? Verified { get; set; }

        /// <summary>Gets or sets free text describing where the values came from.</summary>
        public string? Provenance { get; set; }

        /// <summary>Gets or sets free text notes.</summary>
        public string? Notes { get; set; }

        /// <summary>Gets or sets an explicit cross-section outline, expressed in the file's length unit.</summary>
        public List<OutlinePointDto>? Outline { get; set; }
    }

    /// <summary>One vertex of an explicit cross-section outline.</summary>
    public sealed class OutlinePointDto
    {
        /// <summary>Gets or sets the X position.</summary>
        public decimal? X { get; set; }

        /// <summary>Gets or sets the Y position.</summary>
        public decimal? Y { get; set; }

        /// <summary>
        /// Gets or sets the bulge of the segment leaving this vertex: the tangent of a quarter of
        /// the arc's included angle, zero for a straight segment, positive anticlockwise.
        /// </summary>
        public decimal? Bulge { get; set; }
    }
}
