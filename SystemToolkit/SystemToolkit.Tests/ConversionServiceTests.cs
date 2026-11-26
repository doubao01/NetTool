using System.Text;
using FluentAssertions;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class ConversionServiceTests
{
    private readonly ConversionService _service = new();

    [Fact]
    public void YamlToJson_ConvertsNestedStructures()
    {
        const string yaml = "name: demo\nitems:\n  - id: 1\n    active: true\n  - id: 2\n    active: false\n";

        var json = _service.YamlToJson(yaml);

        json.Should().Contain("\"name\": \"demo\"");
        json.Should().Contain("\"active\": true");
        json.Should().Contain("\"items\"");
    }

    [Fact]
    public void YamlToJson_EmptyInput_Throws()
    {
        var action = () => _service.YamlToJson("   ");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void JsonToYaml_ConvertsNestedStructures()
    {
        const string json = "{\"name\":\"demo\",\"count\":3,\"tags\":[\"a\",\"b\"],\"nested\":{\"ok\":true}}";

        var yaml = _service.JsonToYaml(json);

        yaml.Should().Contain("name: demo");
        yaml.Should().Contain("count: 3");
        yaml.Should().Contain("- a");
        yaml.Should().Contain("ok: true");
    }

    [Fact]
    public void JsonToYaml_InvalidJson_Throws()
    {
        var action = () => _service.JsonToYaml("{not json}");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void YamlJson_RoundTrip_PreservesScalarValues()
    {
        const string json = "{\"title\":\"示例\",\"version\":2,\"enabled\":true}";

        var yaml = _service.JsonToYaml(json);
        var back = _service.YamlToJson(yaml);

        back.Should().Contain("\"title\": \"示例\"");
        back.Should().Contain("\"version\": 2");
        back.Should().Contain("\"enabled\": true");
    }

    [Fact]
    public void QueryJsonPath_SelectsNestedProperty()
    {
        const string json = "{\"store\":{\"book\":[{\"title\":\"A\"},{\"title\":\"B\"}]}}";

        var result = _service.QueryJsonPath(json, "$.store.book[1].title");

        result.Should().Contain("\"B\"");
    }

    [Fact]
    public void QueryJsonPath_Wildcard_ReturnsAllElements()
    {
        const string json = "{\"items\":[{\"id\":1},{\"id\":2},{\"id\":3}]}";

        var result = _service.QueryJsonPath(json, "$.items[*].id");

        result.Should().Contain("1").And.Contain("2").And.Contain("3");
    }

    [Fact]
    public void QueryJsonPath_RecursiveDescent_FindsNestedMatches()
    {
        const string json = "{\"a\":{\"b\":{\"target\":1}},\"target\":2}";

        var result = _service.QueryJsonPath(json, "$..target");

        result.Should().Contain("1").And.Contain("2");
    }

    [Fact]
    public void QueryJsonPath_Slice_ReturnsRange()
    {
        const string json = "[0,1,2,3,4,5]";

        var result = _service.QueryJsonPath(json, "$[1:4]");

        result.Should().Contain("1").And.Contain("2").And.Contain("3").And.NotContain("5");
    }

    [Fact]
    public void QueryJsonPath_NegativeIndex_SelectsFromEnd()
    {
        const string json = "[10,20,30]";

        var result = _service.QueryJsonPath(json, "$[-1]");

        result.Should().Contain("30");
    }

    [Fact]
    public void QueryJsonPath_RequiresDollarPrefix()
    {
        var action = () => _service.QueryJsonPath("{\"a\":1}", "a");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void QueryJsonPath_InvalidJson_Throws()
    {
        var action = () => _service.QueryJsonPath("{oops", "$.a");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void MarkdownToHtml_RendersHeadingsAndEmphasis()
    {
        var html = _service.MarkdownToHtml("# Title\n\n**bold** and *italic*");

        html.Should().Contain("<h1");
        html.Should().Contain("<strong>bold</strong>");
        html.Should().Contain("<em>italic</em>");
    }

    [Fact]
    public void MarkdownToHtml_RendersTablesFromAdvancedExtensions()
    {
        const string markdown = "| a | b |\n|---|---|\n| 1 | 2 |\n";

        var html = _service.MarkdownToHtml(markdown);

        html.Should().Contain("<table>");
    }

    [Fact]
    public void MarkdownToHtml_EmptyInput_ReturnsEmptyString()
    {
        _service.MarkdownToHtml(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void GenerateQrCodePng_ReturnsPngSignature()
    {
        var bytes = _service.GenerateQrCodePng("https://example.com");

        bytes.Should().NotBeEmpty();
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x4E);
        bytes[3].Should().Be(0x47);
    }

    [Fact]
    public void GenerateQrCodePng_EmptyContent_Throws()
    {
        var action = () => _service.GenerateQrCodePng(string.Empty);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GenerateQrCodePng_TooLongContent_Throws()
    {
        var action = () => _service.GenerateQrCodePng(new string('a', 3000));

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ImageBase64_RoundTrip_PreservesBytes()
    {
        var png = _service.GenerateQrCodePng("round-trip");
        var dataUri = "data:image/png;base64," + Convert.ToBase64String(png);

        var back = _service.Base64ToImageBytes(dataUri);

        back.Should().Equal(png);
        _service.GuessImageMimeType(dataUri).Should().Be("image/png");
    }

    [Fact]
    public void Base64ToImageBytes_InvalidBase64_Throws()
    {
        var action = () => _service.Base64ToImageBytes("!!!not-base64!!!");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void GuessImageMimeType_DetectsJpegSignature()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        _service.GuessImageMimeType(Convert.ToBase64String(jpeg)).Should().Be("image/jpeg");
    }

    [Fact]
    public void GuessImageMimeType_UnknownData_ReturnsOctetStream()
    {
        _service.GuessImageMimeType(Convert.ToBase64String(Encoding.UTF8.GetBytes("plain text"))).Should().Be("application/octet-stream");
    }
}