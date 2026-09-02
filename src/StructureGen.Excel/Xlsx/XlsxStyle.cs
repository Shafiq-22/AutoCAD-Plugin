namespace StructureGen.Excel.Xlsx
{
    /// <summary>
    /// The cell formats defined in the generated workbook's style table.
    /// </summary>
    /// <remarks>
    /// The numeric value of each member is its index in the <c>cellXfs</c> list written by
    /// <see cref="XlsxStyleTable"/>. The table is fixed rather than built on demand, which keeps the
    /// writer free of style de-duplication logic and makes the produced file byte-for-byte
    /// predictable for a given set of data.
    /// </remarks>
    public enum XlsxStyle
    {
        /// <summary>Plain text, no border, general number format.</summary>
        Default = 0,

        /// <summary>The document title: bold, larger.</summary>
        Title = 1,

        /// <summary>A bold label used for metadata captions.</summary>
        Label = 2,

        /// <summary>A column heading: bold white on a dark fill, wrapped and centred.</summary>
        Header = 3,

        /// <summary>Bordered text.</summary>
        Text = 4,

        /// <summary>Bordered text that wraps.</summary>
        TextWrapped = 5,

        /// <summary>A bordered whole number with a thousands separator.</summary>
        Integer = 6,

        /// <summary>A bordered number shown to one decimal place.</summary>
        Number1 = 7,

        /// <summary>A bordered number shown to two decimal places.</summary>
        Number2 = 8,

        /// <summary>A bordered number shown to three decimal places.</summary>
        Number3 = 9,

        /// <summary>A bold, filled, bordered label on a totals row.</summary>
        TotalLabel = 10,

        /// <summary>A bold, filled, bordered whole number on a totals row.</summary>
        TotalInteger = 11,

        /// <summary>A bold, filled, bordered number to three decimal places on a totals row.</summary>
        TotalNumber3 = 12,

        /// <summary>Small italic grey text used for notes and disclaimers.</summary>
        Note = 13,

        /// <summary>A date and time.</summary>
        DateTime = 14,

        /// <summary>Bordered text in a warning colour, used for non-conforming or unverified rows.</summary>
        Warning = 15,

        /// <summary>A bold, filled, bordered number to two decimal places on a totals row.</summary>
        TotalNumber2 = 16
    }
}
