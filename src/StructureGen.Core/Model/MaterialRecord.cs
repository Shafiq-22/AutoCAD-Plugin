using System;

namespace StructureGen.Core.Model
{
    /// <summary>
    /// A material and, where the database supplies one, its density.
    /// </summary>
    /// <remarks>
    /// Density is optional on purpose. StructureGen never invents a density: when a plate's
    /// material has no density the computed mass is reported as unavailable rather than guessed.
    /// </remarks>
    public sealed class MaterialRecord
    {
        /// <summary>Initialises a new instance of the <see cref="MaterialRecord"/> class.</summary>
        /// <param name="id">Unique material identifier.</param>
        /// <param name="name">Display name.</param>
        /// <param name="densityKgPerCubicMetre">Density, when the database supplies one.</param>
        /// <param name="grade">Optional grade designation.</param>
        /// <param name="standardId">Optional identifier of the standard the material comes from.</param>
        /// <param name="notes">Free text notes.</param>
        public MaterialRecord(
            string id,
            string name,
            decimal? densityKgPerCubicMetre = null,
            string? grade = null,
            string? standardId = null,
            string? notes = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DensityKgPerCubicMetre = densityKgPerCubicMetre;
            Grade = grade;
            StandardId = standardId;
            Notes = notes;
        }

        /// <summary>Gets the unique material identifier.</summary>
        public string Id { get; }

        /// <summary>Gets the display name.</summary>
        public string Name { get; }

        /// <summary>Gets the density in kilograms per cubic metre, when the database supplies one.</summary>
        public decimal? DensityKgPerCubicMetre { get; }

        /// <summary>Gets the optional grade designation.</summary>
        public string? Grade { get; }

        /// <summary>Gets the optional identifier of the standard the material comes from.</summary>
        public string? StandardId { get; }

        /// <summary>Gets free text notes.</summary>
        public string? Notes { get; }

        /// <inheritdoc />
        public override string ToString() => Grade is null ? Name : $"{Name} {Grade}";
    }
}
