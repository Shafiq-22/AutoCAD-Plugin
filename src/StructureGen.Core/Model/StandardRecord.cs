using System;

namespace StructureGen.Core.Model
{
    /// <summary>One publishing standard, such as EN, ASTM, AISC, BS, DIN, ISO, JIS or AS/NZS.</summary>
    public sealed class StandardRecord
    {
        /// <summary>Initialises a new instance of the <see cref="StandardRecord"/> class.</summary>
        /// <param name="id">Unique standard identifier used by section records.</param>
        /// <param name="name">Display name.</param>
        /// <param name="region">Optional issuing region or body.</param>
        /// <param name="revision">Optional revision or year.</param>
        /// <param name="description">Optional description.</param>
        public StandardRecord(string id, string name, string? region = null, string? revision = null, string? description = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Region = region;
            Revision = revision;
            Description = description;
        }

        /// <summary>Gets the unique standard identifier used by section records.</summary>
        public string Id { get; }

        /// <summary>Gets the display name.</summary>
        public string Name { get; }

        /// <summary>Gets the optional issuing region or body.</summary>
        public string? Region { get; }

        /// <summary>Gets the optional revision or year.</summary>
        public string? Revision { get; }

        /// <summary>Gets the optional description.</summary>
        public string? Description { get; }

        /// <inheritdoc />
        public override string ToString() => Revision is null ? Name : $"{Name} ({Revision})";
    }
}
