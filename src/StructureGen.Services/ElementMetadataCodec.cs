using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Services
{
    /// <summary>
    /// Converts an <see cref="ElementDefinition"/> to and from the compact text that is stored on
    /// the AutoCAD objects generated from it.
    /// </summary>
    /// <remarks>
    /// This is the heart of the parametric behaviour, and it deliberately contains no AutoCAD types
    /// at all. Everything needed to rebuild an element from scratch travels with the object, so
    /// selecting a beam months later and asking what it is does not depend on the palette's session
    /// state, on the drawing having been made on this machine, or even on the database still being
    /// installed.
    ///
    /// Two rules make it safe to evolve. Every payload records the schema version that wrote it, and
    /// reading is tolerant: an unknown field is ignored rather than rejected, and a missing optional
    /// field falls back to its default. A drawing produced by a newer build therefore still opens in
    /// an older one, degraded rather than broken.
    /// </remarks>
    public static class ElementMetadataCodec
    {
        /// <summary>The version of the payload format this build writes.</summary>
        public const int CurrentVersion = 1;

        /// <summary>The registered application name used for the marker written as extended entity data.</summary>
        public const string RegisteredApplicationName = "SGEN";

        /// <summary>The extension dictionary key holding the parameters of one occurrence.</summary>
        public const string InstanceKey = "SG_INSTANCE";

        /// <summary>The extension dictionary key holding the profile a block definition was built from.</summary>
        public const string DefinitionKey = "SG_DEFINITION";

        /// <summary>The named object dictionary key holding drawing-wide StructureGen settings.</summary>
        public const string ProjectKey = "SG_PROJECT";

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Serialises an element to the text stored on its object.</summary>
        /// <param name="definition">The element to serialise.</param>
        /// <param name="databaseStamp">The database name and version the element came from.</param>
        /// <param name="pluginVersion">The plugin version that created it.</param>
        /// <returns>The payload.</returns>
        public static string Encode(ElementDefinition definition, string? databaseStamp, string? pluginVersion)
        {
            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            ElementPayload payload = new()
            {
                V = CurrentVersion,
                Id = definition.ElementId.ToString("N", CultureInfo.InvariantCulture),
                Category = definition.CategoryId,
                Standard = definition.StandardId,
                Record = definition.SectionRecordId,
                Designation = definition.Designation,
                Dimensions = new Dictionary<string, decimal>(definition.Dimensions, StringComparer.Ordinal),
                Quantity = definition.Quantity,
                LengthMm = definition.LengthMm,
                Detail = definition.DetailLevel,
                Representation = definition.Representation,
                Material = definition.Material,
                Grade = definition.Grade,
                UnitWeight = definition.UnitWeightKgPerM,
                PieceMass = definition.PieceMassKg,
                Density = definition.DensityKgPerCubicMetre,
                Layer = definition.Layer,
                Notes = definition.Notes,
                DrawingReference = definition.DrawingReference,
                Custom = definition.IsCustom ? true : null,
                Verified = definition.SourceDataVerified ? true : null,
                CreatedUtc = definition.CreatedUtc.ToString("O", CultureInfo.InvariantCulture),
                DatabaseStamp = databaseStamp,
                PluginVersion = pluginVersion
            };

            // A profile the built-in generators do not cover is described entirely by its outline,
            // so the outline has to travel with the object. Without it the element would regenerate
            // only on a machine that still had the exact database record it came from.
            if (definition.HasOutline)
            {
                double[][] outline = new double[definition.Outline!.Count][];

                for (int index = 0; index < definition.Outline.Count; index++)
                {
                    ProfileVertex vertex = definition.Outline[index];
                    outline[index] = new[] { vertex.Point.X, vertex.Point.Y, vertex.Bulge };
                }

                payload.Outline = outline;
            }

            if (definition.Placements.Count > 0)
            {
                Placement placement = definition.Placements[0];
                payload.Origin = new[] { placement.Origin.X, placement.Origin.Y, placement.Origin.Z };
                payload.Axis = new[] { placement.AxisDirection.X, placement.AxisDirection.Y, placement.AxisDirection.Z };
                payload.Rotation = placement.RotationDegrees == 0d ? null : placement.RotationDegrees;
                payload.Elevation = placement.Elevation == 0d ? null : placement.Elevation;
            }

            return JsonSerializer.Serialize(payload, Options);
        }

        /// <summary>Reads an element back from the text stored on its object.</summary>
        /// <param name="payloadText">The payload.</param>
        /// <returns>The element, or a failure describing why the payload is unusable.</returns>
        public static Result<DecodedElement> Decode(string? payloadText)
        {
            if (string.IsNullOrWhiteSpace(payloadText))
            {
                return Result<DecodedElement>.Failure(
                    "Metadata.Empty",
                    "This object carries no StructureGen parameters. It may have been exploded, or created by " +
                    "something other than StructureGen.");
            }

            ElementPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<ElementPayload>(payloadText!, Options);
            }
            catch (JsonException)
            {
                return Result<DecodedElement>.Failure(
                    "Metadata.Corrupt",
                    "The StructureGen parameters stored on this object could not be read. The object can still be " +
                    "edited by hand, but StructureGen cannot regenerate it.");
            }

            if (payload is null)
            {
                return Result<DecodedElement>.Failure(
                    "Metadata.Empty", "The StructureGen parameters stored on this object are empty.");
            }

            if (payload.V > CurrentVersion)
            {
                return Result<DecodedElement>.Failure(
                    "Metadata.NewerVersion",
                    $"This object was created by a newer version of StructureGen (parameter format {payload.V}; " +
                    $"this build reads format {CurrentVersion}). Update the plugin to edit it.");
            }

            if (string.IsNullOrWhiteSpace(payload.Category) || string.IsNullOrWhiteSpace(payload.Designation))
            {
                return Result<DecodedElement>.Failure(
                    "Metadata.Incomplete",
                    "The StructureGen parameters stored on this object are missing the category or the designation, " +
                    "so it cannot be regenerated.");
            }

            List<Placement> placements = new();
            if (payload.Origin is { Length: 3 } origin)
            {
                Vec3 axis = payload.Axis is { Length: 3 } axisValues
                    ? new Vec3(axisValues[0], axisValues[1], axisValues[2])
                    : Vec3.UnitX;

                placements.Add(new Placement(
                    new Vec3(origin[0], origin[1], origin[2]),
                    axis.Magnitude <= 1e-9 ? Vec3.UnitX : axis,
                    payload.Rotation ?? 0d,
                    payload.Elevation ?? 0d));
            }

            List<ProfileVertex>? outline = null;
            if (payload.Outline is { Length: >= 3 })
            {
                outline = new List<ProfileVertex>(payload.Outline.Length);

                foreach (double[] point in payload.Outline)
                {
                    if (point.Length >= 2)
                    {
                        outline.Add(new ProfileVertex(point[0], point[1], point.Length > 2 ? point[2] : 0d));
                    }
                }

                if (outline.Count < 3)
                {
                    outline = null;
                }
            }

            ElementDefinition definition = new(
                ParseGuid(payload.Id),
                payload.Category!,
                payload.Standard ?? string.Empty,
                payload.Record ?? string.Empty,
                payload.Designation!,
                payload.Dimensions ?? new Dictionary<string, decimal>(StringComparer.Ordinal),
                Math.Max(1, payload.Quantity),
                payload.LengthMm,
                payload.Detail,
                payload.Representation,
                placements,
                payload.Material,
                payload.Grade,
                payload.UnitWeight,
                payload.PieceMass,
                payload.Density,
                payload.Layer,
                payload.Notes,
                payload.DrawingReference,
                payload.Custom ?? false,
                payload.Verified ?? false,
                ParseTimestamp(payload.CreatedUtc),
                outline);

            return Result<DecodedElement>.Success(
                new DecodedElement(definition, payload.DatabaseStamp, payload.PluginVersion, payload.V));
        }

        /// <summary>Splits a payload into chunks small enough to store as individual record entries.</summary>
        /// <param name="payloadText">The payload.</param>
        /// <param name="chunkSize">The greatest number of characters per chunk.</param>
        /// <returns>The chunks, in order.</returns>
        /// <remarks>
        /// Extended entity data limits a single string to 255 bytes, and although a record in an
        /// extension dictionary is more generous, staying well inside the smaller limit means the
        /// same chunking works for both and the payload survives being copied between drawings.
        /// </remarks>
        public static IReadOnlyList<string> Chunk(string payloadText, int chunkSize = 200)
        {
            if (payloadText is null)
            {
                throw new ArgumentNullException(nameof(payloadText));
            }

            if (chunkSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "A chunk must hold at least one character.");
            }

            List<string> chunks = new((payloadText.Length / chunkSize) + 1);
            for (int start = 0; start < payloadText.Length; start += chunkSize)
            {
                chunks.Add(payloadText.Substring(start, Math.Min(chunkSize, payloadText.Length - start)));
            }

            return chunks.Count == 0 ? new List<string> { string.Empty } : chunks;
        }

        /// <summary>Reassembles a payload from its chunks.</summary>
        /// <param name="chunks">The chunks, in order.</param>
        /// <returns>The payload.</returns>
        public static string Unchunk(IEnumerable<string> chunks) => string.Concat(chunks);

        private static Guid ParseGuid(string? text) =>
            Guid.TryParse(text, out Guid value) ? value : Guid.NewGuid();

        private static DateTime ParseTimestamp(string? text) =>
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value)
                ? value
                : DateTime.UtcNow;

        /// <summary>The wire format. Property names are short because every one is written onto every object.</summary>
        private sealed class ElementPayload
        {
            public int V { get; set; } = CurrentVersion;

            public string? Id { get; set; }

            public string? Category { get; set; }

            public string? Standard { get; set; }

            public string? Record { get; set; }

            public string? Designation { get; set; }

            public Dictionary<string, decimal>? Dimensions { get; set; }

            public int Quantity { get; set; } = 1;

            public decimal? LengthMm { get; set; }

            public DetailLevel Detail { get; set; } = DetailLevel.Standard;

            public RepresentationMode Representation { get; set; } = RepresentationMode.Solid3D;

            public string? Material { get; set; }

            public string? Grade { get; set; }

            public decimal? UnitWeight { get; set; }

            public decimal? PieceMass { get; set; }

            public decimal? Density { get; set; }

            public string? Layer { get; set; }

            public string? Notes { get; set; }

            public string? DrawingReference { get; set; }

            public bool? Custom { get; set; }

            public bool? Verified { get; set; }

            public double[]? Origin { get; set; }

            public double[]? Axis { get; set; }

            public double? Rotation { get; set; }

            public double? Elevation { get; set; }

            public double[][]? Outline { get; set; }

            public string? CreatedUtc { get; set; }

            public string? DatabaseStamp { get; set; }

            public string? PluginVersion { get; set; }
        }
    }

    /// <summary>An element recovered from the metadata stored on a drawing object.</summary>
    public sealed class DecodedElement
    {
        /// <summary>Initialises a new instance of the <see cref="DecodedElement"/> class.</summary>
        /// <param name="definition">The recovered element.</param>
        /// <param name="databaseStamp">The database the element was originally created from.</param>
        /// <param name="pluginVersion">The plugin version that created it.</param>
        /// <param name="payloadVersion">The payload format version it was written with.</param>
        public DecodedElement(
            ElementDefinition definition, string? databaseStamp, string? pluginVersion, int payloadVersion)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            DatabaseStamp = databaseStamp;
            PluginVersion = pluginVersion;
            PayloadVersion = payloadVersion;
        }

        /// <summary>Gets the recovered element.</summary>
        public ElementDefinition Definition { get; }

        /// <summary>Gets the database the element was originally created from.</summary>
        public string? DatabaseStamp { get; }

        /// <summary>Gets the plugin version that created it.</summary>
        public string? PluginVersion { get; }

        /// <summary>Gets the payload format version it was written with.</summary>
        public int PayloadVersion { get; }
    }
}
