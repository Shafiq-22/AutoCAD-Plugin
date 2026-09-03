using System;
using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>Generates rectangular and square hollow sections.</summary>
    /// <remarks>
    /// A square hollow section need only supply its depth; the width then matches it.
    ///
    /// The inside corner radius is taken from the database where it gives one, and otherwise
    /// derived as the outside radius less the wall thickness, which is the shape a tube of uniform
    /// wall actually has; either way it is clamped to a sharp corner rather than allowed to go
    /// negative. The distinction is not academic. EN 10210 defines the corner of a hot finished
    /// section as 1.5t outside against 1.0t inside, so the wall is thinner around the corner than
    /// along the flat, and assuming a uniform wall there under-states the area those tables publish
    /// by up to three per cent on a thick walled section.
    /// </remarks>
    public sealed class HollowSectionGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required = { DimensionKeys.Height, DimensionKeys.WallThickness };

        /// <summary>Initialises a new instance of the <see cref="HollowSectionGenerator"/> class.</summary>
        /// <param name="categoryId">
        /// The category served: rectangular or square. The two differ only in whether a width is
        /// expected alongside the depth.
        /// </param>
        public HollowSectionGenerator(string categoryId = ElementCategory.RectangularHollowSection) =>
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));

        /// <inheritdoc />
        public override string CategoryId { get; }

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        private bool IsSquare => string.Equals(CategoryId, ElementCategory.SquareHollowSection, StringComparison.Ordinal);

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = new()
            {
                SchemaFactory.Locked(DimensionKeys.Height, IsSquare ? "Size h" : "Depth h")
            };

            if (!IsSquare)
            {
                parameters.Add(SchemaFactory.Locked(DimensionKeys.Width, "Width b"));
            }

            parameters.Add(SchemaFactory.Locked(DimensionKeys.WallThickness, "Wall thickness t"));
            parameters.Add(SchemaFactory.Locked(DimensionKeys.OuterCornerRadius, "Outside corner radius", false));
            parameters.Add(SchemaFactory.Locked(DimensionKeys.InnerCornerRadius, "Inside corner radius", false));

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double height = Dimension(definition, DimensionKeys.Height);
            double width = IsSquare ? height : Dimension(definition, DimensionKeys.Width, height);
            double wall = Dimension(definition, DimensionKeys.WallThickness);
            double outerRadius = RadiusForDetail(definition, DimensionKeys.OuterCornerRadius, DetailLevel.Standard);
            double statedInner = RadiusForDetail(definition, DimensionKeys.InnerCornerRadius, DetailLevel.Standard);
            double innerRadius = Math.Max(0d, statedInner > 0d ? statedInner : outerRadius - wall);

            List<ProfileLoop> loops = new()
            {
                ProfileLoopBuilder.CentredRectangle(width, height, outerRadius)
            };

            double innerWidth = width - (2d * wall);
            double innerHeight = height - (2d * wall);
            if (innerWidth > 0d && innerHeight > 0d)
            {
                loops.Add(ProfileLoopBuilder.CentredRectangle(innerWidth, innerHeight, innerRadius, false));
            }

            ProfileGeometry profile = new(loops);
            return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            decimal height = definition.Dimension(DimensionKeys.Height) ?? 0m;
            decimal width = IsSquare ? height : definition.Dimension(DimensionKeys.Width) ?? height;
            decimal wall = definition.Dimension(DimensionKeys.WallThickness) ?? 0m;

            if (wall > 0m && (wall * 2m >= height || wall * 2m >= width))
            {
                report.AddError(
                    "HollowSection.NoVoid",
                    $"A wall of {Format(wall)} mm leaves no void in a {Format(width)} x {Format(height)} mm section.",
                    definition.Designation);
            }
        }
    }
}
