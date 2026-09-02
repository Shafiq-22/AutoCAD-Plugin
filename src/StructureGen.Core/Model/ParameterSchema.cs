using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StructureGen.Core.Model
{
    /// <summary>The complete set of parameters a generator exposes for one section record.</summary>
    public sealed class ParameterSchema
    {
        private readonly Dictionary<string, ParameterDescriptor> _byKey;

        /// <summary>Initialises a new instance of the <see cref="ParameterSchema"/> class.</summary>
        /// <param name="categoryId">The category the schema belongs to.</param>
        /// <param name="descriptors">The parameters, in the order they should be displayed.</param>
        public ParameterSchema(string categoryId, IEnumerable<ParameterDescriptor> descriptors)
        {
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));
            Parameters = new ReadOnlyCollection<ParameterDescriptor>(
                (descriptors ?? throw new ArgumentNullException(nameof(descriptors))).ToList());
            _byKey = Parameters.ToDictionary(p => p.Key, StringComparer.Ordinal);
        }

        /// <summary>Gets the category the schema belongs to.</summary>
        public string CategoryId { get; }

        /// <summary>Gets the parameters, in display order.</summary>
        public IReadOnlyList<ParameterDescriptor> Parameters { get; }

        /// <summary>Gets the parameters that are standardised and therefore read-only by default.</summary>
        public IEnumerable<ParameterDescriptor> StandardDimensions =>
            Parameters.Where(p => p.Mutability == ParameterMutability.StandardLocked);

        /// <summary>Gets the parameters the standard leaves genuinely open.</summary>
        public IEnumerable<ParameterDescriptor> AdjustableDimensions =>
            Parameters.Where(p => p.Mutability == ParameterMutability.Adjustable);

        /// <summary>Gets the parameters that belong to the individual occurrence.</summary>
        public IEnumerable<ParameterDescriptor> InstanceParameters =>
            Parameters.Where(p => p.Mutability == ParameterMutability.Instance);

        /// <summary>Finds a parameter by key.</summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>The descriptor, or <see langword="null"/> when the schema has no such parameter.</returns>
        public ParameterDescriptor? Find(string key) =>
            key is not null && _byKey.TryGetValue(key, out ParameterDescriptor? descriptor) ? descriptor : null;

        /// <summary>Determines whether a key names a standardised, locked dimension.</summary>
        /// <param name="key">The key to test.</param>
        /// <returns><see langword="true"/> when the parameter exists and is locked.</returns>
        public bool IsLocked(string key) => Find(key)?.Mutability == ParameterMutability.StandardLocked;
    }
}
