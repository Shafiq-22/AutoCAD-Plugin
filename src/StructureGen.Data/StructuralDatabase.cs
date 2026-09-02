using System;
using System.Collections.Generic;
using System.Linq;
using StructureGen.Core.Model;

namespace StructureGen.Data
{
    /// <summary>
    /// An immutable, indexed view of one loaded structural database.
    /// </summary>
    /// <remarks>
    /// Loading is comparatively expensive and happens rarely; lookups happen on every keystroke in
    /// the palette. The indexes are therefore built once here, and the whole object is replaced
    /// wholesale when the user reloads the database, so no consumer can ever observe a half-updated
    /// dataset.
    /// </remarks>
    public sealed class StructuralDatabase
    {
        private readonly Dictionary<string, StandardRecord> _standardsById;
        private readonly Dictionary<string, MaterialRecord> _materialsById;
        private readonly Dictionary<string, SectionRecord> _sectionsById;
        private readonly Dictionary<string, List<SectionRecord>> _sectionsByCategory;

        /// <summary>Initialises a new instance of the <see cref="StructuralDatabase"/> class.</summary>
        /// <param name="metadata">Identity and provenance of the dataset.</param>
        /// <param name="standards">The standards.</param>
        /// <param name="materials">The materials.</param>
        /// <param name="sections">The section records.</param>
        public StructuralDatabase(
            DatabaseMetadata metadata,
            IEnumerable<StandardRecord> standards,
            IEnumerable<MaterialRecord> materials,
            IEnumerable<SectionRecord> sections)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Standards = (standards ?? throw new ArgumentNullException(nameof(standards))).ToList();
            Materials = (materials ?? throw new ArgumentNullException(nameof(materials))).ToList();
            Sections = (sections ?? throw new ArgumentNullException(nameof(sections))).ToList();

            _standardsById = Standards
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            _materialsById = Materials
                .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            _sectionsById = Sections
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            _sectionsByCategory = Sections
                .GroupBy(s => s.CategoryId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets an empty database, used before the first successful load.</summary>
        public static StructuralDatabase Empty { get; } = new(
            new DatabaseMetadata("none", "No database loaded", "0", DatabaseSchema.CurrentVersion),
            Array.Empty<StandardRecord>(),
            Array.Empty<MaterialRecord>(),
            Array.Empty<SectionRecord>());

        /// <summary>Gets the identity and provenance of the dataset.</summary>
        public DatabaseMetadata Metadata { get; }

        /// <summary>Gets every standard in the dataset.</summary>
        public IReadOnlyList<StandardRecord> Standards { get; }

        /// <summary>Gets every material in the dataset.</summary>
        public IReadOnlyList<MaterialRecord> Materials { get; }

        /// <summary>Gets every section record in the dataset.</summary>
        public IReadOnlyList<SectionRecord> Sections { get; }

        /// <summary>Gets a value indicating whether the dataset holds no sections.</summary>
        public bool IsEmpty => Sections.Count == 0;

        /// <summary>Gets the category identifiers that actually occur in the dataset, in display order.</summary>
        public IReadOnlyList<string> Categories => _sectionsByCategory.Keys
            .OrderBy(CategoryOrder)
            .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        /// <summary>Finds a standard by identifier.</summary>
        /// <param name="id">The identifier to look up.</param>
        /// <returns>The standard, or <see langword="null"/> when it is absent.</returns>
        public StandardRecord? FindStandard(string? id) =>
            id is not null && _standardsById.TryGetValue(id, out StandardRecord? record) ? record : null;

        /// <summary>Finds a material by identifier.</summary>
        /// <param name="id">The identifier to look up.</param>
        /// <returns>The material, or <see langword="null"/> when it is absent.</returns>
        public MaterialRecord? FindMaterial(string? id) =>
            id is not null && _materialsById.TryGetValue(id, out MaterialRecord? record) ? record : null;

        /// <summary>Finds a section record by identifier.</summary>
        /// <param name="id">The identifier to look up.</param>
        /// <returns>The record, or <see langword="null"/> when it is absent.</returns>
        public SectionRecord? FindSection(string? id) =>
            id is not null && _sectionsById.TryGetValue(id, out SectionRecord? record) ? record : null;

        /// <summary>Gets every section in a category.</summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>The matching sections, ordered by designation.</returns>
        public IReadOnlyList<SectionRecord> SectionsInCategory(string categoryId) =>
            _sectionsByCategory.TryGetValue(categoryId, out List<SectionRecord>? list)
                ? list.OrderBy(s => s.Designation, StringComparer.OrdinalIgnoreCase).ToList()
                : Array.Empty<SectionRecord>();

        /// <summary>Gets the standards that publish at least one section in a category.</summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>The matching standards, ordered by name.</returns>
        public IReadOnlyList<StandardRecord> StandardsInCategory(string categoryId) =>
            SectionsInCategory(categoryId)
                .Select(s => s.StandardId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(FindStandard)
                .Where(s => s is not null)
                .Select(s => s!)
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Gets the sections in a category that belong to a particular standard.</summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <param name="standardId">The standard identifier.</param>
        /// <returns>The matching sections, ordered by designation.</returns>
        public IReadOnlyList<SectionRecord> SectionsInCategory(string categoryId, string standardId) =>
            SectionsInCategory(categoryId)
                .Where(s => string.Equals(s.StandardId, standardId, StringComparison.OrdinalIgnoreCase))
                .ToList();

        /// <summary>
        /// Resolves the material a section record refers to, matching first by identifier and then
        /// by name so that a dataset may name its materials informally.
        /// </summary>
        /// <param name="record">The section record.</param>
        /// <returns>The material, or <see langword="null"/> when none matches.</returns>
        public MaterialRecord? ResolveMaterial(SectionRecord record)
        {
            if (record is null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (record.Material is null)
            {
                return null;
            }

            MaterialRecord? byId = FindMaterial(record.Material);
            if (byId is not null)
            {
                return byId;
            }

            return Materials.FirstOrDefault(m =>
                string.Equals(m.Name, record.Material, StringComparison.OrdinalIgnoreCase) &&
                (record.Grade is null || string.Equals(m.Grade, record.Grade, StringComparison.OrdinalIgnoreCase)))
                ?? Materials.FirstOrDefault(m => string.Equals(m.Name, record.Material, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Gets a value indicating whether every section in the dataset is marked verified.</summary>
        public bool AllRecordsVerified => Sections.Count > 0 && Sections.All(s => s.Verified);

        /// <summary>Gets the number of sections not marked verified.</summary>
        public int UnverifiedRecordCount => Sections.Count(s => !s.Verified);

        private static int CategoryOrder(string categoryId)
        {
            for (int index = 0; index < CategoryCatalog.All.Count; index++)
            {
                if (string.Equals(CategoryCatalog.All[index].Id, categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return int.MaxValue;
        }
    }
}
