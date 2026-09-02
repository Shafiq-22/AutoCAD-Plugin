using System;
using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;

namespace StructureGen.Geometry.Generators
{
    /// <summary>Generates plates and flat bars: a rectangular cross-section extruded along its length.</summary>
    /// <remarks>
    /// Plates and flat bars have the same shape but different rules. A flat bar is rolled to
    /// standard sizes, so its width and thickness are locked to the standard. A plate is cut from
    /// sheet to whatever size the job needs, so both are adjustable — which is exactly the
    /// distinction the parameter schema exists to express.
    /// </remarks>
    public sealed class RectangularBarGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required = { DimensionKeys.PlateWidth, DimensionKeys.Thickness };

        /// <summary>Initialises a new instance of the <see cref="RectangularBarGenerator"/> class.</summary>
        /// <param name="categoryId">The category served: a plate or a flat bar.</param>
        public RectangularBarGenerator(string categoryId = ElementCategory.Plate) =>
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));

        /// <inheritdoc />
        public override string CategoryId { get; }

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        private bool IsPlate => string.Equals(CategoryId, ElementCategory.Plate, StringComparison.Ordinal);

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = IsPlate
                ? new List<ParameterDescriptor>
                {
                    SchemaFactory.Adjustable(
                        DimensionKeys.PlateWidth, "Width", true, 0m, null,
                        "Plates are cut to size, so the width is yours to set."),
                    SchemaFactory.Adjustable(
                        DimensionKeys.Thickness, "Thickness", true, 0m, null,
                        "Usually a stocked plate thickness, but not fixed by the profile.")
                }
                : new List<ParameterDescriptor>
                {
                    SchemaFactory.Locked(DimensionKeys.PlateWidth, "Width"),
                    SchemaFactory.Locked(DimensionKeys.Thickness, "Thickness")
                };

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double width = Dimension(definition, DimensionKeys.PlateWidth);
            double thickness = Dimension(definition, DimensionKeys.Thickness);

            ProfileGeometry profile = new(ProfileLoopBuilder.CentredRectangle(width, thickness));
            return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
        }
    }
}
