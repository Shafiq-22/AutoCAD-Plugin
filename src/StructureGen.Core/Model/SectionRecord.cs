using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Geometry;

namespace StructureGen.Core.Model
{
    /// <summary>
    /// One standard profile as it appears in the structural database.
    /// </summary>
    /// <remarks>
    /// A record is immutable. Nothing in the application may edit a standard profile in place;
    /// a user who needs a non-standard size works from a derived <see cref="ElementDefinition"/>
    /// in custom mode, which leaves the source record untouched and marks the element as
    /// non-conforming.
    /// </remarks>
    public sealed class SectionRecord
    {
        /// <summary>Initialises a new instance of the <see cref="SectionRecord"/> class.</summary>
        /// <param name="id">Unique record identifier within the database.</param>
        /// <param name="standardId">Identifier of the owning standard.</param>
        /// <param name="categoryId">Element category identifier.</param>
        /// <param name="designation">The profile designation, for example <c>IPE 300</c>.</param>
        /// <param name="dimensions">Cross-sectional dimensions in millimetres, keyed by <see cref="DimensionKeys"/>.</param>
        /// <param name="unitWeightKgPerM">Tabulated mass per metre, when the standard supplies one.</param>
        /// <param name="pieceMassKg">Tabulated mass per piece, used for fasteners.</param>
        /// <param name="nominalSize">Optional nominal size label, for example <c>M16</c> or <c>DN100</c>.</param>
        /// <param name="material">Optional default material name.</param>
        /// <param name="grade">Optional default material grade.</param>
        /// <param name="properties">Optional additional tabulated properties, informational only.</param>
        /// <param name="verified">Whether the values were checked against an authoritative source by the user.</param>
        /// <param name="provenance">Free text describing where the values came from.</param>
        /// <param name="notes">Free text notes.</param>
        /// <param name="outline">
        /// An explicit cross-section outline, in millimetres. Supplying one lets a database
        /// describe a profile the built-in generators do not cover, without any code change.
        /// </param>
        public SectionRecord(
            string id,
            string standardId,
            string categoryId,
            string designation,
            IReadOnlyDictionary<string, decimal> dimensions,
            decimal? unitWeightKgPerM = null,
            decimal? pieceMassKg = null,
            string? nominalSize = null,
            string? material = null,
            string? grade = null,
            IReadOnlyDictionary<string, decimal>? properties = null,
            bool verified = false,
            string? provenance = null,
            string? notes = null,
            IReadOnlyList<ProfileVertex>? outline = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            StandardId = standardId ?? throw new ArgumentNullException(nameof(standardId));
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));
            Designation = designation ?? throw new ArgumentNullException(nameof(designation));
            Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
            UnitWeightKgPerM = unitWeightKgPerM;
            PieceMassKg = pieceMassKg;
            NominalSize = nominalSize;
            Material = material;
            Grade = grade;
            Properties = properties ?? new Dictionary<string, decimal>();
            Verified = verified;
            Provenance = provenance;
            Notes = notes;
            Outline = outline;
        }

        /// <summary>Gets the unique record identifier within the database.</summary>
        public string Id { get; }

        /// <summary>Gets the identifier of the owning standard.</summary>
        public string StandardId { get; }

        /// <summary>Gets the element category identifier.</summary>
        public string CategoryId { get; }

        /// <summary>Gets the profile designation, for example <c>IPE 300</c>.</summary>
        public string Designation { get; }

        /// <summary>Gets the optional nominal size label, for example <c>M16</c> or <c>DN100</c>.</summary>
        public string? NominalSize { get; }

        /// <summary>Gets the cross-sectional dimensions in millimetres, keyed by <see cref="DimensionKeys"/>.</summary>
        public IReadOnlyDictionary<string, decimal> Dimensions { get; }

        /// <summary>Gets additional tabulated properties such as second moments of area. Informational only.</summary>
        public IReadOnlyDictionary<string, decimal> Properties { get; }

        /// <summary>Gets the tabulated mass per metre, when the standard supplies one.</summary>
        public decimal? UnitWeightKgPerM { get; }

        /// <summary>Gets the tabulated mass per piece, used for fasteners.</summary>
        public decimal? PieceMassKg { get; }

        /// <summary>Gets the optional default material name.</summary>
        public string? Material { get; }

        /// <summary>Gets the optional default material grade.</summary>
        public string? Grade { get; }

        /// <summary>
        /// Gets a value indicating whether the numbers were checked against an authoritative
        /// source. Unverified records are flagged in the user interface, in the bill of
        /// materials and in the exported workbook.
        /// </summary>
        public bool Verified { get; }

        /// <summary>Gets free text describing where the values came from.</summary>
        public string? Provenance { get; }

        /// <summary>Gets free text notes.</summary>
        public string? Notes { get; }

        /// <summary>
        /// Gets an explicit cross-section outline in millimetres, when the database supplies one.
        /// </summary>
        /// <remarks>
        /// This is the escape hatch for a profile no built-in generator covers: a fabricated
        /// section, a cold-rolled shape, a proprietary product. The outline is extruded exactly as
        /// written, so the database alone can introduce a new shape.
        /// </remarks>
        public IReadOnlyList<ProfileVertex>? Outline { get; }

        /// <summary>Gets a value indicating whether this record carries an explicit outline.</summary>
        public bool HasOutline => Outline is { Count: >= 3 };

        /// <summary>Reads a dimension, returning <see langword="null"/> when it is absent.</summary>
        /// <param name="key">One of the <see cref="DimensionKeys"/> constants.</param>
        /// <returns>The value in millimetres, or <see langword="null"/>.</returns>
        public decimal? Dimension(string key) =>
            Dimensions.TryGetValue(key, out decimal value) ? value : null;

        /// <summary>Reads a dimension, throwing when it is absent.</summary>
        /// <param name="key">One of the <see cref="DimensionKeys"/> constants.</param>
        /// <returns>The value in millimetres.</returns>
        /// <exception cref="InvalidOperationException">The dimension is not present on this record.</exception>
        public decimal RequiredDimension(string key) =>
            Dimension(key) ?? throw new InvalidOperationException(
                $"Record '{Id}' ({Designation}) has no value for the required dimension '{key}'.");

        /// <summary>Produces a mutable copy of the dimensions, for use as the starting point of an element.</summary>
        /// <returns>A new dictionary holding the same values.</returns>
        public Dictionary<string, decimal> CopyDimensions() =>
            Dimensions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        /// <inheritdoc />
        public override string ToString() => $"{Designation} ({StandardId}/{CategoryId})";
    }
}
