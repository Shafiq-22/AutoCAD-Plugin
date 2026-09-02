using System.Collections.Generic;

namespace StructureGen.Core.Tabular
{
    /// <summary>
    /// A read-only table of text cells, used as the input to the database importer.
    /// </summary>
    /// <remarks>
    /// Defining the seam here rather than in a concrete reader is what lets the importer accept a
    /// CSV file, a worksheet or an in-memory fixture without knowing the difference, and it keeps
    /// the CSV reader and the spreadsheet reader independent of one another.
    /// </remarks>
    public interface ITabularSource
    {
        /// <summary>Gets a name describing the source, used in error messages.</summary>
        string Name { get; }

        /// <summary>Gets the header row, if the source has one.</summary>
        IReadOnlyList<string> Headers { get; }

        /// <summary>Gets the data rows, excluding the header.</summary>
        IReadOnlyList<IReadOnlyList<string>> Rows { get; }
    }
}
