using System;
using System.Collections.Generic;
using StructureGen.Bom;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Units;
using StructureGen.Services;

namespace StructureGen.Tests
{
    /// <summary>A stand-in for AutoCAD, so the palette's own logic can be exercised without it.</summary>
    /// <remarks>
    /// The point of keeping <see cref="ICadService"/> as the single seam between the user interface
    /// and AutoCAD was always that the interface could be tested on the far side of it. This is what
    /// collects that: what the palette decided to draw, and what it decided to tell the user.
    /// </remarks>
    internal sealed class FakeCadService : ICadService
    {
        private readonly Func<GenerationRequest, Result<GenerationSummary>> _generate;

        public FakeCadService(Func<GenerationRequest, Result<GenerationSummary>> generate) =>
            _generate = generate;

        /// <summary>Gets the dialogs the palette raised, in order.</summary>
        public List<(string Title, string Message)> Dialogs { get; } = new();

        /// <summary>Gets the lines the palette wrote to the command line, in order.</summary>
        public List<string> Messages { get; } = new();

        /// <summary>Gets the requests the palette asked to be generated, in order.</summary>
        public List<GenerationRequest> Requests { get; } = new();

        public bool HasActiveDocument => true;

        public string? DocumentName => "Drawing1.dwg";

        public LengthUnit? DrawingLengthUnit => LengthUnit.Millimetre;

        public Result<GenerationSummary> Generate(GenerationRequest request)
        {
            Requests.Add(request);
            return _generate(request);
        }

        public void ShowMessage(string title, string message) => Dialogs.Add((title, message));

        public void WriteMessage(string message) => Messages.Add(message);

        public void InvokeOnDocumentThread(Action action) => action();

        public Result<Vec3> PickPoint(string prompt) =>
            Result<Vec3>.Failure("Fake.NotSupported", "There is no drawing to pick in.");

        public Result<(Placement Placement, decimal LengthMm)> PickSegment(string startPrompt, string endPrompt) =>
            Result<(Placement, decimal)>.Failure("Fake.NotSupported", "There is no drawing to pick in.");

        public Result<(ElementDefinition Definition, string Handle)> PickElement(string prompt) =>
            Result<(ElementDefinition, string)>.Failure("Fake.NotSupported", "There is no drawing to pick in.");

        public Result<string> UpdateElement(string handle, ElementDefinition definition) =>
            Result<string>.Failure("Fake.NotSupported", "There is no drawing to update.");

        public Result<IReadOnlyList<BomEntry>> HarvestElements(HarvestScope scope) =>
            Result<IReadOnlyList<BomEntry>>.Success(Array.Empty<BomEntry>());

        public Result<string> InsertBomTable(BomDocument document, BomTablePlacement placement) =>
            Result<string>.Failure("Fake.NotSupported", "There is no drawing to insert into.");

        public List<(PlanDrawingOptions Options, HarvestScope Scope)> PlanRequests { get; } = new();

        public Result<PlanDrawingOutcome> DrawPlan(PlanDrawingOptions options, HarvestScope scope)
        {
            PlanRequests.Add((options, scope));
            return Result<PlanDrawingOutcome>.Failure("Fake.NotSupported", "There is no drawing to draw a plan in.");
        }
    }
}
