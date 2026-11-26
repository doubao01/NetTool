using System.Text;
using FluentAssertions;
using System.Drawing;
using SystemToolkit.Core.Models;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class ToolkitV14VerificationTests
{
    private readonly ColorService _color = new();
    private readonly ConversionService _conversion = new();
    private readonly ImageToolService _image = new();
    private readonly CommandPaletteService _palette = new();

    // ---------- 颜色工具 ----------

    [Fact]
    public void Color_ParseRgba_IgnoresAlphaChannel()
    {
        var color = _color.Parse("rgba(255, 0, 0, 0.4)");

        color.Hex.Should().Be("#FF0000");
    }

    [Fact]
    public void Color_ParseNamedColor_IsCaseInsensitive()
    {
        _color.Parse("OrAnGe").Hex.Should().Be("#FFA500");
        _color.Parse("grey").Hex.Should().Be("#808080");
    }

    [Fact]
    public void Color_ParseOutOfRangeRgb_ClampsToByteRange()
    {
        _color.Parse("rgb(300, -20, 0)").Hex.Should().Be("#FF0000");
    }

    [Fact]
    public void Color_ParseInvalidHexLength_Throws()
    {
        var action = () => _color.Parse("#12345");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Color_FromHsl_Achromatic_ReturnsNeutralGray()
    {
        _color.FromHsl(0, 0, 50).Hex.Should().Be("#808080");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(200)]
    [InlineData(280)]
    [InlineData(350)]
    public void Color_HslRoundTrip_PreservesHue(double hue)
    {
        var color = _color.FromHsl(hue, 80, 45);

        color.Hue.Should().BeApproximately(hue, 1.0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(120)]
    [InlineData(210)]
    [InlineData(300)]
    [InlineData(359)]
    public void Color_HsvRoundTrip_PreservesHueAndSaturation(double hue)
    {
        var color = _color.FromHsv(hue, 75, 88);

        color.Hue.Should().BeApproximately(hue, 1.0);
        color.HsvSaturation.Should().BeApproximately(75, 1.0);
        color.Value.Should().BeApproximately(88, 1.0);
    }

    [Fact]
    public void Color_Contrast_IsSymmetric()
    {
        var a = _color.FromRgb(30, 90, 200);
        var b = _color.FromRgb(240, 240, 240);

        _color.GetContrast(a, b).Ratio.Should().Be(_color.GetContrast(b, a).Ratio);
    }

    [Fact]
    public void Color_Palette_ComplementaryIsOppositeHue()
    {
        var palette = _color.BuildPalette(_color.FromHsl(210, 90, 40));

        palette.Complementary.Single().Hue.Should().BeApproximately(30, 1.0);
    }

    [Fact]
    public void Color_Palette_HandlesAchromaticBase()
    {
        var palette = _color.BuildPalette(_color.FromRgb(128, 128, 128));

        palette.Shades.Should().HaveCount(5);
        palette.Complementary.Should().ContainSingle();
    }

    [Fact]
    public void Color_FromHsl_OutOfRangeSaturation_Throws()
    {
        var action = () => _color.FromHsl(0, 101, 50);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---------- 格式转换 ----------

    [Fact]
    public void Conversion_YamlQuotedNumber_RemainsString()
    {
        var json = _conversion.YamlToJson("id: \"123\"\n");

        json.Should().Contain("\"123\"");
    }

    [Fact]
    public void Conversion_YamlScalarTypes_ArePreserved()
    {
        const string yaml = "a: 1\nb: 2.5\nc: true\nd: null\ne: plain\n";

        var json = _conversion.YamlToJson(yaml);

        json.Should().Contain("\"a\": 1");
        json.Should().Contain("\"b\": 2.5");
        json.Should().Contain("\"c\": true");
        json.Should().Contain("\"d\": null");
        json.Should().Contain("\"e\": \"plain\"");
    }

    [Fact]
    public void Conversion_YamlDuplicateKey_Throws()
    {
        var action = () => _conversion.YamlToJson("a: 1\na: 2\n");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Conversion_YamlInvalid_Throws()
    {
        var action = () => _conversion.YamlToJson("a: [1, 2\n");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Conversion_JsonTopLevelArray_ConvertsToYamlSequence()
    {
        var yaml = _conversion.JsonToYaml("[1, 2, 3]");

        yaml.Should().Contain("- 1");
        yaml.Should().Contain("- 2");
        yaml.Should().Contain("- 3");
    }

    [Fact]
    public void Conversion_JsonPathRoot_ReturnsWholeDocument()
    {
        var result = _conversion.QueryJsonPath("{\"a\":1}", "$");

        result.Should().Contain("\"a\": 1");
    }

    [Fact]
    public void Conversion_JsonPathQuotedKey_WithDotIsSupported()
    {
        const string json = "{\"a.b\": 7}";

        var result = _conversion.QueryJsonPath(json, "$['a.b']");

        result.Should().Contain("7");
    }

    [Fact]
    public void Conversion_JsonPathSliceWithStep_ReturnsEveryOtherElement()
    {
        var result = _conversion.QueryJsonPath("[0,1,2,3,4,5]", "$[0:6:2]");

        var payload = System.Text.Json.Nodes.JsonNode.Parse(result)!;
        payload.AsArray().Select(n => (int)n!).Should().Equal(0, 2, 4);
    }

    [Fact]
    public void Conversion_JsonPathIndexOutOfRange_ReturnsEmptyArray()
    {
        _conversion.QueryJsonPath("[1,2]", "$[9]").Should().Be("[]");
    }

    [Fact]
    public void Conversion_JsonPathUnclosedBracket_Throws()
    {
        var action = () => _conversion.QueryJsonPath("[1,2]", "$[0");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Conversion_MarkdownFence_RendersCodeBlock()
    {
        var html = _conversion.MarkdownToHtml("```csharp\nvar x = 1;\n```");

        html.Should().Contain("<pre>");
        html.Should().Contain("<code");
    }

    [Fact]
    public void Conversion_QrCode_MediumPayload_Succeeds()
    {
        var bytes = _conversion.GenerateQrCodePng(new string('a', 1000));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Conversion_GuessMimeType_FromFilePath_DetectsPng()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MimeGuess_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "a.png");
        File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        try
        {
            _conversion.GuessImageMimeType(path).Should().Be("image/png");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ---------- 图片批处理 ----------

    [Fact]
    public void Image_BothOptionsSet_PercentTakesPrecedence()
    {
        var options = new ImageResizeOptions { MaxEdge = 100, ScalePercent = 50 };

        var (width, height) = _image.CalculateTargetSize(400, 200, options);

        width.Should().Be(200);
        height.Should().Be(100);
    }

    [Fact]
    public void Image_PercentAboveHundred_Upscales()
    {
        var options = new ImageResizeOptions { ScalePercent = 150 };

        var (width, height) = _image.CalculateTargetSize(100, 50, options);

        width.Should().Be(150);
        height.Should().Be(75);
    }

    [Fact]
    public void Image_PercentRounding_ProducesAtLeastOnePixel()
    {
        var options = new ImageResizeOptions { ScalePercent = 1 };

        var (width, height) = _image.CalculateTargetSize(3, 3, options);

        width.Should().BeGreaterThanOrEqualTo(1);
        height.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Image_BuildOutputPath_PreserveStructureUsesParentFolder()
    {
        var options = new ImageResizeOptions
        {
            MaxEdge = 100,
            OutputFormat = "PNG",
            OutputDirectory = Path.Combine(Path.GetTempPath(), "out"),
            PreserveStructure = true
        };

        var path = _image.BuildOutputPath(Path.Combine("root", "photos", "a.png"), options);

        path.Should().Contain(Path.Combine("out", "photos"));
    }

    [Fact]
    public void Image_ResizeBatch_NullArguments_Throw()
    {
        var a = () => _image.ResizeBatch(null!, new ImageResizeOptions { MaxEdge = 10 });
        var b = () => _image.ResizeBatch(Array.Empty<string>(), null!);

        a.Should().Throw<ArgumentNullException>();
        b.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Image_ResizeBatch_ZeroPercent_Throws()
    {
        var action = () => _image.ResizeBatch(
            Array.Empty<string>(),
            new ImageResizeOptions { ScalePercent = 0 });

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Image_ResizeBatch_InvalidJpegQuality_Throws()
    {
        var action = () => _image.ResizeBatch(
            Array.Empty<string>(),
            new ImageResizeOptions { MaxEdge = 100, JpegQuality = 0 });

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---------- 剪贴板历史 ----------

    [Fact]
    public void Clipboard_ReAddExisting_KeepsPinnedFlag()
    {
        var service = new ClipboardHistoryService();
        var entry = service.Add("pinned");
        service.TogglePin(entry.Id);
        service.Add("other");

        service.Add("pinned");

        var reloaded = service.GetAll().First(e => e.Content == "pinned");
        reloaded.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Clipboard_SearchTrimsWhitespace()
    {
        var service = new ClipboardHistoryService();
        service.Add("hello world");

        service.Search("  hello  ").Should().ContainSingle();
    }

    [Fact]
    public void Clipboard_MaxItems_IsClampedToLowerBound()
    {
        var service = new ClipboardHistoryService { MaxItems = 0 };

        service.MaxItems.Should().Be(1);
    }

    // ---------- 命令面板 ----------

    [Fact]
    public void Palette_CategoryQuery_ReturnsAllMembersInCategory()
    {
        _palette.Register("Ping 工具", "网络工具", "ping");
        _palette.Register("端口占用", "网络工具", "port");
        _palette.Register("颜色工具", "开发辅助", "color");

        var results = _palette.Search("网络工具");

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Category == "网络工具");
    }

    [Fact]
    public void Palette_FuzzySubsequenceOnTitle_FindsEntry()
    {
        _palette.Register("剪贴板历史", "系统工具", "clipboard");

        _palette.Search("剪贴板").Should().ContainSingle(r => r.Title == "剪贴板历史");
    }

    [Fact]
    public void Palette_KeywordMatchWithoutTitleOverlap_FindsEntry()
    {
        _palette.Register("剪贴板历史", "系统工具", "clipboard");

        _palette.Search("clipboard").Should().ContainSingle(r => r.Title == "剪贴板历史");
    }

    [Fact]
    public void Palette_NegativeLimit_ReturnsEmpty()
    {
        _palette.Register("A", "X");

        _palette.Search("A", limit: -1).Should().BeEmpty();
    }

    // ---------- 缺陷回归 ----------

    [Fact]
    public void Color_ParseRgbFunction_KeepsEachChannel()
    {
        _color.Parse("rgb(255,128,0)").Hex.Should().Be("#FF8000");
        _color.Parse("rgb(0, 128, 255)").Hex.Should().Be("#0080FF");
    }

    [Fact]
    public void Color_ParseBareRgbTriplet_KeepsEachChannel()
    {
        _color.Parse("255,128,0").Hex.Should().Be("#FF8000");
    }

    [Fact]
    public void Color_ParseRgbPercent_ScalesToByteRange()
    {
        _color.Parse("rgb(100%, 50%, 0%)").Hex.Should().Be("#FF8000");
    }

    [Fact]
    public void Color_RgbStringRoundTrip_ReturnsSameColor()
    {
        var original = _color.FromRgb(200, 100, 50);
        var text = _color.ToRgbString(original);

        _color.Parse(text).Hex.Should().Be(original.Hex);
    }

    [Fact]
    public void Conversion_JsonToYaml_NumericString_StaysQuoted()
    {
        var yaml = _conversion.JsonToYaml("{\"s\":\"3\"}");

        yaml.Should().Contain("\"3\"");
        _conversion.YamlToJson(yaml).Should().Contain("\"3\"");
    }

    [Fact]
    public void Conversion_JsonToYaml_NullProperty_Preserved()
    {
        var yaml = _conversion.JsonToYaml("{\"n\":null}");

        _conversion.YamlToJson(yaml).Should().Contain("\"n\": null");
    }

    [Fact]
    public void Conversion_JsonToYaml_NullRoot_EmitsNull()
    {
        _conversion.JsonToYaml("null").Trim().Should().Be("null");
    }

    [Fact]
    public void Conversion_YamlToJson_EmptyDocument_ReturnsNull()
    {
        _conversion.YamlToJson("---").Should().Be("null");
    }

    [Fact]
    public void Conversion_YamlToJson_NullScalar_ReturnsNull()
    {
        _conversion.YamlToJson("null").Should().Be("null");
    }

    [Theory]
    [InlineData("$[0:6:2]", 0, 2, 4)]
    [InlineData("$[4:1:-1]", 4, 3, 2)]
    [InlineData("$[::-1]", 5, 4, 3, 2, 1, 0)]
    public void Conversion_JsonPathSliceWithStep_MatchesPythonSemantics(string path, params int[] expected)
    {
        var result = _conversion.QueryJsonPath("[0,1,2,3,4,5]", path);

        var payload = System.Text.Json.Nodes.JsonNode.Parse(result)!;
        payload.AsArray().Select(n => (int)n!).Should().Equal(expected);
    }

    [Fact]
    public void Conversion_QrCode_OverCapacity_ThrowsArgumentException()
    {
        var action = () => _conversion.GenerateQrCodePng(new string('a', ConversionService.MaxQrPayloadBytes + 1));

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Conversion_QrCode_AtCapacity_Succeeds()
    {
        var bytes = _conversion.GenerateQrCodePng(new string('a', ConversionService.MaxQrPayloadBytes));

        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Clipboard_LoweringMaxItems_TrimsExistingEntries()
    {
        var service = new ClipboardHistoryService();
        service.Add("one");
        service.Add("two");
        service.Add("three");

        service.MaxItems = 2;

        service.GetAll().Should().HaveCount(2);
        service.GetAll().Select(e => e.Content).Should().NotContain("one");
    }
}