using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using SwMateAI.Core.Common;

namespace SwMateAI.Core.DocumentIntelligence
{
    public class CsvMaterialSourceParser : IMaterialSourceParser
    {
        public bool CanRead(string path) =>
            Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<MaterialSourceRecord> Parse(string path)
        {
            var result = new List<MaterialSourceRecord>();
            string delimiter = DetectDelimiter(path);
            using (var parser = new TextFieldParser(path))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(delimiter);
                parser.HasFieldsEnclosedInQuotes = true;
                int rowNumber = 0, codeColumn = -1, materialColumn = -1;

                while (!parser.EndOfData)
                {
                    string[] fields = parser.ReadFields() ?? Array.Empty<string>();
                    rowNumber++;

                    if (codeColumn < 0)
                    {
                        if (rowNumber <= 20 &&
                            MaterialFieldDetector.TryFindColumns(fields, out codeColumn, out materialColumn))
                            continue;
                        if (rowNumber >= 20) break;
                        continue;
                    }

                    string code = Get(fields, codeColumn);
                    string material = Get(fields, materialColumn);
                    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(material)) continue;
                    result.Add(new MaterialSourceRecord
                    {
                        RawPartCode = code.Trim(),
                        NormalizedPartCode = PartCodeNormalizer.Normalize(code),
                        StockMaterial = material.Trim(),
                        SourceFile = path,
                        RowNumber = rowNumber,
                        ExtractionMethod = "CSV",
                        ExtractionConfidence = 1.0
                    });
                }
            }
            return result;
        }

        private static string DetectDelimiter(string path)
        {
            string first = string.Empty;
            using (var reader = new StreamReader(path, true))
                for (int i = 0; i < 20 && !reader.EndOfStream; i++)
                {
                    string line = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line)) { first = line; break; }
                }
            int comma = Count(first, ','), semi = Count(first, ';'), tab = Count(first, '\t');
            if (tab >= comma && tab >= semi && tab > 0) return "\t";
            return semi > comma ? ";" : ",";
        }

        private static int Count(string text, char c)
        {
            int count = 0;
            foreach (char x in text ?? string.Empty) if (x == c) count++;
            return count;
        }

        private static string Get(IReadOnlyList<string> values, int index) =>
            index >= 0 && index < values.Count ? values[index] ?? string.Empty : string.Empty;
    }
}
