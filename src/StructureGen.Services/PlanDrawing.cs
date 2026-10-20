using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Geometry.Drawing;

namespace StructureGen.Services
{
    /// <summary>How a plan drawing is laid out.</summary>
    public enum PlanLayout
    {
        /// <summary>
        /// Every element where it actually is, seen from above: a general arrangement plan of the
        /// whole structure.
        /// </summary>
        Arrangement = 0,

        /// <summary>
        /// One plan per distinct item, laid out in a column and labelled with its quantity: a sheet
        /// of item plans, one entry per schedule line.
        /// </summary>
        Items = 1
    }

    /// <summary>How a piece of plan text is anchored to its position.</summary>
    public enum PlanTextAlignment
    {
        /// <summary>The position is the bottom left corner of the text.</summary>
        BottomLeft = 0,

        /// <summary>The position is the middle of the text's baseline.</summary>
        BottomCentre = 1
    }

    /// <summary>One element found in the drawing, with where it actually sits.</summary>
    public sealed class PlacedElement
    {
        /// <summary>Initialises a new instance of the <see cref="PlacedElement"/> class.</summary>
        /// <param name="definition">The element's parameters.</param>
        /// <param name="frame">Its local frame as it now sits in the drawing, in millimetres.</param>
        /// <param name="handle">The drawing object it was read from, when there is one.</param>
        public PlacedElement(ElementDefinition definition, ElementFrame frame, string? handle = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Handle = handle;
        }

        /// <summary>Gets the element's parameters.</summary>
        public ElementDefinition Definition { get; }

        /// <summary>Gets the element's local frame as it now sits in the drawing.</summary>
        public ElementFrame Frame { get; }

        /// <summary>Gets the drawing object it was read from, when there is one.</summary>
        public string? Handle { get; }
    }

    /// <summary>What a plan drawing should contain.</summary>
    public sealed class PlanDrawingOptions
    {
        /// <summary>Gets or sets how the plan is laid out.</summary>
        public PlanLayout Layout { get; set; } = PlanLayout.Arrangement;

        /// <summary>Gets or sets a value indicating whether elements are labelled.</summary>
        public bool Labels { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether round members get centre lines.</summary>
        public bool CentreLines { get; set; } = true;

        /// <summary>
        /// Gets or sets the label height in millimetres. Zero chooses one in proportion to the
        /// members being drawn.
        /// </summary>
        public double TextHeightMm { get; set; }
    }

    /// <summary>One piece of text on a plan drawing.</summary>
    public sealed class PlanText
    {
        /// <summary>Initialises a new instance of the <see cref="PlanText"/> class.</summary>
        /// <param name="text">The text.</param>
        /// <param name="position">Where it is anchored, in plan millimetres.</param>
        /// <param name="heightMm">The character height, in millimetres.</param>
        /// <param name="angle">The reading direction, in radians anticlockwise from plan X.</param>
        /// <param name="alignment">How it is anchored.</param>
        public PlanText(string text, Vec2 position, double heightMm, double angle, PlanTextAlignment alignment)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Position = position;
            HeightMm = heightMm;
            Angle = angle;
            Alignment = alignment;
        }

        /// <summary>Gets the text.</summary>
        public string Text { get; }

        /// <summary>Gets where it is anchored, in plan millimetres.</summary>
        public Vec2 Position { get; }

        /// <summary>Gets the character height, in millimetres.</summary>
        public double HeightMm { get; }

        /// <summary>Gets the reading direction, in radians anticlockwise from plan X.</summary>
        public double Angle { get; }

        /// <summary>Gets how the text is anchored.</summary>
        public PlanTextAlignment Alignment { get; }

        /// <summary>Returns a copy moved by an offset.</summary>
        /// <param name="offset">The offset.</param>
        /// <returns>The moved text.</returns>
        public PlanText Translated(Vec2 offset) => new(Text, Position + offset, HeightMm, Angle, Alignment);
    }

