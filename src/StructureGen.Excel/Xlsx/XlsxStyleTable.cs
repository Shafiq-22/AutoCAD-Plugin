using System.Text;

namespace StructureGen.Excel.Xlsx
{
    /// <summary>Writes the fixed <c>styles.xml</c> part that backs <see cref="XlsxStyle"/>.</summary>
    internal static class XlsxStyleTable
    {
        /// <summary>The first custom number format identifier. Identifiers below 164 are reserved by the format.</summary>
        private const int FirstCustomNumberFormat = 164;

        /// <summary>Builds the style sheet.</summary>
        /// <returns>The XML of the <c>xl/styles.xml</c> part.</returns>
        public static string Build()
        {
            StringBuilder builder = new();
            builder.Append(XlsxXml.Declaration);
            builder.Append("<styleSheet xmlns=\"").Append(XlsxXml.SpreadsheetNamespace).Append("\">");

            // 164: three decimal places. 165: one decimal place. 166: date and time.
            builder.Append("<numFmts count=\"3\">")
                   .Append("<numFmt numFmtId=\"").Append(FirstCustomNumberFormat).Append("\" formatCode=\"#,##0.000\"/>")
                   .Append("<numFmt numFmtId=\"").Append(FirstCustomNumberFormat + 1).Append("\" formatCode=\"#,##0.0\"/>")
                   .Append("<numFmt numFmtId=\"").Append(FirstCustomNumberFormat + 2).Append("\" formatCode=\"yyyy\\-mm\\-dd\\ hh:mm\"/>")
                   .Append("</numFmts>");

            builder.Append("<fonts count=\"6\">")
                   .Append("<font><sz val=\"10\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/></font>")
                   .Append("<font><b/><sz val=\"10\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/></font>")
                   .Append("<font><b/><sz val=\"14\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/></font>")
                   .Append("<font><b/><sz val=\"10\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/><family val=\"2\"/></font>")
                   .Append("<font><i/><sz val=\"9\"/><color rgb=\"FF7F7F7F\"/><name val=\"Calibri\"/><family val=\"2\"/></font>")
                   .Append("<font><sz val=\"10\"/><color rgb=\"FF9C0006\"/><name val=\"Calibri\"/><family val=\"2\"/></font>")
                   .Append("</fonts>");

            // Fills 0 and 1 are reserved by the format and must appear exactly as written.
            builder.Append("<fills count=\"4\">")
                   .Append("<fill><patternFill patternType=\"none\"/></fill>")
                   .Append("<fill><patternFill patternType=\"gray125\"/></fill>")
                   .Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1F3864\"/><bgColor indexed=\"64\"/></patternFill></fill>")
                   .Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEDEDED\"/><bgColor indexed=\"64\"/></patternFill></fill>")
                   .Append("</fills>");

            builder.Append("<borders count=\"2\">")
                   .Append("<border><left/><right/><top/><bottom/><diagonal/></border>")
                   .Append("<border>")
                   .Append("<left style=\"thin\"><color rgb=\"FFBFBFBF\"/></left>")
                   .Append("<right style=\"thin\"><color rgb=\"FFBFBFBF\"/></right>")
                   .Append("<top style=\"thin\"><color rgb=\"FFBFBFBF\"/></top>")
                   .Append("<bottom style=\"thin\"><color rgb=\"FFBFBFBF\"/></bottom>")
                   .Append("<diagonal/>")
                   .Append("</border>")
                   .Append("</borders>");

            builder.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");

            builder.Append("<cellXfs count=\"17\">");
            Append(builder, 0, 0, 0, 0);                                            // Default
            Append(builder, 0, 2, 0, 0);                                            // Title
            Append(builder, 0, 1, 0, 0);                                            // Label
            Append(builder, 0, 3, 2, 1, horizontal: "center", vertical: "center", wrap: true); // Header
            Append(builder, 0, 0, 0, 1, vertical: "center");                        // Text
            Append(builder, 0, 0, 0, 1, vertical: "center", wrap: true);            // TextWrapped
            Append(builder, 3, 0, 0, 1, horizontal: "right", vertical: "center");   // Integer  (#,##0)
            Append(builder, FirstCustomNumberFormat + 1, 0, 0, 1, horizontal: "right", vertical: "center"); // Number1
            Append(builder, 4, 0, 0, 1, horizontal: "right", vertical: "center");   // Number2  (#,##0.00)
            Append(builder, FirstCustomNumberFormat, 0, 0, 1, horizontal: "right", vertical: "center");     // Number3
            Append(builder, 0, 1, 3, 1, vertical: "center");                        // TotalLabel
            Append(builder, 3, 1, 3, 1, horizontal: "right", vertical: "center");   // TotalInteger
            Append(builder, FirstCustomNumberFormat, 1, 3, 1, horizontal: "right", vertical: "center");     // TotalNumber3
            Append(builder, 0, 4, 0, 0, wrap: true);                                // Note
            Append(builder, FirstCustomNumberFormat + 2, 0, 0, 0);                  // DateTime
            Append(builder, 0, 5, 0, 1, vertical: "center");                        // Warning
            Append(builder, 4, 1, 3, 1, horizontal: "right", vertical: "center");   // TotalNumber2
            builder.Append("</cellXfs>");

            builder.Append("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
            builder.Append("</styleSheet>");

            return builder.ToString();
        }

        private static void Append(
            StringBuilder builder,
            int numberFormatId,
            int fontId,
            int fillId,
            int borderId,
            string? horizontal = null,
            string? vertical = null,
            bool wrap = false)
        {
            builder.Append("<xf numFmtId=\"").Append(numberFormatId)
                   .Append("\" fontId=\"").Append(fontId)
                   .Append("\" fillId=\"").Append(fillId)
                   .Append("\" borderId=\"").Append(borderId)
                   .Append("\" xfId=\"0\"")
                   .Append(numberFormatId == 0 ? string.Empty : " applyNumberFormat=\"1\"")
                   .Append(fontId == 0 ? string.Empty : " applyFont=\"1\"")
                   .Append(fillId == 0 ? string.Empty : " applyFill=\"1\"")
                   .Append(borderId == 0 ? string.Empty : " applyBorder=\"1\"");

            bool hasAlignment = horizontal is not null || vertical is not null || wrap;
            if (!hasAlignment)
            {
                builder.Append("/>");
                return;
            }

            builder.Append(" applyAlignment=\"1\"><alignment");
            if (horizontal is not null)
            {
                builder.Append(" horizontal=\"").Append(horizontal).Append('"');
            }

            if (vertical is not null)
            {
                builder.Append(" vertical=\"").Append(vertical).Append('"');
            }

            if (wrap)
            {
                builder.Append(" wrapText=\"1\"");
            }

            builder.Append("/></xf>");
        }
    }
}
