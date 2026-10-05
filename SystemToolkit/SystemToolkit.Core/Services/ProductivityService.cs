namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;

public interface IProductivityService
{
    void AddToClipboardHistory(string content, string format);
    List<ClipboardHistoryItem> GetClipboardHistory(int limit = 50);
    void ClearClipboardHistory();
    Task<ScreenshotResult> CaptureScreenAsync(string? outputPath = null);
    Task<ScreenshotResult> CaptureRegionAsync(Rectangle region, string? outputPath = null);
    Task<string> ExtractTextFromImageAsync(string imagePath);
    void StartTimer(TimerTask task);
    void StopTimer(string taskId);
    List<TimerTask> GetActiveTimers();
    void RegisterShortcut(ShortcutMapping mapping);
    void UnregisterShortcut(string id);
    List<ShortcutMapping> GetRegisteredShortcuts();
    void StartMacroRecording(string name);
    void StopMacroRecording();
    void PlayMacro(string macroId);
    List<MacroRecording> GetSavedMacros();
}

public class ProductivityService : IProductivityService
{
    private static readonly ConcurrentQueue<ClipboardHistoryItem> ClipboardHistory = new();
    private static readonly ConcurrentDictionary<string, System.Timers.Timer> ActiveTimers = new();
    private static readonly ConcurrentDictionary<string, ShortcutMapping> RegisteredShortcuts = new();
    private static readonly List<MacroRecording> SavedMacros = new();
    private static MacroRecording? CurrentRecording;

    public void AddToClipboardHistory(string content, string format)
    {
        var item = new ClipboardHistoryItem
        {
            Content = content,
            Format = format,
            Timestamp = DateTime.Now,
            Size = Encoding.UTF8.GetByteCount(content)
        };

        ClipboardHistory.Enqueue(item);

        // Keep only last 100 items
        while (ClipboardHistory.Count > 100)
        {
            ClipboardHistory.TryDequeue(out _);
        }
    }

    public List<ClipboardHistoryItem> GetClipboardHistory(int limit = 50)
    {
        return ClipboardHistory.Take(limit).OrderByDescending(x => x.Timestamp).ToList();
    }

    public void ClearClipboardHistory()
    {
        while (ClipboardHistory.TryDequeue(out _)) { }
    }

    public Task<ScreenshotResult> CaptureScreenAsync(string? outputPath = null)
    {
        return Task.Run(() =>
        {
            var screenWidth = GetSystemMetrics(SM_CXSCREEN);
            var screenHeight = GetSystemMetrics(SM_CYSCREEN);

            return CaptureRegionAsync(new Rectangle(0, 0, screenWidth, screenHeight), outputPath);
        });
    }

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public Task<ScreenshotResult> CaptureRegionAsync(Rectangle region, string? outputPath = null)
    {
        return Task.Run(() =>
        {
            var timestamp = DateTime.Now;
            outputPath ??= $@"{Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)}\screenshot_{timestamp:yyyyMMdd_HHmmss}.png";

            using var bitmap = new Bitmap(region.Width, region.Height);
            using var graphics = Graphics.FromImage(bitmap);

            graphics.CopyFromScreen(
                region.X,
                region.Y,
                0,
                0,
                region.Size,
                CopyPixelOperation.SourceCopy
            );

            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);

            return new ScreenshotResult
            {
                ImagePath = outputPath,
                Width = region.Width,
                Height = region.Height,
                Timestamp = timestamp,
                OcrText = null
            };
        });
    }

    public async Task<string> ExtractTextFromImageAsync(string imagePath)
    {
        // OCR implementation would use Windows.Media.OCR or Tesseract
        // This is a placeholder for the actual implementation
        await Task.Delay(100);
        return "OCR 功能需要安装额外的库支持";
    }

    public void StartTimer(TimerTask task)
    {
        var timer = new System.Timers.Timer
        {
            Interval = task.Duration.TotalMilliseconds,
            AutoReset = task.IsRecurring,
            Enabled = true
        };

        timer.Elapsed += (sender, e) =>
        {
            // Show notification
            Console.WriteLine($"Timer completed: {task.Title}");

            if (!task.IsRecurring)
            {
                StopTimer(task.Id);
            }
        };

        ActiveTimers[task.Id] = timer;
        timer.Start();
    }

    public void StopTimer(string taskId)
    {
        if (ActiveTimers.TryRemove(taskId, out var timer))
        {
            timer.Stop();
            timer.Dispose();
        }
    }

    public List<TimerTask> GetActiveTimers()
    {
        return ActiveTimers.Keys.Select(id => new TimerTask { Id = id }).ToList();
    }

    public void RegisterShortcut(ShortcutMapping mapping)
    {
        RegisteredShortcuts[mapping.Id] = mapping;
        // Keyboard hook implementation would go here
    }

    public void UnregisterShortcut(string id)
    {
        RegisteredShortcuts.TryRemove(id, out _);
    }

    public List<ShortcutMapping> GetRegisteredShortcuts()
    {
        return RegisteredShortcuts.Values.ToList();
    }

    public void StartMacroRecording(string name)
    {
        CurrentRecording = new MacroRecording
        {
            Name = name,
            CreatedTime = DateTime.Now,
            Steps = new List<MacroStep>()
        };
        // Mouse and keyboard hooks would be registered here
    }

    public void StopMacroRecording()
    {
        if (CurrentRecording != null)
        {
            SavedMacros.Add(CurrentRecording);
            CurrentRecording = null;
        }
    }

    public void PlayMacro(string macroId)
    {
        // Macro playback implementation using SendKeys/SendMessage
    }

    public List<MacroRecording> GetSavedMacros()
    {
        return SavedMacros;
    }
}
