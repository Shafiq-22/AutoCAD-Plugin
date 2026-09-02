using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using StructureGen.Bom;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Units;
using StructureGen.Excel.Xlsx;

namespace StructureGen.Excel
{
    /// <summary>Turns a <see cref="BomDocument"/> into a formatted workbook.</summary>
    /// <remarks>
    /// The workbook carries four sheets. <c>BOM</c> is the schedule itself. <c>Summary</c> breaks it
    /// down by category. <c>Element Details</c> lists every individual occurrence with its AutoCAD
    /// handle, so a figure on the schedule can be traced back to the object that produced it.
    /// <c>Data Sources</c> records which database version the numbers came from, how each weight was
    /// derived, and the engineering disclaimer — because a weight schedule that leaves the building
    /// without saying where its numbers came from is the thing that causes trouble later.
    /// </remarks>
    public sealed class BomWorkbookBuilder
    {
        private const string Disclaimer =
            "StructureGen is a drafting and documentation aid. It performs no structural engineering design, " +
            "analysis or verification, and the geometry and weights it produces do not constitute engineering " +
            "approval. Profile dimensions and unit weights must be verified against your authoritative copy of " +
            "the relevant standard before this schedule is used for procurement, fabrication or construction.";

        private readonly ILogger _logger;

        /// <summary>Initialises a new instance of the <see cref="BomWorkbookBuilder"/> class.</summary>
        /// <param name="logger">Receives technical detail about export failures.</param>
        public BomWorkbookBuilder(ILogger logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>Builds the workbook without writing it, which is what the tests exercise.</summary>
        /// <param name="document">The schedule to render.</param>
        /// <returns>The workbook.</returns>
        public XlsxWorkbook Build(BomDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            XlsxWorkbook workbook = new()
            {
                Title = document.Options.Title,
                Creator = "StructureGen",
                Application = "StructureGen"
            };

            BuildScheduleSheet(workbook, document);
            BuildSummarySheet(workbook, document);
            BuildElementDetailSheet(workbook, document);
            BuildDataSourceSheet(workbook, document);

            return workbook;
        }

        /// <summary>Builds and writes the workbook.</summary>
        /// <param name="document">The schedule to render.</param>
        /// <param name="path">The file to write.</param>
        /// <returns>The path written on success, or a failure carrying a user-facing reason.</returns>
        public Result<string> Export(BomDocument document, string path)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return Result<string>.Failure(
                    "Export.NoPath", "No file name was given for the Excel export.");
            }

            try
            {
                Build(document).Save(path);
                _logger.Information($"Exported {document.Lines.Count} bill of materials line(s) to '{path}'.");
                return Result<string>.Success(path);
            }
            catch (IOException exception)
            {
                _logger.Error($"Could not write the workbook '{path}'.", exception);
                return Result<string>.Failure(
                    "Export.WriteFailed",
                    $"The file could not be written:{Environment.NewLine}{path}{Environment.NewLine}" +
                    "It may already be open in Excel. Close it and try again, or choose another name.");
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.Error($"Access denied writing the workbook '{path}'.", exception);
                return Result<string>.Failure(
                    "Export.AccessDenied",
                    $"You do not have permission to write to:{Environment.NewLine}{path}{Environment.NewLine}" +
                    "Choose a folder you can write to, such as your Documents folder.");
            }
            catch (System.Security.SecurityException exception)
            {
                _logger.Error($"Security policy blocked writing the workbook '{path}'.", exception);
                return Result<string>.Failure(
                    "Export.AccessDenied",
                    $"Writing to the chosen location is not permitted:{Environment.NewLine}{path}");
            }
        }

