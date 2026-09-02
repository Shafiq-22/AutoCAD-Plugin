using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using StructureGen.Core.Model;
using StructureGen.Data;

namespace StructureGen.Tools.DataImport
{
    /// <summary>Writes section records back out in the StructureGen database format.</summary>
    public static class DatabaseWriter
    {
        /// <summary>Renders a set of records as a database file.</summary>
        /// <param name="sections">The records to write.</param>
        /// <param name="databaseId">The dataset identifier.</param>
        /// <param name="databaseName">The dataset display name.</param>
        /// <param name="version">The dataset version.</param>
        /// <param name="notes">Free text notes recorded in the file.</param>
        /// <param name="standards">Standards to include, when the caller has them.</param>
        /// <param name="materials">Materials to include, when the caller has them.</param>
        /// <returns>The JSON document.</returns>
        /// <remarks>
        /// Values are always written in the canonical internal units, so the produced file declares
        /// millimetres and kilograms per metre regardless of what the source spreadsheet used. That
        /// keeps a converted file unambiguous.
        /// </remarks>
        public static string Write(
            IReadOnlyList<SectionRecord> sections,
            string databaseId,
            string databaseName,
            string version,
            string? notes = null,
            IReadOnlyList<StandardRecord>? standards = null,
            IReadOnlyList<MaterialRecord>? materials = null)
        {
            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            JsonObject root = new()
            {
                ["schemaVersion"] = DatabaseSchema.CurrentVersion,
                ["database"] = new JsonObject
                {
                    ["id"] = databaseId,
                    ["name"] = databaseName,
                    ["version"] = version,
                    ["revisionDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    ["verified"] = sections.Count > 0 && sections.All(s => s.Verified),
                    ["notes"] = notes ?? "Converted by StructureGen.DataImport. Values are stored in millimetres and kilograms."
                },
                ["units"] = new JsonObject
                {
                    ["length"] = "mm",
                    ["unitWeight"] = "kg/m",
                    ["density"] = "kg/m3",
                    ["pieceMass"] = "kg"
                }
            };

            if (standards is { Count: > 0 })
            {
                JsonArray array = new();
                foreach (StandardRecord standard in standards)
                {
                    JsonObject item = new() { ["id"] = standard.Id, ["name"] = standard.Name };
                    AddIfPresent(item, "region", standard.Region);
                    AddIfPresent(item, "revision", standard.Revision);
                    AddIfPresent(item, "description", standard.Description);
                    array.Add(item);
                }

                root["standards"] = array;
            }

            if (materials is { Count: > 0 })
            {
                JsonArray array = new();
                foreach (MaterialRecord material in materials)
                {
                    JsonObject item = new() { ["id"] = material.Id, ["name"] = material.Name };
                    AddIfPresent(item, "grade", material.Grade);

                    if (material.DensityKgPerCubicMetre is { } density)
                    {
                        item["density"] = density;
                    }

                    AddIfPresent(item, "standardId", material.StandardId);
                    AddIfPresent(item, "notes", material.Notes);
                    array.Add(item);
                }

                root["materials"] = array;
            }

            JsonArray sectionArray = new();
            foreach (SectionRecord section in sections)
            {
                JsonObject item = new()
                {
                    ["id"] = section.Id,
                    ["standardId"] = section.StandardId,
                    ["category"] = section.CategoryId,
                    ["designation"] = section.Designation
                };

                AddIfPresent(item, "nominalSize", section.NominalSize);
                AddIfPresent(item, "material", section.Material);
                AddIfPresent(item, "grade", section.Grade);

                if (section.UnitWeightKgPerM is { } unitWeight)
                {
                    item["unitWeight"] = unitWeight;
                }

                if (section.PieceMassKg is { } pieceMass)
                {
                    item["pieceMass"] = pieceMass;
                }

                if (section.Dimensions.Count > 0)
                {
                    JsonObject dimensions = new();
                    foreach (KeyValuePair<string, decimal> pair in section.Dimensions.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        dimensions[pair.Key] = pair.Value;
                    }

                    item["dimensions"] = dimensions;
                }

                if (section.Properties.Count > 0)
                {
                    JsonObject properties = new();
                    foreach (KeyValuePair<string, decimal> pair in section.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        properties[pair.Key] = pair.Value;
                    }

                    item["properties"] = properties;
                }

                item["verified"] = section.Verified;
                AddIfPresent(item, "provenance", section.Provenance);
                AddIfPresent(item, "notes", section.Notes);

                sectionArray.Add(item);
            }

            root["sections"] = sectionArray;

            // The relaxed encoder is what keeps quotes in notes readable as \" rather than \u0022.
            // The output is a file on disk, never HTML, so the stricter default buys nothing here.
            return root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        private static void AddIfPresent(JsonObject item, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                item[name] = value;
            }
        }
    }
}
