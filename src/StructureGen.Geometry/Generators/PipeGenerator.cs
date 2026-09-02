using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>Generates pipes and circular hollow sections from an outside diameter and a wall thickness.</summary>
    /// <remarks>
    /// The bore is a true circular void rather than a polygonal approximation, so a nominal bore
    /// measured off the generated solid matches the tabulated value exactly.
    /// </remarks>
    public sealed class PipeGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required = { DimensionKeys.OutsideDiameter, DimensionKeys.WallThickness };

        /// <inheritdoc />
        public override string CategoryId => ElementCategory.Pipe;

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = new()
            {
                SchemaFactory.Locked(DimensionKeys.OutsideDiameter, "Outside diameter d"),
                SchemaFactory.Locked(DimensionKeys.WallThickness, "Wall thickness t")
            };

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double outsideDiameter = Dimension(definition, DimensionKeys.OutsideDiameter);
            double wall = Dimension(definition, DimensionKeys.WallThickness);

            double outerRadius = outsideDiameter / 2d;
            double innerRadius = outerRadius - wall;

            List<ProfileLoop> loops = new() { ProfileLoopBuilder.Circle(Vec2.Zero, outerRadius) };
            if (innerRadius > 0d)
            {
                loops.Add(ProfileLoopBuilder.Circle(Vec2.Zero, innerRadius, false));
            }

            ProfileGeometry profile = new(loops);
            return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            decimal? diameter = definition.Dimension(DimensionKeys.OutsideDiameter);
            decimal? wall = definition.Dimension(DimensionKeys.WallThickness);

            if (diameter.HasValue && wall.HasValue && wall.Value * 2m >= diameter.Value)
            {
                report.AddError(
                    "Pipe.NoBore",
                    $"A wall of {Format(wall.Value)} mm leaves no bore in an outside diameter of {Format(diameter.Value)} mm.",
                    definition.Designation);
            }
        }
    }
}