    /// <summary>A finished plan drawing, in plan millimetres, ready to be put into a drawing.</summary>
    public sealed class PlanDrawing
    {
        /// <summary>Initialises a new instance of the <see cref="PlanDrawing"/> class.</summary>
        /// <param name="layout">How it is laid out.</param>
        /// <param name="curves">The lines.</param>
        /// <param name="texts">The labels and titles.</param>
        /// <param name="elementCount">How many elements were drawn.</param>
        /// <param name="itemCount">How many distinct items they represent.</param>
        /// <param name="report">Anything worth telling the user about.</param>
        public PlanDrawing(
            PlanLayout layout,
            IEnumerable<PlanCurve> curves,
            IEnumerable<PlanText> texts,
            int elementCount,
            int itemCount,
            ValidationReport report)
        {
            Layout = layout;
            Curves = (curves ?? throw new ArgumentNullException(nameof(curves))).ToList();
            Texts = (texts ?? throw new ArgumentNullException(nameof(texts))).ToList();
            ElementCount = elementCount;
            ItemCount = itemCount;
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        /// <summary>Gets how the drawing is laid out.</summary>
        public PlanLayout Layout { get; }

        /// <summary>Gets the lines.</summary>
        public IReadOnlyList<PlanCurve> Curves { get; }

        /// <summary>Gets the labels and titles.</summary>
        public IReadOnlyList<PlanText> Texts { get; }

        /// <summary>Gets how many elements were drawn.</summary>
        public int ElementCount { get; }

        /// <summary>Gets how many distinct items the elements represent.</summary>
        public int ItemCount { get; }

        /// <summary>Gets anything worth telling the user about, such as elements that were skipped.</summary>
        public ValidationReport Report { get; }

        /// <summary>Gets how many lines of each kind the drawing holds.</summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The count.</returns>
        public int CountOf(PlanLineKind kind) => Curves.Count(c => c.Kind == kind);

        /// <summary>
        /// Gets the lower left corner of the lines, which is the point placed where the user picks
        /// when the plan is drawn away from the model.
        /// </summary>
        public Vec2 Anchor => PlanView.BoundsOf(Curves).Minimum;

        /// <summary>Gets the bounds of the lines.</summary>
        /// <returns>The minimum and maximum corners.</returns>
        public (Vec2 Minimum, Vec2 Maximum) Bounds() => PlanView.BoundsOf(Curves);
    }

    /// <summary>A plan drawing that has been put into the drawing.</summary>
    public sealed class PlanDrawingOutcome
    {
        /// <summary>Initialises a new instance of the <see cref="PlanDrawingOutcome"/> class.</summary>
        /// <param name="drawing">What was drawn.</param>
        /// <param name="blockName">The block definition holding it.</param>
        /// <param name="handle">The block reference placing it.</param>
        public PlanDrawingOutcome(PlanDrawing drawing, string blockName, string handle)
        {
            Drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            BlockName = blockName ?? throw new ArgumentNullException(nameof(blockName));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
        }

        /// <summary>Gets what was drawn.</summary>
        public PlanDrawing Drawing { get; }

        /// <summary>Gets the name of the block definition holding the plan.</summary>
        public string BlockName { get; }

        /// <summary>Gets the handle of the block reference placing the plan.</summary>
        public string Handle { get; }

        /// <summary>Describes the result in a sentence for the command line or the palette.</summary>
        /// <returns>The description.</returns>
        public string ToDisplayString()
        {
            string what = Drawing.Layout == PlanLayout.Items
                ? $"{Drawing.ItemCount} item plan(s) covering {Drawing.ElementCount} element(s)"
                : $"a plan of {Drawing.ElementCount} element(s) ({Drawing.ItemCount} distinct item(s))";

            return $"Drew {what}: {Drawing.CountOf(PlanLineKind.Visible)} visible, " +
                   $"{Drawing.CountOf(PlanLineKind.Hidden)} hidden and {Drawing.CountOf(PlanLineKind.Centre)} centre " +
                   $"line(s), as block '{BlockName}' ({Handle}). Explode it to edit the lines individually.";
        }
    }
}
