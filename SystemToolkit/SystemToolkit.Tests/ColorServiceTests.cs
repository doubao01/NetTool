using FluentAssertions;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class ColorServiceTests
{
    private readonly ColorService _service = new();

    [Theory]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("#0f0", 0, 255, 0)]
    [InlineData("0000FF", 0, 0, 255)]
    [InlineData("rgb(255, 0, 0)", 255, 0, 0)]
    [InlineData("rgb(0 0 0)", 0, 0, 0)]
    [InlineData("RED", 255, 0, 0)]
    public void Parse_RecognizesHexRgbAndNamedColors(string input, int red, int green, int blue)
    {
        var color = _service.Parse(input);

        color.Red.Should().Be(red);
        color.Green.Should().Be(green);
        color.Blue.Should().Be(blue);
    }

    [Fact]
    public void Parse_HslWhite_ReturnsAllChannelsAtMaximum()
    {
        var color = _service.Parse("hsl(0, 0%, 100%)");

        color.Red.Should().Be(255);
        color.Green.Should().Be(255);
        color.Blue.Should().Be(255);
    }

    [Fact]
    public void Parse_HsvRed_MatchesRgbRed()
    {
        var color = _service.Parse("hsv(0, 100%, 100%)");

        color.Hex.Should().Be("#FF0000");
    }

    [Fact]
    public void Parse_CmykBlack_ReturnsBlack()
    {
        var color = _service.Parse("cmyk(0%, 0%, 0%, 100%)");

        color.Hex.Should().Be("#000000");
    }

    [Fact]
    public void Parse_UnknownText_Throws()
    {
        var action = () => _service.Parse("not-a-color");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_EmptyInput_Throws()
    {
        var action = () => _service.Parse("  ");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromRgb_KnownColor_ComputesEveryRepresentation()
    {
        var color = _service.FromRgb(64, 128, 255);

        color.Hex.Should().Be("#4080FF");
        color.Hue.Should().BeApproximately(220.0, 0.1);
        color.Saturation.Should().BeApproximately(100.0, 0.1);
        color.Lightness.Should().BeApproximately(62.55, 0.1);
        color.HsvSaturation.Should().BeApproximately(74.9, 0.1);
        color.Value.Should().BeApproximately(100.0, 0.1);
        color.Cyan.Should().BeApproximately(74.9, 0.1);
    }

    [Fact]
    public void FromHsl_RoundTripsThroughRgb()
    {
        var color = _service.FromHsl(210, 100, 50);

        color.Hex.Should().Be("#0080FF");
    }

    [Fact]
    public void FormatHelpers_ProduceStableStrings()
    {
        var color = _service.FromRgb(255, 128, 0);

        _service.ToHex(color).Should().Be("#FF8000");
        _service.ToRgbString(color).Should().Be("rgb(255, 128, 0)");
        _service.ToHslString(color).Should().Be("hsl(30.12, 100%, 50%)");
        _service.ToHsvString(color).Should().Be("hsv(30.12, 100%, 100%)");
    }

    [Fact]
    public void GetContrast_BlackOnWhite_ReturnsMaximumRatio()
    {
        var foreground = _service.FromRgb(0, 0, 0);
        var background = _service.FromRgb(255, 255, 255);

        var result = _service.GetContrast(foreground, background);

        result.Ratio.Should().BeApproximately(21.0, 0.01);
        result.NormalTextAa.Should().BeTrue();
        result.NormalTextAaa.Should().BeTrue();
        result.LargeTextAa.Should().BeTrue();
        result.LargeTextAaa.Should().BeTrue();
        result.RecommendedForeground.Should().Be("#000000");
    }

    [Fact]
    public void GetContrast_SameColor_ReturnsLowRatio()
    {
        var color = _service.FromRgb(128, 128, 128);

        var result = _service.GetContrast(color, color);

        result.Ratio.Should().BeApproximately(1.0, 0.01);
        result.NormalTextAa.Should().BeFalse();
        result.NormalTextAaa.Should().BeFalse();
    }

    [Fact]
    public void GetContrast_LightBackground_RecommendsDarkText()
    {
        var foreground = _service.FromRgb(200, 200, 200);
        var background = _service.FromRgb(250, 250, 230);

        var result = _service.GetContrast(foreground, background);

        result.RecommendedForeground.Should().Be("#000000");
    }

    [Fact]
    public void BuildPalette_ContainsExpectedHueOffsets()
    {
        var baseColor = _service.FromHsl(0, 100, 50);

        var palette = _service.BuildPalette(baseColor);

        palette.Base.Hex.Should().Be("#FF0000");
        palette.Complementary.Single().Hue.Should().BeApproximately(180, 0.5);
        palette.Analogous.Should().HaveCount(2);
        palette.Analogous.Select(c => Math.Round(c.Hue)).Should().Equal(330, 30);
        palette.Triadic.Select(c => Math.Round(c.Hue)).Should().Equal(120, 240);
        palette.Shades.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(256, 0, 0)]
    [InlineData(0, -1, 0)]
    public void FromRgb_OutOfRangeChannel_Throws(int red, int green, int blue)
    {
        var action = () => _service.FromRgb(red, green, blue);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}