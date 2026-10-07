using System;
using System.IO;

namespace SwMateAI.Core.Exporting
{
    public sealed class OutputPathRequest
    {
        public string RequestedDirectory { get; set; } = string.Empty;
        public string ActiveModelPath { get; set; } = string.Empty;
        public string BaseFileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long MinimumFreeBytes { get; set; } = 1024L * 1024L;
    }

    public sealed class OutputPathResult
    {
        public bool Success { get; set; }
        public string DirectoryPath { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSystemPath { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
        public bool UsedModelDirectory { get; set; }
        public bool DirectoryCreated { get; set; }
        public bool UsedUniqueSuffix { get; set; }
        public bool UsesExtendedLengthPath { get; set; }
    }

    public interface IOutputFileSystem
    {
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        bool FileExists(string path);
        void VerifyWritable(string directoryPath);
        bool TryGetAvailableFreeBytes(string directoryPath, out long availableBytes);
    }

    public sealed class SystemOutputFileSystem : IOutputFileSystem
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public bool FileExists(string path) => File.Exists(path);

        public void VerifyWritable(string directoryPath)
        {
            string probe = Path.Combine(directoryPath, ".swmate-write-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                    stream.WriteByte(0);
                }
            }
            finally
            {
                try { if (File.Exists(probe)) File.Delete(probe); } catch { }
            }
        }

        public bool TryGetAvailableFreeBytes(string directoryPath, out long availableBytes)
        {
            availableBytes = long.MaxValue;
            try
            {
                string root = Path.GetPathRoot(directoryPath);
                if (string.IsNullOrWhiteSpace(root) || root.StartsWith("\\\\", StringComparison.Ordinal))
                    return false;

                availableBytes = new DriveInfo(root).AvailableFreeSpace;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public sealed class OutputPathResolver
    {
        private readonly IOutputFileSystem _fileSystem;

        public OutputPathResolver(IOutputFileSystem fileSystem = null)
        {
            _fileSystem = fileSystem ?? new SystemOutputFileSystem();
        }

        public OutputPathResult Resolve(OutputPathRequest request)
        {
            if (request == null) return Fail("Output path request is null.");

            bool useModelDirectory = string.IsNullOrWhiteSpace(request.RequestedDirectory);
            string directory = useModelDirectory
                ? Path.GetDirectoryName(request.ActiveModelPath ?? string.Empty)
                : request.RequestedDirectory.Trim();

            if (string.IsNullOrWhiteSpace(directory))
                return Fail("Output directory is empty and the active model has no saved file path.");

            if (!Path.IsPathRooted(directory))
                return Fail("Output directory must be an absolute path.");

            try
            {
                directory = Path.GetFullPath(directory);
            }
            catch (PathTooLongException)
            {
                // .NET Framework can reject a long path before Windows extended-length
                // syntax is applied. Because the path is already absolute, preserve the
                // user-facing path and switch to \\?\ syntax for filesystem operations.
                directory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex)
            {
                return Fail("Invalid output directory: " + ex.Message);
            }

            string directoryForIo = ToExtendedLengthPathIfNeeded(directory, out bool directoryExtended);
            bool created = false;
            try
            {
                if (!_fileSystem.DirectoryExists(directoryForIo))
                {
                    _fileSystem.CreateDirectory(directoryForIo);
                    created = true;
                }

                _fileSystem.VerifyWritable(directoryForIo);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Fail("Output directory is read-only or access is denied: " + ex.Message);
            }
            catch (Exception ex)
            {
                return Fail("Cannot prepare output directory: " + ex.Message);
            }

            if (_fileSystem.TryGetAvailableFreeBytes(directory, out long available) &&
                available < Math.Max(0, request.MinimumFreeBytes))
            {
                return Fail("Not enough free disk space in output directory.");
            }

            string baseName = string.IsNullOrWhiteSpace(request.BaseFileName)
                ? Path.GetFileNameWithoutExtension(request.ActiveModelPath ?? string.Empty)
                : request.BaseFileName.Trim();
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "SW-MATE-AI-Export";

            string extension = NormalizeExtension(request.Extension);
            string filePath = Path.Combine(directory, baseName + extension);
            string fileSystemPath = ToExtendedLengthPathIfNeeded(filePath, out bool fileExtended);
            bool uniqueSuffix = false;
            int suffix = 1;
            while (_fileSystem.FileExists(fileSystemPath))
            {
                uniqueSuffix = true;
                filePath = Path.Combine(directory, baseName + "_" + suffix.ToString("000") + extension);
                fileSystemPath = ToExtendedLengthPathIfNeeded(filePath, out fileExtended);
                suffix++;
            }

            bool extended = directoryExtended || fileExtended;
            return new OutputPathResult
            {
                Success = true,
                DirectoryPath = directory,
                FilePath = filePath,
                FileSystemPath = fileSystemPath,
                Warning = extended ? "Long path detected; extended-length Windows path prefix will be used for file I/O." : string.Empty,
                UsedModelDirectory = useModelDirectory,
                DirectoryCreated = created,
                UsedUniqueSuffix = uniqueSuffix,
                UsesExtendedLengthPath = extended
            };
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
            string value = extension.Trim();
            return value.StartsWith(".", StringComparison.Ordinal) ? value : "." + value;
        }

        public static string ToExtendedLengthPathIfNeeded(string path, out bool extended)
        {
            extended = false;
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith("\\\\?\\", StringComparison.Ordinal))
                return path;
            if (path.Length < 260) return path;

            extended = true;
            if (path.StartsWith("\\\\", StringComparison.Ordinal))
                return "\\\\?\\UNC\\" + path.Substring(2);
            return "\\\\?\\" + path;
        }

        private static OutputPathResult Fail(string message)
        {
            return new OutputPathResult { Success = false, ErrorMessage = message ?? string.Empty };
        }
    }
}
