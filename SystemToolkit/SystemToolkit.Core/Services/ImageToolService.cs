using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SystemToolkit.Core.Models;

namespace SystemToolkit.Core.Services;

public interface IImageToolService
{
    IReadOnlyList<string> SupportedExtensions { get; }
    ImageResizeSummary ResizeBatch(IEnumerable<string> sourceFiles, ImageResizeOptions options, CancellationToken ct = default);
    (int Width, int Height) CalculateTargetSize(int width, int height, ImageResizeOptions options);
    string BuildOutputPath(string sourcePath, ImageResizeOptions options);
    static bool IsSupportedPlatform => OperatingSystem.IsWindows();
}

public sealed class ImageToolService : IImageToolService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
    };

    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "PNG", "JPEG", "JPG", "BMP"
    };

    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };

    public ImageResizeSummary ResizeBatch(IEnumerable<string> sourceFiles, ImageResizeOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var summary = new ImageResizeSummary();
        foreach (var source in sourceFiles)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(source)) continue;
            if (!ImageExtensions.Contains(Path.GetExtension(source)))
            {
                summary.Items.Add(new ImageResizeItemResult
                {
                    SourcePath = source,
                    Success = false,
                    Error = "不支持的图片格式。"
                });
                continue;
            }
            summary.Items.Add(ProcessSingle(source, options));
        }
        return summary;
    }

    public (int Width, int Height) CalculateTargetSize(int width, int height, ImageResizeOptions options)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "原始尺寸必须为正数。");
        ArgumentNullException.ThrowIfNull(options);

        if (options.ScalePercent is > 0)
        {
            var factor = options.ScalePercent.Value / 100.0;
            return (Math.Max(1, (int)Math.Round(width * factor)), Math.Max(1, (int)Math.Round(height * factor)));
        }

        if (options.MaxEdge is > 0)
        {
            var longest = Math.Max(width, height);
            if (longest <= options.MaxEdge.Value) return (width, height);
            var factor = (double)options.MaxEdge.Value / longest;
            return (Math.Max(1, (int)Math.Round(width * factor)), Math.Max(1, (int)Math.Round(height * factor)));
        }

        throw new ArgumentException("需要指定最长边像素或缩放百分比。", nameof(options));
    }

    public string BuildOutputPath(string sourcePath, ImageResizeOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(options);

        var extension = NormalizeExtension(options.OutputFormat);
        var directory = options.OutputDirectory;
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
        else if (options.PreserveStructure)
            directory = Path.Combine(directory, Path.GetFileName(Path.GetDirectoryName(sourcePath) ?? string.Empty));

        var name = Path.GetFileNameWithoutExtension(sourcePath) + "_resized" + extension;
        var target = Path.Combine(directory, name);
        for (var index = 1; File.Exists(target); index++)
            target = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(sourcePath)}_resized_{index}{extension}");
        return target;
    }

    private ImageResizeItemResult ProcessSingle(string sourcePath, ImageResizeOptions options)
    {
        var result = new ImageResizeItemResult { SourcePath = sourcePath };
        try
        {
            var fileInfo = new FileInfo(sourcePath);
            if (!fileInfo.Exists)
            {
                result.Error = "文件不存在。";
                return result;
            }
            result.OriginalBytes = fileInfo.Length;

            using var source = Image.FromFile(sourcePath);
            result.OriginalWidth = source.Width;
            result.OriginalHeight = source.Height;
            var (targetWidth, targetHeight) = CalculateTargetSize(source.Width, source.Height, options);
            result.Width = targetWidth;
            result.Height = targetHeight;

            var outputPath = BuildOutputPath(sourcePath, options);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var bitmap = new Bitmap(targetWidth, targetHeight);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight));
            }

            SaveBitmap(bitmap, outputPath, options);
            result.OutputPath = outputPath;
            result.OutputBytes = new FileInfo(outputPath).Length;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        return result;
    }

    private static void SaveBitmap(Bitmap bitmap, string outputPath, ImageResizeOptions options)
    {
        switch (NormalizeFormat(options.OutputFormat))
        {
            case "JPEG":
                var encoder = GetEncoder(ImageFormat.Jpeg);
                using (var parameters = new EncoderParameters(1))
                {
                    parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)Math.Clamp(options.JpegQuality, 1, 100));
                    bitmap.Save(outputPath, encoder, parameters);
                }
                break;
            case "BMP":
                bitmap.Save(outputPath, ImageFormat.Bmp);
                break;
            default:
                bitmap.Save(outputPath, ImageFormat.Png);
                break;
        }
    }

    private static ImageCodecInfo GetEncoder(ImageFormat format) =>
        ImageCodecInfo.GetImageEncoders().FirstOrDefault(codec => codec.FormatID == format.Guid)
        ?? throw new InvalidOperationException("找不到目标编码器。");

    private static void ValidateOptions(ImageResizeOptions options)
    {
        if (options.ScalePercent is null && options.MaxEdge is null)
            throw new ArgumentException("需要指定最长边像素或缩放百分比。", nameof(options));
        if (options.ScalePercent is <= 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(options), "缩放百分比必须在 0 至 1000 之间。");
        if (options.MaxEdge is <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "最长边像素必须为正数。");
        if (!AllowedFormats.Contains(options.OutputFormat))
            throw new ArgumentException("输出格式支持 PNG、JPEG 或 BMP。", nameof(options));
        if (options.JpegQuality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(options), "JPEG 质量必须在 1 至 100 之间。");
    }

    private static string NormalizeFormat(string format)
    {
        var upper = (format ?? string.Empty).Trim().ToUpperInvariant();
        return upper == "JPG" ? "JPEG" : upper;
    }

    private static string NormalizeExtension(string format) => NormalizeFormat(format) switch
    {
        "JPEG" => ".jpg",
        "BMP" => ".bmp",
        _ => ".png"
    };
}