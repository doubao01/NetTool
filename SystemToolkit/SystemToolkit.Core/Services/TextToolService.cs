using System.Text;
using System.Text.RegularExpressions;

namespace SystemToolkit.Core.Services;

using SystemToolkit.Core.Models;

public enum NamingStyle
{
    Camel,
    Pascal,
    Snake,
    Kebab,
    Constant,
    Title
}

public enum LineOperation
{
    TrimLines,
    RemoveBlankLines,
    Deduplicate,
    SortAscending,
    SortDescending,
    Reverse
}

public interface ITextToolService
{
    string ConvertNaming(string input, NamingStyle style);
    string TransformLines(string input, LineOperation operation);
    string ToHalfWidth(string input);
    string ToFullWidth(string input);
    TextStatistics GetStatistics(string input);
}

public class TextToolService : ITextToolService
{
    private static readonly Regex TokenSplit = new(
        @"[A-Z]+(?=[A-Z][a-z])|[A-Z]+(?![a-z])|[A-Z][a-z0-9]*|[a-z0-9]+", RegexOptions.Compiled);

    private static List<string> Tokenize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<string>();
        return TokenSplit.Matches(input).Select(m => m.Value.ToLowerInvariant()).ToList();
    }

    public string ConvertNaming(string input, NamingStyle style)
    {
        var tokens = Tokenize(input);
        if (tokens.Count == 0) return string.Empty;

        return style switch
        {
            NamingStyle.Camel => string.Concat(tokens.Select((t, i) => i == 0 ? t : Capitalize(t))),
            NamingStyle.Pascal => string.Concat(tokens.Select(Capitalize)),
            NamingStyle.Snake => string.Join("_", tokens),
            NamingStyle.Kebab => string.Join("-", tokens),
            NamingStyle.Constant => string.Join("_", tokens.Select(t => t.ToUpperInvariant())),
            NamingStyle.Title => string.Join(" ", tokens.Select(Capitalize)),
            _ => input
        };
    }

    private static string Capitalize(string token) =>
        token.Length == 0 ? token : char.ToUpperInvariant(token[0]) + token.Substring(1);

    public string TransformLines(string input, LineOperation operation)
    {
        var lines = input.Replace("\r\n", "\n").Split('\n');

        return operation switch
        {
            LineOperation.TrimLines => string.Join("\n", lines.Select(l => l.Trim())),
            LineOperation.RemoveBlankLines => string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l))),
            LineOperation.Deduplicate => string.Join("\n", DistinctPreservingOrder(lines)),
            LineOperation.SortAscending => string.Join("\n", lines.OrderBy(l => l, StringComparer.OrdinalIgnoreCase)),
            LineOperation.SortDescending => string.Join("\n", lines.OrderByDescending(l => l, StringComparer.OrdinalIgnoreCase)),
            LineOperation.Reverse => string.Join("\n", lines.Reverse()),
            _ => input
        };
    }

    private static IEnumerable<string> DistinctPreservingOrder(IEnumerable<string> lines)
    {
        var seen = new HashSet<string>();
        foreach (var line in lines)
        {
            if (seen.Add(line))
            {
                yield return line;
            }
        }
    }

    public string ToHalfWidth(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c >= '\uFF01' && c <= '\uFF5E')
            {
                sb.Append((char)(c - 0xFEE0));
            }
            else if (c == '\u3000')
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public string ToFullWidth(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c >= '!' && c <= '~')
            {
                sb.Append((char)(c + 0xFEE0));
            }
            else if (c == ' ')
            {
                sb.Append('\u3000');
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public TextStatistics GetStatistics(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new TextStatistics();
        }

        var lines = input.Replace("\r\n", "\n").Split('\n');

        return new TextStatistics
        {
            Lines = lines.Length,
            NonEmptyLines = lines.Count(l => !string.IsNullOrWhiteSpace(l)),
            Words = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
            Characters = input.Length,
            CharactersNoWhitespace = input.Count(c => !char.IsWhiteSpace(c)),
            BytesUtf8 = Encoding.UTF8.GetByteCount(input)
        };
    }
}
