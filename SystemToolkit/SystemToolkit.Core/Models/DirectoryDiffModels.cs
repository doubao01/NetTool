namespace SystemToolkit.Core.Models;

public class DirectoryDiffResult
{
    public List<string> OnlyInLeft { get; set; } = new();
    public List<string> OnlyInRight { get; set; } = new();
    public List<string> Different { get; set; } = new();
    public List<string> Same { get; set; } = new();

    public int TotalCompared => Different.Count + Same.Count;
    public bool IsIdentical => OnlyInLeft.Count == 0 && OnlyInRight.Count == 0 && Different.Count == 0;
}
