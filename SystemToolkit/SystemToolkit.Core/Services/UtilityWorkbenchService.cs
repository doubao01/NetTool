using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SystemToolkit.Core.Services;

public sealed record UtilityTool(string Id, string Name, string Category, string Description, string Example, string OptionLabel, string DefaultOption);

public interface IUtilityWorkbenchService
{
    IReadOnlyList<UtilityTool> Tools { get; }
    string Execute(string id, string input, string option = "");
}

public sealed class UtilityWorkbenchService : IUtilityWorkbenchService
{
    public const int MaxInputBytes = 256 * 1024;
    public const int MaxOutputBytes = 4 * 1024 * 1024;
    public const int MaxItems = 10_000;
    public const int MaxDepth = 32;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = MaxDepth + 2,
    };
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    public IReadOnlyList<UtilityTool> Tools { get; } = Array.AsReadOnly(new[]
    {
        new UtilityTool("json-validate", "JSON 校验", "JSON 处理", "校验语法、重复键、深度及节点数量。", "{\"姓名\":\"小明\"}", "", ""),
        new UtilityTool("json-minify", "JSON 压缩", "JSON 处理", "去掉排版空白，保留数据与 Unicode。", "{ \"a\": 1 }", "", ""),
        new UtilityTool("json-sort", "JSON 键排序", "JSON 处理", "按 Ordinal 顺序递归排序对象键，保留数组顺序。", "{\"b\":2,\"a\":1}", "", ""),
        new UtilityTool("json-flatten", "JSON 指针展开", "JSON 处理", "输出 JSON Pointer 映射；根为 空字符串，每个容器以 {} 或 [] 标记，保留空容器。", "{\"a/b\":[1,{}]}", "", ""),
        new UtilityTool("json-unflatten", "JSON 指针还原", "JSON 处理", "还原指针映射；要求根、容器标记、连续数组索引，~0 表示 ~，~1 表示 /。", "{\"\":{},\"/a\":[],\"/a/0\":1}", "", ""),
        new UtilityTool("json-to-csv", "JSON 转 CSV", "数据交换", "对象数组转 CSV，取全部列；仅支持标量，缺失与 null 输出空字段。", "[{\"姓名\":\"小明\",\"年龄\":20}]", "", ""),
        new UtilityTool("csv-to-json", "CSV 转 JSON", "数据交换", "首行为唯一列名；支持引号与字段内换行，所有字段保留为字符串。", "姓名,年龄\r\n小明,20", "", ""),
        new UtilityTool("json-escape", "JSON 字符串转义", "文本编码", "把原文编码为带双引号的 JSON 字符串。", "你好\n世界", "", ""),
        new UtilityTool("json-unescape", "JSON 字符串还原", "文本编码", "解码单个带双引号的 JSON 字符串。", "\"你好\\n世界\"", "", ""),
        new UtilityTool("url-encode", "URL 组件编码", "文本编码", "以 UTF-8 百分号编码单个组件，空格编码为 %20。", "你好 a+b", "", ""),
        new UtilityTool("url-decode", "URL 组件解码", "文本编码", "严格解码百分号与 UTF-8，保留字面加号。", "%E4%BD%A0%E5%A5%BD%20a%2Bb", "", ""),
        new UtilityTool("query-parse", "查询参数解析", "URL 参数", "查询串转字符串数组对象，保留重复参数；加号按空格解码。", "q=hello+world&tag=a&tag=b", "", ""),
        new UtilityTool("query-build", "查询参数构建", "URL 参数", "字符串或非空字符串数组对象转查询串，重复值输出重复参数。", "{\"q\":\"你好\",\"tag\":[\"a\",\"b\"]}", "", ""),
        new UtilityTool("html-encode", "HTML 文本编码", "文本编码", "转义 HTML 文本特殊字符，保留 Unicode。", "<b>你好 & 世界</b>", "", ""),
        new UtilityTool("html-decode", "HTML 实体解码", "文本编码", "解码 HTML 实体；未知实体保留原文。", "&lt;b&gt;你好&lt;/b&gt;", "", ""),
        new UtilityTool("hex-encode", "UTF-8 转十六进制", "文本编码", "把 UTF-8 文本编码为大写十六进制字节。", "你好", "", ""),
        new UtilityTool("hex-decode", "十六进制转 UTF-8", "文本编码", "解码偶数位十六进制，严格验证 UTF-8。", "E4BDA0E5A5BD", "", ""),
        new UtilityTool("base64url-encode", "Base64URL 编码", "文本编码", "UTF-8 文本转无填充的 URL 安全 Base64。", "你好", "", ""),
        new UtilityTool("base64url-decode", "Base64URL 解码", "文本编码", "只接受规范的无填充 Base64URL，严格验证 UTF-8。", "5L2g5aW9", "", ""),
        new UtilityTool("unix-seconds-to-date", "秒时间戳转日期", "日期时间", "Unix 整数秒转 UTC ISO 8601 日期。", "0", "", ""),
        new UtilityTool("unix-ms-to-date", "毫秒时间戳转日期", "日期时间", "Unix 整数毫秒转 UTC ISO 8601 日期。", "1000", "", ""),
        new UtilityTool("date-to-unix-seconds", "日期转秒时间戳", "日期时间", "带 Z 或 ±HH:mm 时区的 ISO 8601 日期转 Unix 秒，向下取整。", "1970-01-01T00:00:01Z", "", ""),
        new UtilityTool("date-to-unix-ms", "日期转毫秒时间戳", "日期时间", "带 Z 或 ±HH:mm 时区的 ISO 8601 日期转 Unix 毫秒，向下取整。", "1970-01-01T00:00:00.123Z", "", ""),
        new UtilityTool("guid-batch", "批量 GUID", "随机生成", "生成随机 GUID，每行一个；输入须留空，数量 1 至 1000。", "", "数量（1-1000）", "1"),
        new UtilityTool("password-generate", "随机密码", "随机生成", "使用加密随机数，至少包含大写、小写、数字与符号；输入须留空。", "", "长度（4-1024）", "20"),
        new UtilityTool("integer-base", "整数进制转换", "数值转换", "有符号整数在 2 至 36 进制间转换，最多 4096 位，无进制前缀。", "FF", "源进制:目标进制", "16:10"),
        new UtilityTool("hex-to-rgb", "HEX 转 RGB", "颜色转换", "三位或六位十六进制颜色转十进制 RGB。", "#1A2B3C", "", ""),
        new UtilityTool("rgb-to-hex", "RGB 转 HEX", "颜色转换", "三个逗号分隔的 0 至 255 整数转六位 HEX。", "26,43,60", "", ""),
        new UtilityTool("gzip-compress", "GZip 文本压缩", "压缩转换", "UTF-8 文本压缩为标准 Base64 编码的 GZip 数据。", "你好，世界", "", ""),
        new UtilityTool("gzip-decompress", "GZip 文本解压", "压缩转换", "标准 Base64 GZip 解压为严格 UTF-8；校验尾部 CRC 与长度，输出最多 4 MiB。", "H4sIAAAAAAACAwMAAAAAAAAAAAA=", "", ""),
        new UtilityTool("text-stats", "文本统计", "文本处理", "输出字符数、UTF-8 字节数、字素簇数、行数、空白分词数与非空白字符数的 JSON。", "你好\nworld", "", ""),
        new UtilityTool("reverse-graphemes", "字素反转", "文本处理", "按字素簇（含代理对与组合字符）反转文本，避免拆散表情与重音。", "abc 你好", "", ""),
        new UtilityTool("rot13", "ROT13 变换", "文本处理", "对英文字母做 ROT13 循环移位，其余字符保持原样。", "Hello, World!", "", ""),
        new UtilityTool("morse-encode", "摩斯编码", "文本处理", "字母与数字转为摩斯电码，字母间以空格分隔，单词间以 / 分隔。", "SOS 1", "", ""),
        new UtilityTool("morse-decode", "摩斯解码", "文本处理", "摩斯电码还原为文本；/ 表示空格，无法识别的符号会报错。", "... --- ... / .----", "", ""),
        new UtilityTool("regex-escape", "正则转义", "文本处理", "转义正则元字符，使原文可安全嵌入正则表达式。", "a.b*（中文）", "", ""),
        new UtilityTool("text-diff", "文本差异", "文本处理", "以 --- 独立分隔行分左右两侧逐行 LCS 对比；参数为 0 输出全部差异行，1-20 输出带 @@ 头部的上下文片段。", "apple\nbanana\ncherry\n---\napple\nbanana\ncherry\ndate", "上下文行数（0-20）", "0"),
        new UtilityTool("hash-digest", "摘要哈希", "安全哈希", "MD5、SHA1、SHA256、SHA384 或 SHA512 十六进制摘要。", "hello", "算法（MD5/SHA1/SHA256/SHA384/SHA512）", "SHA256"),
        new UtilityTool("hmac-sha256", "HMAC 摘要", "安全哈希", "以 UTF-8 密钥计算 HMAC-SHA256 十六进制摘要。", "hello", "密钥（UTF-8）", "key"),
        new UtilityTool("jwt-decode", "JWT 解码", "安全哈希", "解码 JWT 的 header 与 payload（不校验签名）；两段须为规范 Base64URL 的 JSON 对象。", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IuWwj-aYjiIsImlhdCI6MTUxNjIzOTAyMn0.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk", "", ""),
        new UtilityTool("seconds-to-duration", "秒数转时长", "日期时间", "64 位整数秒转为 d HH:mm:ss 或 HH:mm:ss 时长，支持负数。", "90061", "", ""),
        new UtilityTool("byte-size", "字节换算", "数值转换", "按输入单位（1024 进制）换算字节数，自动选择最佳单位并保留两位小数。", "1536", "输入单位（B/KB/MB/GB/TB）", "KB"),
        new UtilityTool("bin-encode", "UTF-8 转二进制", "文本编码", "把 UTF-8 文本编码为空格分隔的 8 位二进制字节串。", "Hi", "", ""),
        new UtilityTool("bin-decode", "二进制转 UTF-8", "文本编码", "严格解码 8 位一组、空白分隔的二进制文本为 UTF-8。", "01001000 01101001", "", ""),
        new UtilityTool("crc32", "CRC32 校验", "安全哈希", "计算 UTF-8 文本的 CRC32（IEEE 反射多项式），输出 8 位大写十六进制；仅用于传输校验，不具备抗碰撞性。", "123456789", "", ""),
        new UtilityTool("base32-encode", "Base32 编码", "文本编码", "UTF-8 文本转 RFC 4648 Base32，使用 A-Z 与 2-7 字母表并按规范补位。", "Hello", "", ""),
        new UtilityTool("base32-decode", "Base32 解码", "文本编码", "严格解码 RFC 4648 Base32（忽略大小写，校验填充与未使用尾部位为零），输出 UTF-8 文本。", "JBSWY3DP", "", ""),
        new UtilityTool("number-format", "数字格式化", "数值转换", "按小数位四舍六入五取偶，可选千分位分组，固定输出不变文化小数点。", "1500.005", "分组(,或n):小数位(0-12)", ",:2"),
        new UtilityTool("rgb-to-hsl", "RGB 转 HSL", "颜色转换", "HEX 或逗号分隔 RGB 转 HSL：色相 0 至 360、饱和度与亮度 0 至 100 四舍六入取整。", "#8080FF", "", ""),
        new UtilityTool("hsl-to-rgb", "HSL 转 RGB", "颜色转换", "H、S、L 逗号分隔数值（可带度符与百分号）转六位大写 HEX 颜色。", "240,100,75", "", ""),
        new UtilityTool("text-case", "大小写风格", "文本处理", "在 camel、pascal、snake、kebab、title、upper 风格间转换，按单词边界重组。", "hello world_foo-bar", "目标风格", "camel"),
        new UtilityTool("password-strength", "密码强度", "安全哈希", "按长度、字符类别、同类连续与字符多样性给出弱、中、强评级与明细（本地启发式，非熵估计）。", "Abcd1234!", "", ""),
        new UtilityTool("csv-validate", "CSV 检查", "数据交换", "用与 CSV 转 JSON 相同的严格规则校验文本，输出行列数与各列去重值和空值统计。", "姓名,年龄\r\n小明,20\r\n小红,20", "", ""),
        new UtilityTool("cron-describe", "Cron 展开", "日期时间", "解析五段 Cron（分 时 日 月 周），支持范围、列表与步进，输出各字段允许值 JSON；周字段 7 归一为 0。", "*/5 9-18 * * 1-5", "", ""),
    });

    public string Execute(string id, string input, string option = "")
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(option);
        var tool = Tools.FirstOrDefault(t => t.Id == id)
            ?? throw new ArgumentException("未知的工具标识。", nameof(id));
        try
        {
            if (input.Length > MaxInputBytes || Utf8.GetByteCount(input) > MaxInputBytes)
                throw new ArgumentException("输入最多为 256 KiB UTF-8 文本。", nameof(input));
            if (option.Length > 64) throw new ArgumentException("参数长度最多为 64 个字符。", nameof(option));
            if (tool.OptionLabel.Length == 0 && option.Length != 0)
                throw new ArgumentException("此工具无需参数。", nameof(option));
            var parameter = option.Length == 0 ? tool.DefaultOption : option;
            var result = id switch
            {
                "json-validate" => JsonTransform(input, id),
                "json-minify" or "json-sort" or "json-flatten" or "json-unflatten" or
                "json-to-csv" or "json-unescape" or "query-build" => JsonTransform(input, id),
                "csv-to-json" => CsvToJson(input),
                "json-escape" => Serialize(input),
                "url-encode" => Uri.EscapeDataString(input),
                "url-decode" => DecodeUrl(input),
                "query-parse" => ParseQuery(input),
                "html-encode" => input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;"),
                "html-decode" => WebUtility.HtmlDecode(input),
                "hex-encode" => Convert.ToHexString(Utf8.GetBytes(input)),
                "hex-decode" => Utf8.GetString(Convert.FromHexString(input)),
                "base64url-encode" => Base64Url(Utf8.GetBytes(input)),
                "base64url-decode" => DecodeBase64Url(input),
                "unix-seconds-to-date" => DateTimeOffset.FromUnixTimeSeconds(ParseLong(input)).ToString("O", CultureInfo.InvariantCulture),
                "unix-ms-to-date" => DateTimeOffset.FromUnixTimeMilliseconds(ParseLong(input)).ToString("O", CultureInfo.InvariantCulture),
                "date-to-unix-seconds" => ParseDate(input).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                "date-to-unix-ms" => ParseDate(input).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
                "guid-batch" or "password-generate" => Generate(id, input, parameter),
                "integer-base" => ConvertBase(input, parameter),
                "hex-to-rgb" => HexToRgb(input),
                "rgb-to-hex" => RgbToHex(input),
                "gzip-compress" => Compress(input),
                "gzip-decompress" => Decompress(input),
                "text-stats" => TextStatistics(input),
                "reverse-graphemes" => ReverseGraphemes(input),
                "rot13" => Rot13(input),
                "morse-encode" => EncodeMorse(input),
                "morse-decode" => DecodeMorse(input),
                "regex-escape" => Regex.Escape(input),
                "text-diff" => TextDiff(input, parameter),
                "hash-digest" => HashDigest(input, parameter),
                "hmac-sha256" => Convert.ToHexString(HMACSHA256.HashData(Utf8.GetBytes(parameter), Utf8.GetBytes(input))),
                "jwt-decode" => DecodeJwt(input),
                "seconds-to-duration" => SecondsToDuration(input),
                "byte-size" => ByteSize(input, parameter),
                "bin-encode" => EncodeBinary(input),
                "bin-decode" => DecodeBinary(input),
                "crc32" => ComputeCrc32(input),
                "base32-encode" => EncodeBase32(Utf8.GetBytes(input)),
                "base32-decode" => Utf8.GetString(DecodeBase32(input)),
                "number-format" => NumberFormat(input, parameter),
                "rgb-to-hsl" => RgbToHsl(input),
                "hsl-to-rgb" => HslToRgb(input),
                "text-case" => ConvertTextCase(input, parameter),
                "password-strength" => PasswordStrength(input),
                "csv-validate" => CsvValidate(input),
                "cron-describe" => CronDescribe(input),
                _ => throw new ArgumentException("未知的工具标识。", nameof(id)),
            };
            if (Utf8.GetByteCount(result) > MaxOutputBytes) throw new ArgumentException("输出超过 4 MiB 上限。");
            return result;
        }
        catch (JsonException ex) { throw new FormatException("JSON 格式错误或嵌套过深：" + ex.Message, ex); }
        catch (DecoderFallbackException ex) { throw new FormatException("数据包含无效的 UTF-8 字节。", ex); }
        catch (EncoderFallbackException ex) { throw new FormatException("文本包含无效的 Unicode 代理字符。", ex); }
        catch (InvalidDataException ex) { throw new FormatException("GZip 数据无效或校验失败。", ex); }
        catch (OverflowException ex) { throw new ArgumentException("数值超出允许范围。", ex); }
        catch (ArgumentOutOfRangeException ex) { throw new ArgumentException("数值或日期超出允许范围。", ex); }
    }

    private static string Serialize<T>(T value)
    {
        using var output = new LimitedStream();
        JsonSerializer.Serialize(output, value, JsonOptions);
        return Utf8.GetString(output.GetBuffer(), 0, (int)output.Length);
    }

    private static string JsonTransform(string input, string id)
    {
        using var document = JsonDocument.Parse(input, new JsonDocumentOptions { MaxDepth = MaxDepth });
        var root = document.RootElement;
        var count = 0;
        ValidateJson(root, 0, ref count);
        switch (id)
        {
            case "json-validate": return "JSON 有效";
            case "json-minify": return Serialize(root);
            case "json-sort": return Serialize(SortJson(root));
            case "json-flatten":
                var pointers = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                Flatten(root, "", pointers);
                return Serialize(pointers);
            case "json-unflatten": return Serialize(Unflatten(root));
            case "json-to-csv": return JsonToCsv(root);
            case "query-build": return BuildQuery(root);
            case "json-unescape":
                if (root.ValueKind != JsonValueKind.String) throw new FormatException("输入必须是带双引号的 JSON 字符串。");
                return root.GetString()!;
            default: throw new ArgumentException("未知的 JSON 操作。");
        }
    }

    private static void ValidateJson(JsonElement value, int depth, ref int count)
    {
        if (++count > MaxItems || depth > MaxDepth) throw new ArgumentException("JSON 超过 10000 个节点或 32 层深度上限。");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new FormatException("JSON 对象包含重复键：" + property.Name);
                ValidateJson(property.Value, depth + 1, ref count);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) ValidateJson(child, depth + 1, ref count);
    }

    private static JsonNode? SortJson(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var result = new JsonObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                result.Add(property.Name, SortJson(property.Value));
            return result;
        }
        if (value.ValueKind == JsonValueKind.Array)
            return new JsonArray(value.EnumerateArray().Select(SortJson).ToArray());
        return JsonNode.Parse(value.GetRawText());
    }

    private static string EscapePointer(string key) => key.Replace("~", "~0").Replace("/", "~1");

    private static void Flatten(JsonElement value, string path, Dictionary<string, JsonElement> result)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            result.Add(path, JsonSerializer.SerializeToElement(new Dictionary<string, string>()));
            foreach (var property in value.EnumerateObject()) Flatten(property.Value, path + "/" + EscapePointer(property.Name), result);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            result.Add(path, JsonSerializer.SerializeToElement(Array.Empty<string>()));
            var index = 0;
            foreach (var child in value.EnumerateArray()) Flatten(child, path + "/" + (index++).ToString(CultureInfo.InvariantCulture), result);
        }
        else result.Add(path, value);
    }

    private static JsonNode? Unflatten(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("指针映射必须为 JSON 对象。");
        var entries = root.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        if (!entries.ContainsKey("")) throw new FormatException("指针映射缺少空字符串根节点。");
        var children = new Dictionary<string, List<(string Path, string Key)>>(StringComparer.Ordinal);
        foreach (var (path, value) in entries)
        {
            if (value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Any() ||
                value.ValueKind == JsonValueKind.Array && value.GetArrayLength() != 0)
                throw new FormatException("容器节点必须使用空对象 {} 或空数组 [] 标记。");
            if (path.Length == 0) continue;
            if (path[0] != '/' || path.Count(c => c == '/') > MaxDepth) throw new FormatException("JSON Pointer 必须以 / 开头且深度最多 32 层。");
            var slash = path.LastIndexOf('/');
            var parent = path[..slash];
            var token = path[(slash + 1)..];
            var key = token.Replace("~1", "/").Replace("~0", "~");
            if (EscapePointer(key) != token) throw new FormatException("JSON Pointer 含非法 ~ 转义。");
            if (!entries.TryGetValue(parent, out var parentValue) ||
                parentValue.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                throw new FormatException("每个指针必须具有已声明的父容器。");
            if (!children.TryGetValue(parent, out var list)) children[parent] = list = new();
            list.Add((path, key));
        }

        JsonNode? Build(string path)
        {
            var value = entries[path];
            var list = children.GetValueOrDefault(path) ?? new();
            if (value.ValueKind == JsonValueKind.Object)
            {
                var obj = new JsonObject();
                foreach (var child in list) obj.Add(child.Key, Build(child.Path));
                return obj;
            }
            if (value.ValueKind == JsonValueKind.Array)
            {
                var indexed = new SortedDictionary<int, string>();
                foreach (var child in list)
                {
                    if (!int.TryParse(child.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
                        index < 0 || index >= MaxItems || index.ToString(CultureInfo.InvariantCulture) != child.Key)
                        throw new FormatException("数组索引必须是无前导零的非负整数，且小于 10000。");
                    indexed.Add(index, child.Path);
                }
                var array = new JsonArray();
                foreach (var pair in indexed)
                {
                    if (pair.Key != array.Count) throw new FormatException("数组索引必须从 0 连续排列。");
                    array.Add(Build(pair.Value));
                }
                return array;
            }
            return JsonNode.Parse(value.GetRawText());
        }
        return Build("");
    }

    private static string JsonToCsv(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) throw new FormatException("输入必须为 JSON 对象数组。");
        if (root.GetArrayLength() == 0) return "";
        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in root.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) throw new FormatException("CSV 的每行必须为 JSON 对象。");
            foreach (var property in row.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) throw new FormatException("CSV 字段仅支持字符串、数字、布尔值或 null。");
                if (seen.Add(property.Name)) columns.Add(property.Name);
            }
        }
        if (columns.Count == 0) throw new FormatException("非空数组至少需要一个列名。");
        if ((long)columns.Count * (root.GetArrayLength() + 1) > MaxItems) throw new ArgumentException("CSV 最多包含 10000 个单元格（含表头）。");
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var result = new StringBuilder(string.Join(",", columns.Select(Quote)));
        foreach (var row in root.EnumerateArray())
        {
            result.Append("\r\n");
            result.AppendJoin(",", columns.Select(column =>
            {
                if (!row.TryGetProperty(column, out var value) || value.ValueKind == JsonValueKind.Null) return Quote("");
                return Quote(value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());
            }));
        }
        return result.ToString();
    }

    private static List<List<string>> ParseCsvRows(string input)
    {
        var rows = new List<List<string>>();
        if (input.Length == 0) return rows;
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closed = false;
        var cells = 0;
        void FinishField()
        {
            if (++cells > MaxItems) throw new ArgumentException("CSV 最多包含 10000 个单元格（含表头）。");
            row.Add(field.ToString());
            field.Clear();
            closed = false;
        }
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (quoted)
            {
                if (c != '"') field.Append(c);
                else if (i + 1 < input.Length && input[i + 1] == '"') { field.Append('"'); i++; }
                else { quoted = false; closed = true; }
            }
            else if (c is ',' or '\r' or '\n')
            {
                FinishField();
                if (c != ',')
                {
                    if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n') i++;
                    rows.Add(row);
                    row = new();
                }
            }
            else if (closed) throw new FormatException("CSV 闭合引号后只能接分隔符或换行。");
            else if (c == '"')
            {
                if (field.Length != 0) throw new FormatException("CSV 字段中的引号必须使用成对引号转义。");
                quoted = true;
            }
            else field.Append(c);
        }
        if (quoted) throw new FormatException("CSV 存在未闭合的引号。");
        if (closed || field.Length > 0 || row.Count > 0 || input[^1] == ',')
        {
            FinishField();
            rows.Add(row);
        }
        return rows;
    }

    private static string CsvToJson(string input)
    {
        var rows = ParseCsvRows(input);
        if (rows.Count == 0) return "[]";
        var headers = rows[0];
        if (headers.Distinct(StringComparer.Ordinal).Count() != headers.Count) throw new FormatException("CSV 存在重复列名。");
        var objects = new List<Dictionary<string, string>>();
        foreach (var data in rows.Skip(1))
        {
            if (data.Count != headers.Count) throw new FormatException("CSV 数据行的列数必须与表头一致。");
            objects.Add(headers.Zip(data).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal));
        }
        return Serialize(objects);
    }

    private static string DecodeUrl(string input)
    {
        using var bytes = new MemoryStream();
        for (var i = 0; i < input.Length;)
        {
            if (input[i] == '%')
            {
                if (i + 2 >= input.Length || !byte.TryParse(input.AsSpan(i + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                    throw new FormatException("URL 百分号后必须紧跟两位十六进制数字。");
                bytes.WriteByte(value);
                i += 3;
            }
            else
            {
                var end = input.IndexOf('%', i);
                if (end < 0) end = input.Length;
                bytes.Write(Utf8.GetBytes(input[i..end]));
                i = end;
            }
        }
        return Utf8.GetString(bytes.GetBuffer(), 0, (int)bytes.Length);
    }

    private static string ParseQuery(string input)
    {
        if (input.StartsWith('?')) input = input[1..];
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (input.Length == 0) return "{}";
        var count = 0;
        foreach (var pair in input.Split('&'))
        {
            if (++count > MaxItems) throw new ArgumentException("查询参数最多为 10000 项。");
            if (pair.Length == 0) throw new FormatException("查询串包含空参数段。");
            var separator = pair.IndexOf('=');
            var key = DecodeUrl((separator < 0 ? pair : pair[..separator]).Replace('+', ' '));
            var value = separator < 0 ? "" : DecodeUrl(pair[(separator + 1)..].Replace('+', ' '));
            if (!result.TryGetValue(key, out var values)) result[key] = values = new();
            values.Add(value);
        }
        return Serialize(result);
    }

    private static string BuildQuery(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("查询参数必须是 JSON 对象。");
        var pairs = new List<string>();
        foreach (var property in root.EnumerateObject())
        {
            var values = property.Value.ValueKind == JsonValueKind.Array
                ? property.Value.EnumerateArray().ToArray() : new[] { property.Value };
            if (values.Length == 0) throw new FormatException("查询参数数组必须至少包含一个字符串。");
            foreach (var value in values)
            {
                if (value.ValueKind != JsonValueKind.String) throw new FormatException("查询参数值必须为字符串或字符串数组。");
                pairs.Add(Uri.EscapeDataString(property.Name) + "=" + Uri.EscapeDataString(value.GetString()!));
            }
        }
        return string.Join("&", pairs);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string DecodeBase64Url(string input)
    {
        if (input.Any(c => !(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')) || input.Length % 4 == 1)
            throw new FormatException("Base64URL 只接受无填充的字母、数字、- 和 _，且长度模 4 不能为 1。");
        var bytes = Convert.FromBase64String(input.Replace('-', '+').Replace('_', '/') + new string('=', (4 - input.Length % 4) % 4));
        if (Base64Url(bytes) != input) throw new FormatException("Base64URL 含非零的未使用尾部位。");
        return Utf8.GetString(bytes);
    }

    private static long ParseLong(string input)
    {
        if (!long.TryParse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)) throw new FormatException("时间戳必须为 64 位十进制整数。");
        return value;
    }

    private static DateTimeOffset ParseDate(string input)
    {
        string[] formats = { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz" };
        if (!DateTimeOffset.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            throw new FormatException("日期必须为 ISO 8601，包含秒及 Z 或 ±HH:mm 时区，支持 1 至 7 位小数秒。");
        return date;
    }

    private static int BoundedInt(string value, int min, int max)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < min || result > max)
            throw new ArgumentException($"参数必须为 {min} 至 {max} 的整数。");
        return result;
    }

    private static string Generate(string id, string input, string option)
    {
        if (input.Length != 0) throw new ArgumentException("随机生成工具的输入须留空。");
        if (id == "guid-batch") return string.Join("\n", Enumerable.Range(0, BoundedInt(option, 1, 1000)).Select(_ => Guid.NewGuid().ToString("D")));
        var length = BoundedInt(option, 4, 1024);
        string[] groups = { "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "abcdefghijklmnopqrstuvwxyz", "0123456789", "!@#$%^&*()-_=+" };
        var alphabet = string.Concat(groups);
        var password = new char[length];
        for (var i = 0; i < groups.Length; i++) password[i] = groups[i][RandomNumberGenerator.GetInt32(groups[i].Length)];
        for (var i = groups.Length; i < length; i++) password[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }
        return new string(password);
    }

    private static string ConvertBase(string input, string option)
    {
        var parts = option.Split(':');
        if (parts.Length != 2) throw new ArgumentException("进制参数格式为 源进制:目标进制。");
        var source = BoundedInt(parts[0], 2, 36);
        var target = BoundedInt(parts[1], 2, 36);
        var negative = input.StartsWith('-');
        var digits = input.StartsWith('+') || negative ? input[1..] : input;
        if (digits.Length is 0 or > 4096) throw new ArgumentException("整数必须包含 1 至 4096 位数字。");
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        BigInteger number = 0;
        foreach (var c in digits)
        {
            var digit = alphabet.IndexOf(char.ToUpperInvariant(c));
            if (digit < 0 || digit >= source) throw new FormatException("整数包含不属于源进制的数字。");
            number = number * source + digit;
        }
        if (number.IsZero) return "0";
        var result = new StringBuilder();
        while (number > 0)
        {
            number = BigInteger.DivRem(number, target, out var remainder);
            result.Append(alphabet[(int)remainder]);
        }
        if (negative) result.Append('-');
        return new string(result.ToString().Reverse().ToArray());
    }

    private static string HexToRgb(string input)
    {
        var hex = input.StartsWith('#') ? input[1..] : input;
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6) throw new FormatException("HEX 颜色必须为三位或六位十六进制数。");
        var bytes = Convert.FromHexString(hex);
        return string.Join(",", bytes.Select(b => b.ToString(CultureInfo.InvariantCulture)));
    }

    private static string RgbToHex(string input)
    {
        var parts = input.Split(',');
        if (parts.Length != 3) throw new FormatException("RGB 必须为三个逗号分隔的整数。");
        return "#" + string.Concat(parts.Select(p => BoundedInt(p.Trim(), 0, 255).ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static string Compress(string input)
    {
        // GZipStream 在零输入且从未写入时不会输出头部，需显式返回合法空成员。
        if (input.Length == 0) return EmptyGzipBase64;
        using var output = new LimitedStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true)) gzip.Write(Utf8.GetBytes(input));
        return Convert.ToBase64String(output.GetBuffer(), 0, (int)output.Length);
    }

    private const string EmptyGzipBase64 = "H4sIAAAAAAACAwMAAAAAAAAAAAA=";

    private static string Decompress(string input)
    {
        var bytes = Convert.FromBase64String(input);
        if (bytes.Length < 20 || bytes[0] != 0x1f || bytes[1] != 0x8b || bytes[2] != 8 || (bytes[3] & 0xe0) != 0)
            throw new FormatException("输入必须包含完整的 GZip 头部、压缩数据与尾部。");
        using var source = new MemoryStream(bytes);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var output = new LimitedStream();
        var buffer = new byte[8192];
        int read;
        uint crc = uint.MaxValue;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
            for (var i = 0; i < read; i++)
            {
                crc ^= buffer[i];
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
            }
        }
        var expectedCrc = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length - 8));
        var expectedLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length - 4));
        if (~crc != expectedCrc || output.Length != expectedLength) throw new FormatException("GZip 尾部校验失败；请使用单个完整的 GZip 数据成员。");
        return Utf8.GetString(output.GetBuffer(), 0, (int)output.Length);
    }

    private static string TextStatistics(string input)
    {
        var bytes = Utf8.GetBytes(input);
        var lines = input.Length == 0 ? 0 : 1 + input.Count(c => c == '\n');
        var nonWhitespace = input.Length - input.Count(char.IsWhiteSpace);
        var words = 0;
        var inWord = false;
        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c)) inWord = false;
            else if (!inWord)
            {
                inWord = true;
                words++;
            }
        }
        return Serialize(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["characters"] = input.Length,
            ["bytes"] = bytes.Length,
            ["graphemes"] = CountGraphemes(input),
            ["lines"] = lines,
            ["words"] = words,
            ["nonWhitespace"] = nonWhitespace,
        });
    }

    private static int CountGraphemes(string input)
    {
        var count = 0;
        for (var i = 0; i < input.Length; i = NextClusterEnd(input, i)) count++;
        return count;
    }

    private static int NextClusterEnd(string input, int index)
    {
        var i = index + 1;
        while (i < input.Length && IsClusterContinuation(input[i])) i++;
        return i;
    }

    private static bool IsClusterContinuation(char c) =>
        char.IsLowSurrogate(c) ||
        c is '\u200d' or '\ufe0f' ||
        char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.ModifierSymbol;

    private static string ReverseGraphemes(string input)
    {
        var clusters = new List<string>(input.Length);
        for (var i = 0; i < input.Length;)
        {
            var end = NextClusterEnd(input, i);
            clusters.Add(input[i..end]);
            i = end;
        }
        clusters.Reverse();
        return string.Concat(clusters);
    }

    private static string Rot13(string input)
    {
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                >= 'a' and <= 'z' => (char)('a' + (chars[i] - 'a' + 13) % 26),
                >= 'A' and <= 'Z' => (char)('A' + (chars[i] - 'A' + 13) % 26),
                _ => chars[i],
            };
        }
        return new string(chars);
    }

    private static readonly string[] MorseTable =
    {
        ".-", "-...", "-.-.", "-..", ".", "..-.", "--.", "....", "..", ".---", "-.-", ".-..", "--",
        "-.", "---", ".--.", "--.-", ".-.", "...", "-", "..-", "...-", ".--", "-..-", "-.--", "--..",
        "-----", ".----", "..---", "...--", "....-", ".....", "-....", "--...", "---..", "----.",
    };

    private static string EncodeMorse(string input)
    {
        var words = input.Split('\n');
        var lines = new List<string>(words.Length);
        foreach (var word in words)
        {
            var letters = new List<string>();
            foreach (var c in word)
            {
                if (c == ' ') letters.Add("/");
                else if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
                    letters.Add(MorseTable[c is >= 'a' and <= 'z' ? c - 'a' : c is >= 'A' and <= 'Z' ? c - 'A' : c - '0' + 26]);
                else throw new FormatException("摩斯编码仅支持英文字母、数字与空格。");
            }
            lines.Add(string.Join(" ", letters));
        }
        return string.Join("\n", lines);
    }

    private static string DecodeMorse(string input)
    {
        var words = input.Split('\n');
        var lines = new List<string>(words.Length);
        foreach (var word in words)
        {
            var result = new StringBuilder();
            foreach (var token in word.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "/") result.Append(' ');
                else
                {
                    var index = Array.IndexOf(MorseTable, token);
                    if (index < 0) throw new FormatException("存在无法识别的摩斯符号：" + token);
                    result.Append(index < 26 ? (char)('A' + index) : (char)('0' + index - 26));
                }
            }
            lines.Add(result.ToString());
        }
        return string.Join("\n", lines);
    }

    private const int DiffMaxLines = 2000;

    private static string TextDiff(string input, string option)
    {
        const string separator = "---";
        string leftText, rightText;
        if (input == separator) { leftText = ""; rightText = ""; }
        else if (input.StartsWith(separator + "\n", StringComparison.Ordinal)) { leftText = ""; rightText = input[(separator.Length + 1)..]; }
        else if (input.EndsWith("\n" + separator, StringComparison.Ordinal)) { leftText = input[..^3]; rightText = ""; }
        else
        {
            var position = input.IndexOf("\n" + separator + "\n", StringComparison.Ordinal);
            if (position < 0) { leftText = input; rightText = ""; }
            else { leftText = input[..position]; rightText = input[(position + separator.Length + 2)..]; }
        }
        var context = BoundedInt(option, 0, 20);
        var left = leftText.Replace("\r\n", "\n").Split('\n');
        var right = rightText.Replace("\r\n", "\n").Split('\n');
        if (left.Length + right.Length > DiffMaxLines) throw new ArgumentException($"差异文本两侧合计最多 {DiffMaxLines} 行。");

        // LCS 矩阵按逆序填充，matrix[i, j] 为后缀 left[i..] 与 right[j..] 的最长公共子序列长度。
        var matrix = new int[left.Length + 1, right.Length + 1];
        for (var i = left.Length - 1; i >= 0; i--)
            for (var j = right.Length - 1; j >= 0; j--)
                matrix[i, j] = left[i] == right[j] ? matrix[i + 1, j + 1] + 1 : Math.Max(matrix[i + 1, j], matrix[i, j + 1]);

        var hunks = new List<(int Left, int Right, char Kind, string Text)>();
        int a = 0, b = 0;
        while (a < left.Length && b < right.Length)
        {
            if (left[a] == right[b]) { hunks.Add((a + 1, b + 1, ' ', left[a])); a++; b++; }
            else if (matrix[a + 1, b] >= matrix[a, b + 1]) { hunks.Add((a + 1, b + 1, '-', left[a])); a++; }
            else { hunks.Add((a + 1, b + 1, '+', right[b])); b++; }
        }
        while (a < left.Length) hunks.Add((++a, right.Length + 1, '-', left[a - 1]));
        while (b < right.Length) hunks.Add((left.Length + 1, ++b, '+', right[b - 1]));

        if (hunks.All(h => h.Kind == ' ')) return "文本相同";
        if (context == 0) return string.Join("\n", hunks.Where(h => h.Kind != ' ').Select(h => h.Kind + " " + h.Text));

        var result = new StringBuilder();
        var shown = new bool[hunks.Count];
        for (var i = 0; i < hunks.Count; i++)
        {
            if (hunks[i].Kind == ' ') continue;
            for (var j = Math.Max(0, i - context); j <= Math.Min(hunks.Count - 1, i + context); j++) shown[j] = true;
        }
        var printed = false;
        var cursor = 0;
        while (cursor < hunks.Count)
        {
            if (!shown[cursor])
            {
                cursor++;
                continue;
            }
            var start = cursor;
            while (cursor < hunks.Count && shown[cursor]) cursor++;
            if (printed) result.Append('\n');
            result.Append($"@@ 左侧 {hunks[start].Left} / 右侧 {hunks[start].Right} @@\n");
            for (var k = start; k < cursor; k++)
            {
                result.Append(hunks[k].Kind).Append(' ').Append(hunks[k].Text);
                if (k < cursor - 1) result.Append('\n');
            }
            printed = true;
        }
        return result.ToString();
    }

    private static string HashDigest(string input, string algorithm) => algorithm.ToUpperInvariant() switch
    {
        "MD5" => Convert.ToHexString(MD5.HashData(Utf8.GetBytes(input))),
        "SHA1" => Convert.ToHexString(SHA1.HashData(Utf8.GetBytes(input))),
        "SHA256" => Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(input))),
        "SHA384" => Convert.ToHexString(SHA384.HashData(Utf8.GetBytes(input))),
        "SHA512" => Convert.ToHexString(SHA512.HashData(Utf8.GetBytes(input))),
        _ => throw new ArgumentException("算法必须为 MD5、SHA1、SHA256、SHA384 或 SHA512。"),
    };

    private static string DecodeJwt(string input)
    {
        var parts = input.Split('.');
        if (parts.Length != 3) throw new FormatException("JWT 必须包含三段，以点号分隔。");
        var header = JsonSerializer.Deserialize<JsonObject>(Utf8.GetBytes(DecodeBase64Url(parts[0])), JsonOptions)
            ?? throw new FormatException("JWT header 必须为 JSON 对象。");
        var payload = JsonSerializer.Deserialize<JsonObject>(Utf8.GetBytes(DecodeBase64Url(parts[1])), JsonOptions)
            ?? throw new FormatException("JWT payload 必须为 JSON 对象。");
        return Serialize(new JsonObject { ["header"] = header, ["payload"] = payload });
    }

    private static string SecondsToDuration(string input)
    {
        var seconds = ParseLong(input);
        var negative = seconds < 0;
        var value = Math.Abs(seconds);
        var days = value / 86400;
        var hours = value % 86400 / 3600;
        var minutes = value % 3600 / 60;
        var rest = value % 60;
        var clock = $"{hours:D2}:{minutes:D2}:{rest:D2}";
        return (negative ? "-" : "") + (days > 0 ? days.ToString(CultureInfo.InvariantCulture) + " 天 " + clock : clock);
    }

    private static string ByteSize(string input, string unit)
    {
        var value = ParseLong(input);
        if (value < 0) throw new FormatException("字节数不能为负。");
        var source = unit.ToUpperInvariant() switch
        {
            "B" => 1L,
            "KB" => 1024L,
            "MB" => 1024L * 1024,
            "GB" => 1024L * 1024 * 1024,
            "TB" => 1024L * 1024 * 1024 * 1024,
            _ => throw new ArgumentException("单位必须为 B、KB、MB、GB 或 TB。"),
        };
        if (value > long.MaxValue / source) throw new ArgumentException("字节数超出换算范围。");
        var bytes = value * source;
        string[] names = { "B", "KB", "MB", "GB", "TB", "PB" };
        double best = bytes;
        var index = 0;
        while (best >= 1024 && index < names.Length - 1)
        {
            best /= 1024;
            index++;
        }
        return index == 0
            ? bytes.ToString(CultureInfo.InvariantCulture) + " B"
            : best.ToString("0.##", CultureInfo.InvariantCulture) + " " + names[index];
    }

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var value = (uint)i;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            table[i] = value;
        }
        return table;
    }

    private static string ComputeCrc32(string input)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in Utf8.GetBytes(input))
            crc = Crc32Table[(byte)(crc ^ b)] ^ (crc >> 8);
        return ((crc ^ 0xFFFFFFFFu)).ToString("X8", CultureInfo.InvariantCulture);
    }

    private static string EncodeBinary(string input) =>
        string.Join(' ', Utf8.GetBytes(input).Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));

    private static string DecodeBinary(string input)
    {
        if (input.Length == 0) return "";
        var tokens = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Length != 8 || tokens[i].Any(c => c is not ('0' or '1')))
                throw new FormatException("二进制必须为 8 位一组、空白分隔的 0 与 1 串。");
            bytes[i] = Convert.ToByte(tokens[i], 2);
        }
        return Utf8.GetString(bytes);
    }

    private static string EncodeBase32(byte[] bytes)
    {
        var result = new StringBuilder();
        var bits = 0;
        var value = 0;
        foreach (var b in bytes)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                result.Append(Base32Alphabet[(value >> bits) & 31]);
            }
        }
        if (bits > 0) result.Append(Base32Alphabet[(value << (5 - bits)) & 31]);
        return result.ToString().PadRight((result.Length + 7) / 8 * 8, '=');
    }

    private static byte[] DecodeBase32(string input)
    {
        var upper = input.ToUpperInvariant();
        if (upper.Any(c => !(c is >= 'A' and <= 'Z' or >= '2' and <= '7' or '=')))
            throw new FormatException("Base32 只接受 A 至 Z、2 至 7 与结尾填充等号。");
        var data = upper.TrimEnd('=');
        if (data.Contains('=') || data.Length % 8 is 1 or 3 or 6)
            throw new FormatException("Base32 长度或填充无效。");
        var bytes = new List<byte>();
        var bits = 0;
        var value = 0;
        foreach (var c in data)
        {
            value = (value << 5) | Base32Alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((value >> bits) & 0xFF));
                value &= (1 << bits) - 1;
            }
        }
        var result = bytes.ToArray();
        if (EncodeBase32(result) != upper) throw new FormatException("Base32 含非零的未使用尾部位或填充不规范。");
        return result;
    }

    private static string NumberFormat(string input, string option)
    {
        var parts = option.Split(':');
        if (parts.Length != 2) throw new ArgumentException("格式化参数格式为 分组(,或n):小数位(0-12)。");
        var group = parts[0] switch
        {
            "," => true,
            "n" => false,
            _ => throw new ArgumentException("分组参数必须为 , 或 n。"),
        };
        var digits = BoundedInt(parts[1], 0, 12);
        if (input.Length > 4096) throw new ArgumentException("数字文本最多 4096 个字符。");
        if (!decimal.TryParse(input, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            throw new FormatException("数字必须为不含千分位与指数的十进制文本。");
        var text = Math.Round(value, digits, MidpointRounding.ToEven).ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var negative = text.StartsWith('-');
        var body = negative ? text[1..] : text;
        var dot = body.IndexOf('.');
        var integer = dot < 0 ? body : body[..dot];
        var fraction = dot < 0 ? "" : body[(dot + 1)..];
        if (group)
        {
            var grouped = new StringBuilder();
            for (var i = 0; i < integer.Length; i++)
            {
                if (i > 0 && (integer.Length - i) % 3 == 0) grouped.Append(',');
                grouped.Append(integer[i]);
            }
            integer = grouped.ToString();
        }
        return (negative ? "-" : "") + integer + (digits > 0 ? "." + fraction : "");
    }

    private static (int R, int G, int B) ParseColorRgb(string input)
    {
        if (input.Contains(','))
        {
            var parts = input.Split(',');
            if (parts.Length != 3) throw new FormatException("RGB 必须为三个逗号分隔的 0 至 255 整数。");
            return (BoundedInt(parts[0].Trim(), 0, 255), BoundedInt(parts[1].Trim(), 0, 255), BoundedInt(parts[2].Trim(), 0, 255));
        }
        var hex = input.StartsWith('#') ? input[1..] : input;
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6) throw new FormatException("颜色必须为三位或六位 HEX，或逗号分隔的 RGB。");
        var bytes = Convert.FromHexString(hex);
        return (bytes[0], bytes[1], bytes[2]);
    }

    private static string RgbToHsl(string input)
    {
        var (r, g, b) = ParseColorRgb(input);
        double rf = r / 255d, gf = g / 255d, bf = b / 255d;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var delta = max - Math.Min(rf, Math.Min(gf, bf));
        var lightness = (max + Math.Min(rf, Math.Min(gf, bf))) / 2;
        int hue;
        int saturation;
        if (delta == 0)
        {
            hue = 0;
            saturation = 0;
        }
        else
        {
            saturation = (int)Math.Round(delta / (1 - Math.Abs((2 * lightness) - 1)) * 100, MidpointRounding.ToEven);
            var hueRaw = max == rf ? ((gf - bf) / delta) % 6 : max == gf ? ((bf - rf) / delta) + 2 : ((rf - gf) / delta) + 4;
            hue = (int)(Math.Round(hueRaw * 60, MidpointRounding.ToEven) % 360);
            if (hue < 0) hue += 360;
        }
        var lightPercent = (int)Math.Round(lightness * 100, MidpointRounding.ToEven);
        return hue + "," + saturation + "," + lightPercent;
    }

    private static string HslToRgb(string input)
    {
        var parts = input.Trim().Split(',');
        if (parts.Length != 3) throw new FormatException("HSL 必须为 H,S,L 三个逗号分隔的数值。");
        var hue = ParsePlainDouble(parts[0].Trim().TrimEnd('%', '°'), "色相");
        var saturation = ParsePlainDouble(parts[1].Trim().TrimEnd('%', '°'), "饱和度");
        var lightness = ParsePlainDouble(parts[2].Trim().TrimEnd('%', '°'), "亮度");
        if (hue is < 0 or > 360) throw new ArgumentException("色相必须为 0 至 360。");
        if (saturation is < 0 or > 100 || lightness is < 0 or > 100) throw new ArgumentException("饱和度与亮度必须为 0 至 100。");
        var sf = saturation / 100;
        var lf = lightness / 100;
        var chroma = (1 - Math.Abs((2 * lf) - 1)) * sf;
        var huePrime = (hue % 360) / 60;
        var x = chroma * (1 - Math.Abs((huePrime % 2) - 1));
        var (rp, gp, bp) = huePrime switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        var m = lf - (chroma / 2);
        int Channel(double v) => (int)Math.Round(Math.Clamp((v + m) * 255, 0, 255), MidpointRounding.ToEven);
        return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", Channel(rp), Channel(gp), Channel(bp));
    }

    private static double ParsePlainDouble(string value, string name)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || double.IsNaN(result) || double.IsInfinity(result))
            throw new FormatException(name + "必须为十进制数值。");
        return result;
    }

    private static List<string> SplitWords(string input)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var previous = '\0';
        var hasPrevious = false;
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (hasPrevious && char.IsUpper(c) && char.IsLower(previous))
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
                current.Append(char.ToLowerInvariant(c));
                previous = c;
                hasPrevious = true;
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
                hasPrevious = false;
            }
            else hasPrevious = false;
        }
        if (current.Length > 0) words.Add(current.ToString());
        return words;
    }

    private static string ConvertTextCase(string input, string style)
    {
        var words = SplitWords(input);
        if (words.Count == 0) return "";
        static string Capitalize(string word) => char.ToUpperInvariant(word[0]) + word[1..];
        return style.ToLowerInvariant() switch
        {
            "camel" => string.Concat(words.Select((word, index) => index == 0 ? word : Capitalize(word))),
            "pascal" => string.Concat(words.Select(Capitalize)),
            "snake" => string.Join('_', words),
            "kebab" => string.Join('-', words),
            "title" => string.Join(' ', words.Select(Capitalize)),
            "upper" => string.Join(' ', words.Select(word => word.ToUpperInvariant())),
            _ => throw new ArgumentException("风格必须为 camel、pascal、snake、kebab、title 或 upper。"),
        };
    }

    private static string PasswordStrength(string input)
    {
        if (input.Length == 0) throw new ArgumentException("请输入待评估的密码文本。");
        static int CharClass(char c) => char.IsUpper(c) ? 0 : char.IsLower(c) ? 1 : char.IsDigit(c) ? 2 : 3;
        var classFlags = new bool[4];
        var longestRun = 0;
        var run = 0;
        var previous = -1;
        foreach (var c in input)
        {
            var cls = CharClass(c);
            classFlags[cls] = true;
            run = cls == previous ? run + 1 : 1;
            previous = cls;
            if (run > longestRun) longestRun = run;
        }
        var classes = classFlags.Count(flag => flag);
        var distinct = input.Distinct().Count();
        var score = 0;
        if (input.Length >= 8 && classes >= 2) score++;
        if (input.Length >= 12 && classes >= 3) score++;
        if (input.Length >= 16 && classes >= 4) score++;
        if (input.Length >= 20 && classes >= 2 && distinct >= 12) score++;
        if (longestRun >= 5) score--;
        var level = score <= 0 ? "弱" : score <= 2 ? "中" : "强";
        return $"评级：{level}；长度：{input.Length}；字符类别：{classes}；最长同类连续：{longestRun}；去重字符数：{distinct}";
    }

    private static string CsvValidate(string input)
    {
        var rows = ParseCsvRows(input);
        var headers = rows.Count == 0 ? new List<string>() : rows[0];
        if (headers.Distinct(StringComparer.Ordinal).Count() != headers.Count) throw new FormatException("CSV 存在重复列名。");
        foreach (var data in rows.Skip(1))
            if (data.Count != headers.Count) throw new FormatException("CSV 数据行的列数必须与表头一致。");
        var dataRows = rows.Skip(1).ToList();
        var distinct = new Dictionary<string, int>(StringComparer.Ordinal);
        var empty = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            var column = dataRows.Select(row => row[i]);
            distinct[headers[i]] = column.Distinct(StringComparer.Ordinal).Count();
            empty[headers[i]] = column.Count(value => value.Length == 0);
        }
        return Serialize(new { rowCount = dataRows.Count, columnCount = headers.Count, headers, distinctValueCount = distinct, emptyCellCount = empty });
    }

    private static string CronDescribe(string input)
    {
        var fields = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5) throw new FormatException("Cron 必须为空白分隔的五段：分 时 日 月 周。");
        (int Min, int Max)[] limits = { (0, 59), (0, 23), (1, 31), (1, 12), (0, 7) };
        string[] names = { "分", "时", "日", "月", "周" };
        var result = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        for (var position = 0; position < 5; position++)
            result[names[position]] = ExpandCronField(fields[position], limits[position].Min, limits[position].Max, position == 4, position);
        return Serialize(result);
    }

    private static IReadOnlyList<int> ExpandCronField(string expr, int min, int max, bool isDow, int position)
    {
        var values = new SortedSet<int>();
        foreach (var part in expr.Split(','))
        {
            string basis;
            var step = 1;
            var slash = part.IndexOf('/');
            if (slash >= 0)
            {
                basis = part[..slash];
                step = BoundedInt(part[(slash + 1)..], 1, max);
                if (basis != "*" && !basis.Contains('-')) throw new FormatException($"第 {position + 1} 段的步进只能用于 * 或范围。");
            }
            else basis = part;
            if (basis.Length == 0) throw new FormatException($"第 {position + 1} 段存在空白项。");
            int start;
            int end;
            if (basis == "*")
            {
                start = min;
                end = max;
            }
            else if (basis.Contains('-'))
            {
                var dash = basis.Split('-');
                if (dash.Length != 2) throw new FormatException($"第 {position + 1} 段的范围格式无效。");
                start = BoundedInt(dash[0], min, max);
                end = BoundedInt(dash[1], min, max);
                if (start > end) throw new FormatException($"第 {position + 1} 段范围的起始不能大于结束。");
            }
            else
            {
                start = BoundedInt(basis, min, max);
                end = start;
            }
            for (var value = start; value <= end; value += step) values.Add(value);
        }
        var list = values.ToList();
        if (isDow && list.Remove(7) && !list.Contains(0))
        {
            list.Add(0);
            list.Sort();
        }
        return list;
    }

    // Bound writes before allocating output for JSON expansion or compressed data.
    private sealed class LimitedStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Position + count > MaxOutputBytes) throw new ArgumentException("输出超过 4 MiB 上限。");
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (Position + buffer.Length > MaxOutputBytes) throw new ArgumentException("输出超过 4 MiB 上限。");
            base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            if (Position >= MaxOutputBytes) throw new ArgumentException("输出超过 4 MiB 上限。");
            base.WriteByte(value);
        }
    }
}
