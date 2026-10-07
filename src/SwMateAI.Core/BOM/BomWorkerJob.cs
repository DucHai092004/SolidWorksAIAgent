using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace SwMateAI.Core.BOM
{
    [DataContract]
    public sealed class BomWorkerManifest
    {
        [DataMember(Order = 1)]
        public string JobId { get; set; } = string.Empty;

        [DataMember(Order = 2)]
        public string ImageDirectory { get; set; } = string.Empty;

        [DataMember(Order = 3)]
        public DateTime CreatedUtc { get; set; }

        [DataMember(Order = 4)]
        public DateTime UpdatedUtc { get; set; }

        [DataMember(Order = 5)]
        public bool Finished { get; set; }

        [DataMember(Order = 6)]
        public string FatalError { get; set; } = string.Empty;

        [DataMember(Order = 7)]
        public List<BomWorkerItem> Items { get; set; } = new List<BomWorkerItem>();
    }

    [DataContract]
    public sealed class BomWorkerItem
    {
        [DataMember(Order = 1)]
        public int ItemNumber { get; set; }

        [DataMember(Order = 2)]
        public string SourcePath { get; set; } = string.Empty;

        [DataMember(Order = 3)]
        public string Configuration { get; set; } = string.Empty;

        [DataMember(Order = 4)]
        public string OutputImagePath { get; set; } = string.Empty;

        [DataMember(Order = 5)]
        public bool Completed { get; set; }

        [DataMember(Order = 6)]
        public bool Skipped { get; set; }

        [DataMember(Order = 7)]
        public string Error { get; set; } = string.Empty;
    }

    public static class BomWorkerManifestSerializer
    {
        public static void Write(string path, BomWorkerManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Manifest path is required.", nameof(path));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            manifest.UpdatedUtc = DateTime.UtcNow;

            string temp = path + ".tmp";
            var serializer = new DataContractJsonSerializer(typeof(BomWorkerManifest));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                serializer.WriteObject(stream, manifest);

            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public static BomWorkerManifest Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("BOM worker manifest was not found.", path);

            var serializer = new DataContractJsonSerializer(typeof(BomWorkerManifest));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return (BomWorkerManifest)serializer.ReadObject(stream);
        }
    }

    public sealed class BomWorkerJob
    {
        public string JobDirectory { get; set; } = string.Empty;
        public string ManifestPath { get; set; } = string.Empty;
        public string WorkerExePath { get; set; } = string.Empty;
        public int TotalItems { get; set; }
    }

    public sealed class BomWorkerJobBuilder
    {
        public BomWorkerJob Create(BomResult bom, string workerExePath)
        {
            if (bom == null) throw new ArgumentNullException(nameof(bom));
            if (string.IsNullOrWhiteSpace(workerExePath))
                throw new ArgumentException("Worker executable path is required.", nameof(workerExePath));

            string jobId = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N");
            string jobDirectory = Path.Combine(Path.GetTempPath(), "SW-MATE_AI", "BomJobs", jobId);
            string imageDirectory = Path.Combine(jobDirectory, "images");
            Directory.CreateDirectory(imageDirectory);

            var manifest = new BomWorkerManifest
            {
                JobId = jobId,
                ImageDirectory = imageDirectory,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            foreach (BomItem item in bom.Items)
            {
                string fileName = item.ItemNumber.ToString("D5") + ".png";
                manifest.Items.Add(new BomWorkerItem
                {
                    ItemNumber = item.ItemNumber,
                    SourcePath = item.SourcePath ?? string.Empty,
                    Configuration = item.Configuration ?? string.Empty,
                    OutputImagePath = Path.Combine(imageDirectory, fileName)
                });
            }

            string manifestPath = Path.Combine(jobDirectory, "manifest.json");
            BomWorkerManifestSerializer.Write(manifestPath, manifest);
            return new BomWorkerJob
            {
                JobDirectory = jobDirectory,
                ManifestPath = manifestPath,
                WorkerExePath = workerExePath,
                TotalItems = manifest.Items.Count
            };
        }

        public void Apply(BomResult bom, BomWorkerManifest manifest)
        {
            if (bom == null || manifest == null) return;

            var byItem = new Dictionary<int, BomWorkerItem>();
            foreach (BomWorkerItem item in manifest.Items)
                byItem[item.ItemNumber] = item;

            bom.CapturedImageCount = 0;
            foreach (BomItem item in bom.Items)
            {
                if (!byItem.TryGetValue(item.ItemNumber, out BomWorkerItem workerItem)) continue;
                if (workerItem.Completed && File.Exists(workerItem.OutputImagePath))
                {
                    item.ImagePath = workerItem.OutputImagePath;
                    bom.CapturedImageCount++;
                }
                else if (!string.IsNullOrWhiteSpace(workerItem.Error))
                {
                    bom.Warnings.Add("Thumbnail failed for BOM item " + item.ItemNumber + ": " + workerItem.Error);
                }
            }

            if (!string.IsNullOrWhiteSpace(manifest.FatalError))
                bom.Warnings.Add("BOM image worker: " + manifest.FatalError);
        }
    }
}
