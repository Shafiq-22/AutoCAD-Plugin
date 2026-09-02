using System;
using System.Collections.Generic;
using System.Linq;

namespace StructureGen.Core.Geometry
{
    /// <summary>A complete cross-section: one outer contour and any number of voids.</summary>
    public sealed class ProfileGeometry
    {
        /// <summary>Initialises a new instance of the <see cref="ProfileGeometry"/> class.</summary>
        /// <param name="loops">The contours. Exactly one must be an outer contour.</param>
        public ProfileGeometry(IEnumerable<ProfileLoop> loops)
        {
            Loops = (loops ?? throw new ArgumentNullException(nameof(loops))).ToList();

            if (Loops.Count == 0)
            {
                throw new ArgumentException("A profile needs at least one contour.", nameof(loops));
            }

            if (Loops.Count(l => l.IsOuter) != 1)
            {
                throw new ArgumentException(
                    "A profile must have exactly one outer contour; the rest are voids.", nameof(loops));
            }
        }

        /// <summary>Initialises a new instance of the <see cref="ProfileGeometry"/> class from a single contour.</summary>
        /// <param name="outer">The outer contour.</param>
        public ProfileGeometry(ProfileLoop outer)
            : this(new[] { outer ?? throw new ArgumentNullException(nameof(outer)) })
        {
        }

        /// <summary>Gets the contours.</summary>
        public IReadOnlyList<ProfileLoop> Loops { get; }

        /// <summary>Gets the outer contour.</summary>
        public ProfileLoop Outer => Loops.First(l => l.IsOuter);

        /// <summary>Gets the void contours.</summary>
        public IEnumerable<ProfileLoop> Voids => Loops.Where(l => !l.IsOuter);

        /// <summary>Gets the net cross-sectional area in square millimetres.</summary>
        public double AreaMm2 => Outer.Area - Voids.Sum(v => v.Area);

        /// <summary>Gets the axis-aligned bounding box of the outer contour.</summary>
        /// <returns>The minimum and maximum corners.</returns>
        public (Vec2 Minimum, Vec2 Maximum) Bounds() => Outer.Bounds();

        /// <summary>Gets the overall width of the outer contour along X.</summary>
        public double WidthMm
        {
            get
            {
                (Vec2 minimum, Vec2 maximum) = Bounds();
                return maximum.X - minimum.X;
            }
        }

        /// <summary>Gets the overall height of the outer contour along Y.</summary>
        public double HeightMm
        {
            get
            {
                (Vec2 minimum, Vec2 maximum) = Bounds();
                return maximum.Y - minimum.Y;
            }
        }

        /// <summary>Returns a copy of this profile moved by an offset.</summary>
        /// <param name="offset">The offset to apply.</param>
        /// <returns>The translated profile.</returns>
        public ProfileGeometry Translated(Vec2 offset) => new(Loops.Select(l => l.Translated(offset)));

        /// <summary>Returns a copy of this profile whose outer contour is centred on the origin.</summary>
        /// <returns>The centred profile.</returns>
        public ProfileGeometry Centred()
        {
            (Vec2 minimum, Vec2 maximum) = Bounds();
            Vec2 centre = new((minimum.X + maximum.X) / 2d, (minimum.Y + maximum.Y) / 2d);
            return centre.IsEqualTo(Vec2.Zero, 1e-9) ? this : Translated(Vec2.Zero - centre);
        }
    }
}
