using SystemToolkit.Core.Models;

namespace SystemToolkit.Core.Services;

public interface ICommandPaletteService
{
    void Register(string title, string category, string keywords = "");
    void RegisterRange(IEnumerable<CommandPaletteEntry> entries);
    IReadOnlyList<CommandPaletteEntry> Search(string query, int limit = 20);
    void Clear();
}

public sealed class CommandPaletteService : ICommandPaletteService
{
    private readonly List<CommandPaletteEntry> _entries = new();

    public void Register(string title, string category, string keywords = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (_entries.Any(e => e.Title == title && e.Category == category)) return;
        _entries.Add(new CommandPaletteEntry
        {
            Title = title,
            Category = category,
            Keywords = keywords
        });
    }

    public void RegisterRange(IEnumerable<CommandPaletteEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries)
            Register(entry.Title, entry.Category, entry.Keywords);
    }

    public IReadOnlyList<CommandPaletteEntry> Search(string query, int limit = 20)
    {
        if (limit <= 0) return Array.Empty<CommandPaletteEntry>();
        if (string.IsNullOrWhiteSpace(query)) return _entries.Take(limit).ToList();

        var trimmed = query.Trim();
        var scored = new List<CommandPaletteEntry>();
        foreach (var entry in _entries)
        {
            var score = Score(entry, trimmed);
            if (score <= 0) continue;
            scored.Add(new CommandPaletteEntry
            {
                Title = entry.Title,
                Category = entry.Category,
                Keywords = entry.Keywords,
                Score = score
            });
        }
        return scored
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public void Clear() => _entries.Clear();

    private static int Score(CommandPaletteEntry entry, string query)
    {
        var score = 0;
        if (entry.Title.Equals(query, StringComparison.OrdinalIgnoreCase)) score += 1000;
        else if (entry.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 500;
        else if (entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) score += 200;

        if (entry.Category.Contains(query, StringComparison.OrdinalIgnoreCase)) score += 80;
        if (entry.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase)) score += 60;

        if (score == 0)
        {
            var titleIndex = 0;
            var matched = true;
            foreach (var character in query)
            {
                var found = entry.Title.IndexOf(character.ToString(), titleIndex, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    matched = false;
                    break;
                }
                titleIndex = found + 1;
            }
            if (matched) score += 40 + query.Length;
        }
        return score;
    }
}