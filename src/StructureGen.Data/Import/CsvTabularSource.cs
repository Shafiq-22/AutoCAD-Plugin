using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using StructureGen.Core.Tabular;

namespace StructureGen.Data.Import
{
    /// <summary>
    /// Reads a delimited text file into an <see cref="ITabularSource"/>.
    /// </summary>
    /// <remarks>
    /// Implements the quoting rules of RFC 4180: fields may be wrapped in double quotes, a doubled
    /// quote inside a quoted field is a literal quote, and a quoted field may span physical lines.
    /// Section tables exported from a spreadsheet routinely contain commas inside designations such
    /// as <c>L 100 x 100 x 10, long leg</c>, so naive splitting is not good enough.
    /// </remarks>
    public static class CsvTabularSource
    {
        /// <summary>Reads a delimited file from disk.</summary>
        /// <param name="path">The file to read.</param>
        /// <param name="delimiter">The field delimiter. Defaults to a comma.</param>
        /// <param name="encoding">The encoding. Defaults to UTF-8 with byte-order-mark detection.</param>
        /// <returns>The parsed table.</returns>
        public static ITabularSource FromFile(string path, char delimiter = ',', Encoding? encoding = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A file path is required.", nameof(path));
            }

            string text = File.ReadAllText(path, encoding ?? Encoding.UTF8);
            return Parse(text, Path.GetFileName(path), delimiter);
        }

        /// <summary>Parses delimited text already in memory.</summary>
        /// <param name="text">The delimited text.</param>
        /// <param name="name">A name describing the source, used in error messages.</param>
        /// <param name="delimiter">The field delimiter. Defaults to a comma.</param>
        /// <returns>The parsed table.</returns>
        public static ITabularSource Parse(string text, string name, char delimiter = ',')
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            List<List<string>> records = ParseRecords(text, delimiter);
            if (records.Count == 0)
            {
                return new TabularSource(name, Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>());
            }

            List<string> headers = records[0];
            List<IReadOnlyList<string>> rows = new(records.Count - 1);
            for (int index = 1; index < records.Count; index++)
            {
                rows.Add(records[index]);
            }

            return new TabularSource(name, headers, rows);
        }

        private static List<List<string>> ParseRecords(string text, char delimiter)
        {
            List<List<string>> records = new();
            List<string> current = new();
            StringBuilder field = new();
            bool inQuotes = false;
            bool fieldStarted = false;

            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];

                if (inQuotes)
                {
                    if (character == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(character);
                    }

                    continue;
                }

                if (character == '"' && field.Length == 0)
                {
                    inQuotes = true;
                    fieldStarted = true;
                }
                else if (character == delimiter)
                {
                    current.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                }
                else if (character == '\r')
                {
                    // Swallowed; the following '\n' terminates the record.
                }
                else if (character == '\n')
                {
                    current.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    AppendRecord(records, current);
                    current = new List<string>();
                }
                else
                {
                    field.Append(character);
                    fieldStarted = true;
                }
            }

            if (fieldStarted || field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                AppendRecord(records, current);
            }

            return records;
        }

        private static void AppendRecord(List<List<string>> records, List<string> record)
        {
            // A record consisting of a single empty field is a blank line, not data.
            if (record.Count == 1 && record[0].Length == 0)
            {
                return;
            }

            records.Add(record);
        }
    }
}
