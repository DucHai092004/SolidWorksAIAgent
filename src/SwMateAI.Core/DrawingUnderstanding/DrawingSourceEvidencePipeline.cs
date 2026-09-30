using System;
using System.Collections.Generic;

namespace SwMateAI.Core.DrawingUnderstanding
{
    public sealed class DrawingSourceEvidencePipeline
    {
        private readonly DrawingVisionReadinessAnalyzer _readiness = new DrawingVisionReadinessAnalyzer();
        private readonly List<IDrawingSourceEvidenceProvider> _providers;

        public DrawingSourceEvidencePipeline(IEnumerable<IDrawingSourceEvidenceProvider> providers = null)
        {
            _providers = providers != null
                ? new List<IDrawingSourceEvidenceProvider>(providers)
                : new List<IDrawingSourceEvidenceProvider>
                {
                    new PdfTextDrawingEvidenceProvider()
                };
        }

        public DrawingSourceEvidenceResult Analyze(string path)
        {
            var result = new DrawingSourceEvidenceResult
            {
                Readiness = _readiness.Analyze(path)
            };

            foreach (var provider in _providers)
            {
                if (provider == null) continue;

                bool canAnalyze;
                try { canAnalyze = provider.CanAnalyze(path); }
                catch (Exception ex)
                {
                    result.Errors.Add("Evidence provider capability check failed: " + ex.Message);
                    continue;
                }

                if (!canAnalyze) continue;

                try
                {
                    var providerResult = provider.Analyze(path);
                    if (providerResult == null) continue;
                    foreach (var evidence in providerResult.Evidence)
                        result.Evidence.Add(evidence);
                    foreach (string error in providerResult.Errors)
                        result.Errors.Add(error);
                }
                catch (Exception ex)
                {
                    result.Errors.Add("Evidence provider failed: " + ex.Message);
                }
            }

            return result;
        }
    }
}
