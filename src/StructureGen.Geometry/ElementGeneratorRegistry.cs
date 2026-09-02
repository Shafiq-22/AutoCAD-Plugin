using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Model;

namespace StructureGen.Geometry
{
    /// <summary>
    /// Resolves the generator that serves a category, and reports to the rest of the application
    /// which categories can be generated.
    /// </summary>
    /// <remarks>
    /// The registry is also the <see cref="ISectionRequirementProvider"/> the database validator
    /// uses. That single connection is what makes the element model genuinely extensible: register
    /// a new generator and the database validator immediately starts checking that category's
    /// records for the dimensions the new generator needs, with no change to the validator.
    /// </remarks>
    public sealed class ElementGeneratorRegistry : ISectionRequirementProvider
    {
        private readonly Dictionary<string, IElementGenerator> _generators;

        /// <summary>Initialises a new instance of the <see cref="ElementGeneratorRegistry"/> class.</summary>
        /// <param name="generators">The generators to register.</param>
        /// <exception cref="ArgumentException">Two generators claim the same category.</exception>
        public ElementGeneratorRegistry(IEnumerable<IElementGenerator> generators)
        {
            if (generators is null)
            {
                throw new ArgumentNullException(nameof(generators));
            }

            _generators = new Dictionary<string, IElementGenerator>(StringComparer.OrdinalIgnoreCase);

            foreach (IElementGenerator generator in generators)
            {
                if (!_generators.TryAdd(generator.CategoryId, generator))
                {
                    throw new ArgumentException(
                        $"More than one generator is registered for the category '{generator.CategoryId}'.",
                        nameof(generators));
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyCollection<string> SupportedCategories => _generators.Keys;

        /// <summary>Gets every registered generator.</summary>
        public IReadOnlyCollection<IElementGenerator> Generators => _generators.Values;

        /// <summary>Finds the generator for a category.</summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>The generator, or <see langword="null"/> when none is registered.</returns>
        public IElementGenerator? Find(string? categoryId) =>
            categoryId is not null && _generators.TryGetValue(categoryId, out IElementGenerator? generator)
                ? generator
                : null;

        /// <summary>Finds the generator for a category, throwing when none is registered.</summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>The generator.</returns>
        /// <exception cref="InvalidOperationException">No generator serves the category.</exception>
        public IElementGenerator Require(string categoryId) =>
            Find(categoryId) ?? throw new InvalidOperationException(
                $"No geometry generator is registered for the category '{categoryId}'. " +
                $"Registered categories are: {string.Join(", ", _generators.Keys.OrderBy(k => k, StringComparer.Ordinal))}.");

        /// <inheritdoc />
        public IReadOnlyList<string> RequiredDimensions(string categoryId) =>
            Find(categoryId)?.RequiredDimensions ?? Array.Empty<string>();

        /// <summary>
        /// Builds a registry holding every generator that ships with StructureGen.
        /// </summary>
        /// <param name="recordLookup">
        /// Resolves a record identifier to its record, needed by the custom profile generator to
        /// recover an explicit outline.
        /// </param>
        /// <returns>The registry.</returns>
        public static ElementGeneratorRegistry CreateDefault(Func<string, SectionRecord?> recordLookup) =>
            new(new IElementGenerator[]
            {
                new Generators.ISectionGenerator(ElementCategory.ISection),
                new Generators.ISectionGenerator(ElementCategory.HSection),
                new Generators.ChannelGenerator(),
                new Generators.AngleGenerator(),
                new Generators.TSectionGenerator(),
                new Generators.PipeGenerator(),
                new Generators.HollowSectionGenerator(ElementCategory.RectangularHollowSection),
                new Generators.HollowSectionGenerator(ElementCategory.SquareHollowSection),
                new Generators.RectangularBarGenerator(ElementCategory.Plate),
                new Generators.RectangularBarGenerator(ElementCategory.FlatBar),
                new Generators.RoundBarGenerator(),
                new Generators.BoltGenerator(),
                new Generators.NutGenerator(),
                new Generators.WasherGenerator(),
                new Generators.CustomProfileGenerator(recordLookup)
            });
    }
}
