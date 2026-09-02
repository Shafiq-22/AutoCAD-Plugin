using System;
using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;

namespace StructureGen.Geometry.Generators
{
    /// <summary>
    /// Generates doubly symmetric I and H sections: IPE, HEA, HEB, UB, UC, W and their equivalents.
    /// </summary>
    /// <remarks>
    /// The outline is the classic eight-corner I with four concave root fillets where the flanges
    /// meet the web. Flange taper is not modelled: the overwhelming majority of sections detailed
    /// today are parallel flange, and a tapered-flange profile that must be exact can be described
    /// with an explicit outline in the database record.
    /// </remarks>
    public sealed class ISectionGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required =
        {
            DimensionKeys.Height, DimensionKeys.Width, DimensionKeys.WebThickness, DimensionKeys.FlangeThickness
        };

        /// <summary>Initialises a new instance of the <see cref="ISectionGenerator"/> class.</summary>
        /// <param name="categoryId">
        /// The category served. I and H sections share their geometry entirely and differ only in
        /// how a catalogue groups them, so one implementation is registered under both.
        /// </param>
        public ISectionGenerator(string categoryId = ElementCategory.ISection) =>
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));

        /// <inheritdoc />
        public override string CategoryId { get; }

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
            double webThickness = Dimension(definition, DimensionKeys.WebThickness);
            double flangeThickness = Dimension(definition, DimensionKeys.FlangeThickness);
            double rootRadius = RadiusForDetail(definition, DimensionKeys.RootRadius, DetailLevel.Standard);

            double halfWidth = width / 2d;
            double halfHeight = height / 2d;
            double halfWeb = webThickness / 2d;
            double flangeTop = -halfHeight + flangeThickness;
            double flangeBottom = halfHeight - flangeThickness;

            ProfileLoopBuilder builder = new();
            builder.Add(halfWidth, -halfHeight)
                   .Add(halfWidth, flangeTop)
                   .Add(halfWeb, flangeTop, rootRadius)
                   .Add(halfWeb, flangeBottom, rootRadius)
                   .Add(halfWidth, flangeBottom)
                   .Add(halfWidth, halfHeight)
                   .Add(-halfWidth, halfHeight)
                   .Add(-halfWidth, flangeBottom)
                   .Add(-halfWeb, flangeBottom, rootRadius)
                   .Add(-halfWeb, flangeTop, rootRadius)
                   .Add(-halfWidth, flangeTop)
                   .Add(-halfWidth, -halfHeight);

            ProfileGeometry profile = new(builder.Build());
            return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, Core.Results.ValidationReport report)
        {
            decimal? height = definition.Dimension(DimensionKeys.Height);
            decimal? flange = definition.Dimension(DimensionKeys.FlangeThickness);
            decimal? width = definition.Dimension(DimensionKeys.Width);
            decimal? web = definition.Dimension(DimensionKeys.WebThickness);

            if (height.HasValue && flange.HasValue && flange.Value * 2m >= height.Value)
            {
                report.AddError(
                    "ISection.NoWeb",
                    $"Two flanges of {Format(flange.Value)} mm do not fit in a depth of {Format(height.Value)} mm.",
                    definition.Designation);
            }

            if (width.HasValue && web.HasValue && web.Value >= width.Value)
            {
                report.AddError(
                    "ISection.WebTooWide",
                    $"A web of {Format(web.Value)} mm is not narrower than the flange width of {Format(width.Value)} mm.",
                    definition.Designation);
            }
        }
    }
}
