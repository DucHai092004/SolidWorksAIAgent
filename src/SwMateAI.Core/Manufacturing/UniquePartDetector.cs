using System.Collections.Generic;
using System.Linq;

namespace SwMateAI.Core.Manufacturing
{
    public class UniquePartDetector
    {
        public List<IGrouping<string, ScannedPartOccurrence>> Group(IEnumerable<ScannedPartOccurrence> occurrences)
        {
            return (occurrences ?? Enumerable.Empty<ScannedPartOccurrence>())
                .GroupBy(x => x.IdentityKey)
                .OrderBy(x => x.Key)
                .ToList();
        }
    }
}
