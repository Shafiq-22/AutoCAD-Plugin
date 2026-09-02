using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Core.Results;

namespace StructureGen.Data.Json
{
    /// <summary>Loads a structural database from the JSON file format documented in <c>docs/DATABASE.md</c>.</summary>
    public sealed class JsonDatabaseLoader : IDatabaseLoader
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        private readonly ILogger _logger;
        private readonly DatabaseValidator _validator;

        /// <summary>Initialises a new instance of the <see cref="JsonDatabaseLoader"/> class.</summary>
        /// <param name="logger">Receives technical detail about load failures.</param>
        /// <param name="validator">Validates the loaded content.</param>
        public JsonDatabaseLoader(ILogger logger, DatabaseValidator validator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        /// <inheritdoc />
        public Result<StructuralDatabase> Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Result<StructuralDatabase>.Failure(
                    "Db.NoPath", "No structural database file has been configured. Set one in STRUCTUREGEN settings.");
            }

            string json;
            try
            {
                if (!File.Exists(path))
                {
                    _logger.Warning($"Structural database not found at '{path}'.");
                    return Result<StructuralDatabase>.Failure(
                        "Db.NotFound",
                        $"The structural database file was not found:{Environment.NewLine}{path}{Environment.NewLine}" +
                        "Check the database path in STRUCTUREGEN settings, or use Reload Database after restoring the file.");
                }

                json = File.ReadAllText(path);
            }
            catch (IOException exception)
            {
                _logger.Error($"Could not read structural database '{path}'.", exception);
                return Result<StructuralDatabase>.Failure(
                    "Db.ReadFailed",
                    $"The structural database file could not be read. It may be open in another application:{Environment.NewLine}{path}");
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.Error($"Access denied reading structural database '{path}'.", exception);
                return Result<StructuralDatabase>.Failure(
                    "Db.AccessDenied",
                    $"You do not have permission to read the structural database file:{Environment.NewLine}{path}");
            }

