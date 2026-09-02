using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Data
{
    /// <summary>
    /// Checks a loaded database for the problems that would otherwise surface later as a failed
    /// geometry operation or, worse, as a silently wrong weight on a schedule.
    /// </summary>
    /// <remarks>
    /// The validator is deliberately split from the loader: the same rules run when importing a
    /// spreadsheet, when loading a file and from the command line tool, so a dataset that passes
    /// the importer is guaranteed to load in AutoCAD.
    /// </remarks>
    public sealed class DatabaseValidator
    {
        private readonly ISectionRequirementProvider? _requirements;

        /// <summary>Initialises a new instance of the <see cref="DatabaseValidator"/> class.</summary>
        /// <param name="requirements">
        /// Supplies the dimensions each category's geometry generator needs. When
        /// <see langword="null"/> the structural checks still run but per-category dimension
        /// completeness is not verified.
        /// </param>
        public DatabaseValidator(ISectionRequirementProvider? requirements = null) => _requirements = requirements;

        /// <summary>Validates a database.</summary>
        /// <param name="database">The database to check.</param>
        /// <returns>A report. An empty report means the dataset is fully usable.</returns>
        public ValidationReport Validate(StructuralDatabase database)
        {
            if (database is null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            ValidationReport report = new();

            CheckDuplicateIds(database, report);
            CheckSections(database, report);
            CheckVerificationStatus(database, report);

            return report;
        }

        private static void CheckDuplicateIds(StructuralDatabase database, ValidationReport report)
        {
            foreach (string duplicate in Duplicates(database.Standards.Select(s => s.Id)))
            {
                report.AddError("Db.DuplicateStandardId", $"The standard id '{duplicate}' is used more than once.", duplicate);
            }

            foreach (string duplicate in Duplicates(database.Materials.Select(m => m.Id)))
            {
                report.AddError("Db.DuplicateMaterialId", $"The material id '{duplicate}' is used more than once.", duplicate);
            }

            foreach (string duplicate in Duplicates(database.Sections.Select(s => s.Id)))
            {
                report.AddError("Db.DuplicateSectionId", $"The section id '{duplicate}' is used more than once.", duplicate);
            }

            IEnumerable<IGrouping<string, SectionRecord>> designationGroups = database.Sections
                .GroupBy(s => $"{s.StandardId}|{s.CategoryId}|{s.Designation}", StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (IGrouping<string, SectionRecord> group in designationGroups)
            {
                SectionRecord first = group.First();
                report.AddWarning(
                    "Db.DuplicateDesignation",
                    $"The designation '{first.Designation}' appears {group.Count()} times for standard " +
                    $"'{first.StandardId}' in category '{first.CategoryId}'. Only one of them will be selectable by name.",
                    first.Id);
            }
        }

        private void CheckSections(StructuralDatabase database, ValidationReport report)
        {
            foreach (SectionRecord section in database.Sections)
            {
                if (string.IsNullOrWhiteSpace(section.StandardId))
                {
                    report.AddError(
                        "Db.Section.NoStandard",
                        $"Section '{section.Designation}' does not name a standard.",
                        section.Id);
                }
                else if (database.FindStandard(section.StandardId) is null)
                {
                    report.AddError(
                        "Db.Section.UnknownStandard",
                        $"Section '{section.Designation}' refers to standard '{section.StandardId}', which is not defined in this database.",
                        section.Id);
                }

                if (section.Material is not null && database.ResolveMaterial(section) is null)
                {
                    report.AddWarning(
                        "Db.Section.UnknownMaterial",
                        $"Section '{section.Designation}' refers to material '{section.Material}', which is not defined in this database. " +
                        "Weights that would have to be computed from density will be reported as unavailable.",
                        section.Id);
                }

                foreach (KeyValuePair<string, decimal> dimension in section.Dimensions)
                {
                    if (dimension.Value <= 0m)
                    {
                        report.AddError(
                            "Db.Section.NonPositiveDimension",
                            $"Section '{section.Designation}' has dimension '{dimension.Key}' = " +
                            $"{dimension.Value.ToString(CultureInfo.InvariantCulture)}, which must be greater than zero.",
                            section.Id);
                    }
                }

                CheckRequiredDimensions(section, report);
                CheckGeometricConsistency(section, report);
                CheckMassAvailability(database, section, report);
            }
        }

        private void CheckRequiredDimensions(SectionRecord section, ValidationReport report)
        {
            if (_requirements is null)
            {
                return;
            }

            if (!_requirements.SupportedCategories.Contains(section.CategoryId, StringComparer.OrdinalIgnoreCase))
            {
                report.AddWarning(
                    "Db.Section.UnsupportedCategory",
                    $"Section '{section.Designation}' belongs to category '{section.CategoryId}', for which no geometry " +
                    "generator is registered. The record can be scheduled but no geometry can be created from it.",
                    section.Id);
                return;
            }

            IReadOnlyList<string> required = _requirements.RequiredDimensions(section.CategoryId);
            List<string> missing = required.Where(key => section.Dimension(key) is null).ToList();
            if (missing.Count > 0)
            {
                report.AddError(
                    "Db.Section.MissingDimension",
                    $"Section '{section.Designation}' (category '{section.CategoryId}') is missing the required " +
                    $"dimension(s): {string.Join(", ", missing)}.",
                    section.Id);
            }
        }

        private static void CheckGeometricConsistency(SectionRecord section, ValidationReport report)
        {
            decimal? height = section.Dimension(DimensionKeys.Height);
            decimal? flange = section.Dimension(DimensionKeys.FlangeThickness);
            decimal? web = section.Dimension(DimensionKeys.WebThickness);
            decimal? width = section.Dimension(DimensionKeys.Width);
            decimal? outsideDiameter = section.Dimension(DimensionKeys.OutsideDiameter);
            decimal? wall = section.Dimension(DimensionKeys.WallThickness);

            if (height.HasValue && flange.HasValue && flange.Value * 2m >= height.Value)
            {
                report.AddError(
                    "Db.Section.FlangeTooThick",
                    $"Section '{section.Designation}' has two flanges of {flange.Value} mm in a depth of {height.Value} mm, " +
                    "which leaves no web. Check the tf and h columns.",
                    section.Id);
            }

            if (width.HasValue && web.HasValue && web.Value >= width.Value)
            {
                report.AddError(
                    "Db.Section.WebWiderThanFlange",
                    $"Section '{section.Designation}' has a web thickness of {web.Value} mm that is not narrower than its " +
                    $"width of {width.Value} mm. Check the tw and b columns.",
                    section.Id);
            }

            if (outsideDiameter.HasValue && wall.HasValue && wall.Value * 2m >= outsideDiameter.Value)
            {
                report.AddError(
                    "Db.Section.WallTooThick",
                    $"Section '{section.Designation}' has a wall of {wall.Value} mm in an outside diameter of " +
                    $"{outsideDiameter.Value} mm, which leaves no bore. Check the t and d columns.",
                    section.Id);
            }

            decimal? rootRadius = section.Dimension(DimensionKeys.RootRadius);
            if (rootRadius.HasValue && height.HasValue && rootRadius.Value * 2m >= height.Value)
            {
                report.AddWarning(
                    "Db.Section.ImplausibleRootRadius",
                    $"Section '{section.Designation}' declares a root radius of {rootRadius.Value} mm against a depth of " +
                    $"{height.Value} mm. The radius will be clamped when geometry is generated.",
                    section.Id);
            }
        }

        private static void CheckMassAvailability(StructuralDatabase database, SectionRecord section, ValidationReport report)
        {
            CategoryDescriptor descriptor = CategoryCatalog.FindOrGeneric(section.CategoryId);
            bool hasTabulatedMass = descriptor.Kind == CategoryKind.Fastener
                ? section.PieceMassKg.HasValue
                : section.UnitWeightKgPerM.HasValue;

            if (hasTabulatedMass)
            {
                return;
            }

            MaterialRecord? material = database.ResolveMaterial(section);
            if (material?.DensityKgPerCubicMetre is not null)
            {
                return;
            }

            report.AddWarning(
                "Db.Section.NoMassSource",
                $"Section '{section.Designation}' has neither a tabulated mass nor a material with a density, so its " +
                "weight will be reported as unavailable on the bill of materials rather than estimated.",
                section.Id);
        }

        private static void CheckVerificationStatus(StructuralDatabase database, ValidationReport report)
        {
            if (database.Sections.Count == 0)
            {
                report.AddWarning(
                    "Db.NoSections",
                    "The structural database contains no section records. Load a database that contains your profile data.");
                return;
            }

            int unverified = database.UnverifiedRecordCount;
            if (unverified > 0)
            {
                report.AddInfo(
                    "Db.Unverified",
                    $"{unverified} of {database.Sections.Count} section record(s) are not marked as verified. " +
                    "Their dimensions and weights must be checked against your authoritative copy of the standard " +
                    "before the output is used for procurement or fabrication.");
            }
        }

        private static IEnumerable<string> Duplicates(IEnumerable<string> values) =>
            values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                  .Where(g => g.Count() > 1)
                  .Select(g => g.Key);
    }
}
