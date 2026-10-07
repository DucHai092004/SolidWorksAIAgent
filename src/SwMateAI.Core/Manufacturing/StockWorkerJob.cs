using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace SwMateAI.Core.Manufacturing
{
    [DataContract]
    public sealed class StockWorkerManifest
    {
        [DataMember(Order = 1)]
        public string JobId { get; set; } = string.Empty;

        [DataMember(Order = 2)]
        public string AssemblyPath { get; set; } = string.Empty;

        [DataMember(Order = 3)]
        public string OutputExcelPath { get; set; } = string.Empty;

        [DataMember(Order = 4)]
        public string ImageDirectory { get; set; } = string.Empty;

        [DataMember(Order = 5)]
        public string CancelFlagPath { get; set; } = string.Empty;

        [DataMember(Order = 6)]
        public DateTime CreatedUtc { get; set; }

        [DataMember(Order = 7)]
        public DateTime UpdatedUtc { get; set; }

        [DataMember(Order = 8)]
        public bool Finished { get; set; }

        [DataMember(Order = 9)]
        public bool Cancelled { get; set; }

        [DataMember(Order = 10)]
        public string FatalError { get; set; } = string.Empty;

        [DataMember(Order = 11)]
        public string StatusMessage { get; set; } = string.Empty;

        [DataMember(Order = 12)]
        public int TotalItems { get; set; }

        [DataMember(Order = 13)]
        public int ProcessedItems { get; set; }

        [DataMember(Order = 14)]
        public int CapturedImageCount { get; set; }

        [DataMember(Order = 15)]
        public int OutputRows { get; set; }
    }

    public sealed class StockWorkerJob
    {
        public string JobDirectory { get; set; } = string.Empty;
        public string ManifestPath { get; set; } = string.Empty;
        public string CancelFlagPath { get; set; } = string.Empty;
    }

    public static class StockWorkerManifestSerializer
    {
        public static void Write(string path, StockWorkerManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Manifest path is required.", nameof(path));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            manifest.UpdatedUtc = DateTime.UtcNow;

            string temp = path + ".tmp";
            var serializer = new DataContractJsonSerializer(typeof(StockWorkerManifest));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                serializer.WriteObject(stream, manifest);

            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public static StockWorkerManifest Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Stock worker manifest was not found.", path);

            var serializer = new DataContractJsonSerializer(typeof(StockWorkerManifest));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return (StockWorkerManifest)serializer.ReadObject(stream);
        }
    }

    public sealed class StockWorkerJobBuilder
    {
        public StockWorkerJob Create(string assemblyPath, string outputExcelPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
            if (string.IsNullOrWhiteSpace(outputExcelPath))
                throw new ArgumentException("Output Excel path is required.", nameof(outputExcelPath));

            string fullAssembly = Path.GetFullPath(assemblyPath);
            string fullOutput = Path.GetFullPath(outputExcelPath);
            string jobId = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N");
            string jobDirectory = Path.Combine(Path.GetTempPath(), "SW-MATE_AI", "StockJobs", jobId);
            string imageDirectory = Path.Combine(jobDirectory, "images");
            string cancelFlag = Path.Combine(jobDirectory, "cancel.flag");
            Directory.CreateDirectory(imageDirectory);

            var manifest = new StockWorkerManifest
            {
                JobId = jobId,
                AssemblyPath = fullAssembly,
                OutputExcelPath = fullOutput,
                ImageDirectory = imageDirectory,
                CancelFlagPath = cancelFlag,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                StatusMessage = "Queued"
            };

            string manifestPath = Path.Combine(jobDirectory, "manifest.json");
            StockWorkerManifestSerializer.Write(manifestPath, manifest);
            return new StockWorkerJob
            {
                JobDirectory = jobDirectory,
                ManifestPath = manifestPath,
                CancelFlagPath = cancelFlag
            };
        }

        public static void RequestCancel(StockWorkerJob job)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.CancelFlagPath)) return;
            string directory = Path.GetDirectoryName(job.CancelFlagPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(job.CancelFlagPath, "cancel");
        }
    }
}
