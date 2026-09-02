using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>Generates plain washers as an annular disc.</summary>
    public sealed class WasherGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required =
        {
            DimensionKeys.WasherInsideDiameter, DimensionKeys.WasherOutsideDiameter, DimensionKeys.WasherThickness
        };

        /// <inheritdoc />
        public override string CategoryId => ElementCategory.Washer;

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = new()
            {
                SchemaFactory.Locked(DimensionKeys.WasherInsideDiameter, "Inside diameter d1"),
                SchemaFactory.Locked(DimensionKeys.WasherOutsideDiameter, "Outside diameter d2"),
                SchemaFactory.Locked(DimensionKeys.WasherThickness, "Thickness h")
            };

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double inside = Dimension(definition, DimensionKeys.WasherInsideDiameter);
            double outside = Dimension(definition, DimensionKeys.WasherOutsideDiameter);
            double thickness = Dimension(definition, DimensionKeys.WasherThickness);

            List<ProfileLoop> loops = new()
            {
                ProfileLoopBuilder.Circle(Vec2.Zero, outside / 2d),
                ProfileLoopBuilder.Circle(Vec2.Zero, inside / 2d, false)
            };

            ProfileGeometry profile = new(loops);
            return new ElementGeometry(profile, thickness, "washer");
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            decimal inside = definition.Dimension(DimensionKeys.WasherInsideDiameter) ?? 0m;
            decimal outside = definition.Dimension(DimensionKeys.WasherOutsideDiameter) ?? 0m;

            if (inside > 0m && outside > 0m && inside >= outside)
            {
                report.AddError(
                    "Washer.NoMaterial",
                    $"The inside diameter of {Format(inside)} mm is not smaller than the outside diameter of " +
                    $"{Format(outside)} mm, so the washer has no material.",
                    definition.Designation);
            }
        }
    }
}
