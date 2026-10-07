using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SwMateAI.Core.BOM;

namespace SwMateAI.P1Bom.ComplexImageRunner
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string workerExe = args != null && args.Length > 0 ? Path.GetFullPath(args[0]) : string.Empty;
            string fixturePath = args != null && args.Length > 1 ? Path.GetFullPath(args[1]) : string.Empty;
            string fixtureConfiguration = args != null && args.Length > 2 ? args[2] ?? string.Empty : string.Empty;

            if (string.IsNullOrWhiteSpace(workerExe) || !File.Exists(workerExe))
            {
                Console.WriteLine("[FAIL] Worker executable not found: " + workerExe);
                return 2;
            }

            if (string.IsNullOrWhiteSpace(fixturePath) || !File.Exists(fixturePath))
            {
                Console.WriteLine("[FAIL] TC018 fixture not found: " + fixturePath);
                return 2;
            }

            string extension = Path.GetExtension(fixturePath) ?? string.Empty;
            if (!extension.Equals(".SLDPRT", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".SLDASM", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[FAIL] TC018 fixture must be a SOLIDWORKS Part or Assembly.");
                return 2;
            }

            string root = Path.Combine(
                Path.GetTempPath(),
                "SW_MATE_AI_TC018_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(root);
            Console.WriteLine("TC018 temp: " + root);
            Console.WriteLine("Representative complex fixture: " + fixturePath);
            Console.WriteLine("Fixture size=" + new FileInfo(fixturePath).Length + " bytes");
            Console.WriteLine("Fixture configuration=" + (string.IsNullOrWhiteSpace(fixtureConfiguration) ? "<default>" : fixtureConfiguration));

            BomWorkerJob job = null;
            try
            {
                string partNumber = Path.GetFileNameWithoutExtension(fixturePath) ?? "TC018_ComplexFixture";
                var bom = new BomResult();
                bom.Items.Add(new BomItem
                {
                    ItemNumber = 1,
                    PartNumber = partNumber,
                    Quantity = 1,
                    ComponentType = extension.Equals(".SLDASM", StringComparison.OrdinalIgnoreCase) ? "Assembly" : "Part",
                    Configuration = fixtureConfiguration,
                    SourcePath = fixturePath
                });

                var builder = new BomWorkerJobBuilder();
                job = builder.Create(bom, workerExe);

                var startInfo = new ProcessStartInfo
                {
                    FileName = workerExe,
                    Arguments = Quote(job.ManifestPath),
                    WorkingDirectory = Path.GetDirectoryName(workerExe) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                var watch = Stopwatch.StartNew();
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) return Fail("TC018 worker process could not start.");
                    if (!process.WaitForExit(240000))
                    {
                        try { process.Kill(); } catch { }
                        return Fail("TC018 worker timed out.");
                    }
                    watch.Stop();

                    BomWorkerManifest manifest = BomWorkerManifestSerializer.Read(job.ManifestPath);
                    builder.Apply(bom, manifest);
                    BomWorkerItem workerItem = manifest.Items.SingleOrDefault();

                    bool imageOk = process.ExitCode == 0 &&
                                   manifest.Finished &&
                                   string.IsNullOrWhiteSpace(manifest.FatalError) &&
                                   workerItem != null &&
                                   workerItem.Completed &&
                                   File.Exists(workerItem.OutputImagePath) &&
                                   new FileInfo(workerItem.OutputImagePath).Length > 0 &&
                                   bom.CapturedImageCount == 1;

                    Console.WriteLine("Worker elapsed=" + watch.ElapsedMilliseconds + " ms");
                    Console.WriteLine("Exit=" + process.ExitCode +
                                      " Finished=" + manifest.Finished +
                                      " Completed=" + (workerItem?.Completed.ToString() ?? "<null>") +
                                      " Image=" + (workerItem?.OutputImagePath ?? "<null>") +
                                      " Error=" + (workerItem?.Error ?? manifest.FatalError));

                    if (!imageOk)
                        return Fail("TC018 worker did not create a valid isometric thumbnail for the representative complex fixture.");
                }

                string xlsx = Path.Combine(root, "TC018_ComplexImage_Result.xlsx");
                new BomExcelExporter().Export(bom, xlsx);
                if (!File.Exists(xlsx) || new FileInfo(xlsx).Length == 0)
                    return Fail("TC018 thumbnail could not be embedded into Excel.");

                Console.WriteLine("[PASS] TC018 representative complex mechanical fixture captured safely in isometric view and embedded into Excel.");
                Console.WriteLine("[INFO] This automated test covers complex stored CAD. A dedicated organic/freeform fixture can be added later if required by acceptance.");
                return 0;
            }
            catch (Exception ex)
            {
                return Fail("TC018 fatal :: " + ex);
            }
            finally
            {
                if (job != null)
                {
                    try { if (Directory.Exists(job.JobDirectory)) Directory.Delete(job.JobDirectory, true); } catch { }
                }
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static int Fail(string message)
        {
            Console.WriteLine("[FAIL] " + message);
            return 1;
        }
    }
}
