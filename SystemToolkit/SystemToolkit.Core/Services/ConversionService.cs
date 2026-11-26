using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Markdig;
using QRCoder;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SystemToolkit.Core.Services;

public interface IConversionService
{
    string YamlToJson(string yaml);
    string JsonToYaml(string json);
    string QueryJsonPath(string json, string path);
    string MarkdownToHtml(string markdown);
    byte[] GenerateQrCodePng(string content, int pixelsPerModule = 8);
    string ImageFileToBase64(string imagePath);
    byte[] Base64ToImageBytes(string base64);
    string GuessImageMimeType(string base64OrPath);
}

public sealed class ConversionService : IConversionService
{
    public const int MaxInputBytes = 1024 * 1024;
    public const int MaxQrPayloadBytes = 2048;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string YamlToJson(string yaml)
    {
        EnsureSize(yaml, nameof(yaml));
        if (string.IsNullOrWhiteSpace(yaml)) throw new ArgumentException("请输入 YAML 内容。", nameof(yaml));
        var stream = new YamlStream();
        try
        {
            using var reader = new StringReader(yaml);
            stream.Load(reader);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new FormatException("YAML 语法无效：" + ex.Message, ex);
        }
        if (stream.Documents.Count == 0) return "null";
        var jsonNode = ConvertYamlNode(stream.Documents[0].RootNode, 0);
        return jsonNode?.ToJsonString(JsonOptions) ?? "null";
    }

