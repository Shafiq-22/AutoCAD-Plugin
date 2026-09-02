using System;
using System.Collections.Generic;
using StructureGen.Core.Model;

namespace StructureGen.Bom
{
    /// <summary>The subtotal for one element category.</summary>
    public sealed class BomCategorySummary
    {
        /// <summary>Initialises a new instance of the <see cref="BomCategorySummary"/> class.</summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <param name="lines">The lines belonging to the category.</param>
        public BomCategorySummary(string categoryId, IReadOnlyList<BomLine> lines)
        {
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            Totals = new BomTotals(lines);
        }

        /// <summary>Gets the category identifier.</summary>
        public string CategoryId { get; }

        /// <summary>Gets the display name of the category.</summary>
        public string CategoryName => CategoryCatalog.FindOrGeneric(CategoryId).DisplayName;

        /// <summary>Gets the lines belonging to the category.</summary>
        public IReadOnlyList<BomLine> Lines { get; }

        /// <summary>Gets the subtotal for the category.</summary>
        public BomTotals Totals { get; }
    }
}
