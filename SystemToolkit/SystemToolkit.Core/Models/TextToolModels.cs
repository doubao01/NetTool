namespace SystemToolkit.Core.Models;

public class TextStatistics
{
    public int Lines { get; set; }
    public int NonEmptyLines { get; set; }
    public int Words { get; set; }
    public int Characters { get; set; }
    public int CharactersNoWhitespace { get; set; }
    public int BytesUtf8 { get; set; }
}
