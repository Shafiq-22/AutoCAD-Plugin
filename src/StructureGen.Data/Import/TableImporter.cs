using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Tabular;

namespace StructureGen.Data.Import
{
    /// <summary>
    /// Converts a supplied section table into StructureGen <see cref="SectionRecord"/> instances
    /// using a <see cref="MappingProfile"/>.
    /// </summary>
    public sealed class TableImporter
    {
        /// <summary>Imports a tabular source.</summary>
        /// <param name="source">The table to read.</param>
        /// <param name="profile">The column mapping to apply.</param>
        /// <returns>The converted records together with a row-by-row report.</returns>
        public ImportOutcome Import(ITabularSource source, MappingProfile profile)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            ValidationReport report = new();
            List<SectionRecord> sections = new();

            if (string.IsNullOrWhiteSpace(profile.DesignationColumn))
            {
                report.AddError(
                    "Import.NoDesignationColumn",
                    "The mapping profile does not name a designation column, so rows cannot be identified.",
                    profile.Name);
                return new ImportOutcome(sections, report, 0);
            }

            Dictionary<string, int> columnIndex = BuildColumnIndex(source, report);
            if (report.HasErrors)
            {
                return new ImportOutcome(sections, report, 0);
            }

            DatabaseUnits units = DatabaseUnits.Parse(
                profile.LengthUnit, profile.UnitWeightUnit, null, profile.PieceMassUnit, out string? unrecognised);
            if (unrecognised is not null)
            {
                report.AddWarning(
                    "Import.UnknownUnit",
                    $"The unit token '{unrecognised}' in the mapping profile was not recognised; the default unit was assumed.",
                    profile.Name);
            }

            VerifyMappedColumnsExist(profile, columnIndex, report);

            IReadOnlyList<IReadOnlyList<string>> rows = source.Rows;
            int firstDataRow = Math.Max(0, profile.SkipRows);
            int rowsRead = 0;
            HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);

            for (int rowNumber = firstDataRow; rowNumber < rows.Count; rowNumber++)
            {
                IReadOnlyList<string> row = rows[rowNumber];
                if (row.All(cell => string.IsNullOrWhiteSpace(cell)))
                {
                    continue;
                }

                rowsRead++;
                string where = $"{source.Name} row {rowNumber + 2}";

                string? designation = Cell(row, columnIndex, profile.DesignationColumn!, profile);
                if (string.IsNullOrWhiteSpace(designation))
                {
                    report.AddWarning("Import.NoDesignation", "Row skipped: it has no designation.", where);
                    continue;
                }

                string categoryId = ResolveText(row, columnIndex, profile.CategoryColumn, profile.Category, profile) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(categoryId))
                {
                    report.AddWarning(
                        "Import.NoCategory",
                        $"Row skipped: no category could be determined for '{designation}'. Set 'category' in the mapping profile.",
                        where);
                    continue;
                }

