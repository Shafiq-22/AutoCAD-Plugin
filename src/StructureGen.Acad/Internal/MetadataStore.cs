using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using StructureGen.Core.Model;
using StructureGen.Core.Results;
using StructureGen.Services;

namespace StructureGen.Acad.Internal
{
    /// <summary>
    /// Reads and writes the StructureGen parameters carried by drawing objects.
    /// </summary>
    /// <remarks>
    /// Two mechanisms are used together, each for what it is good at.
    ///
    /// An extension dictionary holding an <see cref="Xrecord"/> carries the full parameter payload.
    /// It has no practical size limit, it travels with the object through copy, block insertion and
    /// WBLOCK, and it is the Autodesk-sanctioned place for an application's own structured data.
    ///
    /// Extended entity data carries nothing but a marker naming the application. It exists purely
    /// because a selection filter can match on it, which is what lets the bill of materials find
    /// every StructureGen object in a large drawing in one pass instead of opening every entity in
    /// model space and inspecting its dictionary.
    ///
    /// Neither mechanism is a custom entity. Creating a genuine custom entity, with its own graphics
    /// and grip behaviour, requires ObjectARX in C++; the Managed .NET API cannot do it, and neither
    /// can it author the parameters and actions of a dynamic block. This design is the strongest
    /// parametric behaviour the supported managed API actually offers: identity and full parameters
    /// travel with the object, and the geometry is rebuilt on demand from them.
    /// </remarks>
    internal static class MetadataStore
    {
        /// <summary>Writes the element's parameters onto an object.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="database">The drawing database.</param>
        /// <param name="target">The object to annotate. It must be open for write.</param>
        /// <param name="key">The dictionary key to store under.</param>
        /// <param name="payload">The payload produced by <see cref="ElementMetadataCodec"/>.</param>
        /// <param name="writeSelectionMarker">
        /// Whether to also write the extended entity data marker. Set for the objects that should be
        /// findable by a selection filter, which means the block references rather than the
        /// definitions.
        /// </param>
        public static void Write(
            Transaction transaction,
            Database database,
            DBObject target,
            string key,
            string payload,
            bool writeSelectionMarker)
        {
            if (transaction is null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.ExtensionDictionary.IsNull)
            {
                target.CreateExtensionDictionary();
            }

            DBDictionary dictionary = (DBDictionary)transaction.GetObject(target.ExtensionDictionary, OpenMode.ForWrite);

            using ResultBuffer buffer = new();
            foreach (string chunk in ElementMetadataCodec.Chunk(payload))
            {
                buffer.Add(new TypedValue((int)DxfCode.Text, chunk));
            }

            Xrecord record = new() { Data = buffer, XlateReferences = false };

            if (dictionary.Contains(key))
            {
                dictionary.Remove(key);
            }

            dictionary.SetAt(key, record);
            transaction.AddNewlyCreatedDBObject(record, true);

            if (writeSelectionMarker && target is Entity entity)
            {
                WriteSelectionMarker(transaction, database, entity);
            }
        }

        /// <summary>Reads a payload back off an object.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="source">The object to read from.</param>
        /// <param name="key">The dictionary key to read.</param>
        /// <returns>The payload, or <see langword="null"/> when the object carries none.</returns>
        public static string? Read(Transaction transaction, DBObject source, string key)
        {
            if (transaction is null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            if (source is null || source.ExtensionDictionary.IsNull)
            {
                return null;
            }

            DBDictionary dictionary = (DBDictionary)transaction.GetObject(source.ExtensionDictionary, OpenMode.ForRead);

            if (!dictionary.Contains(key))
            {
                return null;
            }

            if (transaction.GetObject(dictionary.GetAt(key), OpenMode.ForRead) is not Xrecord record)
            {
                return null;
            }

            using ResultBuffer? buffer = record.Data;
            if (buffer is null)
            {
                return null;
            }

            List<string> chunks = new();
            foreach (TypedValue value in buffer)
            {
                if (value.TypeCode == (short)DxfCode.Text && value.Value is string text)
                {
                    chunks.Add(text);
                }
            }

            return chunks.Count == 0 ? null : ElementMetadataCodec.Unchunk(chunks);
        }

        /// <summary>Reads and decodes the element an object represents.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="source">The object to read from.</param>
        /// <returns>The element, or a failure explaining why it could not be recovered.</returns>
        public static Result<DecodedElement> ReadElement(Transaction transaction, DBObject source) =>
            ElementMetadataCodec.Decode(Read(transaction, source, ElementMetadataCodec.InstanceKey));

        /// <summary>Determines whether an object carries StructureGen parameters.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="source">The object to test.</param>
        /// <returns><see langword="true"/> when the object was generated by StructureGen.</returns>
        public static bool IsStructureGenObject(Transaction transaction, DBObject source) =>
            Read(transaction, source, ElementMetadataCodec.InstanceKey) is not null;

        /// <summary>Records drawing-wide settings in the named object dictionary.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="database">The drawing database.</param>
        /// <param name="databaseStamp">The section database the drawing was built against.</param>
        /// <param name="pluginVersion">The plugin version.</param>
        /// <remarks>
        /// Stamping the drawing means that opening it in a year's time answers the first question a
        /// checker asks, which is which revision of the section data the weights on it came from.
        /// </remarks>
        public static void StampDrawing(
            Transaction transaction, Database database, string? databaseStamp, string? pluginVersion)
        {
            DBDictionary namedObjects =
                (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForWrite);

            using ResultBuffer buffer = new();
            buffer.Add(new TypedValue((int)DxfCode.Text, databaseStamp ?? string.Empty));
            buffer.Add(new TypedValue((int)DxfCode.Text, pluginVersion ?? string.Empty));
            buffer.Add(new TypedValue((int)DxfCode.Text, DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)));

            Xrecord record = new() { Data = buffer, XlateReferences = false };

            if (namedObjects.Contains(ElementMetadataCodec.ProjectKey))
            {
                namedObjects.Remove(ElementMetadataCodec.ProjectKey);
            }

            namedObjects.SetAt(ElementMetadataCodec.ProjectKey, record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        /// <summary>Builds the selection filter that finds every StructureGen object.</summary>
        /// <returns>The filter.</returns>
        public static SelectionFilter CreateSelectionFilter() => new(new[]
        {
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, ElementMetadataCodec.RegisteredApplicationName)
        });

        /// <summary>Ensures the registered application name exists in the drawing.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="database">The drawing database.</param>
        /// <remarks>
        /// Extended entity data cannot be attached under an application name the drawing has never
        /// heard of, so the name is registered once per drawing before the first write.
        /// </remarks>
        public static void EnsureRegisteredApplication(Transaction transaction, Database database)
        {
            RegAppTable table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);

            if (table.Has(ElementMetadataCodec.RegisteredApplicationName))
            {
                return;
            }

            table.UpgradeOpen();

            RegAppTableRecord record = new() { Name = ElementMetadataCodec.RegisteredApplicationName };
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        private static void WriteSelectionMarker(Transaction transaction, Database database, Entity entity)
        {
            EnsureRegisteredApplication(transaction, database);

            // The marker holds only the application name and a short kind tag. The real payload is
            // in the extension dictionary; duplicating it here would risk the 16 KB limit that
            // extended entity data imposes on an object.
            entity.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, ElementMetadataCodec.RegisteredApplicationName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, "element"));
        }
    }
}
