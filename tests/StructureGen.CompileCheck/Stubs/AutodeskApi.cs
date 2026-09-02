// A stand-in for the slice of the AutoCAD Managed API that StructureGen touches, so the AutoCAD
// layer can be type-checked on a machine with no AutoCAD and no Windows.
//
// What this proves: every call the AutoCAD layer makes into StructureGen's own code is correct -
// right names, right signatures, right types - across roughly fifteen hundred lines that otherwise
// could not be compiled here at all.
//
// What this does NOT prove: that the AutoCAD API looks like this. These stubs were written from the
// same understanding as the code they check, so they cannot catch a mistaken assumption about
// Autodesk's own surface. That is what the Windows CI job compiling against the real assemblies is
// for.
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Autodesk.AutoCAD.Runtime
{
    public interface IExtensionApplication { void Initialize(); void Terminate(); }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CommandMethodAttribute : Attribute
    {
        public CommandMethodAttribute(string name) { }
        public CommandMethodAttribute(string name, CommandFlags flags) { }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class ExtensionApplicationAttribute : Attribute { public ExtensionApplicationAttribute(Type t) { } }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class CommandClassAttribute : Attribute { public CommandClassAttribute(Type t) { } }

    [Flags]
    public enum CommandFlags { Modal = 0, UsePickSet = 2, Transparent = 4 }

    public class Exception : System.Exception
    {
        public Exception() { }
        public Exception(string message) : base(message) { }
    }
}

namespace Autodesk.AutoCAD.Geometry
{
    public readonly struct Point2d { public Point2d(double x, double y) { X = x; Y = y; } public double X { get; } public double Y { get; } }

    public readonly struct Point3d
    {
        public Point3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; } public double Y { get; } public double Z { get; }
        public static Point3d Origin => new(0, 0, 0);
    }

    public readonly struct Vector3d
    {
        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; } public double Y { get; } public double Z { get; }
        public static Vector3d XAxis => new(1, 0, 0);
        public static Vector3d YAxis => new(0, 1, 0);
        public static Vector3d ZAxis => new(0, 0, 1);
        public Vector3d GetNormal() => this;
        public Vector3d CrossProduct(Vector3d other) => this;
        public Vector3d TransformBy(Matrix3d m) => this;
    }

    public sealed class Matrix3d
    {
        public static Matrix3d Displacement(Vector3d v) => new();
        public static Matrix3d Rotation(double angle, Vector3d axis, Point3d centre) => new();
        public static Matrix3d AlignCoordinateSystem(
            Point3d fo, Vector3d fx, Vector3d fy, Vector3d fz,
            Point3d to, Vector3d tx, Vector3d ty, Vector3d tz) => new();
    }
}

namespace Autodesk.AutoCAD.Colors
{
    public enum ColorMethod { ByAci }
    public sealed class Color { public static Color FromColorIndex(ColorMethod m, short index) => new(); }
}

namespace Autodesk.AutoCAD.DatabaseServices
{
    using Autodesk.AutoCAD.Geometry;

    public enum OpenMode { ForRead, ForWrite }
    public enum BooleanOperationType { BoolUnite, BoolSubtract, BoolIntersect }
    public enum BlockScaling { Any, Uniform }
    public enum UnitsValue { Undefined, Inches, Feet, Millimeters, Centimeters, Meters, Decimeters, Kilometers }
    public enum CellAlignment { TopLeft, MiddleLeft, MiddleCenter, MiddleRight, BottomRight }

    public enum DxfCode
    {
        Text = 1, ExtendedDataAsciiString = 1000, ExtendedDataRegAppName = 1001
    }

    public readonly struct Handle
    {
        public Handle(long value) { Value = value; }
        public long Value { get; }
        public override string ToString() => Value.ToString();
    }

    public readonly struct ObjectId
    {
        public static ObjectId Null => default;
        public bool IsNull => true;
        public bool IsErased => false;
    }

    public sealed class ObjectIdCollection : IDisposable
    {
        public int Count => 0;
        public void Dispose() { }
    }

    public readonly struct TypedValue
    {
        public TypedValue(int code, object value) { TypeCode = (short)code; Value = value; }
        public short TypeCode { get; }
        public object Value { get; }
    }

    public sealed class ResultBuffer : IDisposable, IEnumerable<TypedValue>
    {
        public ResultBuffer() { }
        public ResultBuffer(params TypedValue[] values) { }
        public void Add(TypedValue value) { }
        public IEnumerator<TypedValue> GetEnumerator() => new List<TypedValue>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose() { }
    }

    public class DBObject : IDisposable
    {
        public ObjectId ExtensionDictionary => default;
        public ObjectId ObjectId => default;
        public Handle Handle => default;
        public Database? Database => null;
        public void CreateExtensionDictionary() { }
        public void UpgradeOpen() { }
        public void Erase() { }
        public void Dispose() { }
    }

