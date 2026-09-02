using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;

namespace StructureGen.Geometry.Generators
{
    /// <summary>Generates solid round bar.</summary>
    public sealed class RoundBarGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required = { DimensionKeys.OutsideDiameter };

        /// <inheritdoc />
        public override string CategoryId => ElementCategory.RoundBar;

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = new()
            {
                SchemaFactory.Locked(DimensionKeys.OutsideDiameter, "Diameter d")
            };

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double diameter = Dimension(definition, DimensionKeys.OutsideDiameter);
            ProfileGeometry profile = new(ProfileLoopBuilder.Circle(Vec2.Zero, diameter / 2d));
            return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
        }
    }
}
