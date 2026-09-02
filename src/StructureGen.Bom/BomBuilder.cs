using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Bom
{
    /// <summary>Collapses harvested occurrences into a numbered, totalled schedule.</summary>
    public sealed class BomBuilder
    {
        /// <summary>Builds a schedule.</summary>
        /// <param name="entries">The occurrences to schedule.</param>
        /// <param name="options">The grouping and presentation rules to apply.</param>
        /// <param name="databaseStamp">The name and version of the database the elements came from.</param>
        /// <param name="sourceDescription">Where the elements were harvested from.</param>
        /// <returns>The finished schedule.</returns>
        public BomDocument Build(
            IEnumerable<BomEntry> entries,
            BomOptions options,
            string? databaseStamp = null,
            string? sourceDescription = null)
        {
            if (entries is null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            ValidationReport report = new();
            List<BomEntry> materialised = entries.ToList();

            Dictionary<BomGroupKey, List<BomEntry>> groups = new();
            List<BomGroupKey> order = new();

            foreach (BomEntry entry in materialised)
            {
                BomGroupKey key = BomGroupKey.For(entry, options);

                if (!groups.TryGetValue(key, out List<BomEntry>? bucket))
                {
                    bucket = new List<BomEntry>();
                    groups[key] = bucket;
                    order.Add(key);
                }

                bucket.Add(entry);
            }

            List<BomLine> lines = OrderGroups(order, groups)
                .Select((bucket, index) => new BomLine(index + 1, bucket))
                .ToList();

            foreach (BomLine line in lines.Where(l => !l.MassIsKnown))
            {
                report.AddWarning(
                    "Bom.UnknownMass",
                    $"Item {line.ItemNumber} ({line.Designation}) has no established weight, so it is excluded from " +
                    "the totals. " + line.MassExplanation,
                    line.Designation);
            }

            foreach (BomLine line in lines.Where(l => l.IsCustom))
            {
                report.AddWarning(
                    "Bom.NonConforming",
                    $"Item {line.ItemNumber} ({line.Designation}) has been edited away from its standard profile.",
                    line.Designation);
            }

            return new BomDocument(lines, options, databaseStamp, DateTime.UtcNow, report, sourceDescription);
        }

        /// <summary>
        /// Orders the grouped buckets so the schedule reads consistently: by category in catalogue
        /// order, then by standard, then by designation, then by length.
        /// </summary>
        /// <param name="order">The keys in the order they were first encountered.</param>
        /// <param name="groups">The buckets.</param>
        /// <returns>The ordered buckets.</returns>
        /// <remarks>
        /// A stable, meaningful order matters more than it looks: it means regenerating the schedule
        /// after adding a member does not renumber everything that came before it.
        /// </remarks>
        private static IEnumerable<List<BomEntry>> OrderGroups(
            IReadOnlyList<BomGroupKey> order, IReadOnlyDictionary<BomGroupKey, List<BomEntry>> groups) =>
            order
                .OrderBy(key => CategoryOrder(key.Representative.Definition.CategoryId))
                .ThenBy(key => key.Representative.Definition.CategoryId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(key => key.Representative.Definition.StandardId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(key => key.Representative.Definition.Designation, StringComparer.OrdinalIgnoreCase)
                .ThenBy(key => key.Representative.Definition.LengthMm ?? 0m)
                .Select(key => groups[key]);

        private static int CategoryOrder(string categoryId)
        {
            for (int index = 0; index < CategoryCatalog.All.Count; index++)
            {
                if (string.Equals(CategoryCatalog.All[index].Id, categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return int.MaxValue;
        }
    }
}
