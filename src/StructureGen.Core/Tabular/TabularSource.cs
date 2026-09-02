using System;
using System.Collections.Generic;
using System.Linq;

namespace StructureGen.Core.Tabular
{
    /// <summary>A materialised, in-memory <see cref="ITabularSource"/>.</summary>
    public sealed class TabularSource : ITabularSource
    {
        /// <summary>Initialises a new instance of the <see cref="TabularSource"/> class.</summary>
        /// <param name="name">A name describing the source, used in error messages.</param>
        /// <param name="headers">The header row.</param>
        /// <param name="rows">The data rows.</param>
        public TabularSource(string name, IEnumerable<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Headers = (headers ?? throw new ArgumentNullException(nameof(headers))).ToList();
            Rows = (rows ?? throw new ArgumentNullException(nameof(rows))).ToList();
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public IReadOnlyList<string> Headers { get; }

        /// <inheritdoc />
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; }
    }
}
