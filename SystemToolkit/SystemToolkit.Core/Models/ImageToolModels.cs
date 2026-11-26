namespace SystemToolkit.Core.Models;

public class ImageResizeOptions
{
    public int? MaxEdge { get; set; }
    public double? ScalePercent { get; set; }
    public string OutputFormat { get; set; } = "PNG";
    public int JpegQuality { get; set; } = 85;
    public bool PreserveStructure { get; set; } = true;
    public string? OutputDirectory { get; set; }
}

public class ImageResizeItemResult
{
    public string SourcePath { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long OriginalBytes { get; set; }
    public long OutputBytes { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class ImageResizeSummary
{
    public List<ImageResizeItemResult> Items { get; set; } = new();
    public int Succeeded => Items.Count(i => i.Success);
    public int Failed => Items.Count(i => !i.Success);
}