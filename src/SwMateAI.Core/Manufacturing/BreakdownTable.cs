using System.Collections.Generic;

namespace SwMateAI.Core.Manufacturing
{
    public class BreakdownTable
    {
        public List<string> Headers { get; } = new List<string>();
        public List<List<object>> Rows { get; } = new List<List<object>>();
    }
}
