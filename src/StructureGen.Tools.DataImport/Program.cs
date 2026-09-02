using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Core.Tabular;
using StructureGen.Data;
using StructureGen.Data.Import;
using StructureGen.Data.Json;
using StructureGen.Excel.Xlsx;
using StructureGen.Geometry;

namespace StructureGen.Tools.DataImport
{
    /// <summary>
    /// A command line companion to the plugin: converts a section spreadsheet into the StructureGen
    /// database format, and validates a database without AutoCAD being involved.
    /// </summary>
    /// <remarks>
    /// Keeping this outside the plugin matters for the workflow. Preparing a section database is a
    /// desk job that happens once and is then repeated whenever a catalogue is revised; making the
    /// user open AutoCAD to do it, or discover their conversion errors only when the palette refuses
    /// to load, would be the wrong shape. It also means the conversion can run in a build pipeline.
    /// </remarks>
    public static class Program
    {
        private static readonly JsonSerializerOptions MappingOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>Runs the tool.</summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>Zero on success, one on a usage error, two when the data was rejected.</returns>
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            try
            {
                return args[0].ToLowerInvariant() switch
                {
                    "import" => RunImport(args.Skip(1).ToArray()),
                    "validate" => RunValidate(args.Skip(1).ToArray()),
                    "sheets" => RunSheets(args.Skip(1).ToArray()),
                    "help" or "--help" or "-h" => PrintUsageAndSucceed(),
                    _ => UnknownCommand(args[0])
                };
            }
            catch (FileNotFoundException exception)
            {
                Console.Error.WriteLine($"File not found: {exception.FileName ?? exception.Message}");
                return 2;
            }
            catch (DirectoryNotFoundException exception)
            {
                Console.Error.WriteLine($"Folder not found: {exception.Message}");
                return 2;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"The file could not be read or written: {exception.Message}");
                return 2;
            }
            catch (UnauthorizedAccessException exception)
            {
                Console.Error.WriteLine($"Access denied: {exception.Message}");
                return 2;
            }
            catch (JsonException exception)
            {
                Console.Error.WriteLine($"A JSON file could not be parsed: {exception.Message}");
                return 2;
            }
            catch (InvalidOperationException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 2;
            }
        }

        private static int RunImport(string[] args)
        {
            Dictionary<string, string> options = ParseOptions(args);

            if (!options.TryGetValue("in", out string? input) || !options.TryGetValue("map", out string? mapPath))
            {
                Console.Error.WriteLine("import requires --in <spreadsheet> and --map <mapping.json>.");
                PrintUsage();
                return 1;
            }

            string output = options.TryGetValue("out", out string? outPath)
                ? outPath
                : Path.ChangeExtension(input, ".json");

            MappingProfile profile = JsonSerializer.Deserialize<MappingProfile>(File.ReadAllText(mapPath), MappingOptions)
                                     ?? throw new InvalidOperationException($"The mapping file '{mapPath}' is empty.");

            ITabularSource source = OpenSource(input, options.GetValueOrDefault("sheet"));
            Console.WriteLine($"Reading '{source.Name}': {source.Headers.Count} column(s), {source.Rows.Count} row(s).");

            ImportOutcome outcome = new TableImporter().Import(source, profile);
            PrintReport(outcome.Report);

            if (outcome.Sections.Count == 0)
            {
                Console.Error.WriteLine("No records were produced. Check that the column names in the mapping match the spreadsheet.");
                return 2;
            }

            string json = DatabaseWriter.Write(
                outcome.Sections,
                options.GetValueOrDefault("id") ?? Path.GetFileNameWithoutExtension(output),
                options.GetValueOrDefault("name") ?? Path.GetFileNameWithoutExtension(output),
                options.GetValueOrDefault("version") ?? "1.0.0",
                profile.MarkVerified
                    ? null
                    : "Converted by StructureGen.DataImport. Every record is marked unverified; set \"verified\": true " +
                      "once you have checked it against your authoritative source.");

            File.WriteAllText(output, json);
            Console.WriteLine($"Wrote {outcome.Sections.Count} record(s) to '{output}' ({outcome.RowsSkipped} row(s) skipped).");

            Console.WriteLine();
            Console.WriteLine("Validating the result...");
            int exitCode = ValidateFile(output);

            if (exitCode != 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "A mapping profile produces sections only; it does not invent the standards and materials they " +
                    $"refer to. Either place the output beside a '{JsonDatabaseLoader.SharedDefinitionsFileName}' " +
                    "that declares them, or add \"standards\" and \"materials\" blocks to the produced file. " +
                    "See docs/DATABASE.md.");
            }

            return exitCode;
        }

        private static int RunValidate(string[] args)
        {
            Dictionary<string, string> options = ParseOptions(args);
            string? path = options.GetValueOrDefault("in") ?? args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

            if (path is null)
            {
                Console.Error.WriteLine("validate requires a database file: validate --in <database.json>");
                return 1;
            }

            return ValidateFile(path);
        }

        private static int RunSheets(string[] args)
        {
            Dictionary<string, string> options = ParseOptions(args);
            string? path = options.GetValueOrDefault("in") ?? args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

            if (path is null)
            {
                Console.Error.WriteLine("sheets requires a workbook: sheets --in <workbook.xlsx>");
                return 1;
            }

            foreach (string name in XlsxReader.SheetNames(path))
            {
                Console.WriteLine(name);
            }

            return 0;
        }

        private static int ValidateFile(string path)
        {
            ConsoleLogger logger = new();

            // The custom profile generator needs to resolve records by id, but the database it will
            // resolve against is the one this call is about to load. The closure reads a local that
            // is filled in once loading succeeds, which is the same knot the plugin's composition
            // root ties and unties the same way.
            StructuralDatabase? loaded = null;
            ElementGeneratorRegistry generators = ElementGeneratorRegistry.CreateDefault(id => loaded?.FindSection(id));
            JsonDatabaseLoader loader = new(logger, new DatabaseValidator(generators));

            Result<StructuralDatabase> result = loader.Load(Path.GetFullPath(path));
            if (result.IsSuccess)
            {
                loaded = result.Value;
            }

            if (result.Report is not null)
            {
                PrintReport(result.Report);
            }

            if (result.IsFailure)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(result.ErrorMessage);
                return 2;
            }

            StructuralDatabase database = result.Value;
            Console.WriteLine();
            Console.WriteLine($"OK: '{database.Metadata.Stamp}'");
            Console.WriteLine($"  {database.Standards.Count} standard(s), {database.Materials.Count} material(s), {database.Sections.Count} section(s).");

            foreach (IGrouping<string, SectionRecord> group in database.Sections
                         .GroupBy(s => s.CategoryId, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                bool generable = generators.Find(group.Key) is not null;
                Console.WriteLine($"  {group.Key,-14} {group.Count(),5}  {(generable ? "geometry supported" : "SCHEDULE ONLY - no generator")}");
            }

            if (database.UnverifiedRecordCount > 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"  NOTE: {database.UnverifiedRecordCount} of {database.Sections.Count} record(s) are not marked verified.");
            }

            return 0;
        }

        private static ITabularSource OpenSource(string path, string? sheetName)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();

            return extension switch
            {
                ".xlsx" or ".xlsm" => XlsxReader.Read(path, sheetName),
                ".tsv" => CsvTabularSource.FromFile(path, '\t'),
                _ => CsvTabularSource.FromFile(path)
            };
        }

        private static void PrintReport(ValidationReport report)
        {
            foreach (ValidationMessage message in report.Messages)
            {
                TextWriter writer = message.Severity == Severity.Error ? Console.Error : Console.Out;
                writer.WriteLine($"  {message}");
            }

            if (report.Messages.Count > 0)
            {
                Console.WriteLine($"  ({report.ErrorCount} error(s), {report.WarningCount} warning(s))");
            }
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < args.Length; index++)
            {
                if (!args[index].StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                string key = args[index].Substring(2);
                string value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
                    ? args[++index]
                    : "true";

                options[key] = value;
            }

            return options;
        }

        private static int UnknownCommand(string command)
        {
            Console.Error.WriteLine($"Unknown command '{command}'.");
            PrintUsage();
            return 1;
        }

        private static int PrintUsageAndSucceed()
        {
            PrintUsage();
            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("StructureGen.DataImport - convert and validate structural section databases.");
            Console.WriteLine();
            Console.WriteLine("  import   --in <file.csv|file.xlsx> --map <mapping.json> [--out <db.json>]");
            Console.WriteLine("           [--sheet <name>] [--id <id>] [--name <name>] [--version <version>]");
            Console.WriteLine("               Converts a section table into the StructureGen database format,");
            Console.WriteLine("               then validates the result.");
            Console.WriteLine();
            Console.WriteLine("  validate --in <db.json>");
            Console.WriteLine("               Checks a database exactly as the plugin would on load.");
            Console.WriteLine();
            Console.WriteLine("  sheets   --in <file.xlsx>");
            Console.WriteLine("               Lists the worksheets in a workbook.");
            Console.WriteLine();
            Console.WriteLine("Exit codes: 0 success, 1 usage error, 2 the data was rejected.");
            Console.WriteLine("See docs/DATABASE.md for the schema and the mapping profile format.");
        }

        /// <summary>Writes log entries to the console so the tool needs no log file.</summary>
        private sealed class ConsoleLogger : ILogger
        {
            public void Log(LogLevel level, string message, Exception? exception = null)
            {
                if (level < LogLevel.Warning)
                {
                    return;
                }

                TextWriter writer = level >= LogLevel.Error ? Console.Error : Console.Out;
                writer.WriteLine($"  [{level}] {message}");

                if (exception is not null)
                {
                    writer.WriteLine($"    {exception.GetType().Name}: {exception.Message}");
                }
            }
        }
    }
}
