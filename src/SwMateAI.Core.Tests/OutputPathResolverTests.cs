using System;
using System.Collections.Generic;
using System.IO;
using SwMateAI.Core.Exporting;

namespace SwMateAI.Core.Tests;

[TestClass]
public class OutputPathResolverTests
{
    [TestMethod]
    public void TC001_BlankDirectory_UsesActiveModelFolder()
    {
        var fs = new FakeFileSystem();
        var result = Resolve(fs, string.Empty, @"C:\CAD\Machine\Top.SLDASM", "BOM", ".xlsx");
        Assert.IsTrue(result.Success);
        Assert.AreEqual(@"C:\CAD\Machine", result.DirectoryPath);
        Assert.IsTrue(result.UsedModelDirectory);
    }

    [TestMethod]
    public void TC002_AbsoluteDirectory_IsUsedExactly()
    {
        var fs = new FakeFileSystem();
        var result = Resolve(fs, @"D:\Exports", @"C:\CAD\Top.SLDASM", "BOM", "xlsx");
        Assert.IsTrue(result.Success);
        Assert.AreEqual(@"D:\Exports", result.DirectoryPath);
        Assert.AreEqual(@"D:\Exports\BOM.xlsx", result.FilePath);
    }

    [TestMethod]
    public void TC003_MissingDirectory_IsCreated()
    {
        var fs = new FakeFileSystem { DirectoryExistsValue = false };
        var result = Resolve(fs, @"D:\NewExport", @"C:\CAD\Top.SLDASM", "BOM", ".xlsx");
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.DirectoryCreated);
        Assert.AreEqual(@"D:\NewExport", fs.CreatedDirectory);
    }

    [TestMethod]
    public void TC004_VietnameseUnicodePath_IsPreserved()
    {
        var fs = new FakeFileSystem();
        string path = @"D:\Dự án Cơ Khí\Bản vẽ 2026";
        var result = Resolve(fs, path, @"C:\CAD\Top.SLDASM", "BOM", ".xlsx");
        Assert.IsTrue(result.Success);
        Assert.AreEqual(path, result.DirectoryPath);
    }

    [TestMethod]
    public void TC005_SpecialCharactersAndSpaces_ArePreserved()
    {
        var fs = new FakeFileSystem();
        string path = @"D:\CAD Projects #1 (Rev_02)\Export";
        var result = Resolve(fs, path, @"C:\CAD\Top.SLDASM", "BOM", ".xlsx");
        Assert.IsTrue(result.Success);
        Assert.AreEqual(path, result.DirectoryPath);
    }

    [TestMethod]
    public void TC006_LongPath_UsesExtendedLengthPrefix()
    {
        var fs = new FakeFileSystem();
        string path = @"C:\" + new string('A', 270);
        var result = Resolve(fs, path, @"C:\CAD\Top.SLDASM", "BOM", ".xlsx");
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.UsesExtendedLengthPath);
        Assert.IsTrue(result.FileSystemPath.StartsWith(@"\\?\", StringComparison.Ordinal));
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Warning));
    }

    [TestMethod]
    public void TC007_UncPath_IsAccepted()
    {
        var fs = new FakeFileSystem { HasFreeSpaceInfo = false };
        string path = @"\\192.168.1.100\Shared_CAD\Output";
        var result = Resolve(fs, path, @"C:\CAD\Top.SLDASM", "BOM", ".xlsx");
        Assert.IsTrue(result.Success);
        Assert.AreEqual(path, result.DirectoryPath);
        Assert.IsTrue(fs.WritableVerified);
    }

    [TestMethod]
    public void TC008_ReadOnlyDirectory_ReturnsClearError()
    {
        var fs = new FakeFileSystem { WritableException = new UnauthorizedAccessException("denied") };
        var result = Resolve(fs, @"D:\ReadOnly", @"C:\CAD\Top.SLDASM", "BOM", ".xlsx");
        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "read-only");
    }

    [TestMethod]
    public void TC009_LessThanOneMegabyteFree_ReturnsError()
    {
        var fs = new FakeFileSystem { AvailableFreeBytes = 512 * 1024 };
        var result = Resolve(fs, @"D:\Exports", @"C:\CAD\Top.SLDASM", "BOM", ".xlsx");
        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.ErrorMessage, "free disk space");
    }

    [TestMethod]
    public void TC010_ExistingFile_GetsUniqueSuffixWithoutOverwrite()
    {
        var fs = new FakeFileSystem();
        fs.ExistingFiles.Add(@"D:\Exports\BOM.xlsx");
        var result = Resolve(fs, @"D:\Exports", @"C:\CAD\Top.SLDASM", "BOM", ".xlsx");
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.UsedUniqueSuffix);
        Assert.AreEqual(@"D:\Exports\BOM_001.xlsx", result.FilePath);
    }

    private static OutputPathResult Resolve(FakeFileSystem fs, string directory, string modelPath, string baseName, string extension)
    {
        return new OutputPathResolver(fs).Resolve(new OutputPathRequest
        {
            RequestedDirectory = directory,
            ActiveModelPath = modelPath,
            BaseFileName = baseName,
            Extension = extension
        });
    }

    private sealed class FakeFileSystem : IOutputFileSystem
    {
        public bool DirectoryExistsValue { get; set; } = true;
        public string CreatedDirectory { get; private set; } = string.Empty;
        public bool WritableVerified { get; private set; }
        public Exception? WritableException { get; set; }
        public bool HasFreeSpaceInfo { get; set; } = true;
        public long AvailableFreeBytes { get; set; } = 1024L * 1024L * 1024L;
        public HashSet<string> ExistingFiles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool DirectoryExists(string path) => DirectoryExistsValue;
        public void CreateDirectory(string path) { CreatedDirectory = path; DirectoryExistsValue = true; }
        public bool FileExists(string path) => ExistingFiles.Contains(path);
        public void VerifyWritable(string directoryPath)
        {
            WritableVerified = true;
            if (WritableException != null) throw WritableException;
        }
        public bool TryGetAvailableFreeBytes(string directoryPath, out long availableBytes)
        {
            availableBytes = AvailableFreeBytes;
            return HasFreeSpaceInfo;
        }
    }
}
