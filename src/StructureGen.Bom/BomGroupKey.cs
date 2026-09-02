using System;
using System.Globalization;
using StructureGen.Core.Model;

namespace StructureGen.Bom
{
    /// <summary>
    /// The identity by which two occurrences count as the same schedule line.
    /// </summary>
    /// <remarks>
    /// Ten identical beams belong on one line with a quantity of ten. Ten beams of the same profile
    /// but different lengths do not, and neither do two that differ in standard, material, grade or
    /// custom status — a non-conforming member must never be hidden inside the count of a
    /// conforming one. Which of the softer attributes participate is configurable, because whether
    /// a layer or a note distinguishes two members is a matter of drawing office practice rather
    /// than of engineering.
    /// </remarks>
    public sealed class BomGroupKey : IEquatable<BomGroupKey>
    {
        private readonly string _key;

        private BomGroupKey(string key, BomEntry representative)
        {
            _key = key;
            Representative = representative;
        }

        /// <summary>Gets the first entry that produced this key, used as the source of display values.</summary>
        public BomEntry Representative { get; }

        /// <summary>Builds the grouping key for an entry.</summary>
        /// <param name="entry">The entry to key.</param>
        /// <param name="options">The grouping rules to apply.</param>
        /// <returns>The key.</returns>
        public static BomGroupKey For(BomEntry entry, BomOptions options)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            ElementDefinition definition = entry.Definition;

            if (!options.GroupIdenticalElements)
            {
                // Every occurrence keeps its own line, keyed by its own identity.
                return new BomGroupKey(definition.ElementId.ToString("N") + "|" + (entry.Handle ?? string.Empty), entry);
            }

            string length = QuantiseLength(definition.LengthMm, options.LengthToleranceMm);

            string key = string.Join("|",
                definition.CategoryId,
                definition.StandardId,
                definition.Designation,
                definition.SectionRecordId,
                length,
                definition.Material ?? string.Empty,
                definition.Grade ?? string.Empty,
                definition.IsCustom ? "custom" : "standard",
                DimensionSignature(definition),
                options.GroupByLayer ? entry.Layer ?? string.Empty : string.Empty,
                options.GroupByNotes ? definition.Notes ?? string.Empty : string.Empty);

            return new BomGroupKey(key, entry);
        }

        /// <summary>
        /// Rounds a length onto the grouping tolerance so that members within the tolerance share a key.
        /// </summary>
        /// <param name="lengthMm">The length in millimetres.</param>
        /// <param name="toleranceMm">The tolerance in millimetres. Zero requires an exact match.</param>
        /// <returns>A stable textual representation of the quantised length.</returns>
        public static string QuantiseLength(decimal? lengthMm, decimal toleranceMm)
        {
            if (lengthMm is not { } length)
            {
                return "none";
            }

            if (toleranceMm <= 0m)
            {
                return length.ToString("0.######", CultureInfo.InvariantCulture);
            }

            decimal steps = Math.Round(length / toleranceMm, 0, MidpointRounding.AwayFromZero);
            return (steps * toleranceMm).ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Builds a signature of the cross-sectional dimensions so that two elements claiming the
        /// same designation but carrying different dimensions never merge.
        /// </summary>
        /// <param name="definition">The definition to summarise.</param>
        /// <returns>A stable textual signature.</returns>
        /// <remarks>
        /// This matters after an element has been edited in custom mode: two items may still both
        /// say "IPE 300" while one of them has been deliberately altered.
        /// </remarks>
        public static string DimensionSignature(ElementDefinition definition)
        {
            if (definition.Dimensions.Count == 0)
            {
                return string.Empty;
            }

            string[] parts = new string[definition.Dimensions.Count];
            int index = 0;

            foreach (System.Collections.Generic.KeyValuePair<string, decimal> pair in definition.Dimensions)
            {
                parts[index++] = pair.Key + "=" + pair.Value.ToString("0.######", CultureInfo.InvariantCulture);
            }

            Array.Sort(parts, StringComparer.Ordinal);
            return string.Join(";", parts);
        }

        /// <inheritdoc />
        public bool Equals(BomGroupKey? other) =>
            other is not null && string.Equals(_key, other._key, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as BomGroupKey);

        /// <inheritdoc />
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_key);

        /// <inheritdoc />
        public override string ToString() => _key;
    }
}
