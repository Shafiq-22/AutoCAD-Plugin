using System;

namespace StructureGen.Core.Model
{
    /// <summary>Describes one editable or displayed parameter of an element.</summary>
    public sealed class ParameterDescriptor
    {
        /// <summary>Initialises a new instance of the <see cref="ParameterDescriptor"/> class.</summary>
        /// <param name="key">The dimension or property key this descriptor governs.</param>
        /// <param name="displayName">The label shown in the user interface.</param>
        /// <param name="mutability">Whether and how the user may change the value.</param>
        /// <param name="isRequired">Whether geometry generation fails without a value.</param>
        /// <param name="minimum">Optional inclusive lower bound, in millimetres.</param>
        /// <param name="maximum">Optional inclusive upper bound, in millimetres.</param>
        /// <param name="description">Optional explanatory text shown as a tool tip.</param>
        public ParameterDescriptor(
            string key,
            string displayName,
            ParameterMutability mutability,
            bool isRequired = true,
            decimal? minimum = null,
            decimal? maximum = null,
            string? description = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Mutability = mutability;
            IsRequired = isRequired;
            Minimum = minimum;
            Maximum = maximum;
            Description = description;
        }

        /// <summary>Gets the dimension or property key this descriptor governs.</summary>
        public string Key { get; }

        /// <summary>Gets the label shown in the user interface.</summary>
        public string DisplayName { get; }

        /// <summary>Gets whether and how the user may change the value.</summary>
        public ParameterMutability Mutability { get; }

        /// <summary>Gets a value indicating whether geometry generation fails without a value.</summary>
        public bool IsRequired { get; }

        /// <summary>Gets the optional inclusive lower bound, in millimetres.</summary>
        public decimal? Minimum { get; }

        /// <summary>Gets the optional inclusive upper bound, in millimetres.</summary>
        public decimal? Maximum { get; }

        /// <summary>Gets the optional explanatory text shown as a tool tip.</summary>
        public string? Description { get; }

        /// <summary>Gets a value indicating whether editing this value breaks conformity with the standard.</summary>
        public bool IsStandardDimension => Mutability == ParameterMutability.StandardLocked;

        /// <inheritdoc />
        public override string ToString() => $"{DisplayName} [{Key}] ({Mutability})";
    }
}
