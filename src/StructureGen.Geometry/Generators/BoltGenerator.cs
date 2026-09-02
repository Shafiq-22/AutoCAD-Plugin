using System;
using System.Collections.Generic;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Geometry.Generators
{
    /// <summary>
    /// Generates hexagon head bolts as a hexagonal head over a cylindrical shank.
    /// </summary>
    /// <remarks>
    /// The origin sits on the underside of the head, so a bolt inserted at a hole in a flange lands
    /// where a detailer expects it: the head occupies negative Z and the shank runs along positive
    /// Z into the connection.
    ///
    /// Threads are represented by a change of diameter, never by helical geometry. A drawing with a
    /// few thousand bolts in it has to stay usable, and a modelled thread form multiplies the face
    /// count of every one of them for detail that no fabrication drawing uses. When the database
    /// supplies a thread minor diameter the threaded portion is drawn at that diameter at the
    /// detailed level; when it does not, the shank is a plain cylinder and nothing is invented.
    /// </remarks>
    public sealed class BoltGenerator : ElementGeneratorBase
    {
        private static readonly string[] Required =
        {
            DimensionKeys.ThreadDiameter, DimensionKeys.AcrossFlats, DimensionKeys.HeadHeight, DimensionKeys.BoltLength
        };

        /// <inheritdoc />
        public override string CategoryId => ElementCategory.Bolt;

        /// <inheritdoc />
        public override IReadOnlyList<string> RequiredDimensions => Required;

        /// <inheritdoc />
        public override ParameterSchema GetParameterSchema(SectionRecord record)
        {
            decimal? minimum = record.Dimension(FastenerDimensionKeys.MinimumBoltLength);
            decimal? maximum = record.Dimension(FastenerDimensionKeys.MaximumBoltLength);

            List<ParameterDescriptor> parameters = new()
            {
                SchemaFactory.Locked(DimensionKeys.ThreadDiameter, "Thread diameter d"),
                SchemaFactory.Locked(DimensionKeys.AcrossFlats, "Across flats s"),
                SchemaFactory.Locked(DimensionKeys.HeadHeight, "Head height k"),
                new ParameterDescriptor(
                    DimensionKeys.BoltLength,
                    "Bolt length l",
                    ParameterMutability.Adjustable,
                    true,
                    minimum,
                    maximum,
                    minimum.HasValue || maximum.HasValue
                        ? "The standard publishes a range of lengths for this diameter; any value in the range is valid."
                        : "Length under the head. The database does not publish a permitted range for this size, so " +
                          "check the value against your copy of the standard."),
                SchemaFactory.Locked(FastenerDimensionKeys.ThreadMinorDiameter, "Thread minor diameter", false),
                SchemaFactory.Locked(FastenerDimensionKeys.UnthreadedLength, "Unthreaded shank length", false)
            };

            SchemaFactory.AddInstanceParameters(parameters, Descriptor);
            return new ParameterSchema(CategoryId, parameters);
        }

        /// <inheritdoc />
        protected override ElementGeometry BuildGeometryCore(ElementDefinition definition)
        {
            double threadDiameter = Dimension(definition, DimensionKeys.ThreadDiameter);
            double acrossFlats = Dimension(definition, DimensionKeys.AcrossFlats);
            double headHeight = Dimension(definition, DimensionKeys.HeadHeight);
            double boltLength = Dimension(definition, DimensionKeys.BoltLength);

            ProfileGeometry head = new(ProfileLoopBuilder.RegularPolygonAcrossFlats(Vec2.Zero, acrossFlats));
            ProfileGeometry shank = new(ProfileLoopBuilder.Circle(Vec2.Zero, threadDiameter / 2d));

            List<GeometryPart> parts = new()
            {
                new GeometryPart("head", head, -headHeight, headHeight)
            };

            double minorDiameter = Dimension(definition, FastenerDimensionKeys.ThreadMinorDiameter);
            double unthreadedLength = Dimension(definition, FastenerDimensionKeys.UnthreadedLength);

            bool splitShank = definition.DetailLevel == DetailLevel.Detailed
                              && minorDiameter > 0d
                              && minorDiameter < threadDiameter
                              && unthreadedLength > 0d
                              && unthreadedLength < boltLength;

            if (splitShank)
            {
                ProfileGeometry threaded = new(ProfileLoopBuilder.Circle(Vec2.Zero, minorDiameter / 2d));
                parts.Add(new GeometryPart("shank", shank, 0d, unthreadedLength));
                parts.Add(new GeometryPart("thread", threaded, unthreadedLength, boltLength - unthreadedLength));
            }
            else
            {
                parts.Add(new GeometryPart("shank", shank, 0d, boltLength));
            }

            return new ElementGeometry(parts, shank);
        }

        /// <inheritdoc />
        protected override void ValidateCore(ElementDefinition definition, ValidationReport report)
        {
            decimal length = definition.Dimension(DimensionKeys.BoltLength) ?? 0m;
            decimal threadDiameter = definition.Dimension(DimensionKeys.ThreadDiameter) ?? 0m;
            decimal acrossFlats = definition.Dimension(DimensionKeys.AcrossFlats) ?? 0m;

            if (length <= 0m)
            {
                report.AddError(
                    "Bolt.LengthNotPositive",
                    "The bolt length must be greater than zero.",
                    definition.Designation);
            }

            if (threadDiameter > 0m && acrossFlats > 0m && acrossFlats <= threadDiameter)
            {
                report.AddError(
                    "Bolt.HeadTooSmall",
                    $"The across-flats dimension of {Format(acrossFlats)} mm is not larger than the thread diameter " +
                    $"of {Format(threadDiameter)} mm, so the head would be smaller than the shank.",
                    definition.Designation);
            }

            decimal? minimum = definition.Dimension(FastenerDimensionKeys.MinimumBoltLength);
            decimal? maximum = definition.Dimension(FastenerDimensionKeys.MaximumBoltLength);

            if (minimum.HasValue && length > 0m && length < minimum.Value)
            {
                report.AddError(
                    "Bolt.LengthBelowStandardRange",
                    $"A length of {Format(length)} mm is shorter than the smallest length of {Format(minimum.Value)} mm " +
                    "that the standard publishes for this diameter.",
                    definition.Designation);
            }

            if (maximum.HasValue && length > maximum.Value)
            {
                report.AddError(
                    "Bolt.LengthAboveStandardRange",
                    $"A length of {Format(length)} mm is longer than the greatest length of {Format(maximum.Value)} mm " +
                    "that the standard publishes for this diameter.",
                    definition.Designation);
            }
        }

        /// <inheritdoc />
        public override ValidationReport Validate(ElementDefinition definition, SectionRecord? record = null)
        {
            ValidationReport report = base.Validate(definition, record);

            // The bolt length is adjustable within the range the standard allows, so it must not be
            // reported as an altered standard dimension. The base class already honours the schema,
            // which marks it adjustable; this override exists only to document that intent.
            return report;
        }
    }
}
