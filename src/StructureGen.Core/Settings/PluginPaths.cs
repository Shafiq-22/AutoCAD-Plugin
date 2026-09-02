using System;
using System.IO;
using System.Reflection;

namespace StructureGen.Core.Settings
{
    /// <summary>
    /// Resolves the folders the plugin reads from and writes to.
    /// </summary>
    /// <remarks>
    /// Everything is derived at run time from the location of the executing assembly and from the
    /// current user's profile. No absolute path and no user name is ever compiled in, which is
    /// what lets the same build be copied into any installation without editing source.
    /// </remarks>
    public static class PluginPaths
    {
        /// <summary>Gets the folder the StructureGen assemblies were loaded from.</summary>
        public static string AssemblyDirectory =>
            Path.GetDirectoryName(typeof(PluginPaths).Assembly.Location) is { Length: > 0 } directory
                ? directory
                : AppContext.BaseDirectory;

        /// <summary>
        /// Gets the folder holding the data files that ship with the plugin.
        /// </summary>
        /// <remarks>
        /// Inside an installed bundle the layout is
        /// <c>StructureGen.bundle/Contents/&lt;year&gt;/</c> for the assemblies and
        /// <c>StructureGen.bundle/Contents/Data/</c> for the shared data, so the sibling
        /// <c>Data</c> folder is preferred and the assembly folder is the fallback used when
        /// running from a development build.
        /// </remarks>
        public static string DataDirectory
        {
            get
            {
                string bundled = Path.GetFullPath(Path.Combine(AssemblyDirectory, "..", "Data"));
                if (Directory.Exists(bundled))
                {
                    return bundled;
                }

                string local = Path.Combine(AssemblyDirectory, "Data");
                return Directory.Exists(local) ? local : AssemblyDirectory;
            }
        }

        /// <summary>Gets the per-user folder StructureGen writes settings and logs into.</summary>
        public static string UserDataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StructureGen");

        /// <summary>Gets the version of the StructureGen assemblies.</summary>
        public static string PluginVersion =>
            typeof(PluginPaths).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(PluginPaths).Assembly.GetName().Version?.ToString()
            ?? "unknown";

        /// <summary>
        /// Turns a possibly relative configured path into an absolute one, resolving relative
        /// paths against the plugin's data folder.
        /// </summary>
        /// <param name="configuredPath">The path as configured by the user.</param>
        /// <returns>An absolute path.</returns>
        public static string ResolveDataPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                throw new ArgumentException("A path is required.", nameof(configuredPath));
            }

            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(Path.Combine(DataDirectory, configuredPath));
        }
    }
}
