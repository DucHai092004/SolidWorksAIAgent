using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SwMateAI.Core.DocumentIntelligence;

namespace SwMateAI.Core.Manufacturing
{
    public class ManufacturingBreakdownBuilder
    {
        private readonly ISldWorks _swApp;
        private readonly AssemblyScanner _scanner;
        private readonly UniquePartDetector _unique;
        private readonly QuantityCounter _quantity;
        private readonly PartPropertyReader _properties;
        private readonly StockClassifier _classifier = new StockClassifier();
        private readonly StockCalculator _stockCalculator = new StockCalculator();
        private readonly StockWeightCalculator _weightCalculator = new StockWeightCalculator();
        private readonly StockCalculationOptions _options;

        public ManufacturingBreakdownBuilder(ISldWorks swApp, StockCalculationOptions options = null)
        {
            _swApp = swApp;
            _scanner = new AssemblyScanner(swApp);
            _unique = new UniquePartDetector();
            _quantity = new QuantityCounter();
            _properties = new PartPropertyReader(swApp);
            _options = options ?? new StockCalculationOptions();
        }

        public BreakdownResult Build()
        {
            var occurrences = _scanner.Scan();
            var result = new BreakdownResult
            {
                TotalPartOccurrences = occurrences.Count,
                SuppressedSkipped = _scanner.SuppressedSkipped,
                AllowancePerSideMm = _options.AllowancePerSideMm
            };

            foreach (var group in _unique.Group(occurrences))
            {
                var representative = group.First();
                var item = _properties.Read(representative, _quantity.Count(group));
                item.StockType = _classifier.Classify(item);
                result.Items.Add(item);
                if (!item.IsLoaded) result.UnloadedPartCount++;
            }

            string documentRoot = ResolveDocumentRoot();
            var import = new MaterialDocumentReader().Read(documentRoot);
            var enrichment = new StockMaterialEnricher().Enrich(result.Items, import);

            result.MaterialFilesScanned = import.FilesScanned;
            result.MaterialRecordsFound = import.Records.Count;
            result.MaterialImportErrors = import.Errors.Count;
            result.StockMaterialsFromDocuments = enrichment.DocumentAssigned;
            result.StockMaterialsFromCad = enrichment.CadFallback;
            result.StockMaterialsNeedReview = enrichment.NeedsReview;

            if (string.IsNullOrWhiteSpace(_options.StandardThicknessCatalogPath))
                _options.StandardThicknessCatalogPath = FindThicknessCatalog(documentRoot);

            foreach (var item in result.Items)
            {
                _stockCalculator.Calculate(item, _options);
                _weightCalculator.Calculate(item);
            }
            return result;
        }

        private string ResolveDocumentRoot()
        {
            if (!string.IsNullOrWhiteSpace(_options.DocumentRoot) && Directory.Exists(_options.DocumentRoot))
                return _options.DocumentRoot;
            string path = (_swApp.ActiveDoc as IModelDoc2)?.GetPathName() ?? string.Empty;
            return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetDirectoryName(path);
        }

        private static string FindThicknessCatalog(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return string.Empty;
            string direct = Path.Combine(root, "StockThicknesses.csv");
            if (File.Exists(direct)) return direct;
            string config = Path.Combine(root, "Config", "StockThicknesses.csv");
            return File.Exists(config) ? config : string.Empty;
        }
    }
}
