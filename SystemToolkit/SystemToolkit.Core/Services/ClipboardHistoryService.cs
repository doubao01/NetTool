using System.Text;
using SystemToolkit.Core.Models;

namespace SystemToolkit.Core.Services;

public interface IClipboardHistoryService
{
    int MaxItems { get; set; }
    ClipboardEntry Add(string content, string format = "text");
    IReadOnlyList<ClipboardEntry> GetAll();
    IReadOnlyList<ClipboardEntry> Search(string keyword);
    bool TogglePin(string id);
    bool Remove(string id);
    void Clear();
    void ClearUnpinned();
}

public sealed class ClipboardHistoryService : IClipboardHistoryService
{
    private readonly List<ClipboardEntry> _entries = new();
    private readonly object _sync = new();
    private int _maxItems = 200;

    public const int MaxContentBytes = 256 * 1024;

    public int MaxItems
    {
        get => _maxItems;
        set => _maxItems = Math.Clamp(value, 1, 5000);
    }

    public ClipboardEntry Add(string content, string format = "text")
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0) throw new ArgumentException("内容不能为空。", nameof(content));
        var byteSize = Encoding.UTF8.GetByteCount(content);
        if (byteSize > MaxContentBytes) throw new ArgumentException($"单条内容最多 {MaxContentBytes / 1024} KiB。", nameof(content));

        lock (_sync)
        {
            var existing = _entries.FirstOrDefault(e => e.Content == content && e.Format == format);
            if (existing is not null)
            {
                _entries.Remove(existing);
                existing.CreatedTime = DateTime.Now;
                _entries.Insert(0, existing);
                return existing;
            }

            var entry = new ClipboardEntry
            {
                Content = content,
                Format = format,
                ByteSize = byteSize,
                CreatedTime = DateTime.Now
            };
            _entries.Insert(0, entry);
            TrimToLimit();
            return entry;
        }
    }

    public IReadOnlyList<ClipboardEntry> GetAll()
    {
        lock (_sync)
            return _entries.OrderByDescending(e => e.IsPinned).ThenByDescending(e => e.CreatedTime).ToList();
    }

    public IReadOnlyList<ClipboardEntry> Search(string keyword)
    {
        var all = GetAll();
        if (string.IsNullOrWhiteSpace(keyword)) return all;
        var trimmed = keyword.Trim();
        return all.Where(e => e.Content.Contains(trimmed, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public bool TogglePin(string id)
    {
        lock (_sync)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry is null) return false;
            entry.IsPinned = !entry.IsPinned;
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_sync)
            return _entries.RemoveAll(e => e.Id == id) > 0;
    }

    public void Clear()
    {
        lock (_sync)
            _entries.Clear();
    }

    public void ClearUnpinned()
    {
        lock (_sync)
            _entries.RemoveAll(e => !e.IsPinned);
    }

    private void TrimToLimit()
    {
        while (_entries.Count > _maxItems)
        {
            var removable = _entries.LastOrDefault(e => !e.IsPinned);
            if (removable is null) break;
            _entries.Remove(removable);
        }
    }
}