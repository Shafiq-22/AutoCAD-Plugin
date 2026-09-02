using System;

namespace StructureGen.Data
{
    /// <summary>Identity and provenance of a loaded structural database.</summary>
    public sealed class DatabaseMetadata
    {
        /// <summary>Initialises a new instance of the <see cref="DatabaseMetadata"/> class.</summary>
        /// <param name="id">Stable identifier of the dataset.</param>
        /// <param name="name">Display name.</param>
        /// <param name="version">Dataset version, so a drawing can record which data it was built from.</param>
        /// <param name="schemaVersion">Version of the file format.</param>
        /// <param name="revisionDate">Optional revision date.</param>
        /// <param name="description">Optional description.</param>
        /// <param name="verified">Whether the publisher of the dataset asserts the values are checked.</param>
        /// <param name="notes">Free text notes, used by the shipped dataset to state that it is a demonstration.</param>
        /// <param name="sourcePath">The file the dataset was loaded from.</param>
        public DatabaseMetadata(
            string id,
            string name,
            string version,
            string schemaVersion,
            string? revisionDate = null,
            string? description = null,
            bool verified = false,
            string? notes = null,
            string? sourcePath = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            SchemaVersion = schemaVersion ?? throw new ArgumentNullException(nameof(schemaVersion));
            RevisionDate = revisionDate;
            Description = description;
            Verified = verified;
            Notes = notes;
            SourcePath = sourcePath;
        }

        /// <summary>Gets the stable identifier of the dataset.</summary>
        public string Id { get; }

        /// <summary>Gets the display name.</summary>
        public string Name { get; }

        /// <summary>Gets the dataset version.</summary>
        public string Version { get; }

        /// <summary>Gets the version of the file format.</summary>
        public string SchemaVersion { get; }

        /// <summary>Gets the optional revision date.</summary>
        public string? RevisionDate { get; }

        /// <summary>Gets the optional description.</summary>
        public string? Description { get; }

        /// <summary>Gets a value indicating whether the publisher asserts the values are checked.</summary>
        public bool Verified { get; }

        /// <summary>Gets free text notes.</summary>
        public string? Notes { get; }

        /// <summary>Gets the file the dataset was loaded from.</summary>
        public string? SourcePath { get; }

        /// <summary>Gets a short label combining the name and version, for stamping onto drawings.</summary>
        public string Stamp => $"{Name} v{Version}";

        /// <inheritdoc />
        public override string ToString() => Stamp;
    }
}
