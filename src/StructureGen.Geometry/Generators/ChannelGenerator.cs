using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>
    /// Generates channel sections: UPN, PFC, C and their equivalents.
    /// </summary>
    /// <remarks>
    /// The web lies on the negative X side and the flanges point towards positive X, so a channel
    /// inserted unrotated has its back to the left in plan. Taper on the inside face of the flange
    /// is not modelled; a taper-flange channel that must be exact can carry an explicit outline in
    /// its database record.
    /// </remarks>
    public sealed class ChannelGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required =
        {
            DimensionKeys.Height, DimensionKeys.Width, DimensionKeys.WebThickness, DimensionKeys.FlangeThickness
        };

        /// <inheritdoc />
        public override string CategoryId => ElementCategory.Channel;

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = new()
            {
                SchemaFactory.Locked(DimensionKeys.Height, "Depth h"),
                SchemaFactory.Locked(DimensionKeys.Width, "Flange width b"),
                SchemaFactory.Locked(DimensionKeys.WebThickness, "Web thickness tw"),
                SchemaFactory.Locked(DimensionKeys.FlangeThickness, "Flange thickness tf"),
                SchemaFactory.Locked(DimensionKeys.RootRadius, "Root radius r", false),
                SchemaFactory.Locked(DimensionKeys.ToeRadius, "Toe radius r2", false)
            };

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double height = Dimension(definition, DimensionKeys.Height);
            double width = Dimension(definition, DimensionKeys.Width);
            double webThickness = Dimension(definition, DimensionKeys.WebThickness);
            double flangeThickness = Dimension(definition, DimensionKeys.FlangeThickness);
            double rootRadius = RadiusForDetail(definition, DimensionKeys.RootRadius, DetailLevel.Standard);
            double toeRadius = RadiusForDetail(definition, DimensionKeys.ToeRadius, DetailLevel.Detailed);

            double halfWidth = width / 2d;
            double halfHeight = height / 2d;
            double webFace = -halfWidth + webThickness;
            double lowerFlangeTop = -halfHeight + flangeThickness;
            double upperFlangeBottom = halfHeight - flangeThickness;

            ProfileLoopBuilder builder = new();
            builder.Add(-halfWidth, -halfHeight)
                   .Add(halfWidth, -halfHeight)
                   .Add(halfWidth, lowerFlangeTop, toeRadius)
                   .Add(webFace, lowerFlangeTop, rootRadius)
                   .Add(webFace, upperFlangeBottom, rootRadius)
                   .Add(halfWidth, upperFlangeBottom, toeRadius)
                   .Add(halfWidth, halfHeight)
                   .Add(-halfWidth, halfHeight);

            ProfileGeometry profile = new(builder.Build());
            return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            decimal? height = definition.Dimension(DimensionKeys.Height);
            decimal? flange = definition.Dimension(DimensionKeys.FlangeThickness);
            decimal? width = definition.Dimension(DimensionKeys.Width);
            decimal? web = definition.Dimension(DimensionKeys.WebThickness);

            if (height.HasValue && flange.HasValue && flange.Value * 2m >= height.Value)
            {
                report.AddError(
                    "Channel.NoWeb",
                    $"Two flanges of {Format(flange.Value)} mm do not fit in a depth of {Format(height.Value)} mm.",
                    definition.Designation);
            }

            if (width.HasValue && web.HasValue && web.Value >= width.Value)
            {
                report.AddError(
                    "Channel.WebTooWide",
                    $"A web of {Format(web.Value)} mm is not narrower than the flange width of {Format(width.Value)} mm.",
                    definition.Designation);
            }
        }
    }
}
