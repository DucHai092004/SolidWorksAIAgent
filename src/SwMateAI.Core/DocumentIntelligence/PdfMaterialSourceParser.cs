using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SwMateAI.Core.Common;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SwMateAI.Core.DocumentIntelligence
{
    public class PdfMaterialSourceParser : IMaterialSourceParser
    {
        private readonly List<int> _imageOnlyPages = new List<int>();
        public IReadOnlyList<int> ImageOnlyPages => _imageOnlyPages;
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
            _imageOnlyPages.Clear();
            var result = new List<MaterialSourceRecord>();

            using (var pdf = PdfDocument.Open(path))
            {
                int pageNumber = 0;
                foreach (var page in pdf.GetPages())
                {
                    pageNumber++;
                    string text = ExtractInReadingOrder(page);
                    if (text.Trim().Length < 10)
                    {
                        _imageOnlyPages.Add(pageNumber);
                        continue;
                    }

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

        private static string ExtractInReadingOrder(Page page)
        {
            // This PdfPig build predates ContentOrderTextExtractor. Reconstruct rows
            // from word coordinates instead of relying on the PDF drawing order.
            var words = page.GetWords().OrderByDescending(w => w.BoundingBox.Top);
            var lines = new List<List<Word>>();
            foreach (var word in words)
            {
                if (lines.Count == 0 ||
                    Math.Abs(word.BoundingBox.Top - lines[lines.Count - 1][0].BoundingBox.Top) > 3.0)
                    lines.Add(new List<Word>());
                lines[lines.Count - 1].Add(word);
            }
            var text = new StringBuilder();
            foreach (var line in lines)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(string.Join(" ", line.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
            }
            return text.ToString();
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
