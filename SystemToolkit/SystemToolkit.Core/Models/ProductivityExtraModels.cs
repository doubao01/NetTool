namespace SystemToolkit.Core.Models;

public class ClipboardEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Content { get; set; } = string.Empty;
    public string Format { get; set; } = "text";
    public int ByteSize { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public string Preview => BuildPreview(Content);

    private static string BuildPreview(string content)
    {
        var singleLine = content.Replace("\r", " ").Replace("\n", " ").Trim();
        return singleLine.Length <= 120 ? singleLine : singleLine[..120] + "…";
    }
}

public class CommandPaletteEntry
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public int Score { get; set; }
}