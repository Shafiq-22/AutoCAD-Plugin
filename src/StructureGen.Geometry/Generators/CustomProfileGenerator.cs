using System;
using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>
    /// Generates a profile from an explicit outline held in the database record, or from a simple
    /// rectangular or circular description when no outline is supplied.
    /// </summary>
    /// <remarks>
    /// This is what lets the database introduce a shape the code has never heard of. A fabricated
    /// box girder, a cold-rolled purlin, a proprietary section: write its outline into the record as
    /// a list of points and bulges and it generates, is scheduled and is weighed like anything else,
    /// with no new code and no rebuild.
    ///
    /// Because the outline lives on the record rather than on the element, it cannot be edited from
    /// the palette. That is deliberate: an arbitrary outline has no meaningful notion of which of
    /// its vertices are standardised, so it is treated as wholly fixed by the database.
    /// </remarks>
    public sealed class CustomProfileGenerator : ElementGeneratorBase
    {
        private readonly Func<string, SectionRecord?> _recordLookup;

        /// <summary>Initialises a new instance of the <see cref="CustomProfileGenerator"/> class.</summary>
        /// <param name="recordLookup">
        /// Resolves a record identifier to its record, so the explicit outline can be recovered when
        /// an element is regenerated from the metadata stored on a drawing object.
        /// </param>
        public CustomProfileGenerator(Func<string, SectionRecord?> recordLookup) =>
            _recordLookup = recordLookup ?? throw new ArgumentNullException(nameof(recordLookup));

        /// <inheritdoc />
        public override string CategoryId => ElementCategory.Custom;

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Array.Empty<string>();

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            List<ParameterDescriptor> parameters = new();

            if (record.HasOutline)
            {
                parameters.Add(SchemaFactory.Locked(
                    "outline", "Outline", true,
                    "This profile is described by an explicit outline in the database and cannot be edited here."));
            }
            else if (record.Dimension(DimensionKeys.OutsideDiameter).HasValue)
            {
                parameters.Add(SchemaFactory.Locked(DimensionKeys.OutsideDiameter, "Diameter d"));
                parameters.Add(SchemaFactory.Locked(DimensionKeys.WallThickness, "Wall thickness t", false));
            }
            else
            {
                parameters.Add(SchemaFactory.Locked(DimensionKeys.PlateWidth, "Width"));
                parameters.Add(SchemaFactory.Locked(DimensionKeys.Thickness, "Thickness"));
            }

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            ProfileGeometry profile = BuildProfile(definition);
            return new ElementGeometry(profile, (double)definition.LengthMm!.Value);
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            SectionRecord? record = _recordLookup(definition.SectionRecordId);

            if (record is { HasOutline: true })
            {
                return;
            }

            bool hasCircle = definition.Dimension(DimensionKeys.OutsideDiameter) is > 0m;
            bool hasRectangle = definition.Dimension(DimensionKeys.PlateWidth) is > 0m
                                && definition.Dimension(DimensionKeys.Thickness) is > 0m;

            if (!hasCircle && !hasRectangle)
            {
                report.AddError(
                    "Custom.NoShape",
                    $"'{definition.Designation}' is a custom profile but its database record supplies neither an " +
                    "explicit outline, nor a diameter, nor a width and thickness, so no shape can be generated. " +
                    "See docs/DATABASE.md for how to describe a custom outline.",
                    definition.Designation);
            }
        }

        private ProfileGeometry BuildProfile(ElementDefinition definition)
        {
            SectionRecord? record = _recordLookup(definition.SectionRecordId);

            if (record is { HasOutline: true })
            {
                ProfileLoop outer = new ProfileLoop(record.Outline!, true).WithCanonicalWinding();
                return new ProfileGeometry(outer);
            }

            double diameter = Dimension(definition, DimensionKeys.OutsideDiameter);
            if (diameter > 0d)
            {
                double wall = Dimension(definition, DimensionKeys.WallThickness);
                List<ProfileLoop> loops = new() { ProfileLoopBuilder.Circle(Vec2.Zero, diameter / 2d) };

                double innerRadius = (diameter / 2d) - wall;
                if (wall > 0d && innerRadius > 0d)
                {
                    loops.Add(ProfileLoopBuilder.Circle(Vec2.Zero, innerRadius, false));
                }

                return new ProfileGeometry(loops);
            }

            double width = Dimension(definition, DimensionKeys.PlateWidth);
            double thickness = Dimension(definition, DimensionKeys.Thickness);
            return new ProfileGeometry(ProfileLoopBuilder.CentredRectangle(width, thickness));
        }
    }
}