    public class Entity : DBObject
    {
        public string Layer { get; set; } = "0";
        public ObjectId BlockId => default;
        public ResultBuffer? XData { get; set; }
        public void TransformBy(Matrix3d m) { }
        public void SetDatabaseDefaults(Database? db) { }
    }

    public sealed class DBObjectCollection : IDisposable, IEnumerable
    {
        public int Count => 1;
        public object this[int index] => new Region();
        public int Add(DBObject o) => 0;
        public IEnumerator GetEnumerator() => new List<object>().GetEnumerator();
        public void Dispose() { }
    }

    public sealed class Polyline : Entity
    {
        public Polyline() { }
        public Polyline(int vertices) { }
        public bool Closed { get; set; }
        public void AddVertexAt(int index, Point2d point, double bulge, double startWidth, double endWidth) { }
    }

    public sealed class Region : Entity
    {
        public Vector3d Normal => Vector3d.ZAxis;
        public static DBObjectCollection CreateFromCurves(DBObjectCollection curves) => new();
        public void BooleanOperation(BooleanOperationType type, Region other) { }
    }

    public sealed class Solid3d : Entity
    {
        public bool RecordHistory { get; set; }
        public void Extrude(Region region, double height, double taperAngle) { }
    }

    public sealed class BlockReference : Entity
    {
        public BlockReference(Point3d position, ObjectId blockTableRecord) { }
        public ObjectId BlockTableRecord { get; set; }
    }

    public sealed class BlockTableRecord : DBObject, IEnumerable<ObjectId>
    {
        public string Name { get; set; } = string.Empty;
        public Point3d Origin { get; set; }
        public bool Explodable { get; set; }
        public BlockScaling BlockScaling { get; set; }
        public ObjectId AppendEntity(Entity e) => default;
        public ObjectIdCollection GetBlockReferenceIds(bool directOnly, bool forceValidity) => new();
        public IEnumerator<ObjectId> GetEnumerator() => new List<ObjectId>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class BlockTable : DBObject
    {
        public bool Has(string name) => false;
        public ObjectId this[string name] => default;
        public ObjectId Add(BlockTableRecord record) => default;
    }

    public sealed class LayerTableRecord : DBObject
    {
        public string Name { get; set; } = string.Empty;
        public Colors.Color Color { get; set; } = Colors.Color.FromColorIndex(Colors.ColorMethod.ByAci, 7);
    }

    public sealed class LayerTable : DBObject
    {
        public bool Has(string name) => false;
        public ObjectId Add(LayerTableRecord record) => default;
    }

    public sealed class RegAppTableRecord : DBObject { public string Name { get; set; } = string.Empty; }

    public sealed class RegAppTable : DBObject
    {
        public bool Has(string name) => false;
        public ObjectId Add(RegAppTableRecord record) => default;
    }

    public sealed class DBDictionary : DBObject
    {
        public bool Contains(string key) => false;
        public ObjectId GetAt(string key) => default;
        public void SetAt(string key, DBObject value) { }
        public void Remove(string key) { }
    }

    public sealed class Xrecord : DBObject
    {
        public ResultBuffer? Data { get; set; }
        public bool XlateReferences { get; set; }
    }

    public sealed class SelectionFilter { public SelectionFilter(TypedValue[] values) { } }

    public sealed class ViewTableRecord : DBObject
    {
        public Point2d CenterPoint { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public sealed class Layout : DBObject
    {
        public bool ModelType => false;
        public ObjectId BlockTableRecordId => default;
    }

    public sealed class Cell
    {
        public string TextString { get; set; } = string.Empty;
        public double TextHeight { get; set; }
        public CellAlignment Alignment { get; set; }
    }

    public sealed class Column { public double Width { get; set; } }

    public sealed class Row { public double Height { get; set; } }

    public sealed class CellRange
    {
        public static CellRange Create(Table table, int topRow, int leftColumn, int bottomRow, int rightColumn) => new();
    }

    public sealed class CellCollection { public Cell this[int row, int column] => new(); }

    public sealed class ColumnCollection { public Column this[int index] => new(); }

    public sealed class RowCollection { public Row this[int index] => new(); }

    public sealed class Table : Entity
    {
        public ObjectId TableStyle { get; set; }
        public Point3d Position { get; set; }
        public CellCollection Cells => new();
        public ColumnCollection Columns => new();
        public RowCollection Rows => new();
        public void SetSize(int rows, int columns) { }
        public void MergeCells(CellRange range) { }
        public void GenerateLayout() { }
    }

    public sealed class Transaction : IDisposable
    {
        public DBObject GetObject(ObjectId id, OpenMode mode) => new();
        public void AddNewlyCreatedDBObject(DBObject o, bool add) { }
        public void Commit() { }
        public void Dispose() { }
    }

    public sealed class TransactionManager { public Transaction StartTransaction() => new(); }

    public sealed class Database
    {
        public UnitsValue Insunits => UnitsValue.Millimeters;
        public ObjectId BlockTableId => default;
        public ObjectId LayerTableId => default;
        public ObjectId RegAppTableId => default;
        public ObjectId NamedObjectsDictionaryId => default;
        public ObjectId LayoutDictionaryId => default;
        public ObjectId CurrentSpaceId => default;
        public ObjectId Clayer => default;
        public ObjectId Tablestyle => default;
        public Point3d Extmin => default;
        public Point3d Extmax => default;
        public TransactionManager TransactionManager => new();
        public bool TryGetObjectId(Handle handle, out ObjectId id) { id = default; return false; }
        public void UpdateExt(bool force) { }
    }

    public static class SymbolUtilityServices
    {
        public static ObjectId GetBlockModelSpaceId(Database db) => default;
    }

    public sealed class LayoutManager
    {
        public static LayoutManager Current => new();
        public string CurrentLayout => "Model";
    }
}

namespace Autodesk.AutoCAD.EditorInput
{
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.Geometry;

