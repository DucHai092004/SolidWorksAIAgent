using System.Collections.Generic;
using System.Linq;

namespace SwMateAI.Core.Manufacturing
{
    public class QuantityCounter
    {
        public int Count(IEnumerable<ScannedPartOccurrence> occurrences)
        {
            return occurrences?.Count() ?? 0;
        }
    }
}
