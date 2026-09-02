using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructureGen.Core.Geometry;

namespace StructureGen.Acad.Internal
{
    /// <summary>
    /// Turns the CAD-neutral geometry produced by the element generators into AutoCAD entities.
    /// </summary>
    /// <remarks>
    /// This is the only place in the plugin where a profile becomes an entity, and it is a
    /// deliberately thin translation: the outlines, fillets and arcs were all decided and unit
    /// tested off-line, and nothing here re-derives them. A profile vertex carries a bulge, which is
    /// exactly what a lightweight polyline stores, so the conversion is a copy.
    /// </remarks>
    internal static class EntityFactory
    {
        /// <summary>Creates a closed polyline from one contour.</summary>
        /// <param name="loop">The contour, in millimetres.</param>
        /// <param name="scale">Drawing units per millimetre.</param>
        /// <returns>The polyline. The caller owns it.</returns>
        public static Polyline CreatePolyline(ProfileLoop loop, double scale)
        {
            if (loop is null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            Polyline polyline = new(loop.Vertices.Count);

            for (int index = 0; index < loop.Vertices.Count; index++)
            {
                ProfileVertex vertex = loop.Vertices[index];

                // A bulge is a ratio, so it is unaffected by the unit scale; only the points move.
                polyline.AddVertexAt(
                    index,
                    new Point2d(vertex.Point.X * scale, vertex.Point.Y * scale),
                    vertex.Bulge,
                    0d,
                    0d);
            }

            polyline.Closed = true;
            return polyline;
        }

        /// <summary>Creates the closed polylines that make up a two-dimensional representation.</summary>
        /// <param name="profile">The cross-section.</param>
        /// <param name="scale">Drawing units per millimetre.</param>
        /// <returns>One polyline per contour. The caller owns them.</returns>
        public static List<Polyline> CreateProfilePolylines(ProfileGeometry profile, double scale)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            List<Polyline> polylines = new(profile.Loops.Count);

            try
            {
                foreach (ProfileLoop loop in profile.Loops)
                {
                    polylines.Add(CreatePolyline(loop, scale));
                }
            }
            catch
            {
                foreach (Polyline polyline in polylines)
                {
                    polyline.Dispose();
                }

                throw;
            }

            return polylines;
        }

        /// <summary>
        /// Builds a region for a cross-section, subtracting every void from the outer contour.
        /// </summary>
        /// <param name="profile">The cross-section.</param>
        /// <param name="scale">Drawing units per millimetre.</param>
        /// <returns>The region. The caller owns it.</returns>
        /// <exception cref="InvalidOperationException">AutoCAD could not build a region from the contour.</exception>
        public static Region CreateRegion(ProfileGeometry profile, double scale)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            Region outer = RegionFromLoop(profile.Outer, scale);

            try
            {
                foreach (ProfileLoop hole in profile.Voids)
                {
                    using Region voidRegion = RegionFromLoop(hole, scale);
                    outer.BooleanOperation(BooleanOperationType.BoolSubtract, voidRegion);
                }
            }
            catch
            {
                outer.Dispose();
                throw;
            }

            return outer;
        }

        /// <summary>Extrudes a cross-section into a solid spanning a range along the local Z axis.</summary>
        /// <param name="profile">The cross-section.</param>
        /// <param name="startOffsetMm">Where the solid begins along the local Z axis, in millimetres.</param>
        /// <param name="lengthMm">How far it extends, in millimetres.</param>
        /// <param name="scale">Drawing units per millimetre.</param>
        /// <returns>The solid. The caller owns it.</returns>
        public static Solid3d CreateExtrusion(ProfileGeometry profile, double startOffsetMm, double lengthMm, double scale)
        {
            if (lengthMm <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(lengthMm), lengthMm, "An extrusion needs a length greater than zero.");
            }

            using Region region = CreateRegion(profile, scale);

            Solid3d solid = new();

            try
            {
                // History would be stored on every one of what may be thousands of members and is of
                // no use to a plugin that regenerates from its own metadata instead.
                solid.RecordHistory = false;

                // Extrusion runs along the region's own normal. Which way that points depends on how
                // AutoCAD read the contour, so the height is signed to match and the solid always
                // ends up spanning zero to length along positive local Z.
                double height = lengthMm * scale;
                double signedHeight = region.Normal.Z >= 0d ? height : -height;
                solid.Extrude(region, signedHeight, 0d);

                if (Math.Abs(startOffsetMm) > 1e-9)
                {
                    solid.TransformBy(Matrix3d.Displacement(new Vector3d(0d, 0d, startOffsetMm * scale)));
                }

                return solid;
            }
            catch
            {
                solid.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Builds the transform that carries geometry from the element's local frame, where the
        /// profile lies in XY and the member runs along positive Z, into the drawing.
        /// </summary>
        /// <param name="placement">Where and how the element sits.</param>
        /// <param name="scale">Drawing units per millimetre.</param>
        /// <returns>The transform.</returns>
        /// <remarks>
        /// The local X axis is derived from a world reference that is chosen to avoid being parallel
        /// to the member, so a vertical column gets a stable, repeatable orientation instead of an
        /// arbitrary one.
        /// </remarks>
        public static Matrix3d PlacementTransform(Placement placement, double scale)
        {
            if (placement is null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            Vector3d zAxis = new Vector3d(
                placement.AxisDirection.X, placement.AxisDirection.Y, placement.AxisDirection.Z).GetNormal();

            Vector3d reference = Math.Abs(zAxis.Z) > 0.9999d ? Vector3d.XAxis : Vector3d.ZAxis;
            Vector3d xAxis = reference.CrossProduct(zAxis).GetNormal();
            Vector3d yAxis = zAxis.CrossProduct(xAxis).GetNormal();

            if (Math.Abs(placement.RotationDegrees) > 1e-9)
            {
                double radians = placement.RotationDegrees * Math.PI / 180d;
                Matrix3d spin = Matrix3d.Rotation(radians, zAxis, Point3d.Origin);
                xAxis = xAxis.TransformBy(spin);
                yAxis = yAxis.TransformBy(spin);
            }

            Vec3 origin = placement.EffectiveOrigin;
            Point3d target = new(origin.X * scale, origin.Y * scale, origin.Z * scale);

            return Matrix3d.AlignCoordinateSystem(
                Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
                target, xAxis, yAxis, zAxis);
        }

        private static Region RegionFromLoop(ProfileLoop loop, double scale)
        {
            using Polyline polyline = CreatePolyline(loop, scale);
            using DBObjectCollection curves = new();
            curves.Add(polyline);

            using DBObjectCollection regions = Region.CreateFromCurves(curves);

            if (regions.Count == 0)
            {
                throw new InvalidOperationException(
                    "AutoCAD could not build a region from the profile outline. The outline may be self-intersecting.");
            }

            Region region = (Region)regions[0];

            // CreateFromCurves can return more than one region when a contour is disjoint, which a
            // valid profile never is. Anything beyond the first is discarded rather than leaked.
            for (int index = 1; index < regions.Count; index++)
            {
                ((DBObject)regions[index]).Dispose();
            }

            return region;
        }
    }
}
