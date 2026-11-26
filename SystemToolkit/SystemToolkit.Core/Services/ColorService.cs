using System.Globalization;
using System.Text.RegularExpressions;
using SystemToolkit.Core.Models;

namespace SystemToolkit.Core.Services;

public interface IColorService
{
    ColorInfo Parse(string input);
    string ToHex(ColorInfo color);
    string ToRgbString(ColorInfo color);
    string ToHslString(ColorInfo color);
    string ToHsvString(ColorInfo color);
    string ToCmykString(ColorInfo color);
    WcagContrastResult GetContrast(ColorInfo foreground, ColorInfo background);
    ColorPaletteResult BuildPalette(ColorInfo baseColor);
    ColorInfo FromRgb(int red, int green, int blue);
    ColorInfo FromHsl(double hue, double saturation, double lightness);
    ColorInfo FromHsv(double hue, double saturation, double value);
}

public sealed class ColorService : IColorService
{
    private static readonly Regex HexPattern = new("^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.Compiled);
    private static readonly Regex RgbPattern = new(@"^rgba?\(([^)]*)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HslPattern = new(@"^hsla?\(([^)]*)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HsvPattern = new(@"^hsva?\(([^)]*)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CmykPattern = new(@"^cmyk?\(([^)]*)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ColorInfo FromRgb(int red, int green, int blue)
    {
        EnsureRange(red, nameof(red));
        EnsureRange(green, nameof(green));
        EnsureRange(blue, nameof(blue));

        var (hue, saturation, lightness) = RgbToHsl(red, green, blue);
        var (hsvSaturation, value) = RgbToHsv(red, green, blue);
        var (cyan, magenta, yellow, key) = RgbToCmyk(red, green, blue);

        return new ColorInfo
        {
            Hex = $"#{red:X2}{green:X2}{blue:X2}",
            Red = red,
            Green = green,
            Blue = blue,
            Hue = Round(hue),
            Saturation = Round(saturation),
            Lightness = Round(lightness),
            HsvSaturation = Round(hsvSaturation),
            Value = Round(value),
            Cyan = Round(cyan),
            Magenta = Round(magenta),
            Yellow = Round(yellow),
            Key = Round(key)
        };
    }

    public ColorInfo FromHsl(double hue, double saturation, double lightness)
    {
        if (saturation is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(saturation), "饱和度必须在 0 至 100 之间。");
        if (lightness is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(lightness), "亮度必须在 0 至 100 之间。");
        var normalizedHue = NormalizeHue(hue);
        var s = saturation / 100.0;
        var l = lightness / 100.0;

        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs((normalizedHue / 60.0) % 2 - 1));
        var m = l - c / 2;

        var (r, g, b) = normalizedHue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return FromRgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    public ColorInfo FromHsv(double hue, double saturation, double value)
    {
        if (saturation is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(saturation), "饱和度必须在 0 至 100 之间。");
        if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value), "明度必须在 0 至 100 之间。");
        var normalizedHue = NormalizeHue(hue);
        var s = saturation / 100.0;
        var v = value / 100.0;

        var c = v * s;
        var x = c * (1 - Math.Abs((normalizedHue / 60.0) % 2 - 1));
        var m = v - c;

        var (r, g, b) = normalizedHue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return FromRgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    public ColorInfo Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("请输入颜色值。", nameof(input));
        var text = input.Trim();

        var hexMatch = HexPattern.Match(text);
        if (hexMatch.Success)
        {
            var hex = hexMatch.Groups[1].Value;
            if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
            return FromRgb(
                int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                int.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        var rgbMatch = RgbPattern.Match(text);
        if (rgbMatch.Success)
        {
            var channels = ParseRgbChannels(rgbMatch.Groups[1].Value);
            return FromRgb(channels[0], channels[1], channels[2]);
        }

        var hslMatch = HslPattern.Match(text);
        if (hslMatch.Success)
        {
            var parts = SplitNumbers(hslMatch.Groups[1].Value, 3, 4);
            return FromHsl(parts[0], Math.Clamp(parts[1], 0, 100), Math.Clamp(parts[2], 0, 100));
        }

        var hsvMatch = HsvPattern.Match(text);
        if (hsvMatch.Success)
        {
            var parts = SplitNumbers(hsvMatch.Groups[1].Value, 3, 4);
            return FromHsv(parts[0], Math.Clamp(parts[1], 0, 100), Math.Clamp(parts[2], 0, 100));
        }

        var cmykMatch = CmykPattern.Match(text);
        if (cmykMatch.Success)
        {
            var parts = SplitNumbers(cmykMatch.Groups[1].Value, 4, 4);
            return FromCmyk(parts[0], parts[1], parts[2], parts[3]);
        }

        if (text.Contains(','))
        {
            var channels = ParseRgbChannels(text);
            return FromRgb(channels[0], channels[1], channels[2]);
        }

        var named = NamedColors.TryGetValue(text, out var namedHex)
            ? namedHex
            : null;
        if (named is not null) return Parse(named);

        throw new FormatException("无法识别的颜色格式。支持 HEX、rgb()、hsl()、hsv()、cmyk()、RGB 三元组与常用颜色名。");
    }

    public string ToHex(ColorInfo color) => color.Hex;

    public string ToRgbString(ColorInfo color) =>
        string.Create(CultureInfo.InvariantCulture, $"rgb({color.Red}, {color.Green}, {color.Blue})");

    public string ToHslString(ColorInfo color) =>
        string.Create(CultureInfo.InvariantCulture, $"hsl({Round(color.Hue):0.##}, {Round(color.Saturation):0.##}%, {Round(color.Lightness):0.##}%)");

    public string ToHsvString(ColorInfo color) =>
        string.Create(CultureInfo.InvariantCulture, $"hsv({Round(color.Hue):0.##}, {Round(color.HsvSaturation):0.##}%, {Round(color.Value):0.##}%)");

    public string ToCmykString(ColorInfo color) =>
        string.Create(CultureInfo.InvariantCulture, $"cmyk({Round(color.Cyan):0.##}%, {Round(color.Magenta):0.##}%, {Round(color.Yellow):0.##}%, {Round(color.Key):0.##}%)");

    public WcagContrastResult GetContrast(ColorInfo foreground, ColorInfo background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        var ratio = (lighter + 0.05) / (darker + 0.05);

        var white = FromRgb(255, 255, 255);
        var black = FromRgb(0, 0, 0);
        var whiteRatio = ContrastRatio(white, background);
        var blackRatio = ContrastRatio(black, background);
        var recommended = whiteRatio >= blackRatio ? white : black;

        return new WcagContrastResult
        {
            Ratio = Math.Round(ratio, 2),
            NormalTextAa = ratio >= 4.5,
            NormalTextAaa = ratio >= 7.0,
            LargeTextAa = ratio >= 3.0,
            LargeTextAaa = ratio >= 4.5,
            RecommendedForeground = recommended.Hex,
            RecommendedRatio = Math.Round(Math.Max(whiteRatio, blackRatio), 2)
        };
    }

    public ColorPaletteResult BuildPalette(ColorInfo baseColor)
    {
        var hue = baseColor.Hue;
        var result = new ColorPaletteResult { Base = baseColor };
        result.Complementary.Add(FromHsl((hue + 180) % 360, baseColor.Saturation, baseColor.Lightness));
        result.Analogous.Add(FromHsl((hue + 330) % 360, baseColor.Saturation, baseColor.Lightness));
        result.Analogous.Add(FromHsl((hue + 30) % 360, baseColor.Saturation, baseColor.Lightness));
        result.Triadic.Add(FromHsl((hue + 120) % 360, baseColor.Saturation, baseColor.Lightness));
        result.Triadic.Add(FromHsl((hue + 240) % 360, baseColor.Saturation, baseColor.Lightness));
        foreach (var step in new[] { 20.0, 35.0, 50.0, 65.0, 80.0 })
            result.Shades.Add(FromHsl(hue, baseColor.Saturation, step));
        return result;
    }

    private ColorInfo FromCmyk(double cyan, double magenta, double yellow, double key)
    {
        foreach (var value in new[] { cyan, magenta, yellow, key })
            if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(cyan), "CMYK 各分量必须在 0 至 100 之间。");
        var c = cyan / 100.0;
        var m = magenta / 100.0;
        var y = yellow / 100.0;
        var k = key / 100.0;
        return FromRgb(
            ToByte((1 - c) * (1 - k)),
            ToByte((1 - m) * (1 - k)),
            ToByte((1 - y) * (1 - k)));
    }

    private static (double Hue, double Saturation, double Lightness) RgbToHsl(int red, int green, int blue)
    {
        var r = red / 255.0;
        var g = green / 255.0;
        var b = blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var lightness = (max + min) / 2;

        double hue = 0;
        if (delta > 0)
        {
            if (max == r) hue = 60 * (((g - b) / delta) % 6);
            else if (max == g) hue = 60 * ((b - r) / delta + 2);
            else hue = 60 * ((r - g) / delta + 4);
        }
        if (hue < 0) hue += 360;

        var saturation = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * lightness - 1));
        return (hue, saturation * 100, lightness * 100);
    }

