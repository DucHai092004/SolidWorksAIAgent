using System.Collections.Generic;

namespace SwMateAI.Core.DocumentIntelligence
{
    public interface IMaterialSourceParser
    {
        bool CanRead(string path);
        IReadOnlyList<MaterialSourceRecord> Parse(string path);
    }
}
