using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>Generates hexagon nuts as a hexagonal prism with a central bore.</summary>
    /// <remarks>
    /// The bore is drawn at the thread minor diameter when the database publishes one and at the
    /// nominal thread diameter otherwise. As with bolts, no thread form is modelled.
    /// </remarks>
    public sealed class NutGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required =
        {
            DimensionKeys.ThreadDiameter, DimensionKeys.AcrossFlats, DimensionKeys.NutHeight
        };

        /// <inheritdoc />
        public override string CategoryId => ElementCategory.Nut;

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = new()
            {
                SchemaFactory.Locked(DimensionKeys.ThreadDiameter, "Thread diameter d"),
                SchemaFactory.Locked(DimensionKeys.AcrossFlats, "Across flats s"),
                SchemaFactory.Locked(DimensionKeys.NutHeight, "Height m"),
                SchemaFactory.Locked(FastenerDimensionKeys.ThreadMinorDiameter, "Thread minor diameter", false)
            };

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double threadDiameter = Dimension(definition, DimensionKeys.ThreadDiameter);
            double acrossFlats = Dimension(definition, DimensionKeys.AcrossFlats);
            double height = Dimension(definition, DimensionKeys.NutHeight);
            double minorDiameter = Dimension(definition, FastenerDimensionKeys.ThreadMinorDiameter);

            double boreDiameter = minorDiameter > 0d && minorDiameter < threadDiameter ? minorDiameter : threadDiameter;

            List<ProfileLoop> loops = new()
            {
                ProfileLoopBuilder.RegularPolygonAcrossFlats(Vec2.Zero, acrossFlats),
                ProfileLoopBuilder.Circle(Vec2.Zero, boreDiameter / 2d, false)
            };

            ProfileGeometry profile = new(loops);
            return new ElementGeometry(profile, height, "nut");
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            decimal threadDiameter = definition.Dimension(DimensionKeys.ThreadDiameter) ?? 0m;
            decimal acrossFlats = definition.Dimension(DimensionKeys.AcrossFlats) ?? 0m;

            if (threadDiameter > 0m && acrossFlats > 0m && acrossFlats <= threadDiameter)
            {
                report.AddError(
                    "Nut.BoreTooLarge",
                    $"A bore of {Format(threadDiameter)} mm does not fit inside an across-flats dimension of " +
                    $"{Format(acrossFlats)} mm.",
                    definition.Designation);
            }
        }
    }
}
