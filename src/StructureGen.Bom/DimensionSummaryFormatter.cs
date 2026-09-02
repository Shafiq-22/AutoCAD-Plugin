using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StructureGen.Core.Model;

namespace StructureGen.Bom
{
    /// <summary>Renders an element's standardised cross-section as a single readable string.</summary>
    /// <remarks>
    /// The order of the dimensions is chosen per category so the result reads the way a detailer
    /// writes it: depth before width before web before flange for an I section, outside diameter
    /// before wall for a pipe. Any dimension the category does not have a preferred position for is
    /// appended afterwards, so a database carrying extra keys still shows them.
    /// </remarks>
    public static class DimensionSummaryFormatter
    {
        private static readonly Dictionary<string, string[]> PreferredOrder = new()
        {
            [ElementCategory.ISection] = new[]
            {
                DimensionKeys.Height, DimensionKeys.Width, DimensionKeys.WebThickness, DimensionKeys.FlangeThickness
            },
            [ElementCategory.HSection] = new[]
            {
                DimensionKeys.Height, DimensionKeys.Width, DimensionKeys.WebThickness, DimensionKeys.FlangeThickness
            },
            [ElementCategory.Channel] = new[]
            {
                DimensionKeys.Height, DimensionKeys.Width, DimensionKeys.WebThickness, DimensionKeys.FlangeThickness
            },
            [ElementCategory.TSection] = new[]
            {
                DimensionKeys.Height, DimensionKeys.Width, DimensionKeys.WebThickness, DimensionKeys.FlangeThickness
            },
            [ElementCategory.Angle] = new[] { DimensionKeys.LegA, DimensionKeys.LegB, DimensionKeys.Thickness },
            [ElementCategory.Pipe] = new[] { DimensionKeys.OutsideDiameter, DimensionKeys.WallThickness },
            [ElementCategory.RectangularHollowSection] = new[]
            {
                DimensionKeys.Height, DimensionKeys.Width, DimensionKeys.WallThickness
            },
            [ElementCategory.SquareHollowSection] = new[] { DimensionKeys.Height, DimensionKeys.WallThickness },
            [ElementCategory.Plate] = new[] { DimensionKeys.PlateWidth, DimensionKeys.Thickness },
            [ElementCategory.FlatBar] = new[] { DimensionKeys.PlateWidth, DimensionKeys.Thickness },
            [ElementCategory.RoundBar] = new[] { DimensionKeys.OutsideDiameter },
            [ElementCategory.Bolt] = new[]
            {
                DimensionKeys.ThreadDiameter, DimensionKeys.BoltLength, DimensionKeys.AcrossFlats, DimensionKeys.HeadHeight
            },
            [ElementCategory.Nut] = new[]
            {
                DimensionKeys.ThreadDiameter, DimensionKeys.AcrossFlats, DimensionKeys.NutHeight
            },
            [ElementCategory.Washer] = new[]
            {
                DimensionKeys.WasherInsideDiameter, DimensionKeys.WasherOutsideDiameter, DimensionKeys.WasherThickness
            }
        };

        /// <summary>The dimension keys that are informational rather than shape-defining.</summary>
        private static readonly HashSet<string> Excluded = new(new[]
        {
            DimensionKeys.Area,
            FastenerDimensionKeys.MinimumBoltLength,
            FastenerDimensionKeys.MaximumBoltLength
        });

        /// <summary>Renders the cross-section of an element.</summary>
        /// <param name="definition">The element to describe.</param>
        /// <returns>A string such as <c>300 x 150 x 7.1 x 10.7</c>, or an empty string when there is nothing to show.</returns>
        public static string Describe(ElementDefinition definition)
        {
            if (definition is null || definition.Dimensions.Count == 0)
            {
                return string.Empty;
            }

            List<string> ordered = new();
            HashSet<string> used = new(System.StringComparer.Ordinal);

            if (PreferredOrder.TryGetValue(definition.CategoryId, out string[]? preferred))
            {
                foreach (string key in preferred)
                {
                    if (definition.Dimension(key) is { } value)
                    {
                        ordered.Add(Format(value));
                        used.Add(key);
                    }
                }
            }

            IEnumerable<KeyValuePair<string, decimal>> remaining = definition.Dimensions
                .Where(pair => !used.Contains(pair.Key) && !Excluded.Contains(pair.Key))
                .OrderBy(pair => pair.Key, System.StringComparer.Ordinal);

            foreach (KeyValuePair<string, decimal> pair in remaining)
            {
                ordered.Add($"{pair.Key}={Format(pair.Value)}");
            }

            return string.Join(" x ", ordered);
        }

        private static string Format(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
