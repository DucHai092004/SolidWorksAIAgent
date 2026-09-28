using System;
using System.IO;
using System.Text;

namespace SwMateAI.Core.BOM
{
    public class BomCsvExporter
    {
        public string Export(BomResult result, string path)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var lines = new System.Collections.Generic.List<string>
            {
                "Item,Part Number,Description,Quantity,Material,Type,Configuration"
            };

            foreach (var x in result.Items)
            {
                lines.Add(string.Join(",", new[]
                {
                    x.ItemNumber.ToString(), Esc(x.PartNumber), Esc(x.Description),
                    x.Quantity.ToString(), Esc(x.Material), Esc(x.ComponentType), Esc(x.Configuration)
                }));
            }

            File.WriteAllLines(path, lines, new UTF8Encoding(true));
            return path;
        }

        private static string Esc(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
