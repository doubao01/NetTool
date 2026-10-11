using FluentAssertions;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class TextToolServiceTests
{
    private readonly TextToolService _service = new();

    [Theory]
    [InlineData("getUserNames", "getUserNames", NamingStyle.Camel)]
    [InlineData("GetUserNames", "GetUserNames", NamingStyle.Pascal)]
    [InlineData("get_user_names", "get-user-names", NamingStyle.Kebab)]
    [InlineData("get-user-names", "get_user_names", NamingStyle.Snake)]
    [InlineData("user name", "USER_NAME", NamingStyle.Constant)]
    [InlineData("USERNAME", "Username", NamingStyle.Title)]
    public void ConvertNaming_SupportsCommonStyles(string input, string expected, NamingStyle style)
    {
        _service.ConvertNaming(input, style).Should().Be(expected);
    }

    [Fact]
    public void ConvertNaming_EmptyInput_ReturnsEmpty()
    {
        _service.ConvertNaming("   ", NamingStyle.Camel).Should().BeEmpty();
    }

    [Fact]
    public void TransformLines_RemoveBlankLines_DropsWhitespaceRows()
    {
        var input = "a\n \nb\n\n c ";
        _service.TransformLines(input, LineOperation.RemoveBlankLines)
            .Should().Be("a\nb\n c ");
    }

    [Fact]
    public void TransformLines_Deduplicate_PreservesFirstOccurrence()
    {
        var input = "b\na\nb\nc\na";
        _service.TransformLines(input, LineOperation.Deduplicate)
            .Should().Be("b\na\nc");
    }

    [Fact]
    public void TransformLines_SortAscending_IsCaseInsensitive()
    {
        var input = "Banana\napple\nCherry";
        _service.TransformLines(input, LineOperation.SortAscending)
            .Should().Be("apple\nBanana\nCherry");
    }

    [Fact]
    public void TransformLines_Reverse_FlipsOrder()
    {
        _service.TransformLines("1\n2\n3", LineOperation.Reverse).Should().Be("3\n2\n1");
    }

    [Fact]
    public void FullWidthHalfWidth_RoundTrip()
    {
        var half = "ABC abc 123 !";
        var full = _service.ToFullWidth(half);
        full.Should().Contain("Ａ");
        _service.ToHalfWidth(full).Should().Be(half);
    }

    [Fact]
    public void GetStatistics_CountsAllDimensions()
    {
        var stats = _service.GetStatistics("hello world\nsecond line\n\n");

        stats.Lines.Should().Be(4);
        stats.NonEmptyLines.Should().Be(2);
        stats.Words.Should().Be(4);
        stats.Characters.Should().Be("hello world\nsecond line\n\n".Length);
        stats.BytesUtf8.Should().Be(stats.Characters);
    }

    [Fact]
    public void GetStatistics_ChineseText_CountsBytesUtf8()
    {
        var stats = _service.GetStatistics("你好");
        stats.Characters.Should().Be(2);
        stats.BytesUtf8.Should().Be(6);
        stats.Words.Should().Be(1);
    }

    [Fact]
    public void GetStatistics_EmptyInput_ReturnsZeros()
    {
        var stats = _service.GetStatistics(string.Empty);
        stats.Lines.Should().Be(0);
        stats.Words.Should().Be(0);
        stats.BytesUtf8.Should().Be(0);
    }
}

public class SystemInfoServiceTests
{
    private readonly SystemInfoService _service = new();

    [Fact]
    public void GetOverview_PopulatesMachineBasics()
    {
        if (!OperatingSystem.IsWindows()) return;
        var info = _service.GetOverview();

        info.MachineName.Should().Be(Environment.MachineName);
        info.ProcessorCount.Should().BeGreaterThan(0);
        info.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
        info.ClrVersion.Should().NotBeNullOrWhiteSpace();
        info.TotalMemoryBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetDisks_ReturnsAtLeastOneUsableDisk()
    {
        if (!OperatingSystem.IsWindows()) return;
        var disks = _service.GetDisks();

        disks.Should().NotBeEmpty();
        disks.Should().Contain(d => d.TotalBytes > 0 && d.Name.StartsWith("C:", StringComparison.OrdinalIgnoreCase));
        disks.Where(d => d.TotalBytes > 0)
            .Should().OnlyContain(d => d.UsedPercentage >= 0 && d.UsedPercentage <= 100);
    }

    [Fact]
    public void GetNetworkAdapters_ExcludesLoopbackAndIsNonThrowing()
    {
        var adapters = _service.GetNetworkAdapters();

        adapters.Should().NotBeNull();
        adapters.Should().NotContain(a => a.AdapterType == "Loopback");
    }
}
