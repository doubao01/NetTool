using FluentAssertions;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class CommandPaletteServiceTests
{
    private static CommandPaletteService CreateService()
    {
        var service = new CommandPaletteService();
        service.Register("Ping 工具", "网络工具", "ping icmp 延迟");
        service.Register("端口占用", "网络工具", "port netstat");
        service.Register("批量重命名", "文件管理", "rename batch");
        service.Register("颜色工具", "开发辅助", "color hex rgb");
        service.Register("剪贴板历史", "系统工具", "clipboard history");
        return service;
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsAllRegistered()
    {
        CreateService().Search(string.Empty).Should().HaveCount(5);
    }

    [Fact]
    public void Search_ExactTitle_RanksFirst()
    {
        var results = CreateService().Search("Ping 工具");

        results.Should().NotBeEmpty();
        results[0].Title.Should().Be("Ping 工具");
    }

    [Fact]
    public void Search_Keyword_MatchesEntry()
    {
        var results = CreateService().Search("netstat");

        results.Should().ContainSingle(r => r.Title == "端口占用");
    }

    [Fact]
    public void Search_Category_MatchesAllMembers()
    {
        var results = CreateService().Search("网络工具");

        results.Select(r => r.Title).Should().Contain(new[] { "Ping 工具", "端口占用" });
    }

    [Fact]
    public void Search_FuzzySubsequence_StillFindsEntry()
    {
        var results = CreateService().Search("剪贴板");

        results.Should().Contain(r => r.Title == "剪贴板历史");
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        CreateService().Search("zzz-nonexistent").Should().BeEmpty();
    }

    [Fact]
    public void Search_RespectsLimit()
    {
        CreateService().Search(string.Empty, limit: 2).Should().HaveCount(2);
    }

    [Fact]
    public void Search_NonPositiveLimit_ReturnsEmpty()
    {
        CreateService().Search("ping", limit: 0).Should().BeEmpty();
    }

    [Fact]
    public void Register_DuplicateTitleAndCategory_IsIgnored()
    {
        var service = new CommandPaletteService();
        service.Register("Same", "Cat");
        service.Register("Same", "Cat");

        service.Search(string.Empty).Should().HaveCount(1);
    }

    [Fact]
    public void Register_EmptyTitle_Throws()
    {
        var service = new CommandPaletteService();

        var action = () => service.Register("  ", "Cat");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterRange_AddsAllEntries()
    {
        var service = new CommandPaletteService();

        service.RegisterRange(new[]
        {
            new CommandPaletteEntry { Title = "A", Category = "X" },
            new CommandPaletteEntry { Title = "B", Category = "Y" }
        });

        service.Search(string.Empty).Should().HaveCount(2);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var service = CreateService();

        service.Clear();

        service.Search(string.Empty).Should().BeEmpty();
    }
}