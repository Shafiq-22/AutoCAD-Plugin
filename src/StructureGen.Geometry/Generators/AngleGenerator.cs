using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>
    /// Generates equal and unequal leg angles.
    /// </summary>
    /// <remarks>
    /// Leg <c>a</c> runs up the positive Y axis and leg <c>b2</c> along the positive X axis, with the
    /// heel at the lower left. An equal angle need only supply <c>a</c>; the second leg then matches
    /// it, so a database of equal angles does not have to repeat the same number twice.
    /// </remarks>
    public sealed class AngleGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required = { DimensionKeys.LegA, DimensionKeys.Thickness };

        /// <inheritdoc />
        public override string CategoryId => ElementCategory.Angle;

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = new()
            {
                SchemaFactory.Locked(DimensionKeys.LegA, "Leg a"),
                SchemaFactory.Locked(DimensionKeys.LegB, "Leg b", false, "Omit for an equal angle; leg a is then used for both."),
                SchemaFactory.Locked(DimensionKeys.Thickness, "Thickness t"),
                SchemaFactory.Locked(DimensionKeys.RootRadius, "Root radius r", false),
                SchemaFactory.Locked(DimensionKeys.ToeRadius, "Toe radius r2", false)
            };

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double legA = Dimension(definition, DimensionKeys.LegA);
            double legB = Dimension(definition, DimensionKeys.LegB, legA);
            double thickness = Dimension(definition, DimensionKeys.Thickness);
            double rootRadius = RadiusForDetail(definition, DimensionKeys.RootRadius, DetailLevel.Standard);
            double toeRadius = RadiusForDetail(definition, DimensionKeys.ToeRadius, DetailLevel.Detailed);

            ProfileLoopBuilder builder = new();
            builder.Add(0d, 0d)
                   .Add(legB, 0d)
                   .Add(legB, thickness, toeRadius)
                   .Add(thickness, thickness, rootRadius)
                   .Add(thickness, legA, toeRadius)
                   .Add(0d, legA);

            ProfileGeometry profile = new ProfileGeometry(builder.Build()).Centred();
            return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            decimal legA = definition.Dimension(DimensionKeys.LegA) ?? 0m;
            decimal legB = definition.Dimension(DimensionKeys.LegB) ?? legA;
            decimal thickness = definition.Dimension(DimensionKeys.Thickness) ?? 0m;

            if (thickness > 0m && (thickness >= legA || thickness >= legB))
            {
                report.AddError(
                    "Angle.ThicknessTooLarge",
                    $"A thickness of {Format(thickness)} mm is not smaller than the leg lengths " +
                    $"({Format(legA)} mm and {Format(legB)} mm), so the angle has no shape.",
                    definition.Designation);
            }
        }
    }
}