            return Parse(json, path);
        }

        /// <summary>Parses a database from JSON text already in memory.</summary>
        /// <param name="json">The JSON document.</param>
        /// <param name="sourcePath">The path to record as the origin of the data, for diagnostics.</param>
        /// <returns>The loaded database, or a failure describing why the content is unusable.</returns>
        public Result<StructuralDatabase> Parse(string json, string sourcePath)
        {
            DatabaseFileDto? file;
            try
            {
                file = JsonSerializer.Deserialize<DatabaseFileDto>(json, Options);
            }
            catch (JsonException exception)
            {
                _logger.Error($"Structural database '{sourcePath}' is not valid JSON.", exception);
                string where = exception.LineNumber.HasValue
                    ? $" near line {exception.LineNumber.Value + 1}"
                    : string.Empty;
                return Result<StructuralDatabase>.Failure(
                    "Db.InvalidJson",
                    $"The structural database file is not valid JSON{where}. Fix the file and use Reload Database.");
            }

            if (file is null)
            {
                return Result<StructuralDatabase>.Failure(
                    "Db.Empty", $"The structural database file is empty: {sourcePath}");
            }

            if (!DatabaseSchema.IsReadable(file.SchemaVersion))
            {
                return Result<StructuralDatabase>.Failure(
                    "Db.UnsupportedSchema",
                    $"The database declares schema version '{file.SchemaVersion ?? "(none)"}' but this build of " +
                    $"StructureGen reads schema version {DatabaseSchema.CurrentVersion}. " +
                    "Update the plugin, or convert the file with the StructureGen.DataImport tool.");
            }

            ValidationReport report = new();
            DatabaseUnits units = DatabaseUnits.Parse(
                file.Units?.Length, file.Units?.UnitWeight, file.Units?.Density, file.Units?.PieceMass,
                out string? unrecognised);

            if (unrecognised is not null)
            {
                report.AddWarning(
                    "Db.UnknownUnit",
                    $"The unit token '{unrecognised}' was not recognised, so the default unit was assumed. " +
                    "Supported tokens are listed in docs/DATABASE.md.",
                    sourcePath);
            }

            DatabaseMetadata metadata = BuildMetadata(file, sourcePath);
            List<StandardRecord> standards = BuildStandards(file, report);
            List<MaterialRecord> materials = BuildMaterials(file, units, report);
            List<SectionRecord> sections = BuildSections(file, units, report);

            StructuralDatabase database = new(metadata, standards, materials, sections);
            report.Merge(_validator.Validate(database));

            if (report.HasErrors)
            {
                _logger.Error(
                    $"Structural database '{sourcePath}' failed validation with {report.ErrorCount} error(s):" +
                    Environment.NewLine + report.ToDisplayString(int.MaxValue));
                return Result<StructuralDatabase>.Failure(
                    "Db.Invalid",
                    $"The structural database contains {report.ErrorCount} error(s) and was not loaded. " +
                    "The first problems are listed below; the full list is in the StructureGen log file." +
                    Environment.NewLine + Environment.NewLine + report.ToDisplayString(),
                    report);
            }

            if (report.HasWarnings)
            {
                _logger.Warning(
                    $"Structural database '{sourcePath}' loaded with {report.WarningCount} warning(s):" +
                    Environment.NewLine + report.ToDisplayString(int.MaxValue));
            }

            _logger.Information(
                $"Loaded structural database '{metadata.Stamp}' from '{sourcePath}': " +
                $"{standards.Count} standard(s), {materials.Count} material(s), {sections.Count} section(s).");

            return Result<StructuralDatabase>.Success(database);
        }

        private static DatabaseMetadata BuildMetadata(DatabaseFileDto file, string sourcePath) => new(
            file.Database?.Id ?? Path.GetFileNameWithoutExtension(sourcePath),
            file.Database?.Name ?? Path.GetFileName(sourcePath),
            file.Database?.Version ?? "0",
            file.SchemaVersion ?? DatabaseSchema.CurrentVersion,
            file.Database?.RevisionDate,
            file.Database?.Description,
            file.Database?.Verified ?? false,
            file.Database?.Notes,
            sourcePath);

        private static List<StandardRecord> BuildStandards(DatabaseFileDto file, ValidationReport report)
        {
            List<StandardRecord> standards = new();
            foreach (StandardDto dto in file.Standards ?? new List<StandardDto>())
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    report.AddError("Db.Standard.MissingId", "A standard entry has no 'id'.", dto.Name);
                    continue;
                }

                standards.Add(new StandardRecord(
                    dto.Id!.Trim(),
                    string.IsNullOrWhiteSpace(dto.Name) ? dto.Id!.Trim() : dto.Name!.Trim(),
                    dto.Region,
                    dto.Revision,
                    dto.Description));
            }

            return standards;
        }

        private static List<MaterialRecord> BuildMaterials(DatabaseFileDto file, DatabaseUnits units, ValidationReport report)
        {
            List<MaterialRecord> materials = new();
            foreach (MaterialDto dto in file.Materials ?? new List<MaterialDto>())
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    report.AddError("Db.Material.MissingId", "A material entry has no 'id'.", dto.Name);
                    continue;
                }

                decimal? density = dto.Density.HasValue ? units.DensityToKgPerM3(dto.Density.Value) : null;
                if (density is <= 0m)
                {
                    report.AddError(
                        "Db.Material.InvalidDensity",
                        $"Material '{dto.Id}' declares a density of {dto.Density} which is not greater than zero.",
                        dto.Id);
                    density = null;
                }

                materials.Add(new MaterialRecord(
                    dto.Id!.Trim(),
                    string.IsNullOrWhiteSpace(dto.Name) ? dto.Id!.Trim() : dto.Name!.Trim(),
                    density,
                    dto.Grade,
                    dto.StandardId,
                    dto.Notes));
            }

            return materials;
        }

        private static List<SectionRecord> BuildSections(DatabaseFileDto file, DatabaseUnits units, ValidationReport report)
        {
            List<SectionRecord> sections = new();
            int index = 0;

            foreach (SectionDto dto in file.Sections ?? new List<SectionDto>())
            {
                index++;
                string where = string.IsNullOrWhiteSpace(dto.Id)
                    ? $"sections[{index.ToString(CultureInfo.InvariantCulture)}]"
                    : dto.Id!;

                if (string.IsNullOrWhiteSpace(dto.Id))
                {
                    report.AddError("Db.Section.MissingId", "A section entry has no 'id'.", where);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.Category))
                {
                    report.AddError("Db.Section.MissingCategory", $"Section '{where}' has no 'category'.", where);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(dto.Designation))
                {
                    report.AddError("Db.Section.MissingDesignation", $"Section '{where}' has no 'designation'.", where);
                    continue;
                }

                // Every dimension is a length except the tabulated area, which is published in
                // square units and therefore converts by the square of the linear factor.
                decimal linearFactor = units.LengthToMillimetres(1m);
                Dictionary<string, decimal> dimensions = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, decimal> pair in dto.Dimensions ?? new Dictionary<string, decimal>())
                {
                    string key = pair.Key.Trim();
                    dimensions[key] = string.Equals(key, DimensionKeys.Area, StringComparison.Ordinal)
                        ? pair.Value * linearFactor * linearFactor
                        : pair.Value * linearFactor;
                }

                decimal? unitWeight = dto.UnitWeight.HasValue ? units.UnitWeightToKgPerM(dto.UnitWeight.Value) : null;
                if (unitWeight is <= 0m)
                {
                    report.AddError(
                        "Db.Section.InvalidUnitWeight",
                        $"Section '{where}' declares a unit weight of {dto.UnitWeight} which is not greater than zero.",
                        where);
                    unitWeight = null;
                }

                decimal? pieceMass = dto.PieceMass.HasValue ? units.PieceMassToKg(dto.PieceMass.Value) : null;
                if (pieceMass is <= 0m)
                {
                    report.AddError(
                        "Db.Section.InvalidPieceMass",
                        $"Section '{where}' declares a piece mass of {dto.PieceMass} which is not greater than zero.",
                        where);
                    pieceMass = null;
                }

                IReadOnlyList<ProfileVertex>? outline = BuildOutline(dto, linearFactor, where, report);

                sections.Add(new SectionRecord(
                    dto.Id!.Trim(),
                    (dto.StandardId ?? string.Empty).Trim(),
                    dto.Category!.Trim(),
                    dto.Designation!.Trim(),
                    dimensions,
                    unitWeight,
                    pieceMass,
                    dto.NominalSize,
                    dto.Material,
                    dto.Grade,
                    dto.Properties is null
                        ? null
                        : dto.Properties.ToDictionary(p => p.Key.Trim(), p => p.Value, StringComparer.Ordinal),
                    dto.Verified ?? false,
                    dto.Provenance,
                    dto.Notes,
                    outline));
            }

            return sections;
        }

        private static IReadOnlyList<ProfileVertex>? BuildOutline(
            SectionDto dto, decimal linearFactor, string where, ValidationReport report)
        {
            if (dto.Outline is null || dto.Outline.Count == 0)
            {
                return null;
            }

            if (dto.Outline.Count < 3)
            {
                report.AddError(
                    "Db.Section.ShortOutline",
                    $"Section '{where}' declares an outline of {dto.Outline.Count} point(s); a closed profile needs at least three.",
                    where);
                return null;
            }

            List<ProfileVertex> vertices = new(dto.Outline.Count);
            foreach (OutlinePointDto point in dto.Outline)
            {
                if (point.X is null || point.Y is null)
                {
                    report.AddError(
                        "Db.Section.InvalidOutlinePoint",
                        $"Section '{where}' has an outline point missing 'x' or 'y'.",
                        where);
                    return null;
                }

                vertices.Add(new ProfileVertex(
                    (double)(point.X.Value * linearFactor),
                    (double)(point.Y.Value * linearFactor),
                    (double)(point.Bulge ?? 0m)));
            }

            return vertices;
        }
    }
}
