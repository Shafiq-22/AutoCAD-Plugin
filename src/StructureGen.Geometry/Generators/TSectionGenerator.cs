using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>Generates T sections, whether rolled or split from an I section.</summary>
    /// <remarks>The flange is at the top and the stem hangs below it.</remarks>
    public sealed class TSectionGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required =
        {
            DimensionKeys.Height, DimensionKeys.Width, DimensionKeys.WebThickness, DimensionKeys.FlangeThickness
        };

        /// <inheritdoc />
        public override string CategoryId => ElementCategory.TSection;

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = new()
            {
                SchemaFactory.Locked(DimensionKeys.Height, "Overall depth h"),
                SchemaFactory.Locked(DimensionKeys.Width, "Flange width b"),
                SchemaFactory.Locked(DimensionKeys.WebThickness, "Stem thickness tw"),
                SchemaFactory.Locked(DimensionKeys.FlangeThickness, "Flange thickness tf"),
                SchemaFactory.Locked(DimensionKeys.RootRadius, "Root radius r", false)
            };

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double height = Dimension(definition, DimensionKeys.Height);
            double width = Dimension(definition, DimensionKeys.Width);
            double stemThickness = Dimension(definition, DimensionKeys.WebThickness);
            double flangeThickness = Dimension(definition, DimensionKeys.FlangeThickness);
            double rootRadius = RadiusForDetail(definition, DimensionKeys.RootRadius, DetailLevel.Standard);

            double halfWidth = width / 2d;
            double halfHeight = height / 2d;
            double halfStem = stemThickness / 2d;
            double flangeUnderside = halfHeight - flangeThickness;

            ProfileLoopBuilder builder = new();
            builder.Add(-halfStem, -halfHeight)
                   .Add(halfStem, -halfHeight)
                   .Add(halfStem, flangeUnderside, rootRadius)
                   .Add(halfWidth, flangeUnderside)
                   .Add(halfWidth, halfHeight)
                   .Add(-halfWidth, halfHeight)
                   .Add(-halfWidth, flangeUnderside)
                   .Add(-halfStem, flangeUnderside, rootRadius);

            ProfileGeometry profile = new(builder.Build());
            return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            decimal? height = definition.Dimension(DimensionKeys.Height);
            decimal? flange = definition.Dimension(DimensionKeys.FlangeThickness);

            if (height.HasValue && flange.HasValue && flange.Value >= height.Value)
            {
                report.AddError(
                    "TSection.NoStem",
                    $"A flange of {Format(flange.Value)} mm leaves no stem in a depth of {Format(height.Value)} mm.",
                    definition.Designation);
            }
        }
    }
}