                string standardId = ResolveText(row, columnIndex, profile.StandardIdColumn, profile.StandardId, profile) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(standardId))
                {
                    report.AddWarning(
                        "Import.NoStandard",
                        $"Row skipped: no standard could be determined for '{designation}'. Set 'standardId' in the mapping profile.",
                        where);
                    continue;
                }

                string id = ResolveText(row, columnIndex, profile.IdColumn, null, profile)
                            ?? BuildIdentifier(standardId, categoryId, designation!);

                if (!seenIds.Add(id))
                {
                    report.AddWarning(
                        "Import.DuplicateId",
                        $"Row skipped: the identifier '{id}' was already produced by an earlier row. " +
                        "Add an explicit id column if two rows legitimately share a designation.",
                        where);
                    continue;
                }

                Dictionary<string, decimal> dimensions = new(StringComparer.Ordinal);
                bool rowFailed = false;

                foreach (KeyValuePair<string, string> mapping in profile.Dimensions)
                {
                    string? raw = Cell(row, columnIndex, mapping.Value, profile);
                    if (raw is null)
                    {
                        continue;
                    }

                    if (!TryParseNumber(raw, out decimal value))
                    {
                        report.AddWarning(
                            "Import.UnparsableNumber",
                            $"Column '{mapping.Value}' contains '{raw}', which is not a number. The dimension " +
                            $"'{mapping.Key}' was left unset for '{designation}'.",
                            where);
                        continue;
                    }

                    if (value <= 0m)
                    {
                        report.AddWarning(
                            "Import.NonPositiveDimension",
                            $"Dimension '{mapping.Key}' for '{designation}' is {raw}, which is not greater than zero. It was left unset.",
                            where);
                        continue;
                    }

                    decimal factor = units.LengthToMillimetres(1m);
                    dimensions[mapping.Key] = string.Equals(mapping.Key, DimensionKeys.Area, StringComparison.Ordinal)
                        ? value * factor * factor
                        : value * factor;
                }

                Dictionary<string, decimal> properties = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> mapping in profile.Properties)
                {
                    string? raw = Cell(row, columnIndex, mapping.Value, profile);
                    if (raw is not null && TryParseNumber(raw, out decimal value))
                    {
                        properties[mapping.Key] = value;
                    }
                }

                decimal? unitWeight = ReadOptionalNumber(
                    row, columnIndex, profile.UnitWeightColumn, profile, where, "unit weight", designation!, report, ref rowFailed);
                decimal? pieceMass = ReadOptionalNumber(
                    row, columnIndex, profile.PieceMassColumn, profile, where, "piece mass", designation!, report, ref rowFailed);

                if (rowFailed)
                {
                    continue;
                }

                sections.Add(new SectionRecord(
                    id,
                    standardId,
                    categoryId,
                    designation!,
                    dimensions,
                    unitWeight.HasValue ? units.UnitWeightToKgPerM(unitWeight.Value) : null,
                    pieceMass.HasValue ? units.PieceMassToKg(pieceMass.Value) : null,
                    ResolveText(row, columnIndex, profile.NominalSizeColumn, null, profile),
                    ResolveText(row, columnIndex, profile.MaterialColumn, profile.Material, profile),
                    ResolveText(row, columnIndex, profile.GradeColumn, profile.Grade, profile),
                    properties.Count == 0 ? null : properties,
                    profile.MarkVerified,
                    profile.Provenance ?? $"Imported from {source.Name} using mapping '{profile.Name}'.",
                    null));
            }

            report.AddInfo(
                "Import.Summary",
                $"Read {rowsRead} data row(s) from '{source.Name}' and produced {sections.Count} section record(s).",
                profile.Name);

            return new ImportOutcome(sections, report, rowsRead);
        }

        private static Dictionary<string, int> BuildColumnIndex(ITabularSource source, ValidationReport report)
        {
            Dictionary<string, int> index = new(StringComparer.OrdinalIgnoreCase);
            if (source.Headers.Count == 0)
            {
                report.AddError("Import.NoHeaders", $"The source '{source.Name}' has no header row.", source.Name);
                return index;
            }

            for (int position = 0; position < source.Headers.Count; position++)
            {
                string header = source.Headers[position].Trim();
                if (header.Length == 0)
                {
                    continue;
                }

                if (!index.ContainsKey(header))
                {
                    index[header] = position;
                }
            }

            return index;
        }

        private static void VerifyMappedColumnsExist(
            MappingProfile profile, Dictionary<string, int> columnIndex, ValidationReport report)
        {
            IEnumerable<string?> named = new[]
                {
                    profile.DesignationColumn, profile.IdColumn, profile.NominalSizeColumn,
                    profile.UnitWeightColumn, profile.PieceMassColumn, profile.MaterialColumn,
                    profile.GradeColumn, profile.StandardIdColumn, profile.CategoryColumn
                }
                .Concat(profile.Dimensions.Values)
                .Concat(profile.Properties.Values);

            foreach (string column in named.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!columnIndex.ContainsKey(column.Trim()))
                {
                    report.AddWarning(
                        "Import.MissingColumn",
                        $"The mapping profile refers to column '{column}', which is not present in the source. " +
                        "Every value that would have come from it will be left unset.",
                        profile.Name);
                }
            }
        }

        private static string? Cell(
            IReadOnlyList<string> row, Dictionary<string, int> columnIndex, string columnName, MappingProfile profile)
        {
            if (string.IsNullOrWhiteSpace(columnName) || !columnIndex.TryGetValue(columnName.Trim(), out int position))
            {
                return null;
            }

            if (position >= row.Count)
            {
                return null;
            }

            string value = row[position].Trim();
            if (value.Length == 0)
            {
                return null;
            }

            return profile.BlankMarkers.Any(marker => string.Equals(marker, value, StringComparison.OrdinalIgnoreCase))
                ? null
                : value;
        }

        private static string? ResolveText(
            IReadOnlyList<string> row,
            Dictionary<string, int> columnIndex,
            string? columnName,
            string? constant,
            MappingProfile profile)
        {
            string? fromColumn = columnName is null ? null : Cell(row, columnIndex, columnName, profile);
            return fromColumn ?? (string.IsNullOrWhiteSpace(constant) ? null : constant);
        }

        private static decimal? ReadOptionalNumber(
            IReadOnlyList<string> row,
            Dictionary<string, int> columnIndex,
            string? columnName,
            MappingProfile profile,
            string where,
            string label,
            string designation,
            ValidationReport report,
            ref bool rowFailed)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                return null;
            }

            string? raw = Cell(row, columnIndex, columnName!, profile);
            if (raw is null)
            {
                return null;
            }

            if (!TryParseNumber(raw, out decimal value))
            {
                report.AddWarning(
                    "Import.UnparsableNumber",
                    $"The {label} for '{designation}' is '{raw}', which is not a number. It was left unset.",
                    where);
                return null;
            }

            if (value <= 0m)
            {
                report.AddWarning(
                    "Import.NonPositiveMass",
                    $"The {label} for '{designation}' is {raw}, which is not greater than zero. It was left unset.",
                    where);
                return null;
            }

            return value;
        }

        /// <summary>
        /// Parses a number from a published table, tolerating thousands separators, stray unit
        /// suffixes and both decimal conventions.
        /// </summary>
        /// <param name="text">The cell text.</param>
        /// <param name="value">Receives the parsed value.</param>
        /// <returns><see langword="true"/> when the text yielded a number.</returns>
        public static bool TryParseNumber(string? text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            StringBuilder cleaned = new();
            foreach (char character in text!)
            {
                if (char.IsDigit(character) || character is '.' or ',' or '-' or '+')
                {
                    cleaned.Append(character);
                }
                else if (character is ' ' or ' ' or '\'')
                {
                    // Thousands separators used by published tables are discarded.
                }
                else if (cleaned.Length > 0)
                {
                    // A trailing unit suffix such as "mm" ends the number.
                    break;
                }
            }

            string candidate = cleaned.ToString();
            if (candidate.Length == 0)
            {
                return false;
            }

            // Decide which separator is decimal. When both appear the last one wins, which handles
            // "1,234.5" and "1.234,5" alike. When only commas appear and there is exactly one, and
            // it is not acting as a thousands separator, it is decimal.
            int lastComma = candidate.LastIndexOf(',');
            int lastDot = candidate.LastIndexOf('.');

            if (lastComma >= 0 && lastDot >= 0)
            {
                char decimalSeparator = lastComma > lastDot ? ',' : '.';
                char groupSeparator = decimalSeparator == ',' ? '.' : ',';
                candidate = candidate.Replace(groupSeparator.ToString(), string.Empty)
                                     .Replace(decimalSeparator, '.');
            }
            else if (lastComma >= 0)
            {
                int digitsAfter = candidate.Length - lastComma - 1;
                candidate = digitsAfter == 3 && candidate.IndexOf(',') != lastComma
                    ? candidate.Replace(",", string.Empty)
                    : candidate.Replace(',', '.');
            }

            return decimal.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string BuildIdentifier(string standardId, string categoryId, string designation)
        {
            StringBuilder builder = new();
            foreach (char character in $"{standardId}-{categoryId}-{designation}")
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
                else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                {
                    builder.Append('-');
                }
            }

            return builder.ToString().Trim('-');
        }
    }
}
