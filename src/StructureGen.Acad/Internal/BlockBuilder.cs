using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StructureGen.Core.Geometry;
using StructureGen.Core.Model;
using StructureGen.Services;

namespace StructureGen.Acad.Internal
{
    /// <summary>Creates, reuses and rebuilds the block definitions that hold generated geometry.</summary>
    internal sealed class BlockBuilder
    {
        private readonly double _scale;

        /// <summary>Initialises a new instance of the <see cref="BlockBuilder"/> class.</summary>
        /// <param name="drawingUnitsPerMillimetre">The scale from internal millimetres to drawing units.</param>
        public BlockBuilder(double drawingUnitsPerMillimetre) => _scale = drawingUnitsPerMillimetre;

        /// <summary>Gets how many definitions were created during this builder's lifetime.</summary>
        public int Created { get; private set; }

        /// <summary>Gets how many existing definitions were reused during this builder's lifetime.</summary>
        public int Reused { get; private set; }

        /// <summary>
        /// Finds the block definition for an element's geometry, creating it only if the drawing does
        /// not already hold it.
        /// </summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="database">The drawing database.</param>
        /// <param name="definition">The element.</param>
        /// <param name="geometry">Its generated geometry.</param>
        /// <param name="representation">Which representation the definition should hold.</param>
        /// <param name="databaseStamp">The section database the element came from.</param>
        /// <param name="pluginVersion">The plugin version.</param>
        /// <returns>The block definition's object identifier.</returns>
        public ObjectId ResolveDefinition(
            Transaction transaction,
            Database database,
            ElementDefinition definition,
            ElementGeometry geometry,
            RepresentationMode representation,
            string? databaseStamp,
            string? pluginVersion)
        {
            string name = BlockNaming.BlockNameFor(definition, representation);
            BlockTable table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);

            if (table.Has(name))
            {
                Reused++;
                return table[name];
            }

            table.UpgradeOpen();

            BlockTableRecord record = new()
            {
                Name = name,
                Origin = Point3d.Origin,
                Explodable = true,
                BlockScaling = BlockScaling.Any
            };

            ObjectId recordId = table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);

            PopulateDefinition(transaction, record, geometry, representation);

            MetadataStore.Write(
                transaction,
                database,
                record,
                ElementMetadataCodec.DefinitionKey,
                ElementMetadataCodec.Encode(definition, databaseStamp, pluginVersion),
                writeSelectionMarker: false);

            Created++;
            return recordId;
        }

        /// <summary>
        /// Rebuilds the contents of an existing block definition in place.
        /// </summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="database">The drawing database.</param>
        /// <param name="recordId">The definition to rebuild.</param>
        /// <param name="definition">The element the new geometry comes from.</param>
        /// <param name="geometry">The new geometry.</param>
        /// <param name="representation">Which representation the definition holds.</param>
        /// <param name="databaseStamp">The section database the element came from.</param>
        /// <param name="pluginVersion">The plugin version.</param>
        /// <remarks>
        /// Every reference to the definition updates at once, which is what makes editing a shared
        /// profile cheap. The caller is responsible for having established that the definition is not
        /// shared with elements that should not change.
        /// </remarks>
        public void RebuildDefinition(
            Transaction transaction,
            Database database,
            ObjectId recordId,
            ElementDefinition definition,
            ElementGeometry geometry,
            RepresentationMode representation,
            string? databaseStamp,
            string? pluginVersion)
        {
            BlockTableRecord record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForWrite);

            foreach (ObjectId existing in record)
            {
                DBObject entity = transaction.GetObject(existing, OpenMode.ForWrite);
                entity.Erase();
            }

            PopulateDefinition(transaction, record, geometry, representation);

            MetadataStore.Write(
                transaction,
                database,
                record,
                ElementMetadataCodec.DefinitionKey,
                ElementMetadataCodec.Encode(definition, databaseStamp, pluginVersion),
                writeSelectionMarker: false);
        }

        /// <summary>Counts how many references a block definition has in the drawing.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="recordId">The definition to inspect.</param>
        /// <returns>The number of references.</returns>
        public static int ReferenceCount(Transaction transaction, ObjectId recordId)
        {
            BlockTableRecord record = (BlockTableRecord)transaction.GetObject(recordId, OpenMode.ForRead);

            int count = 0;
            using (ObjectIdCollection references = record.GetBlockReferenceIds(true, true))
            {
                count = references.Count;
            }

            return count;
        }

        /// <summary>Places a reference to a block definition.</summary>
        /// <param name="transaction">The active transaction.</param>
        /// <param name="space">The space to place it in, open for write.</param>
        /// <param name="recordId">The definition to reference.</param>
        /// <param name="placement">Where and how it sits.</param>
        /// <param name="layerName">The layer to place it on.</param>
        /// <returns>The new reference, owned by the transaction.</returns>
        public BlockReference PlaceReference(
            Transaction transaction,
            BlockTableRecord space,
            ObjectId recordId,
            Placement placement,
            string layerName)
        {
            BlockReference reference = new(Point3d.Origin, recordId);

            try
            {
                space.AppendEntity(reference);
                transaction.AddNewlyCreatedDBObject(reference, true);

                // The layer is assigned only once the reference belongs to a database, because the
                // property resolves against that database's layer table.
                reference.Layer = layerName;

                // The reference is created at the origin and then transformed, rather than being
                // given a position and a rotation. A block reference's own rotation turns about its
                // normal only, which cannot express a member running along an arbitrary vector; a
                // full transform can.
                reference.TransformBy(EntityFactory.PlacementTransform(placement, _scale));

                return reference;
            }
            catch
            {
                reference.Dispose();
                throw;
            }
        }

        private void PopulateDefinition(
            Transaction transaction,
            BlockTableRecord record,
            ElementGeometry geometry,
            RepresentationMode representation)
        {
            if (representation.HasFlag(RepresentationMode.Solid3D))
            {
                foreach (GeometryPart part in geometry.Parts)
                {
                    Solid3d solid = EntityFactory.CreateExtrusion(part.Profile, part.StartOffset, part.Length, _scale);

                    try
                    {
                        solid.SetDatabaseDefaults(record.Database);
                        record.AppendEntity(solid);
                        transaction.AddNewlyCreatedDBObject(solid, true);
                    }
                    catch
                    {
                        solid.Dispose();
                        throw;
                    }
                }
            }

            if (representation.HasFlag(RepresentationMode.Profile2D))
            {
                List<Polyline> polylines = EntityFactory.CreateProfilePolylines(geometry.PrimaryProfile, _scale);

                foreach (Polyline polyline in polylines)
                {
                    try
                    {
                        polyline.SetDatabaseDefaults(record.Database);
                        record.AppendEntity(polyline);
                        transaction.AddNewlyCreatedDBObject(polyline, true);
                    }
                    catch
                    {
                        polyline.Dispose();
                        throw;
                    }
                }
            }
        }
    }
}
