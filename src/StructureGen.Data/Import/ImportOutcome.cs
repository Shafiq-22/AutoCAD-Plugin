using System;
using System.Collections.Generic;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Data.Import
{
    /// <summary>The result of importing one tabular source.</summary>
    public sealed class ImportOutcome
    {
        /// <summary>Initialises a new instance of the <see cref="ImportOutcome"/> class.</summary>
        /// <param name="sections">The records that were successfully converted.</param>
        /// <param name="report">Everything the importer noticed, row by row.</param>
        /// <param name="rowsRead">How many data rows were examined.</param>
        public ImportOutcome(IReadOnlyList<SectionRecord> sections, ValidationReport report, int rowsRead)
        {
            Sections = sections ?? throw new ArgumentNullException(nameof(sections));
            Report = report ?? throw new ArgumentNullException(nameof(report));
            RowsRead = rowsRead;
        }

        /// <summary>Gets the records that were successfully converted.</summary>
        public IReadOnlyList<SectionRecord> Sections { get; }

        /// <summary>Gets everything the importer noticed, row by row.</summary>
        public ValidationReport Report { get; }

        /// <summary>Gets how many data rows were examined.</summary>
        public int RowsRead { get; }

        /// <summary>Gets how many rows were skipped because they could not be converted.</summary>
        public int RowsSkipped => RowsRead - Sections.Count;
    }
}