        private void BuildScheduleSheet(XlsxWorkbook workbook, BomDocument document)
        {
            BomOptions options = document.Options;
            string lengthUnit = UnitConverter.Abbreviation(options.LengthUnit);

            XlsxWorksheet sheet = workbook.AddSheet("BOM");
            sheet.SetColumnWidths(6, 22, 14, 26, 30, 12, 8, 12, 14, 14, 14, 16, 14, 14, 18, 30);

            string[] headers =
            {
                "Item", "Category", "Standard", "Profile / designation", "Standard dimensions (mm)",
                $"Length ({lengthUnit})", "Qty", $"Total length ({lengthUnit})", "Unit weight (kg/m)",
                "Piece weight (kg)", "Total weight (kg)", "Total weight (t)", "Material", "Grade",
                "Weight basis", "Notes"
            };

            int columnCount = headers.Length;

            XlsxRow titleRow = sheet.AddRow();
            titleRow.Text(options.Title, XlsxStyle.Title);
            sheet.Merge(titleRow.RowIndex, 1, titleRow.RowIndex, columnCount);

            AppendMetadataRows(sheet, document, columnCount);
            sheet.SkipRows();

            XlsxRow headerRow = sheet.AddRow();
            foreach (string header in headers)
            {
                headerRow.Text(header, XlsxStyle.Header);
            }

            int firstDataRow = headerRow.RowIndex + 1;

            foreach (BomLine line in document.Lines)
            {
                XlsxStyle textStyle = line.IsCustom || !line.SourceDataVerified ? XlsxStyle.Warning : XlsxStyle.Text;

                sheet.AddRow()
                     .Integer(line.ItemNumber)
                     .Text(line.CategoryName, textStyle)
                     .Text(line.StandardId, textStyle)
                     .Text(line.Designation + (line.IsCustom ? " (non-standard)" : string.Empty), textStyle)
                     .Text(line.DimensionSummary, textStyle)
                     .NumberOrDash(line.LengthIn(options.LengthUnit))
                     .Integer(line.Quantity)
                     .NumberOrDash(line.TotalLengthIn(options.LengthUnit))
                     .NumberOrDash(line.UnitWeightKgPerM, XlsxStyle.Number2)
                     .NumberOrDash(line.PieceMassKg)
                     .NumberOrDash(line.TotalMassKg)
                     .NumberOrDash(line.TotalMassTonnes)
                     .Text(line.Material, textStyle)
                     .Text(line.Grade, textStyle)
                     .Text(DescribeWeightSource(line.WeightSource), textStyle)
                     .Text(line.Notes, textStyle);
            }

            int lastDataRow = Math.Max(firstDataRow, sheet.LastRowIndex);

            XlsxRow totalRow = sheet.AddRow();
            totalRow.Text("GRAND TOTAL", XlsxStyle.TotalLabel)
                    .Blank(XlsxStyle.TotalLabel, 4)
                    .Blank(XlsxStyle.TotalLabel)
                    .Integer(document.Totals.TotalPieces, XlsxStyle.TotalInteger)
                    .Number(document.Totals.TotalLengthIn(options.LengthUnit), XlsxStyle.TotalNumber3)
                    .Blank(XlsxStyle.TotalLabel, 2)
                    .Number(document.Totals.TotalMassKg, XlsxStyle.TotalNumber3)
                    .Number(document.Totals.TotalMassTonnes, XlsxStyle.TotalNumber3)
                    .Blank(XlsxStyle.TotalLabel, 4);

            sheet.FrozenRows = headerRow.RowIndex;
            sheet.FrozenColumns = 1;

            if (document.Lines.Count > 0)
            {
                sheet.AutoFilterRange = (headerRow.RowIndex, 1, lastDataRow, columnCount);
            }

            sheet.SkipRows();
            foreach (string caveat in document.Caveats())
            {
                sheet.AddRow().Text(caveat, XlsxStyle.Note);
            }

            sheet.SkipRows();
            sheet.AddRow().Text(Disclaimer, XlsxStyle.Note);
        }

