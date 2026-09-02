using System;
using System.Collections.Generic;
using System.IO;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Data;
using StructureGen.Data.Json;
using StructureGen.Geometry;
using StructureGen.Services;

namespace StructureGen.Tests
{
    /// <summary>Shared fixtures. Kept deliberately explicit so a failing test reads as a statement of fact.</summary>
    internal static class TestData
    {
        /// <summary>The folder the shipped data files are copied to beside the test binaries.</summary>
        public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "data");

        /// <summary>The shipped demonstration database.</summary>
        public static string DemoDatabasePath => Path.Combine(DataDirectory, "structural-sections.json");

        /// <summary>The shipped imperial demonstration database.</summary>
        public static string ImperialDatabasePath =>
            Path.Combine(DataDirectory, "structural-sections-imperial-demo.json");

        /// <summary>Builds a loader wired the same way the plugin wires it.</summary>
        /// <param name="database">Receives a holder the custom generator resolves records through.</param>
        /// <returns>The loader.</returns>
        public static JsonDatabaseLoader Loader(out Func<StructuralDatabase?> database)
        {
            StructuralDatabase? loaded = null;
            database = () => loaded;

            ElementGeneratorRegistry registry = Registry(() => loaded);
            JsonDatabaseLoader loader = new(NullLogger.Instance, new DatabaseValidator(registry));
            return loader;
        }

        /// <summary>Builds a generator registry.</summary>
        /// <param name="database">Supplies the database custom outlines are resolved against.</param>
        /// <returns>The registry.</returns>
        public static ElementGeneratorRegistry Registry(Func<StructuralDatabase?>? database = null) =>
            ElementGeneratorRegistry.CreateDefault(id => database?.Invoke()?.FindSection(id));

        /// <summary>Loads the shipped demonstration database, failing the test if it does not load.</summary>
        /// <returns>The database.</returns>
        public static StructuralDatabase LoadDemoDatabase()
        {
            StructuralDatabase? loaded = null;
            ElementGeneratorRegistry registry = ElementGeneratorRegistry.CreateDefault(id => loaded?.FindSection(id));
            JsonDatabaseLoader loader = new(NullLogger.Instance, new DatabaseValidator(registry));

            Core.Results.Result<StructuralDatabase> result = loader.Load(DemoDatabasePath);

            if (result.IsFailure)
            {
                throw new InvalidOperationException(
                    $"The shipped demonstration database failed to load: {result.ErrorMessage}");
            }

            loaded = result.Value;
            return loaded;
        }

        /// <summary>Builds an element definition without going through the factory.</summary>
        /// <param name="categoryId">The category.</param>
        /// <param name="dimensions">The dimensions in millimetres.</param>
        /// <param name="lengthMm">The member length.</param>
        /// <param name="quantity">The number of pieces.</param>
        /// <param name="unitWeight">A tabulated mass per metre, when there is one.</param>
        /// <param name="density">A material density, when there is one.</param>
        /// <param name="designation">The profile designation.</param>
        /// <param name="detail">The detail level.</param>
        /// <param name="pieceMass">A tabulated mass per piece, when there is one.</param>
        /// <param name="isCustom">Whether the element departs from its standard.</param>
        /// <param name="elementId">The element identity, when a specific one is wanted.</param>
        /// <param name="material">The material name.</param>
        /// <param name="sectionRecordId">The database record identifier.</param>
        /// <param name="outline">An explicit cross-section outline.</param>
        /// <returns>The element.</returns>
        public static ElementDefinition Element(
            string categoryId,
            IReadOnlyDictionary<string, decimal> dimensions,
            decimal? lengthMm = 6000m,
            int quantity = 1,
            decimal? unitWeight = null,
            decimal? density = null,
            string designation = "TEST",
            DetailLevel detail = DetailLevel.Standard,
            decimal? pieceMass = null,
            bool isCustom = false,
            Guid? elementId = null,
            string? material = "Structural steel",
            string sectionRecordId = "test-record",
            IReadOnlyList<ProfileVertex>? outline = null) =>
            new(
                elementId ?? Guid.NewGuid(),
                categoryId,
                "test-standard",
                sectionRecordId,
                designation,
                dimensions,
                quantity,
                lengthMm,
                detail,
                RepresentationMode.Solid3D,
                Array.Empty<Placement>(),
                material,
                "S355",
                unitWeight,
                pieceMass,
                density,
                null,
                null,
                null,
                isCustom,
                true,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                outline);

        /// <summary>Builds an element definition straight from a database record.</summary>
        /// <param name="record">The record to base it on.</param>
        /// <param name="lengthMm">The member length, or <see langword="null"/> to derive one from the category.</param>
        /// <param name="quantity">The number of pieces.</param>
        /// <param name="detail">The detail level.</param>
        /// <returns>The element.</returns>
        public static ElementDefinition FromRecord(
            SectionRecord record,
            decimal? lengthMm = null,
            int quantity = 1,
            DetailLevel detail = DetailLevel.Standard) =>
            Element(
                record.CategoryId,
                record.CopyDimensions(),
                lengthMm ?? (CategoryCatalog.FindOrGeneric(record.CategoryId).LengthIsUserParameter ? 6000m : null),
                quantity,
                record.UnitWeightKgPerM,
                7850m,
                record.Designation,
                detail,
                record.PieceMassKg,
                sectionRecordId: record.Id,
                outline: record.Outline);

        /// <summary>The dimensions of an IPE 300, a section whose published figures are easy to check.</summary>
        public static Dictionary<string, decimal> Ipe300 => new()
        {
            ["h"] = 300m, ["b"] = 150m, ["tw"] = 7.1m, ["tf"] = 10.7m, ["r"] = 15m
        };

        /// <summary>The dimensions of a 200 by 10 plate.</summary>
        public static Dictionary<string, decimal> Plate => new() { ["w"] = 200m, ["tt"] = 10m };
    }
}
