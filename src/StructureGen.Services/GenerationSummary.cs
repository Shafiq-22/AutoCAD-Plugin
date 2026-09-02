using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Results;

namespace StructureGen.Services
{
    /// <summary>What a generation run actually produced.</summary>
    public sealed class GenerationSummary
    {
        /// <summary>Initialises a new instance of the <see cref="GenerationSummary"/> class.</summary>
        /// <param name="createdHandles">The AutoCAD handles of the objects created, keyed by element.</param>
        /// <param name="blockDefinitionsCreated">How many new block definitions were added.</param>
        /// <param name="blockDefinitionsReused">How many existing block definitions were reused.</param>
        /// <param name="report">Everything noticed during the run.</param>
        public GenerationSummary(
            IReadOnlyDictionary<Guid, IReadOnlyList<string>> createdHandles,
            int blockDefinitionsCreated,
            int blockDefinitionsReused,
            ValidationReport report)
        {
            CreatedHandles = createdHandles ?? throw new ArgumentNullException(nameof(createdHandles));
            BlockDefinitionsCreated = blockDefinitionsCreated;
            BlockDefinitionsReused = blockDefinitionsReused;
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        /// <summary>Gets the AutoCAD handles of the objects created, keyed by element.</summary>
        public IReadOnlyDictionary<Guid, IReadOnlyList<string>> CreatedHandles { get; }

        /// <summary>Gets how many new block definitions were added to the drawing.</summary>
        public int BlockDefinitionsCreated { get; }

        /// <summary>
        /// Gets how many existing block definitions were reused.
        /// </summary>
        /// <remarks>
        /// A high reuse count is the point of the design: a thousand identical bolts share one
        /// definition and cost the drawing a thousand lightweight references rather than a thousand
        /// solids.
        /// </remarks>
        public int BlockDefinitionsReused { get; }

        /// <summary>Gets everything noticed during the run.</summary>
        public ValidationReport Report { get; }

        /// <summary>Gets the total number of objects created.</summary>
        public int ObjectCount => CreatedHandles.Values.Sum(handles => handles.Count);

        /// <summary>Gets the number of elements that produced at least one object.</summary>
        public int ElementCount => CreatedHandles.Count;

        /// <summary>Renders a one-line summary suitable for the AutoCAD command line.</summary>
        /// <returns>The summary text.</returns>
        public string ToDisplayString()
        {
            string text = $"StructureGen created {ObjectCount} object(s) from {ElementCount} item(s); " +
                          $"{BlockDefinitionsCreated} new block definition(s), {BlockDefinitionsReused} reused.";

            return Report.HasErrors || Report.HasWarnings
                ? text + $" {Report.ErrorCount} error(s), {Report.WarningCount} warning(s) - see the log for detail."
                : text;
        }
    }
}
