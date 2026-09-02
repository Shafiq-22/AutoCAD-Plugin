using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Results;

namespace StructureGen.Bom
{
    /// <summary>A finished bill of materials: its lines, its subtotals and its provenance.</summary>
    public sealed class BomDocument
    {
        /// <summary>Initialises a new instance of the <see cref="BomDocument"/> class.</summary>
        /// <param name="lines">The schedule lines, already numbered.</param>
        /// <param name="options">The options the schedule was assembled with.</param>
        /// <param name="databaseStamp">The name and version of the database the elements came from.</param>
        /// <param name="generatedUtc">When the schedule was assembled.</param>
        /// <param name="report">Anything noticed while assembling it.</param>
        /// <param name="sourceDescription">Where the elements were harvested from.</param>
        public BomDocument(
            IReadOnlyList<BomLine> lines,
            BomOptions options,
            string? databaseStamp = null,
            DateTime? generatedUtc = null,
            ValidationReport? report = null,
            string? sourceDescription = null)
        {
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            DatabaseStamp = databaseStamp;
            GeneratedUtc = generatedUtc ?? DateTime.UtcNow;
            Report = report ?? new ValidationReport();
            SourceDescription = sourceDescription;

            Totals = new BomTotals(lines);
            Categories = lines
                .GroupBy(l => l.CategoryId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new BomCategorySummary(g.Key, g.ToList()))
                .ToList();
        }

        /// <summary>Gets the schedule lines, already numbered.</summary>
        public IReadOnlyList<BomLine> Lines { get; }

        /// <summary>Gets the options the schedule was assembled with.</summary>
        public BomOptions Options { get; }

        /// <summary>Gets the grand totals.</summary>
        public BomTotals Totals { get; }

        /// <summary>Gets the per-category subtotals.</summary>
        public IReadOnlyList<BomCategorySummary> Categories { get; }

        /// <summary>Gets the name and version of the database the elements came from.</summary>
        public string? DatabaseStamp { get; }

        /// <summary>Gets when the schedule was assembled.</summary>
        public DateTime GeneratedUtc { get; }

        /// <summary>Gets where the elements were harvested from.</summary>
        public string? SourceDescription { get; }

        /// <summary>Gets anything noticed while assembling the schedule.</summary>
        public ValidationReport Report { get; }

        /// <summary>Gets a value indicating whether the schedule has no lines.</summary>
        public bool IsEmpty => Lines.Count == 0;

        /// <summary>
        /// Gets the warnings that must accompany the schedule wherever it is shown, printed or
        /// exported.
        /// </summary>
        /// <returns>The warning lines, which may be empty.</returns>
        public IReadOnlyList<string> Caveats()
        {
            List<string> caveats = new();

            if (Totals.LinesWithUnknownMass > 0)
            {
                caveats.Add(
                    $"{Totals.LinesWithUnknownMass} line(s) covering {Totals.PiecesWithUnknownMass} piece(s) have no " +
                    "established weight and are EXCLUDED from the totals. Their weight is shown as a dash, not as zero.");
            }

            if (Totals.NonConformingLines > 0)
            {
                caveats.Add(
                    $"{Totals.NonConformingLines} line(s) have been edited away from their standard profile and are " +
                    "marked as non-conforming.");
            }

            if (Totals.UnverifiedSourceLines > 0)
            {
                caveats.Add(
                    $"{Totals.UnverifiedSourceLines} line(s) come from database records that are not marked as verified. " +
                    "Check their dimensions and weights against your authoritative copy of the standard before using " +
                    "this schedule for procurement or fabrication.");
            }

            return caveats;
        }
    }
}