    public enum PromptStatus { OK, Cancel, Error, None, Keyword }

    public class PromptResult { public PromptStatus Status => PromptStatus.OK; public string StringResult => string.Empty; }
    public sealed class PromptPointResult : PromptResult { public Point3d Value => default; }
    public sealed class PromptEntityResult : PromptResult { public ObjectId ObjectId => default; }
    public sealed class PromptFileNameResult : PromptResult { }
    public sealed class PromptSelectionResult { public PromptStatus Status => PromptStatus.OK; public SelectionSet? Value => null; }

    public sealed class SelectedObject { public ObjectId ObjectId => default; }

    public sealed class SelectionSet : IEnumerable<SelectedObject>
    {
        public int Count => 0;
        public IEnumerator<SelectedObject> GetEnumerator() => new List<SelectedObject>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class PromptOptions { public PromptOptions(string message) { } }

    public sealed class PromptPointOptions : PromptOptions
    {
        public PromptPointOptions(string message) : base(message) { }
        public bool AllowNone { get; set; }
        public bool UseBasePoint { get; set; }
        public Point3d BasePoint { get; set; }
    }

    public sealed class PromptEntityOptions : PromptOptions
    {
        public PromptEntityOptions(string message) : base(message) { }
        public void SetRejectMessage(string message) { }
        public void AddAllowedClass(Type type, bool exactMatch) { }
    }

    public sealed class KeywordCollection { public void Add(string keyword) { } public string Default { get; set; } = string.Empty; }

    public sealed class PromptKeywordOptions : PromptOptions
    {
        public PromptKeywordOptions(string message) : base(message) { }
        public KeywordCollection Keywords { get; } = new();
        public bool AllowNone { get; set; }
    }

    public sealed class PromptSaveFileOptions
    {
        public PromptSaveFileOptions(string message) { }
        public string Filter { get; set; } = string.Empty;
        public string InitialFileName { get; set; } = string.Empty;
        public string InitialDirectory { get; set; } = string.Empty;
        public string DialogName { get; set; } = string.Empty;
    }

    public sealed class Editor
    {
        public PromptPointResult GetPoint(PromptPointOptions options) => new();
        public PromptEntityResult GetEntity(PromptEntityOptions options) => new();
        public PromptResult GetKeywords(PromptKeywordOptions options) => new();
        public PromptFileNameResult GetFileNameForSave(PromptSaveFileOptions options) => new();
        public PromptSelectionResult GetSelection(SelectionFilter filter) => new();
        public PromptSelectionResult SelectAll(SelectionFilter filter) => new();
        public void WriteMessage(string message) { }
        public ViewTableRecord GetCurrentView() => new();
        public void SetCurrentView(ViewTableRecord view) { }
    }
}

namespace Autodesk.AutoCAD.ApplicationServices
{
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;

    public sealed class DocumentLock : IDisposable { public void Dispose() { } }

    public sealed class Document
    {
        public string Name => string.Empty;
        public Database Database => new();
        public Editor Editor => new();
        public DocumentLock LockDocument() => new();
    }

    public sealed class DocumentCollection
    {
        public Document? MdiActiveDocument => null;
        public Task ExecuteInCommandContextAsync(Func<object, Task> handler, object? parameter) => Task.CompletedTask;
    }

    public static class Application
    {
        public static DocumentCollection DocumentManager { get; } = new();
        public static void ShowAlertDialog(string message) { }
        public static object GetSystemVariable(string name) => string.Empty;
    }
}

namespace Autodesk.AutoCAD.Windows
{
    [Flags]
    public enum PaletteSetStyles { ShowPropertiesMenu = 1, ShowAutoHideButton = 2, ShowCloseButton = 4 }

    public sealed class Palette { }

    public sealed class PaletteSet
    {
        public PaletteSet(string name, Guid id) { }
        public PaletteSetStyles Style { get; set; }
        public System.Drawing.Size MinimumSize { get; set; }
        public bool KeepFocus { get; set; }
        public bool Visible { get; set; }
        public Palette AddVisual(string name, object visual) => new();
    }
}
