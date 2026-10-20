using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Geometry;

namespace StructureGen.Geometry.Drawing
{
    /// <summary>The plan of one element: what it looks like seen from directly above.</summary>
    public sealed class PlanView
    {
        /// <summary>Initialises a new instance of the <see cref="PlanView"/> class.</summary>
        /// <param name="curves">The lines.</param>
        /// <param name="isEndView">Whether the element is seen along its own axis.</param>
        /// <param name="labelAnchor">The plan point a label should refer to.</param>
        /// <param name="labelAngle">The direction a label should read along, in radians.</param>
        /// <param name="halfWidthMm">Half the element's plan width across its axis.</param>
        public PlanView(
            IEnumerable<PlanCurve> curves, bool isEndView, Vec2 labelAnchor, double labelAngle, double halfWidthMm)
        {
            Curves = (curves ?? throw new ArgumentNullException(nameof(curves))).ToList();
            IsEndView = isEndView;
            LabelAnchor = labelAnchor;
            LabelAngle = labelAngle;
            HalfWidthMm = halfWidthMm;
        }

        /// <summary>Gets the lines, in plan millimetres.</summary>
        public IReadOnlyList<PlanCurve> Curves { get; }

        /// <summary>
        /// Gets a value indicating whether the element is seen along its own axis - a column, or a
        /// bolt standing upright - so the plan shows its cross-section.
        /// </summary>
        public bool IsEndView { get; }

        /// <summary>Gets the plan point a label should refer to: the middle of the member.</summary>
        public Vec2 LabelAnchor { get; }

        /// <summary>
        /// Gets the direction a label should read along, in radians, always turned so the text
        /// reads from left to right or from bottom to top.
        /// </summary>
        public double LabelAngle { get; }

        /// <summary>Gets half the element's plan width across its axis, in millimetres.</summary>
        public double HalfWidthMm { get; }

        /// <summary>Gets the bounds of every line.</summary>
        /// <returns>The minimum and maximum corners.</returns>
        public (Vec2 Minimum, Vec2 Maximum) Bounds() => BoundsOf(Curves);

        /// <summary>Returns a copy moved by an offset.</summary>
        /// <param name="offset">The offset.</param>
        /// <returns>The moved view.</returns>
        public PlanView Translated(Vec2 offset) =>
            new(Curves.Select(c => c.Translated(offset)), IsEndView, LabelAnchor + offset, LabelAngle, HalfWidthMm);

        /// <summary>Gets the bounds of a set of curves.</summary>
        /// <param name="curves">The curves.</param>
        /// <returns>The minimum and maximum corners, or two zero points when there are none.</returns>
        public static (Vec2 Minimum, Vec2 Maximum) BoundsOf(IEnumerable<PlanCurve> curves)
        {
            bool any = false;
            double minimumX = double.MaxValue;
            double minimumY = double.MaxValue;
            double maximumX = double.MinValue;
            double maximumY = double.MinValue;

            foreach (PlanCurve curve in curves)
            {
                (Vec2 minimum, Vec2 maximum) = curve.Bounds();
                minimumX = Math.Min(minimumX, minimum.X);
                minimumY = Math.Min(minimumY, minimum.Y);
                maximumX = Math.Max(maximumX, maximum.X);
                maximumY = Math.Max(maximumY, maximum.Y);
                any = true;
            }

            return any
                ? (new Vec2(minimumX, minimumY), new Vec2(maximumX, maximumY))
                : (Vec2.Zero, Vec2.Zero);
        }
    }
}
