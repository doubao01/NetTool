using System.Drawing;
using FluentAssertions;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class ImageToolServiceTests
{
    private readonly ImageToolService _service = new();

    [Fact]
    public void CalculateTargetSize_ByMaxEdge_PreservesAspectRatio()
    {
        var options = new ImageResizeOptions { MaxEdge = 100 };

        var (width, height) = _service.CalculateTargetSize(400, 200, options);

        width.Should().Be(100);
        height.Should().Be(50);
    }

    [Fact]
    public void CalculateTargetSize_ByMaxEdgeOnPortrait_KeepsHeightAsLongest()
    {
        var options = new ImageResizeOptions { MaxEdge = 90 };

        var (width, height) = _service.CalculateTargetSize(300, 450, options);

        width.Should().Be(60);
        height.Should().Be(90);
    }

    [Fact]
    public void CalculateTargetSize_WhenSmallerThanMaxEdge_ReturnsOriginal()
    {
        var options = new ImageResizeOptions { MaxEdge = 1000 };

        var (width, height) = _service.CalculateTargetSize(320, 240, options);

        width.Should().Be(320);
        height.Should().Be(240);
    }

    [Fact]
    public void CalculateTargetSize_ByPercent_ScalesBothDimensions()
    {
        var options = new ImageResizeOptions { ScalePercent = 50 };

        var (width, height) = _service.CalculateTargetSize(800, 600, options);

        width.Should().Be(400);
        height.Should().Be(300);
    }

    [Fact]
    public void CalculateTargetSize_WithoutAnyOption_Throws()
    {
        var action = () => _service.CalculateTargetSize(100, 100, new ImageResizeOptions());

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateTargetSize_InvalidSourceDimension_Throws()
    {
        var action = () => _service.CalculateTargetSize(0, 100, new ImageResizeOptions { MaxEdge = 50 });

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BuildOutputPath_UsesOutputFormatExtension()
    {
        var options = new ImageResizeOptions { MaxEdge = 100, OutputFormat = "JPEG" };

        var path = _service.BuildOutputPath(Path.Combine(Path.GetTempPath(), "photo.png"), options);

        Path.GetExtension(path).Should().Be(".jpg");
        Path.GetFileName(path).Should().StartWith("photo_resized");
    }

    [Fact]
    public void BuildOutputPath_UnknownFormat_FallsBackToPng()
    {
        var options = new ImageResizeOptions { MaxEdge = 100, OutputFormat = "TIFF" };

        var path = _service.BuildOutputPath(Path.Combine(Path.GetTempPath(), "photo.bmp"), options);

        Path.GetExtension(path).Should().Be(".png");
    }

    [Fact]
    public void ResizeBatch_UnsupportedExtension_ReportsError()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ImageToolServiceTests_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var textFile = Path.Combine(dir, "notes.txt");
        File.WriteAllText(textFile, "not an image");

        try
        {
            var summary = _service.ResizeBatch(new[] { textFile }, new ImageResizeOptions { MaxEdge = 100 });

            summary.Failed.Should().Be(1);
            summary.Succeeded.Should().Be(0);
            summary.Items[0].Error.Should().Contain("不支持的图片格式");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ResizeBatch_MissingFile_ReportsError()
    {
        var summary = _service.ResizeBatch(
            new[] { Path.Combine(Path.GetTempPath(), "missing_" + Guid.NewGuid() + ".png") },
            new ImageResizeOptions { MaxEdge = 100 });

        summary.Failed.Should().Be(1);
        summary.Items[0].Error.Should().Contain("文件不存在");
    }

    [Fact]
    public void ResizeBatch_InvalidOptions_Throws()
    {
        var action = () => _service.ResizeBatch(Array.Empty<string>(), new ImageResizeOptions { OutputFormat = "GIF" });

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResizeBatch_RealImage_ProducesResizedOutput()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = Path.Combine(Path.GetTempPath(), "ImageToolResize_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var sourcePath = Path.Combine(dir, "source.png");
        using (var bitmap = new Bitmap(40, 20))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.CornflowerBlue);
            bitmap.Save(sourcePath, System.Drawing.Imaging.ImageFormat.Png);
        }

        try
        {
            var summary = _service.ResizeBatch(new[] { sourcePath }, new ImageResizeOptions { MaxEdge = 10, OutputFormat = "PNG" });

            summary.Succeeded.Should().Be(1);
            var item = summary.Items[0];
            item.Width.Should().Be(10);
            item.Height.Should().Be(5);
            item.OutputPath.Should().NotBeNull();
            File.Exists(item.OutputPath!).Should().BeTrue();
            item.OutputBytes.Should().BeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}