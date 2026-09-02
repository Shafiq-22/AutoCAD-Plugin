using System;
using System.Collections.Generic;
using System.Linq;

namespace StructureGen.Core.Model
{
    /// <summary>Describes one element category to the user interface and to the validation rules.</summary>
    public sealed class CategoryDescriptor
    {
        /// <summary>Initialises a new instance of the <see cref="CategoryDescriptor"/> class.</summary>
        /// <param name="id">The stable category identifier.</param>
        /// <param name="displayName">The name shown in the user interface.</param>
        /// <param name="kind">How the category behaves parametrically.</param>
        /// <param name="lengthIsUserParameter">Whether the user supplies a member length.</param>
        public CategoryDescriptor(string id, string displayName, CategoryKind kind, bool lengthIsUserParameter)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Kind = kind;
            LengthIsUserParameter = lengthIsUserParameter;
        }

        /// <summary>Gets the stable category identifier.</summary>
        public string Id { get; }

        /// <summary>Gets the name shown in the user interface.</summary>
        public string DisplayName { get; }

        /// <summary>Gets how the category behaves parametrically.</summary>
        public CategoryKind Kind { get; }

        /// <summary>Gets a value indicating whether the user supplies a member length for this category.</summary>
        public bool LengthIsUserParameter { get; }
    }

    /// <summary>The set of categories StructureGen knows about out of the box.</summary>
    public static class CategoryCatalog
    {
        private static readonly CategoryDescriptor[] BuiltIn =
        {
            new(ElementCategory.ISection, "I Section (beam)", CategoryKind.Linear, true),
            new(ElementCategory.HSection, "H Section (column)", CategoryKind.Linear, true),
            new(ElementCategory.Channel, "Channel", CategoryKind.Linear, true),
            new(ElementCategory.Angle, "Angle", CategoryKind.Linear, true),
            new(ElementCategory.TSection, "T Section", CategoryKind.Linear, true),
            new(ElementCategory.Pipe, "Pipe / Circular Hollow Section", CategoryKind.Linear, true),
            new(ElementCategory.RectangularHollowSection, "Rectangular Hollow Section", CategoryKind.Linear, true),
            new(ElementCategory.SquareHollowSection, "Square Hollow Section", CategoryKind.Linear, true),
            new(ElementCategory.Plate, "Plate", CategoryKind.Planar, true),
            new(ElementCategory.FlatBar, "Flat Bar", CategoryKind.Linear, true),
            new(ElementCategory.RoundBar, "Round Bar", CategoryKind.Linear, true),
            new(ElementCategory.Bolt, "Bolt", CategoryKind.Fastener, false),
            new(ElementCategory.Nut, "Nut", CategoryKind.Fastener, false),
            new(ElementCategory.Washer, "Washer", CategoryKind.Fastener, false),
            new(ElementCategory.Custom, "Custom / Other", CategoryKind.Linear, true)
        };

        /// <summary>Gets every built-in category descriptor.</summary>
        public static IReadOnlyList<CategoryDescriptor> All => BuiltIn;

        /// <summary>Finds the descriptor for a category identifier.</summary>
        /// <param name="categoryId">The identifier to look up.</param>
        /// <returns>The descriptor, or <see langword="null"/> when the category is not built in.</returns>
        public static CategoryDescriptor? Find(string? categoryId) =>
            categoryId is null
                ? null
                : BuiltIn.FirstOrDefault(c => string.Equals(c.Id, categoryId, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Finds the descriptor for a category identifier, falling back to a generic linear
        /// descriptor so that a database referencing an unknown category still works.
        /// </summary>
        /// <param name="categoryId">The identifier to look up.</param>
        /// <returns>A descriptor; never <see langword="null"/>.</returns>
        public static CategoryDescriptor FindOrGeneric(string categoryId) =>
            Find(categoryId) ?? new CategoryDescriptor(categoryId, categoryId, CategoryKind.Linear, true);
    }
}
