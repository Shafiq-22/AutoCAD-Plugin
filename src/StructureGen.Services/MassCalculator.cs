using System;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Geometry;

namespace StructureGen.Services
{
    /// <summary>Computes the mass of an element through its category's generator.</summary>
    /// <remarks>
    /// Weight is never computed twice by two different routes. The palette, the schedule harvested
    /// from the drawing and the exported workbook all arrive at their figures through this one call,
    /// so a weight shown in the palette and the same weight on the exported schedule cannot drift
    /// apart.
    /// </remarks>
    public sealed class MassCalculator
    {
        private readonly ElementGeneratorRegistry _generators;

        /// <summary>Initialises a new instance of the <see cref="MassCalculator"/> class.</summary>
        /// <param name="generators">Supplies the per-category mass rules.</param>
        public MassCalculator(ElementGeneratorRegistry generators) =>
            _generators = generators ?? throw new ArgumentNullException(nameof(generators));

        /// <summary>Computes the mass of an element's whole quantity.</summary>
        /// <param name="definition">The element to weigh.</param>
        /// <returns>The mass figures together with a traceable explanation.</returns>
        public MassProperties Compute(ElementDefinition definition)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            IElementGenerator? generator = _generators.Find(definition.CategoryId);
            if (generator is null)
            {
                return MassProperties.Unavailable(
                    $"No generator is registered for the category '{definition.CategoryId}', so no weight can be computed.");
            }

            // Geometry is only needed when the mass has to come from volume and density. Building it
            // is cheap compared with creating entities, and skipping it when a tabulated mass exists
            // keeps a large schedule fast.
            ElementGeometry? geometry = null;
            if (RequiresGeometry(definition))
            {
                Result<ElementGeometry> built = generator.BuildGeometry(definition);
                if (built.IsFailure)
                {
                    return MassProperties.Unavailable(
                        "The geometry could not be built, so no weight could be derived from its volume: " +
                        built.ErrorMessage);
                }

                geometry = built.Value;
            }

            return generator.ComputeMass(definition, geometry);
        }

        private static bool RequiresGeometry(ElementDefinition definition)
        {
            CategoryDescriptor descriptor = CategoryCatalog.FindOrGeneric(definition.CategoryId);

            bool hasTabulatedMass = descriptor.Kind == CategoryKind.Fastener
                ? definition.PieceMassKg is > 0m
                : definition.UnitWeightKgPerM is > 0m;

            return !hasTabulatedMass;
        }
    }
}
