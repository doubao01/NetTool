using FluentAssertions;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class DirectoryDiffServiceTests
{
    private static string CreateDirWith(params (string Path, string Content)[] files)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "diff_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        foreach (var (rel, content) in files)
        {
            var full = System.IO.Path.Combine(root, rel);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return root;
    }

    [Fact]
    public async Task CompareAsync_IdenticalDirs_ReportSame()
    {
        var a = CreateDirWith(("x.txt", "abc"), ("sub\\y.txt", "def"));
        var b = CreateDirWith(("x.txt", "abc"), ("sub\\y.txt", "def"));

        try
        {
            var result = await new DirectoryDiffService().CompareAsync(a, b);

            result.IsIdentical.Should().BeTrue();
            result.Same.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(a, true);
            Directory.Delete(b, true);
        }
    }

    [Fact]
    public async Task CompareAsync_ClassifiesAllFourCategories()
    {
        var a = CreateDirWith(("onlyA.txt", "1"), ("shared.txt", "same"), ("changed.txt", "old"));
        var b = CreateDirWith(("onlyB.txt", "2"), ("shared.txt", "same"), ("changed.txt", "new"));

        try
        {
            var result = await new DirectoryDiffService().CompareAsync(a, b);

            result.OnlyInLeft.Should().Equal("onlyA.txt");
            result.OnlyInRight.Should().Equal("onlyB.txt");
            result.Different.Should().Equal("changed.txt");
            result.Same.Should().Equal("shared.txt");
            result.IsIdentical.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(a, true);
            Directory.Delete(b, true);
        }
    }

    [Fact]
    public async Task CompareAsync_SizeOnlyMode_SkipsHashing()
    {
        var a = CreateDirWith(("file.bin", "aaaa"));
        var b = CreateDirWith(("file.bin", "bbbb"));

        try
        {
            var result = await new DirectoryDiffService().CompareAsync(a, b, compareContent: false);

            // Same size, different content: size-only mode treats it as "same"
            result.Same.Should().Equal("file.bin");
            result.Different.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(a, true);
            Directory.Delete(b, true);
        }
    }

    [Fact]
    public async Task CompareAsync_MissingDirectory_Throws()
    {
        var a = CreateDirWith(("f.txt", "x"));
        try
        {
            var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "missing_" + Guid.NewGuid().ToString("N"));
            await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
                await new DirectoryDiffService().CompareAsync(a, missing));
        }
        finally
        {
            Directory.Delete(a, true);
        }
    }
}

public class HostsFileServiceTests
{
    private readonly HostsFileService _service = new();

    [Fact]
    public void ValidateLines_IgnoresCommentsAndBlanks()
    {
        var content = "# comment\n\n   \n";
        _service.ValidateLines(content).Should().BeEmpty();
    }

    [Theory]
    [InlineData("127.0.0.1 localhost")]
    [InlineData("::1 localhost # inline comment allowed")]
    [InlineData("192.168.1.10 web.local")]
    public void ValidateLines_AcceptsWellFormedMapping(string line)
    {
        _service.ValidateLines(line).Should().BeEmpty();
    }

    [Fact]
    public void ValidateLines_FlagsInvalidIpAndMissingHost()
    {
        var content = "not-an-ip example.com\nlonelyvalue";
        var problems = _service.ValidateLines(content);

        problems.Should().HaveCount(2);
        problems[0].Should().Contain("IP 格式无效");
        problems[1].Should().Contain("缺少主机名");
    }

    [Fact]
    public void GetHostsPath_PointsAtSystemDriversEtc()
    {
        var path = _service.GetHostsPath();

        path.Should().EndWith(Path.Combine("drivers", "etc", "hosts"));
        path.Should().Contain("System32");
        path.Should().StartWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
    }
}
