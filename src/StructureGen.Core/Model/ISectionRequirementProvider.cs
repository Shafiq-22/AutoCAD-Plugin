using System.Collections.Generic;

namespace StructureGen.Core.Model
{
    /// <summary>
    /// Reports which dimensions a category's geometry generator cannot work without.
    /// </summary>
    /// <remarks>
    /// The database validator needs this to tell a user that an <c>i-section</c> row is missing its
    /// web thickness, but the validator must not depend on the geometry assembly. The geometry
    /// layer supplies an implementation and the composition root wires the two together, so adding
    /// a new element type automatically extends database validation with no change to the
    /// validator itself.
    /// </remarks>
    public interface ISectionRequirementProvider
    {
        /// <summary>Gets the dimension keys required to generate geometry for a category.</summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>
        /// The required keys, or an empty list when the category is unknown to the geometry layer.
        /// </returns>
        IReadOnlyList<string> RequiredDimensions(string categoryId);

        /// <summary>Gets the categories the geometry layer can generate.</summary>
        /// <returns>The known category identifiers.</returns>
        IReadOnlyCollection<string> SupportedCategories { get; }
    }
}
