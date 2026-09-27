using System;
using System.Collections.Generic;

namespace SwMateAI.Core.Common
{
    public class InMemoryAgentLogger : IAgentLogger
    {
        private readonly List<string> _entries = new List<string>();
        public IReadOnlyList<string> Entries => _entries.AsReadOnly();

        public void Info(string message) => Add("INFO", message);
        public void Error(string message) => Add("ERROR", message);

        private void Add(string level, string message)
        {
            _entries.Add($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");
            if (_entries.Count > 500) _entries.RemoveAt(0);
        }
    }
}