        private static void AppendMetadataRows(XlsxWorksheet sheet, BomDocument document, int columnCount)
        {
            void Metadata(string label, string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                XlsxRow row = sheet.AddRow();
                row.Text(label, XlsxStyle.Label).Text(value, XlsxStyle.Default);
                sheet.Merge(row.RowIndex, 2, row.RowIndex, columnCount);
            }

            Metadata("Project", document.Options.ProjectName);
            Metadata("Drawing", document.Options.DrawingReference);
            Metadata("Source", document.SourceDescription);
            Metadata("Section database", document.DatabaseStamp);

            XlsxRow generated = sheet.AddRow();
            generated.Text("Generated", XlsxStyle.Label).DateTime(document.GeneratedUtc.ToLocalTime());
        }

        private static void BuildSummarySheet(XlsxWorkbook workbook, BomDocument document)
        {
            XlsxWorksheet sheet = workbook.AddSheet("Summary");
            sheet.SetColumnWidths(30, 12, 12, 18, 18, 18, 20);

            XlsxRow title = sheet.AddRow();
            title.Text("SUMMARY BY CATEGORY", XlsxStyle.Title);
            sheet.Merge(title.RowIndex, 1, title.RowIndex, 7);
            sheet.SkipRows();

            string lengthUnit = UnitConverter.Abbreviation(document.Options.LengthUnit);
            XlsxRow header = sheet.AddRow();
            foreach (string caption in new[]
                     {
                         "Category", "Lines", "Pieces", $"Total length ({lengthUnit})",
                         "Total weight (kg)", "Total weight (t)", "Lines with unknown weight"
                     })
            {
                header.Text(caption, XlsxStyle.Header);
            }

            foreach (BomCategorySummary category in document.Categories)
            {
                sheet.AddRow()
                     .Text(category.CategoryName)
                     .Integer(category.Totals.LineCount)
                     .Integer(category.Totals.TotalPieces)
                     .Number(category.Totals.TotalLengthIn(document.Options.LengthUnit))
                     .Number(category.Totals.TotalMassKg)
                     .Number(category.Totals.TotalMassTonnes)
                     .Integer(category.Totals.LinesWithUnknownMass);
            }

            sheet.AddRow()
                 .Text("TOTAL", XlsxStyle.TotalLabel)
                 .Integer(document.Totals.LineCount, XlsxStyle.TotalInteger)
                 .Integer(document.Totals.TotalPieces, XlsxStyle.TotalInteger)
                 .Number(document.Totals.TotalLengthIn(document.Options.LengthUnit), XlsxStyle.TotalNumber3)
                 .Number(document.Totals.TotalMassKg, XlsxStyle.TotalNumber3)
                 .Number(document.Totals.TotalMassTonnes, XlsxStyle.TotalNumber3)
                 .Integer(document.Totals.LinesWithUnknownMass, XlsxStyle.TotalInteger);

            sheet.FrozenRows = header.RowIndex;
        }

