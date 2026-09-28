using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SwMateAI.Core.Common;
using UglyToad.PdfPig;

namespace SwMateAI.Core.DocumentIntelligence
{
    public class PdfMaterialSourceParser : IMaterialSourceParser
    {
        private static readonly Regex Code = new Regex(
            @"(?:Mã\s*(?:chi\s*tiết|CT|SP)|Ma\s*(?:chi\s*tiet|CT|SP)|Part\s*(?:Number|No\.?|Code)|Drawing\s*(?:No\.?|Number))\s*[:=\-]?\s*" +
            @"(?<v>[A-Za-z0-9_.\-]+?)(?=(?:Vật\s*liệu|Vat\s*lieu|VL\s*phôi|VL\s*phoi|Stock\s*Material|Raw\s*Material|Material\s*Grade|Mác\s*vật\s*liệu|Mac\s*vat\s*lieu)|\s|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Material = new Regex(
            @"(?:Vật\s*liệu\s*phôi|Vat\s*lieu\s*phoi|VL\s*phôi|VL\s*phoi|Stock\s*Material|Raw\s*Material|Material\s*Grade|Mác\s*vật\s*liệu|Mac\s*vat\s*lieu)\s*[:=\-]?\s*(?<v>[^\r\n;|]{1,80})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public bool CanRead(string path) =>
            Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<MaterialSourceRecord> Parse(string path)
        {
            var result = new List<MaterialSourceRecord>();

            using (var pdf = PdfDocument.Open(path))
            {
                int pageNumber = 0;
                foreach (var page in pdf.GetPages())
                {
                    pageNumber++;
                    string text = page.Text ?? string.Empty;
                    if (text.Trim().Length < 10) continue; // image-only page: OCR checkpoint handles this later.

                    var codes = Code.Matches(text);
                    var materials = Material.Matches(text);
                    if (codes.Count == 1 && materials.Count >= 1)
                    {
                        Add(result, path, pageNumber,
                            codes[0].Groups["v"].Value,
                            CleanMaterial(materials[0].Groups["v"].Value), 0.95);
                        continue;
                    }

                    // Reconstruct simple table/key-value lines when PDF text keeps line breaks.
                    foreach (string line in Regex.Split(text, @"[\r\n]+"))
                        TryParseLine(line, path, pageNumber, result);
                }
            }
            return result;
        }

        private static void TryParseLine(string line, string path, int page, List<MaterialSourceRecord> result)
        {
            var code = Code.Match(line ?? string.Empty);
            var material = Material.Match(line ?? string.Empty);
            if (!code.Success || !material.Success) return;
            Add(result, path, page, code.Groups["v"].Value,
                CleanMaterial(material.Groups["v"].Value), 0.95);
        }

        private static void Add(List<MaterialSourceRecord> result, string path, int page,
            string code, string material, double confidence)
        {
            code = code?.Trim() ?? string.Empty;
            material = material?.Trim() ?? string.Empty;
            if (code.Length == 0 || material.Length == 0) return;
            result.Add(new MaterialSourceRecord
            {
                RawPartCode = code,
                NormalizedPartCode = PartCodeNormalizer.Normalize(code),
                StockMaterial = material,
                SourceFile = path,
                PageNumber = page,
                ExtractionMethod = "PDF_TEXT",
                ExtractionConfidence = confidence
            });
        }

        private static string CleanMaterial(string value)
        {
            string text = (value ?? string.Empty).Trim();
            int stop = text.IndexOfAny(new[] { '\t', '|' });
            if (stop > 0) text = text.Substring(0, stop);
            return text.Trim(' ', ':', '=', '-', '.', ',');
        }
    }
}
