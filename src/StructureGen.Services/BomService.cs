using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StructureGen.Bom;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Settings;
using StructureGen.Excel;

namespace StructureGen.Services
{
    /// <summary>Assembles schedules and exports them.</summary>
    public sealed class BomService
    {
        private readonly BomBuilder _builder = new();
        private readonly MassCalculator _mass;
        private readonly DatabaseService _database;
        private readonly BomWorkbookBuilder _workbooks;
        private readonly ILogger _logger;

        /// <summary>Initialises a new instance of the <see cref="BomService"/> class.</summary>
        /// <param name="mass">Computes the weight of each occurrence.</param>
        /// <param name="database">Supplies the database stamp recorded on the schedule.</param>
        /// <param name="workbooks">Renders a schedule as a workbook.</param>
        /// <param name="logger">Receives technical detail about failures.</param>
        public BomService(
            MassCalculator mass, DatabaseService database, BomWorkbookBuilder workbooks, ILogger logger)
        {
            _mass = mass ?? throw new ArgumentNullException(nameof(mass));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _workbooks = workbooks ?? throw new ArgumentNullException(nameof(workbooks));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Builds a schedule from occurrences already harvested from a drawing.</summary>
        /// <param name="entries">The occurrences.</param>
        /// <param name="options">The grouping and presentation rules.</param>
        /// <param name="sourceDescription">Where the occurrences came from.</param>
        /// <returns>The schedule.</returns>
        public BomDocument Build(
            IEnumerable<BomEntry> entries, BomOptions options, string? sourceDescription = null) =>
            _builder.Build(entries, options, _database.Stamp, sourceDescription);

        /// <summary>
        /// Builds a preview schedule from definitions that have not been drawn yet, so the palette
        /// can show weights before anything is committed to the drawing.
        /// </summary>
        /// <param name="definitions">The elements to schedule.</param>
        /// <param name="options">The grouping and presentation rules.</param>
        /// <returns>The schedule.</returns>
        /// <remarks>
        /// A preview is explicitly labelled as such. The authoritative schedule is always the one
        /// harvested from the drawing, because that is the one that reflects what was actually
        /// created, copied or erased.
        /// </remarks>
        public BomDocument BuildPreview(IEnumerable<ElementDefinition> definitions, BomOptions options)
        {
            if (definitions is null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            List<BomEntry> entries = definitions
                .Select(definition => new BomEntry(definition, _mass.Compute(definition), null, definition.Layer))
                .ToList();

            return _builder.Build(entries, options, _database.Stamp, "Palette list (preview - not yet drawn)");
        }

        /// <summary>Exports a schedule to a workbook.</summary>
        /// <param name="document">The schedule to export.</param>
        /// <param name="path">The file to write.</param>
        /// <returns>The path written, or a failure carrying a user-facing reason.</returns>
        public Result<string> Export(BomDocument document, string path) => _workbooks.Export(document, path);

        /// <summary>
        /// Suggests a file name for an export, based on the drawing name and the current time.
        /// </summary>
        /// <param name="documentName">The drawing name, which may be a full path or <see langword="null"/>.</param>
        /// <param name="directory">The folder to place it in, or <see langword="null"/> for the user's documents folder.</param>
        /// <returns>An absolute path that does not yet exist.</returns>
        public string SuggestExportPath(string? documentName, string? directory = null)
        {
            string folder = string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : directory!;

            string stem = string.IsNullOrWhiteSpace(documentName)
                ? "StructureGen-BOM"
                : Path.GetFileNameWithoutExtension(documentName) + "-BOM";

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                stem = stem.Replace(invalid, '-');
            }

            string candidate = Path.Combine(folder, $"{stem}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
            int suffix = 1;

            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder, $"{stem}-{DateTime.Now:yyyyMMdd-HHmm}-{suffix++}.xlsx");
            }

            return candidate;
        }

        /// <summary>Builds the options a schedule should use from the current settings.</summary>
        /// <param name="settings">The settings to read.</param>
        /// <returns>The options.</returns>
        public static BomOptions OptionsFrom(ProjectSettings settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return new BomOptions
            {
                GroupIdenticalElements = settings.GroupIdenticalElements,
                LengthToleranceMm = settings.LengthGroupingToleranceMm,
                GroupByLayer = settings.GroupByLayer,
                LengthUnit = settings.BomLengthUnit,
                MassUnit = settings.DisplayMassUnit
            };
        }

        /// <summary>Logs a summary of an assembled schedule.</summary>
        /// <param name="document">The schedule to log.</param>
        public void LogSummary(BomDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _logger.Information(
                $"Bill of materials: {document.Totals.LineCount} line(s), {document.Totals.TotalPieces} piece(s), " +
                $"{document.Totals.TotalMassKg:0.###} kg, {document.Totals.LinesWithUnknownMass} line(s) with unknown weight.");
        }
    }
}
