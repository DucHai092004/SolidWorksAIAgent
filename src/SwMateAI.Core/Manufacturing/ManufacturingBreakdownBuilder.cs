using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SwMateAI.Core.Manufacturing
{
    public class ManufacturingBreakdownBuilder
    {
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
                int quantity = _quantity.Count(group);
                var item = _properties.Read(representative, quantity);
                item.StockType = _classifier.Classify(item);
                _stockCalculator.Calculate(item, _options);
                _weightCalculator.Calculate(item);
                result.Items.Add(item);
                if (!item.IsLoaded) result.UnloadedPartCount++;
            }
            return result;
        }
    }
}