        private static void BuildElementDetailSheet(XlsxWorkbook workbook, BomDocument document)
        {
            XlsxWorksheet sheet = workbook.AddSheet("Element Details");
            sheet.SetColumnWidths(6, 14, 38, 26, 14, 8, 14, 16, 16, 14, 18, 44);

            XlsxRow title = sheet.AddRow();
            title.Text("ELEMENT DETAILS", XlsxStyle.Title);
            sheet.Merge(title.RowIndex, 1, title.RowIndex, 12);

            XlsxRow subtitle = sheet.AddRow();
            subtitle.Text(
                "Every generated occurrence, so that any figure on the schedule can be traced back to the object that produced it.",
                XlsxStyle.Note);
            sheet.Merge(subtitle.RowIndex, 1, subtitle.RowIndex, 12);
            sheet.SkipRows();

            XlsxRow header = sheet.AddRow();
            foreach (string caption in new[]
                     {
                         "Item", "Handle", "Element id", "Designation", "Length (mm)", "Qty",
                         "Weight (kg)", "Layer", "Space", "Detail level", "Standard?", "Weight calculation"
                     })
            {
                header.Text(caption, XlsxStyle.Header);
            }

            int firstDataRow = header.RowIndex + 1;

            foreach (BomLine line in document.Lines)
            {
                foreach (BomEntry entry in line.Entries)
                {
                    sheet.AddRow()
                         .Integer(line.ItemNumber)
                         .Text(entry.Handle)
                         .Text(entry.Definition.ElementId.ToString("D", CultureInfo.InvariantCulture))
                         .Text(entry.Definition.Designation)
                         .NumberOrDash(entry.Definition.LengthMm)
                         .Integer(entry.Definition.Quantity)
                         .NumberOrDash(entry.Mass.TotalMassKg)
                         .Text(entry.Layer)
                         .Text(entry.SpaceName)
                         .Text(entry.Definition.DetailLevel.ToString())
                         .Text(entry.Definition.IsCustom ? "Non-standard" : "Standard",
                             entry.Definition.IsCustom ? XlsxStyle.Warning : XlsxStyle.Text)
                         .Text(entry.Mass.Explanation, XlsxStyle.TextWrapped);
                }
            }

            sheet.FrozenRows = header.RowIndex;

            if (sheet.LastRowIndex >= firstDataRow)
            {
                sheet.AutoFilterRange = (header.RowIndex, 1, sheet.LastRowIndex, 12);
            }
        }

        private static void BuildDataSourceSheet(XlsxWorkbook workbook, BomDocument document)
        {
            XlsxWorksheet sheet = workbook.AddSheet("Data Sources");
            sheet.SetColumnWidths(30, 90);

            XlsxRow title = sheet.AddRow();
            title.Text("DATA SOURCES AND DISCLAIMER", XlsxStyle.Title);
            sheet.Merge(title.RowIndex, 1, title.RowIndex, 2);
            sheet.SkipRows();

            void Entry(string label, string? value)
            {
                XlsxRow row = sheet.AddRow();
                row.Text(label, XlsxStyle.Label).Text(value ?? "-", XlsxStyle.TextWrapped);
            }

            Entry("Section database", document.DatabaseStamp);
            Entry("Harvested from", document.SourceDescription);
            Entry("Generated (UTC)", document.GeneratedUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Entry("Schedule lines", document.Totals.LineCount.ToString(CultureInfo.InvariantCulture));
            Entry("Total pieces", document.Totals.TotalPieces.ToString(CultureInfo.InvariantCulture));
            Entry("Lines from unverified data", document.Totals.UnverifiedSourceLines.ToString(CultureInfo.InvariantCulture));
            Entry("Non-conforming lines", document.Totals.NonConformingLines.ToString(CultureInfo.InvariantCulture));
            Entry("Lines with unknown weight", document.Totals.LinesWithUnknownMass.ToString(CultureInfo.InvariantCulture));

            sheet.SkipRows();
            XlsxRow standardsHeading = sheet.AddRow();
            standardsHeading.Text("Standards referenced", XlsxStyle.Label);

            IEnumerable<string> standards = document.Lines
                .Select(l => l.StandardId)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

            foreach (string standard in standards)
            {
                sheet.AddRow().Blank().Text(standard, XlsxStyle.Text);
            }

            sheet.SkipRows();
            foreach (string caveat in document.Caveats())
            {
                XlsxRow row = sheet.AddRow();
                row.Text("Caveat", XlsxStyle.Label).Text(caveat, XlsxStyle.TextWrapped);
            }

            sheet.SkipRows();
            XlsxRow disclaimer = sheet.AddRow();
            disclaimer.Text("Disclaimer", XlsxStyle.Label).Text(Disclaimer, XlsxStyle.TextWrapped);
        }

        private static string DescribeWeightSource(WeightSource source) => source switch
        {
            WeightSource.UnitWeightTable => "Tabulated kg/m",
            WeightSource.PieceMassTable => "Tabulated kg/piece",
            WeightSource.ComputedFromDensity => "Volume x density",
            _ => "Not available"
        };
    }
}
