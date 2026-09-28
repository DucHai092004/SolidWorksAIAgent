using System;
using System.Collections.Generic;
using System.IO;

namespace SwMateAI.Core.DocumentIntelligence
{
    public class MaterialDocumentReader
    {
        private readonly ProjectDocumentDiscovery _discovery = new ProjectDocumentDiscovery();
        private readonly List<IMaterialSourceParser> _parsers = new List<IMaterialSourceParser>
        {
            new XlsxMaterialSourceParser(),
            new CsvMaterialSourceParser(),
            new PdfMaterialSourceParser()
        };

        public MaterialImportResult Read(string root)
        {
            var result = new MaterialImportResult();
            foreach (string file in _discovery.Discover(root))
            {
                result.FilesScanned++;
                string ext = Path.GetExtension(file);
                if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) result.PdfFiles++;
                else result.ExcelCsvFiles++;

                try
                {
                    IMaterialSourceParser parser = _parsers.Find(p => p.CanRead(file));
                    if (parser == null) continue;
                    result.Records.AddRange(parser.Parse(file));
                }
                catch (Exception ex)
                {
                    result.Errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                }
            }
            return result;
        }
    }
}
