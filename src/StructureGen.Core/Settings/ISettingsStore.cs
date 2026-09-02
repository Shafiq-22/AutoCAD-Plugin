using StructureGen.Core.Results;

namespace StructureGen.Core.Settings
{
    /// <summary>Loads and saves <see cref="ProjectSettings"/>.</summary>
    public interface ISettingsStore
    {
        /// <summary>Gets the full path of the file settings are persisted to.</summary>
        string SettingsFilePath { get; }

        /// <summary>
        /// Loads the settings, falling back to defaults when the file is absent or unreadable.
        /// </summary>
        /// <returns>The settings. Never <see langword="null"/>.</returns>
        ProjectSettings Load();

        /// <summary>Saves the settings.</summary>
        /// <param name="settings">The settings to persist.</param>
        /// <returns>The outcome, carrying a user-facing message on failure.</returns>
        Result Save(ProjectSettings settings);
    }
}
