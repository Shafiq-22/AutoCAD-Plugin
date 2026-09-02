using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using StructureGen.Core.Diagnostics;
using StructureGen.Core.Results;

namespace StructureGen.Core.Settings
{
    /// <summary>Persists <see cref="ProjectSettings"/> as JSON under the user's application data folder.</summary>
    public sealed class JsonSettingsStore : ISettingsStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private readonly ILogger _logger;

        /// <summary>Initialises a new instance of the <see cref="JsonSettingsStore"/> class.</summary>
        /// <param name="logger">Receives technical detail about load and save failures.</param>
        /// <param name="settingsFilePath">
        /// Where to persist. When <see langword="null"/> the default per-user location is used.
        /// </param>
        public JsonSettingsStore(ILogger logger, string? settingsFilePath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            SettingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
                ? Path.Combine(PluginPaths.UserDataDirectory, "settings.json")
                : settingsFilePath!;
        }

        /// <inheritdoc />
        public string SettingsFilePath { get; }

        /// <inheritdoc />
        public ProjectSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    _logger.Information($"No settings file at '{SettingsFilePath}'; using defaults.");
                    return new ProjectSettings();
                }

                string json = File.ReadAllText(SettingsFilePath);
                ProjectSettings? settings = JsonSerializer.Deserialize<ProjectSettings>(json, Options);
                if (settings is null)
                {
                    _logger.Warning($"Settings file '{SettingsFilePath}' was empty; using defaults.");
                    return new ProjectSettings();
                }

                return settings;
            }
            catch (JsonException exception)
            {
                _logger.Error($"Settings file '{SettingsFilePath}' is not valid JSON; using defaults.", exception);
                return new ProjectSettings();
            }
            catch (IOException exception)
            {
                _logger.Error($"Could not read settings file '{SettingsFilePath}'; using defaults.", exception);
                return new ProjectSettings();
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.Error($"Access denied reading settings file '{SettingsFilePath}'; using defaults.", exception);
                return new ProjectSettings();
            }
        }

        /// <inheritdoc />
        public Result Save(ProjectSettings settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            try
            {
                string? directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, Options));
                return Result.Success();
            }
            catch (IOException exception)
            {
                _logger.Error($"Could not write settings file '{SettingsFilePath}'.", exception);
                return Result.Failure(
                    "Settings.WriteFailed",
                    $"The settings could not be saved to '{SettingsFilePath}'. Check that the file is not open in another application.");
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.Error($"Access denied writing settings file '{SettingsFilePath}'.", exception);
                return Result.Failure(
                    "Settings.AccessDenied",
                    $"You do not have permission to write '{SettingsFilePath}'.");
            }
        }
    }
}