    public string JsonToYaml(string json)
    {
        EnsureSize(json, nameof(json));
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("请输入 JSON 内容。", nameof(json));
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException("JSON 语法无效：" + ex.Message, ex);
        }
        var serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();
        return serializer.Serialize(node is null ? null : ConvertToPlainObject(node));
    }

    public string MarkdownToHtml(string markdown)
    {
        EnsureSize(markdown, nameof(markdown));
        ArgumentNullException.ThrowIfNull(markdown);
        return Markdown.ToHtml(markdown, Pipeline);
    }

    public byte[] GenerateQrCodePng(string content, int pixelsPerModule = 8)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0) throw new ArgumentException("请输入需要生成二维码的内容。", nameof(content));
        if (Encoding.UTF8.GetByteCount(content) > MaxQrPayloadBytes)
            throw new ArgumentException($"二维码内容最多 {MaxQrPayloadBytes} 字节。", nameof(content));
        if (pixelsPerModule is < 1 or > 40) throw new ArgumentOutOfRangeException(nameof(pixelsPerModule), "像素倍率必须在 1 至 40 之间。");

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    public string ImageFileToBase64(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        var file = new FileInfo(imagePath);
        if (!file.Exists) throw new FileNotFoundException("图片文件不存在。", imagePath);
        if (file.Length > MaxInputBytes) throw new ArgumentException($"图片文件最多 {MaxInputBytes / 1024} KiB。", nameof(imagePath));
        var bytes = File.ReadAllBytes(imagePath);
        var mime = GuessMimeFromBytes(bytes);
        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    public byte[] Base64ToImageBytes(string base64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64);
        var payload = base64.Trim();
        var commaIndex = payload.IndexOf(',');
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
            payload = payload[(commaIndex + 1)..];
        payload = payload.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace(" ", string.Empty);
        if (payload.Length % 4 != 0) throw new FormatException("Base64 长度无效。");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException ex)
        {
            throw new FormatException("Base64 内容无效：" + ex.Message, ex);
        }
        if (bytes.Length > MaxInputBytes) throw new ArgumentException($"解码结果最多 {MaxInputBytes / 1024} KiB。");
        return bytes;
    }

    public string GuessImageMimeType(string base64OrPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64OrPath);
        var text = base64OrPath.Trim();
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var semicolon = text.IndexOf(';');
            return semicolon > 5 ? text[5..semicolon] : "application/octet-stream";
        }
        if (File.Exists(text))
            return GuessMimeFromBytes(File.ReadAllBytes(text));
        try
        {
            return GuessMimeFromBytes(Base64ToImageBytes(text));
        }
        catch (FormatException)
        {
            return "application/octet-stream";
        }
    }

    public string QueryJsonPath(string json, string path)
    {
        EnsureSize(json, nameof(json));
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("请输入 JSON 内容。", nameof(json));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("请输入 JSONPath 表达式。", nameof(path));
        if (!path.StartsWith('$')) throw new FormatException("JSONPath 必须以 $ 开头。");

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException("JSON 语法无效：" + ex.Message, ex);
        }

        var results = JsonPathEvaluator.Evaluate(root, path);
        var array = new JsonArray();
        foreach (var result in results)
            array.Add(result?.DeepClone());
        return array.ToJsonString(JsonOptions);
    }

    private static void EnsureSize(string value, string parameter)
    {
        if (value.Length > MaxInputBytes || Encoding.UTF8.GetByteCount(value) > MaxInputBytes)
            throw new ArgumentException($"输入最多 {MaxInputBytes / 1024} KiB。", parameter);
    }

    private static string GuessMimeFromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return "image/gif";
        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
            return "image/bmp";
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";
        return "application/octet-stream";
    }

    private static JsonNode? ConvertYamlNode(YamlNode node, int depth)
    {
        if (depth > 64) throw new FormatException("YAML 嵌套层级超过 64 层。");
        return node switch
        {
            YamlMappingNode mapping => ConvertYamlMapping(mapping, depth),
            YamlSequenceNode sequence => ConvertYamlSequence(sequence, depth),
            YamlScalarNode scalar => ConvertYamlScalar(scalar),
            _ => null
        };
    }

    private static JsonObject ConvertYamlMapping(YamlMappingNode mapping, int depth)
    {
        var result = new JsonObject();
        foreach (var pair in mapping.Children)
        {
            var key = pair.Key is YamlScalarNode scalar
                ? scalar.Value ?? string.Empty
                : pair.Key.ToString();
            if (result.ContainsKey(key)) throw new FormatException($"YAML 存在重复键：{key}。");
            result[key] = ConvertYamlNode(pair.Value, depth + 1);
        }
        return result;
    }

    private static JsonArray ConvertYamlSequence(YamlSequenceNode sequence, int depth)
    {
        var result = new JsonArray();
        foreach (var item in sequence.Children)
            result.Add(ConvertYamlNode(item, depth + 1));
        return result;
    }

    private static JsonNode? ConvertYamlScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style is YamlDotNet.Core.ScalarStyle.SingleQuoted or YamlDotNet.Core.ScalarStyle.DoubleQuoted or YamlDotNet.Core.ScalarStyle.Literal)
            return JsonValue.Create(value);
        if (value.Length == 0) return JsonValue.Create(string.Empty);
        return value switch
        {
            "~" or "null" or "Null" or "NULL" => null,
            "true" or "True" or "TRUE" => JsonValue.Create(true),
            "false" or "False" or "FALSE" => JsonValue.Create(false),
            _ => ParseYamlNumber(value)
        };
    }

    private static JsonNode ParseYamlNumber(string value)
    {
        if (long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer)
            && value.IndexOf('.') < 0 && value.IndexOf('e') < 0 && value.IndexOf('E') < 0)
            return JsonValue.Create(integer);
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
            return JsonValue.Create(real);
        return JsonValue.Create(value);
    }

    private static object? ConvertToPlainObject(JsonNode node) => node switch
    {
        JsonObject obj => obj.ToDictionary(pair => pair.Key, pair => ConvertToPlainObject(pair.Value)),
        JsonArray array => array.Select(ConvertToPlainObject).ToList(),
        JsonValue value => ToPrimitive(value),
        _ => null
    };

    private static object? ToPrimitive(JsonValue value)
    {
        if (value.TryGetValue<bool>(out var flag)) return flag;
        if (value.TryGetValue<long>(out var integer)) return integer;
        if (value.TryGetValue<double>(out var real)) return real;
        if (value.TryGetValue<string>(out var text)) return text;
        return value.ToJsonString();
    }

    private static class JsonPathEvaluator
    {
        public static List<JsonNode?> Evaluate(JsonNode? root, string path)
        {
            var nodes = new List<JsonNode?> { root };
            var index = 1;
            while (index < path.Length)
            {
                var character = path[index];
                if (character == '.')
                {
                    index++;
                    if (index < path.Length && path[index] == '.')
                    {
                        index++;
                        var name = ReadIdentifier(path, ref index);
                        nodes = nodes.SelectMany(Descendants).Distinct().ToList();
                        nodes = FilterByName(nodes, name);
                        continue;
                    }
                    if (index < path.Length && path[index] == '*')
                    {
                        index++;
                        nodes = nodes.SelectMany(Children).ToList();
                        continue;
                    }
                    var identifier = ReadIdentifier(path, ref index);
                    nodes = FilterByName(nodes, identifier);
                }
                else if (character == '[')
                {
                    var close = path.IndexOf(']', index);
                    if (close < 0) throw new FormatException("JSONPath 存在未闭合的方括号。");
                    var token = path[(index + 1)..close].Trim();
                    index = close + 1;
                    nodes = ApplyBracket(nodes, token);
                }
                else
                {
                    throw new FormatException($"JSONPath 在位置 {index} 出现无法识别的字符：{character}。");
                }
            }
            return nodes;
        }

        private static List<JsonNode?> ApplyBracket(List<JsonNode?> nodes, string token)
        {
            if (token == "*") return nodes.SelectMany(Children).ToList();

            if (token.Length >= 2 && (token[0] == '\'' || token[0] == '"'))
            {
                var delimiter = token[0];
                if (token[^1] != delimiter) throw new FormatException("JSONPath 属性名的引号不匹配。");
                var name = token[1..^1];
                return FilterByName(nodes, name);
            }

            if (int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var index))
            {
                var result = new List<JsonNode?>();
                foreach (var node in nodes)
                {
                    if (node is not JsonArray array) continue;
                    var resolved = index < 0 ? array.Count + index : index;
                    if (resolved >= 0 && resolved < array.Count) result.Add(array[resolved]);
                }
                return result;
            }

            var parts = token.Split(':');
            if (parts.Length is 2 or 3)
            {
                var result = new List<JsonNode?>();
                foreach (var node in nodes)
                {
                    if (node is not JsonArray array) continue;
                    var start = ParseSliceBound(parts[0], 0, array.Count);
                    var end = parts.Length > 1 ? ParseSliceBound(parts[1], array.Count, array.Count) : array.Count;
                    var step = parts.Length > 2 ? ParseStep(parts[2]) : 1;
                    for (var i = start; i < end && i < array.Count; i += step)
                    {
                        var resolved = i < 0 ? array.Count + i : i;
                        if (resolved >= 0 && resolved < array.Count) result.Add(array[resolved]);
                    }
                }
                return result;
            }

            throw new FormatException($"无法解析 JSONPath 方括号内容：{token}。");
        }

        private static int ParseSliceBound(string text, int fallback, int count)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"切片下标无效：{text}。");
            if (value < 0) value = Math.Max(0, count + value);
            return value;
        }

        private static int ParseStep(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 1;
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var step) || step == 0)
                throw new FormatException("切片步长必须为非零整数。");
            return step;
        }

        private static string ReadIdentifier(string path, ref int index)
        {
            var start = index;
            while (index < path.Length && (char.IsLetterOrDigit(path[index]) || path[index] is '_' or '-' or '$'))
                index++;
            if (start == index) throw new FormatException($"JSONPath 在位置 {index} 缺少属性名。");
            return path[start..index];
        }

        private static IEnumerable<JsonNode?> Children(JsonNode? node) => node switch
        {
            JsonObject obj => obj.Select(pair => pair.Value),
            JsonArray array => array.AsEnumerable(),
            _ => Enumerable.Empty<JsonNode?>()
        };

        private static IEnumerable<JsonNode?> Descendants(JsonNode? node)
        {
            yield return node;
            foreach (var child in Children(node))
                foreach (var descendant in Descendants(child))
                    yield return descendant;
        }

        private static List<JsonNode?> FilterByName(IEnumerable<JsonNode?> nodes, string name)
        {
            var result = new List<JsonNode?>();
            foreach (var node in nodes)
                if (node is JsonObject obj && obj.TryGetPropertyValue(name, out var value))
                    result.Add(value);
            return result;
        }
    }
}