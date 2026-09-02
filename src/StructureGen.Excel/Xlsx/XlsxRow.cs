using System;

namespace StructureGen.Excel.Xlsx
{
    /// <summary>Writes cells across one row, advancing the column as it goes.</summary>
    public sealed class XlsxRow
    {
        private readonly XlsxWorksheet _sheet;

        internal XlsxRow(XlsxWorksheet sheet, int rowIndex)
        {
            _sheet = sheet;
            RowIndex = rowIndex;
        }

        /// <summary>Gets the one-based index of this row.</summary>
        public int RowIndex { get; }

        /// <summary>Gets the one-based index of the column the next value will be written to.</summary>
        public int NextColumnIndex { get; private set; } = 1;

        /// <summary>Writes text.</summary>
        /// <param name="text">The text. A null value writes a formatted blank.</param>
        /// <param name="style">The cell format.</param>
        /// <returns>This row, so calls can be chained.</returns>
        public XlsxRow Text(string? text, XlsxStyle style = XlsxStyle.Text) =>
            Write(string.IsNullOrEmpty(text) ? XlsxCell.Blank(style) : XlsxCell.FromText(text!, style));

        /// <summary>Writes a number.</summary>
        /// <param name="value">The value.</param>
        /// <param name="style">The cell format.</param>
        /// <returns>This row, so calls can be chained.</returns>
        public XlsxRow Number(double value, XlsxStyle style = XlsxStyle.Number3) =>
            Write(XlsxCell.FromNumber(value, style));

        /// <summary>Writes a number.</summary>
        /// <param name="value">The value.</param>
        /// <param name="style">The cell format.</param>
        /// <returns>This row, so calls can be chained.</returns>
        public XlsxRow Number(decimal value, XlsxStyle style = XlsxStyle.Number3) =>
            Write(XlsxCell.FromNumber((double)value, style));

        /// <summary>
        /// Writes a number, or a dash when the value is absent.
        /// </summary>
        /// <param name="value">The value, or <see langword="null"/> when it is unknown.</param>
        /// <param name="style">The cell format used when a value is present.</param>
        /// <returns>This row, so calls can be chained.</returns>
        /// <remarks>
        /// An unknown weight is written as a dash and never as a zero, so that a schedule cannot be
        /// summed by hand into a total that silently omits it.
        /// </remarks>
        public XlsxRow NumberOrDash(decimal? value, XlsxStyle style = XlsxStyle.Number3) =>
            value.HasValue ? Number(value.Value, style) : Text("-", XlsxStyle.Text);

        /// <summary>Writes a whole number.</summary>
        /// <param name="value">The value.</param>
        /// <param name="style">The cell format.</param>
        /// <returns>This row, so calls can be chained.</returns>
        public XlsxRow Integer(int value, XlsxStyle style = XlsxStyle.Integer) =>
            Write(XlsxCell.FromNumber(value, style));

        /// <summary>Writes a date and time.</summary>
        /// <param name="value">The value.</param>
        /// <param name="style">The cell format.</param>
        /// <returns>This row, so calls can be chained.</returns>
        public XlsxRow DateTime(DateTime value, XlsxStyle style = XlsxStyle.DateTime) =>
            Write(XlsxCell.FromDate(value, style));

        /// <summary>Writes a boolean.</summary>
        /// <param name="value">The value.</param>
        /// <param name="style">The cell format.</param>
        /// <returns>This row, so calls can be chained.</returns>
        public XlsxRow Boolean(bool value, XlsxStyle style = XlsxStyle.Text) =>
            Write(XlsxCell.FromBoolean(value, style));

        /// <summary>Writes an empty but formatted cell.</summary>
        /// <param name="style">The cell format.</param>
        /// <param name="count">How many blanks to write.</param>
        /// <returns>This row, so calls can be chained.</returns>
        public XlsxRow Blank(XlsxStyle style = XlsxStyle.Default, int count = 1)
        {
            for (int index = 0; index < count; index++)
            {
                Write(XlsxCell.Blank(style));
            }

            return this;
        }

        /// <summary>Moves the write position without writing anything.</summary>
        /// <param name="count">How many columns to skip.</param>
        /// <returns>This row, so calls can be chained.</returns>
        public XlsxRow Skip(int count = 1)
        {
            NextColumnIndex += Math.Max(0, count);
            return this;
        }

        /// <summary>Moves the write position to a specific column.</summary>
        /// <param name="columnIndex">The one-based column index.</param>
        /// <returns>This row, so calls can be chained.</returns>
        public XlsxRow At(int columnIndex)
        {
            if (columnIndex < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, "Column indexes are one-based.");
            }

            NextColumnIndex = columnIndex;
            return this;
        }

        private XlsxRow Write(XlsxCell cell)
        {
            _sheet.SetCell(RowIndex, NextColumnIndex++, cell);
            return this;
        }
    }
}