    private static (double Saturation, double Value) RgbToHsv(int red, int green, int blue)
    {
        var r = red / 255.0;
        var g = green / 255.0;
        var b = blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var saturation = max == 0 ? 0 : delta / max;
        return (saturation * 100, max * 100);
    }

    private static (double Cyan, double Magenta, double Yellow, double Key) RgbToCmyk(int red, int green, int blue)
    {
        var r = red / 255.0;
        var g = green / 255.0;
        var b = blue / 255.0;
        var key = 1 - Math.Max(r, Math.Max(g, b));
        if (key >= 1) return (0, 0, 0, 100);
        var cyan = (1 - r - key) / (1 - key);
        var magenta = (1 - g - key) / (1 - key);
        var yellow = (1 - b - key) / (1 - key);
        return (cyan * 100, magenta * 100, yellow * 100, key * 100);
    }

    private static double RelativeLuminance(ColorInfo color)
    {
        static double Channel(int value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.Red) + 0.7152 * Channel(color.Green) + 0.0722 * Channel(color.Blue);
    }

    private static double ContrastRatio(ColorInfo first, ColorInfo second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double[] SplitNumbers(string body, int min, int max)
    {
        var parts = body.Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < min || parts.Length > max)
            throw new FormatException($"需要 {min} 至 {max} 个数值分量。");
        var values = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var raw = parts[i];
            var percentIndex = raw.IndexOf('%');
            var cleaned = percentIndex >= 0 ? raw[..percentIndex] : raw;
            if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                throw new FormatException($"无法解析数值分量：{raw}。");
        }
        return values;
    }

    private static int[] ParseRgbChannels(string body)
    {
        var tokens = body.Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length is < 3 or > 4)
            throw new FormatException("rgb() 需要 3 至 4 个数值分量。");

        var channels = new int[3];
        for (var i = 0; i < 3; i++)
        {
            var raw = tokens[i];
            var isPercent = raw.EndsWith('%');
            var text = isPercent ? raw[..^1] : raw;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"无法解析 RGB 分量：{raw}。");
            channels[i] = isPercent
                ? (int)Math.Clamp(Math.Round(value * 255 / 100), 0, 255)
                : (int)Math.Clamp(Math.Round(value), 0, 255);
        }
        return channels;
    }

    private static double NormalizeHue(double hue)
    {
        var normalized = hue % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static void EnsureRange(int value, string name)
    {
        if (value is < 0 or > 255) throw new ArgumentOutOfRangeException(name, "RGB 分量必须在 0 至 255 之间。");
    }

    private static int ToByte(double value) => (int)Math.Clamp(Math.Round(value * 255), 0, 255);

    private static double Round(double value) => Math.Round(value, 2);

    private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000",
        ["white"] = "#FFFFFF",
        ["red"] = "#FF0000",
        ["green"] = "#008000",
        ["blue"] = "#0000FF",
        ["yellow"] = "#FFFF00",
        ["cyan"] = "#00FFFF",
        ["magenta"] = "#FF00FF",
        ["gray"] = "#808080",
        ["grey"] = "#808080",
        ["orange"] = "#FFA500",
        ["purple"] = "#800080",
        ["pink"] = "#FFC0CB",
        ["brown"] = "#A52A2A",
        ["transparent"] = "#000000"
    };
}