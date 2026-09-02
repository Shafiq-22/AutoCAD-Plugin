using System;

namespace StructureGen.Excel.Xlsx
{
    /// <summary>The kind of value a cell holds.</summary>
    internal enum XlsxCellKind
    {
        /// <summary>The cell is empty but may still carry a format.</summary>
        Blank = 0,

        /// <summary>The cell holds text, written through the shared string table.</summary>
        Text = 1,

        /// <summary>The cell holds a number.</summary>
        Number = 2,

        /// <summary>The cell holds a boolean.</summary>
        Boolean = 3
    }

    /// <summary>One cell of a generated worksheet.</summary>
    internal readonly struct XlsxCell
    {
        private XlsxCell(XlsxCellKind kind, string? text, double number, XlsxStyle style)
        {
            Kind = kind;
            Text = text;
            Number = number;
            Style = style;
        }

        public XlsxCellKind Kind { get; }

        public string? Text { get; }

        public double Number { get; }

        public XlsxStyle Style { get; }

        public static XlsxCell Blank(XlsxStyle style) => new(XlsxCellKind.Blank, null, 0d, style);

        public static XlsxCell FromText(string text, XlsxStyle style) => new(XlsxCellKind.Text, text, 0d, style);

        public static XlsxCell FromNumber(double value, XlsxStyle style) => new(XlsxCellKind.Number, null, value, style);

        public static XlsxCell FromBoolean(bool value, XlsxStyle style) =>
            new(XlsxCellKind.Boolean, null, value ? 1d : 0d, style);

        public static XlsxCell FromDate(DateTime value, XlsxStyle style) =>
            new(XlsxCellKind.Number, null, value.ToOADate(), style);
    }
}
