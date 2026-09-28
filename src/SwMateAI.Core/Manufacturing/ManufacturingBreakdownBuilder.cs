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

        public ManufacturingBreakdownBuilder(ISldWorks swApp)
        {
            _scanner = new AssemblyScanner(swApp);
            _unique = new UniquePartDetector();
            _quantity = new QuantityCounter();
            _properties = new PartPropertyReader(swApp);
        }

        public BreakdownResult Build()
        {
            var occurrences = _scanner.Scan();
            var result = new BreakdownResult
            {
                TotalPartOccurrences = occurrences.Count,
                SuppressedSkipped = _scanner.SuppressedSkipped
            };

            foreach (var group in _unique.Group(occurrences))
            {
                var representative = group.First();
                int quantity = _quantity.Count(group);
                var item = _properties.Read(representative, quantity);
                result.Items.Add(item);
                if (!item.IsLoaded) result.UnloadedPartCount++;
            }
            return result;
        }
    }
}
