using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SwMateAI.Core.Common;
using SwMateAI.Core.DocumentIntelligence;

namespace SwMateAI.Core.Manufacturing
{
    public class StockMaterialEnrichmentSummary
    {
        public int DocumentAssigned { get; set; }
        public int CadFallback { get; set; }
        public int NeedsReview { get; set; }
    }

    public class StockMaterialEnricher
    {
        private readonly PartCodeMatcher _matcher = new PartCodeMatcher();

        public StockMaterialEnrichmentSummary Enrich(
            IEnumerable<BreakdownItem> items, MaterialImportResult import)
        {
            var summary = new StockMaterialEnrichmentSummary();
            var records = import?.Records ?? new List<MaterialSourceRecord>();
            foreach (var item in items ?? Enumerable.Empty<BreakdownItem>())
            {
                string code = PartCodeNormalizer.Normalize(item.PartNumber);
                var exact = records.Where(x => x.NormalizedPartCode == code).ToList();

                var materials = exact.Select(x => (x.StockMaterial ?? string.Empty).Trim())
                    .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (materials.Count == 1)
                {
                    Assign(item, exact[0], 1.0, "EXACT");
                    summary.DocumentAssigned++;
                    continue;
                }
                if (materials.Count > 1)
                {
                    item.StockMaterialNeedsReview = true;
                    item.StockMatchMethod = "CONFLICT";
                    item.StockMaterialSource = "Conflicting document sources";
                    summary.NeedsReview++;
                    continue;
                }

                var match = _matcher.Find(item.PartNumber, records);
                if (match != null && match.AutoAccept)
                {
                    Assign(item, match.Record, match.Confidence, match.Method);
                    summary.DocumentAssigned++;
                    continue;
                }
                if (match != null)
                {
                    item.StockMaterialNeedsReview = true;
                    item.StockMatchMethod = match.Method;
                    item.StockMaterialSource = Path.GetFileName(match.Record.SourceFile);
                    item.StockMaterialConfidence = match.Confidence;
                    summary.NeedsReview++;
                    continue;
                }

                // No external evidence: preserve useful CAD material but mark its provenance.
                item.StockMaterial = item.Material ?? string.Empty;
                item.StockMaterialSource = string.IsNullOrWhiteSpace(item.StockMaterial) ? "Unassigned" : "SOLIDWORKS Material";
                item.StockMatchMethod = string.IsNullOrWhiteSpace(item.StockMaterial) ? "UNASSIGNED" : "CAD_FALLBACK";
                item.StockMaterialConfidence = string.IsNullOrWhiteSpace(item.StockMaterial) ? 0.0 : 0.5;
                if (string.IsNullOrWhiteSpace(item.StockMaterial))
                {
                    item.StockMaterialNeedsReview = true;
                    summary.NeedsReview++;
                }
                else summary.CadFallback++;
            }
            return summary;
        }

        private static void Assign(BreakdownItem item, MaterialSourceRecord source, double confidence, string method)
        {
            item.StockMaterial = source.StockMaterial?.Trim() ?? string.Empty;
            item.StockMaterialSource = Path.GetFileName(source.SourceFile);
            item.StockMaterialSourceLocation = source.SheetName.Length > 0
                ? $"{source.SheetName}!Row {source.RowNumber}"
                : source.PageNumber.HasValue ? $"Page {source.PageNumber.Value}" : string.Empty;
            item.StockMaterialConfidence = confidence * source.ExtractionConfidence;
            item.StockMatchMethod = method;
            item.StockMaterialNeedsReview = false;
        }
    }
}
