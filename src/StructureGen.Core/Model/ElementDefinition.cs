using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Geometry;

namespace StructureGen.Core.Model
{
    /// <summary>
    /// A fully specified element: which standard profile, at what length, how many, and where.
    /// </summary>
    /// <remarks>
    /// This is the unit of work that travels from the palette into the drawing and back out of it
    /// again. Everything needed to regenerate the geometry from scratch is present, which is what
    /// makes the generated blocks editable after the fact: <c>SGEDIT</c> reads the metadata off an
    /// object, rebuilds one of these, and hands it back to the same generator that made it.
    /// </remarks>
    public sealed class ElementDefinition
    {
        private readonly Dictionary<string, decimal> _dimensions;

        /// <summary>Initialises a new instance of the <see cref="ElementDefinition"/> class.</summary>
        /// <param name="elementId">Stable identity of this element, preserved across every edit.</param>
        /// <param name="categoryId">The element category identifier.</param>
        /// <param name="standardId">The identifier of the standard the profile comes from.</param>
        /// <param name="sectionRecordId">The database record identifier the element was created from.</param>
        /// <param name="designation">The profile designation.</param>
        /// <param name="dimensions">Effective cross-sectional dimensions in millimetres.</param>
        /// <param name="quantity">How many identical pieces this definition represents.</param>
        /// <param name="lengthMm">Member length in millimetres, for categories that have one.</param>
        /// <param name="detailLevel">How much geometric detail to generate.</param>
        /// <param name="representation">Which representations to generate.</param>
        /// <param name="placements">Where each piece goes. May be empty when only a schedule is wanted.</param>
        /// <param name="material">Material name.</param>
        /// <param name="grade">Material grade.</param>
        /// <param name="unitWeightKgPerM">Tabulated mass per metre carried over from the record.</param>
        /// <param name="pieceMassKg">Tabulated mass per piece carried over from the record.</param>
        /// <param name="densityKgPerCubicMetre">Density, when the material supplies one.</param>
        /// <param name="layer">Target layer name, or <see langword="null"/> for the automatic layer.</param>
        /// <param name="notes">Free text notes carried into the bill of materials.</param>
        /// <param name="drawingReference">Free text drawing reference carried into the bill of materials.</param>
        /// <param name="isCustom">Whether the user has deliberately departed from the standard profile.</param>
        /// <param name="sourceDataVerified">Whether the source record was marked verified.</param>
        /// <param name="createdUtc">When the element was first created.</param>
        public ElementDefinition(
            Guid elementId,
            string categoryId,
            string standardId,
            string sectionRecordId,
            string designation,
            IReadOnlyDictionary<string, decimal> dimensions,
            int quantity,
            decimal? lengthMm = null,
            DetailLevel detailLevel = DetailLevel.Standard,
            RepresentationMode representation = RepresentationMode.Solid3D,
            IReadOnlyList<Placement>? placements = null,
            string? material = null,
            string? grade = null,
            decimal? unitWeightKgPerM = null,
            decimal? pieceMassKg = null,
            decimal? densityKgPerCubicMetre = null,
            string? layer = null,
            string? notes = null,
            string? drawingReference = null,
            bool isCustom = false,
            bool sourceDataVerified = false,
            DateTime? createdUtc = null)
        {
            ElementId = elementId;
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));
            StandardId = standardId ?? throw new ArgumentNullException(nameof(standardId));
            SectionRecordId = sectionRecordId ?? throw new ArgumentNullException(nameof(sectionRecordId));
            Designation = designation ?? throw new ArgumentNullException(nameof(designation));
            _dimensions = (dimensions ?? throw new ArgumentNullException(nameof(dimensions)))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            Quantity = quantity;
            LengthMm = lengthMm;
            DetailLevel = detailLevel;
            Representation = representation;
            Placements = placements ?? Array.Empty<Placement>();
            Material = material;
            Grade = grade;
            UnitWeightKgPerM = unitWeightKgPerM;
            PieceMassKg = pieceMassKg;
            DensityKgPerCubicMetre = densityKgPerCubicMetre;
            Layer = layer;
            Notes = notes;
            DrawingReference = drawingReference;
            IsCustom = isCustom;
            SourceDataVerified = sourceDataVerified;
            CreatedUtc = createdUtc ?? DateTime.UtcNow;
        }

        /// <summary>Gets the stable identity of this element, preserved across every edit.</summary>
        public Guid ElementId { get; }

        /// <summary>Gets the element category identifier.</summary>
        public string CategoryId { get; }

        /// <summary>Gets the identifier of the standard the profile comes from.</summary>
        public string StandardId { get; }

        /// <summary>Gets the database record identifier the element was created from.</summary>
        public string SectionRecordId { get; }

        /// <summary>Gets the profile designation.</summary>
        public string Designation { get; }

        /// <summary>Gets the effective cross-sectional dimensions in millimetres.</summary>
        public IReadOnlyDictionary<string, decimal> Dimensions => _dimensions;

        /// <summary>Gets how many identical pieces this definition represents.</summary>
        public int Quantity { get; }

        /// <summary>Gets the member length in millimetres, for categories that have one.</summary>
        public decimal? LengthMm { get; }

        /// <summary>Gets how much geometric detail to generate.</summary>
        public DetailLevel DetailLevel { get; }

        /// <summary>Gets which representations to generate.</summary>
        public RepresentationMode Representation { get; }

        /// <summary>Gets where each piece goes.</summary>
        public IReadOnlyList<Placement> Placements { get; }

        /// <summary>Gets the material name.</summary>
        public string? Material { get; }

        /// <summary>Gets the material grade.</summary>
        public string? Grade { get; }

        /// <summary>Gets the tabulated mass per metre carried over from the record.</summary>
        public decimal? UnitWeightKgPerM { get; }

        /// <summary>Gets the tabulated mass per piece carried over from the record.</summary>
        public decimal? PieceMassKg { get; }

        /// <summary>Gets the density, when the material supplies one.</summary>
        public decimal? DensityKgPerCubicMetre { get; }

        /// <summary>Gets the target layer name, or <see langword="null"/> for the automatic layer.</summary>
        public string? Layer { get; }

        /// <summary>Gets free text notes carried into the bill of materials.</summary>
        public string? Notes { get; }

        /// <summary>Gets the free text drawing reference carried into the bill of materials.</summary>
        public string? DrawingReference { get; }

        /// <summary>
        /// Gets a value indicating whether the user has deliberately departed from the standard
        /// profile. When true the element is reported as non-conforming everywhere it appears.
        /// </summary>
        public bool IsCustom { get; }

        /// <summary>Gets a value indicating whether the source record was marked verified.</summary>
        public bool SourceDataVerified { get; }

        /// <summary>Gets the moment the element was first created.</summary>
        public DateTime CreatedUtc { get; }

        /// <summary>Gets the member length expressed as a <see cref="Units.Length"/>, if it has one.</summary>
        public Units.Length? Length => LengthMm.HasValue ? Units.Length.FromMillimetres(LengthMm.Value) : null;

        /// <summary>Reads an effective dimension, returning <see langword="null"/> when it is absent.</summary>
        /// <param name="key">One of the <see cref="DimensionKeys"/> constants.</param>
        /// <returns>The value in millimetres, or <see langword="null"/>.</returns>
        public decimal? Dimension(string key) =>
            _dimensions.TryGetValue(key, out decimal value) ? value : null;

        /// <summary>Reads an effective dimension as a <see cref="double"/> for geometry work.</summary>
        /// <param name="key">One of the <see cref="DimensionKeys"/> constants.</param>
        /// <returns>The value in millimetres.</returns>
        /// <exception cref="InvalidOperationException">The dimension is not present.</exception>
        public double RequiredDimension(string key) =>
            (double)(Dimension(key) ?? throw new InvalidOperationException(
                $"Element '{Designation}' has no value for the required dimension '{key}'."));

        /// <summary>Creates a copy of this definition with selected properties replaced.</summary>
        /// <param name="quantity">The replacement quantity, or <see langword="null"/> to keep the current one.</param>
        /// <param name="lengthMm">The replacement length, or <see langword="null"/> to keep the current one.</param>
        /// <param name="dimensions">Replacement dimensions, or <see langword="null"/> to keep the current ones.</param>
        /// <param name="detailLevel">The replacement detail level, or <see langword="null"/> to keep the current one.</param>
        /// <param name="representation">The replacement representation, or <see langword="null"/> to keep the current one.</param>
        /// <param name="placements">Replacement placements, or <see langword="null"/> to keep the current ones.</param>
        /// <param name="layer">The replacement layer, or <see langword="null"/> to keep the current one.</param>
        /// <param name="notes">The replacement notes, or <see langword="null"/> to keep the current ones.</param>
        /// <param name="isCustom">The replacement custom flag, or <see langword="null"/> to keep the current one.</param>
        /// <returns>A new definition. The <see cref="ElementId"/> is always preserved.</returns>
        public ElementDefinition With(
            int? quantity = null,
            decimal? lengthMm = null,
            IReadOnlyDictionary<string, decimal>? dimensions = null,
            DetailLevel? detailLevel = null,
            RepresentationMode? representation = null,
            IReadOnlyList<Placement>? placements = null,
            string? layer = null,
            string? notes = null,
            bool? isCustom = null) =>
            new(
                ElementId,
                CategoryId,
                StandardId,
                SectionRecordId,
                Designation,
                dimensions ?? _dimensions,
                quantity ?? Quantity,
                lengthMm ?? LengthMm,
                detailLevel ?? DetailLevel,
                representation ?? Representation,
                placements ?? Placements,
                Material,
                Grade,
                UnitWeightKgPerM,
                PieceMassKg,
                DensityKgPerCubicMetre,
                layer ?? Layer,
                notes ?? Notes,
                DrawingReference,
                isCustom ?? IsCustom,
                SourceDataVerified,
                CreatedUtc);

        /// <inheritdoc />
        public override string ToString() =>
            LengthMm.HasValue
                ? $"{Quantity} x {Designation} @ {LengthMm.Value:0.###} mm"
                : $"{Quantity} x {Designation}";
    }
}
