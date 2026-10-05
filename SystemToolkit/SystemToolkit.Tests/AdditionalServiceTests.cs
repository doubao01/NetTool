using FluentAssertions;
using SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

using Xunit;

namespace SystemToolkit.Tests;

public class ProductivityServiceTests
{
    [Fact]
    public void AddToClipboardHistory_Then_Get_ReturnsItem()
    {
        var service = new ProductivityService();
        service.ClearClipboardHistory();

        service.AddToClipboardHistory("hello", "text");

        var history = service.GetClipboardHistory();
        history.Should().Contain(i => i.Content == "hello" && i.Format == "text");
    }

    [Fact]
    public void AddToClipboardHistory_ComputesUtf8Size()
    {
        var service = new ProductivityService();
        service.ClearClipboardHistory();

        service.AddToClipboardHistory("abc", "text");

        var item = service.GetClipboardHistory().Single(i => i.Content == "abc");
        item.Size.Should().Be(3);
    }

    [Fact]
    public void ClearClipboardHistory_RemovesAllItems()
    {
        var service = new ProductivityService();
        service.AddToClipboardHistory("a", "text");
        service.AddToClipboardHistory("b", "text");

        service.ClearClipboardHistory();

        service.GetClipboardHistory().Should().BeEmpty();
    }

    [Fact]
    public void RegisterShortcut_Then_GetRegistered_ReturnsMapping()
    {
        var service = new ProductivityService();
        var mapping = new ShortcutMapping { Id = "sc-" + Guid.NewGuid(), Description = "测试快捷键" };

        service.RegisterShortcut(mapping);

        service.GetRegisteredShortcuts().Should().Contain(m => m.Id == mapping.Id);

        service.UnregisterShortcut(mapping.Id);
    }

    [Fact]
    public void UnregisterShortcut_RemovesMapping()
    {
        var service = new ProductivityService();
        var mapping = new ShortcutMapping { Id = "sc-" + Guid.NewGuid(), Description = "临时" };
        service.RegisterShortcut(mapping);

        service.UnregisterShortcut(mapping.Id);

        service.GetRegisteredShortcuts().Should().NotContain(m => m.Id == mapping.Id);
    }
}

public class DiskServiceTests
{
    [Fact]
    public async Task GetDirectorySizeAsync_ReturnsTotalBytes()
    {
        var service = new DiskService();
        var dir = Path.Combine(Path.GetTempPath(), "DiskServiceTests_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.txt"), new string('x', 100));
        File.WriteAllText(Path.Combine(dir, "b.txt"), new string('y', 200));

        try
        {
            var size = await service.GetDirectorySizeAsync(dir);
            size.Should().Be(300);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FindLargeFilesAsync_ReturnsOnlyFilesAboveThreshold()
    {
        var service = new DiskService();
        var dir = Path.Combine(Path.GetTempPath(), "DiskLargeTests_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "small.txt"), "abc");
        File.WriteAllText(Path.Combine(dir, "large.bin"), new string('z', 5000));

        try
        {
            var files = await service.FindLargeFilesAsync(dir, minSizeBytes: 1000);

            files.Should().Contain(f => f.Path.EndsWith("large.bin"));
            files.Should().NotContain(f => f.Path.EndsWith("small.txt"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GetAllDrives_ReturnsAtLeastOneDrive()
    {
        var service = new DiskService();

        var drives = service.GetAllDrives();

        drives.Should().NotBeEmpty();
    }
}
