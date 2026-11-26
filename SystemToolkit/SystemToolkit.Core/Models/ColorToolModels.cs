namespace SystemToolkit.Core.Models;

public class ColorInfo
{
    public string Hex { get; set; } = string.Empty;
    public int Red { get; set; }
    public int Green { get; set; }
    public int Blue { get; set; }
    public double Hue { get; set; }
    public double Saturation { get; set; }
    public double Lightness { get; set; }
    public double HsvSaturation { get; set; }
    public double Value { get; set; }
    public double Cyan { get; set; }
    public double Magenta { get; set; }
    public double Yellow { get; set; }
    public double Key { get; set; }
}

public class WcagContrastResult
{
    public double Ratio { get; set; }
    public bool NormalTextAa { get; set; }
    public bool NormalTextAaa { get; set; }
    public bool LargeTextAa { get; set; }
    public bool LargeTextAaa { get; set; }
    public string RecommendedForeground { get; set; } = string.Empty;
    public double RecommendedRatio { get; set; }
}

public class ColorPaletteResult
{
    public ColorInfo Base { get; set; } = new();
    public List<ColorInfo> Complementary { get; set; } = new();
    public List<ColorInfo> Analogous { get; set; } = new();
    public List<ColorInfo> Triadic { get; set; } = new();
    public List<ColorInfo> Shades { get; set; } = new();
}

public class PickedColor
{
    public int X { get; set; }
    public int Y { get; set; }
    public ColorInfo Color { get; set; } = new();
}