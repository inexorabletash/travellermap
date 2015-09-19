using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Maps.Serialization
{
    internal class ColumnParser
    {
        public ColumnParser(TextReader reader)
        {
            string header = null;
            string separator = null;
            string line;
            int lineNumber = 0;

            // TODO: Make this a generator, parsing on demand.
            while (true)
            {
                line = reader.ReadLine();
                ++lineNumber;
                if (line == null)
                    break;

                // Ignore trailing whitespace
                line = line.TrimEnd();

                // Ignore blanks
                if (line.Length == 0)
                    continue;

                // Ignore comments
                if (line.StartsWith("#"))
                    continue;

                if (header == null)
                {
                    header = line;
                    continue;
                }

                if (separator == null)
                {
                    separator = line;
                    ComputeFields(header, separator);
                    continue;
                }

                ParseLine(line, lineNumber);
            }
        }

        private struct Column
        {
            public int start;
            public int length;
            public string name;
        }

        internal struct Row
        {
            public StringDictionary dict;
            public int lineNumber;
            public string line;
        }
 
        private List<Column> columns = new List<Column>();
        private List<Row> data = new List<Row>();
        public List<Row> Data { get { return data; } }

        public IEnumerable<string> Fields { get { return (from col in columns select col.name); } }

        private void ComputeFields(string header, string separator)
        {
            string[] chunks = Regex.Split(separator, @"( +)");
            int c = 0;
            for (int i = 0; i < chunks.Length; i += 2) {
                int len = chunks[i].Length;
                columns.Add(new Column { start = c, length = len, name = header.SafeSubstring(c, len).TrimEnd() });
                c += len;
                if (i + 1 < chunks.Length)
                    c += chunks[i + 1].Length;
            }
        }

        private void ParseLine(string line, int lineNumber)
        {
            StringDictionary dict = new StringDictionary();
            foreach (var column in columns)
                dict[column.name] = line.SafeSubstring(column.start, column.length).TrimEnd();
            data.Add(new Row { dict = dict, line = line, lineNumber = lineNumber });
        }
    }

    internal class ColumnSerializer
    {
        public ColumnSerializer(IList<string> header)
        {
            string[] row = new string[header.Count()];
            for (var i = 0; i < header.Count(); ++i)
            {
                row[i] = header[i].Trim();
                columns[row[i]] = i;
            }
            rows.Add(row);

            Padding = ' ';
            Delimiter = " ";
            Separator = '-';
        }
        public void AddRow(IList<string> data)
        {
            if (rows[0].Length != data.Count())
                throw new ParseException("Differing column counts");

            string[] row = new string[rows[0].Length];
            for (int i = 0; i < row.Length; ++i)
            {
                if (data[i] == null)
                    row[i] = "";
                else
                    row[i] = data[i].Trim();
            }
            rows.Add(row);
        }
        public int[] ComputeWidths()
        {
            int[] widths = new int[rows[0].Length];
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Length; ++i)
                    widths[i] = Math.Max(widths[i], row[i].Length);
            }
            for (int i = 0; i < widths.Length; ++i)
            {
                if (minimums.ContainsKey(i))
                    widths[i] = Math.Max(widths[i], minimums[i]);
            }

            return widths;
        }

        public void SetMinimumWidth(string col, int width)
        {
            minimums[columns[col]] = width;
        }

        public void Serialize(Stream stream, Encoding encoding = null)
        {
            using (var writer = new StreamWriter(stream, encoding))
            {
                Serialize(writer);
            }
        }

        public void Serialize(TextWriter writer, bool includeHeader = true)
        {
            int[] widths = ComputeWidths();

            StringBuilder line = new StringBuilder();
            line.EnsureCapacity(widths.Sum() + Delimiter.Length * (widths.Length - 1));

            for (int rowindex = (includeHeader ? 0 : 1); rowindex < rows.Count; ++rowindex)
            {
                string[] row = rows[rowindex];
                for (int i = 0; i < row.Length; ++i)
                {
                    if (i != 0)
                        line.Append(Delimiter);
                    string col = row[i];
                    line.Append(col);
                    line.Append(Padding, widths[i] - col.Length);
                }
                writer.WriteLine(line.ToString());
                line.Clear();

                if (rowindex == 0)
                {
                    for (int i = 0; i < row.Length; ++i)
                    {
                        if (i != 0)
                            line.Append(Delimiter);
                        line.Append(Separator, widths[i]);
                    }
                    writer.WriteLine(line.ToString());
                    line.Clear();
                }
            }
        }

        public char Padding { get; set; }
        public string Delimiter { get; set; }
        public char Separator { get; set; }
        private Dictionary<string, int> columns = new Dictionary<string, int>();
        private Dictionary<int, int> minimums = new Dictionary<int, int>();
        private List<string[]> rows = new List<string[]>();
    }
}