using System;
using System.Collections.Generic;
using StructureGen.Bom;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Services
{
    /// <summary>Where the elements to schedule are collected from.</summary>
    public enum HarvestScope
    {
        /// <summary>Every StructureGen element in model space.</summary>
        ModelSpace = 0,

        /// <summary>Every StructureGen element in model space and in every layout.</summary>
        EntireDrawing = 1,

        /// <summary>Only the StructureGen elements the user has selected.</summary>
        Selection = 2
    }

    /// <summary>Where a generated schedule table is placed.</summary>
    public enum BomTablePlacement
    {
        /// <summary>Placed in model space at a point the user picks.</summary>
        ModelSpace = 0,

        /// <summary>Placed in the current layout at a point the user picks.</summary>
        CurrentLayout = 1
    }

    /// <summary>
    /// Everything the user interface needs from the CAD host, expressed without a single AutoCAD
    /// type.
    /// </summary>
    /// <remarks>
    /// This is the seam that keeps the palette testable and the AutoCAD code replaceable. The
    /// window-side code compiles against nothing but this interface and the domain model, so the
    /// whole of the user interface layer can be exercised without AutoCAD running, and the AutoCAD
    /// layer can change how it stores metadata or builds solids without the palette knowing.
    /// </remarks>
    public interface ICadService
    {
        /// <summary>Gets a value indicating whether a drawing is open and usable.</summary>
        bool HasActiveDocument { get; }

        /// <summary>Gets the name of the active drawing, or <see langword="null"/> when there is none.</summary>
        string? DocumentName { get; }

        /// <summary>
        /// Gets the drawing's insertion unit, or <see langword="null"/> when the drawing is unitless.
        /// </summary>
        /// <remarks>
        /// StructureGen works internally in millimetres. When a drawing declares a different unit
        /// the geometry is scaled on the way in; when a drawing is unitless the user is warned once
        /// rather than silently given geometry a thousand times too large.
        /// </remarks>
        Core.Units.LengthUnit? DrawingLengthUnit { get; }

        /// <summary>Asks the user to pick a point.</summary>
        /// <param name="prompt">The prompt to show on the command line.</param>
        /// <returns>The point, or a failure when the user cancelled.</returns>
        Result<Vec3> PickPoint(string prompt);

        /// <summary>
        /// Asks the user to pick a start point and an end point, from which the member direction and
        /// its true length are derived.
        /// </summary>
        /// <param name="startPrompt">The prompt for the start point.</param>
        /// <param name="endPrompt">The prompt for the end point.</param>
        /// <returns>The placement and the measured length in millimetres, or a failure when cancelled.</returns>
        Result<(Placement Placement, decimal LengthMm)> PickSegment(string startPrompt, string endPrompt);

        /// <summary>Asks the user to select a StructureGen element and recovers its stored parameters.</summary>
        /// <param name="prompt">The prompt to show on the command line.</param>
        /// <returns>The element definition and the object's handle, or a failure when cancelled.</returns>
        Result<(ElementDefinition Definition, string Handle)> PickElement(string prompt);

        /// <summary>Creates geometry in the drawing.</summary>
        /// <param name="request">What to create.</param>
        /// <returns>What was created, or a failure carrying a user-facing reason.</returns>
        Result<GenerationSummary> Generate(GenerationRequest request);

        /// <summary>Rebuilds the geometry of an element that already exists in the drawing.</summary>
        /// <param name="handle">The handle of the object to update.</param>
        /// <param name="definition">The new parameters. Its element identity must be preserved.</param>
        /// <returns>The handle of the updated object, or a failure carrying a user-facing reason.</returns>
        Result<string> UpdateElement(string handle, ElementDefinition definition);

        /// <summary>Collects the StructureGen elements present in the drawing.</summary>
        /// <param name="scope">Where to look.</param>
        /// <returns>One entry per generated object, or a failure carrying a user-facing reason.</returns>
        Result<IReadOnlyList<BomEntry>> HarvestElements(HarvestScope scope);

        /// <summary>Draws a schedule as a native AutoCAD table.</summary>
        /// <param name="document">The schedule to draw.</param>
        /// <param name="placement">Where to put it.</param>
        /// <returns>The handle of the created table, or a failure carrying a user-facing reason.</returns>
        Result<string> InsertBomTable(BomDocument document, BomTablePlacement placement);

        /// <summary>Writes a line to the AutoCAD command line.</summary>
        /// <param name="message">The message.</param>
        void WriteMessage(string message);

        /// <summary>Shows a message to the user in a way they cannot miss.</summary>
        /// <param name="title">A short caption.</param>
        /// <param name="message">The message, written for a drafter rather than a developer.</param>
        void ShowMessage(string title, string message);

        /// <summary>
        /// Runs an action on the thread AutoCAD requires document work to happen on.
        /// </summary>
        /// <param name="action">The work to run.</param>
        /// <remarks>
        /// AutoCAD's database is not thread safe and document operations started from a modeless
        /// palette must be marshalled onto the document's execution context. Routing every such call
        /// through here keeps that rule in one place instead of scattered through the palette.
        /// </remarks>
        void InvokeOnDocumentThread(Action action);
    }
}
