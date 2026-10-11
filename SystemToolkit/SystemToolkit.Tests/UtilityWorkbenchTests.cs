using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using SystemToolkit.Core.Services;
using Xunit;

namespace SystemToolkit.Tests;

public class UtilityWorkbenchTests
{
    private readonly IUtilityWorkbenchService _service = new UtilityWorkbenchService();

    [Theory]
    [InlineData("json-validate", "{\"a\":1}", "", "JSON 有效")]
    [InlineData("json-minify", "{ \"姓名\": \"小明\" }", "", "{\"姓名\":\"小明\"}")]
    [InlineData("json-sort", "{\"b\":{\"z\":0,\"a\":1},\"a\":[2,1]}", "", "{\"a\":[2,1],\"b\":{\"a\":1,\"z\":0}}")]
    [InlineData("json-flatten", "{\"a/b\":[1,{}]}", "", "{\"\":{},\"/a~1b\":[],\"/a~1b/0\":1,\"/a~1b/1\":{}}")]
    [InlineData("json-unflatten", "{\"\":{},\"/a\":[],\"/a/0\":1}", "", "{\"a\":[1]}")]
    [InlineData("json-to-csv", "[{\"姓名\":\"小明\",\"年龄\":20}]", "", "\"姓名\",\"年龄\"\r\n\"小明\",\"20\"")]
    [InlineData("csv-to-json", "姓名,年龄\r\n小明,20", "", "[{\"姓名\":\"小明\",\"年龄\":\"20\"}]")]
    [InlineData("json-escape", "你好\n世界", "", "\"你好\\n世界\"")]
    [InlineData("json-unescape", "\"你好\\n世界\"", "", "你好\n世界")]
    [InlineData("url-encode", "你好 a+b", "", "%E4%BD%A0%E5%A5%BD%20a%2Bb")]
    [InlineData("url-decode", "%E4%BD%A0%E5%A5%BD%20a+b", "", "你好 a+b")]
    [InlineData("query-parse", "?q=hello+world&tag=a&tag=b", "", "{\"q\":[\"hello world\"],\"tag\":[\"a\",\"b\"]}")]
    [InlineData("query-build", "{\"q\":\"你好\",\"tag\":[\"a\",\"b\"]}", "", "q=%E4%BD%A0%E5%A5%BD&tag=a&tag=b")]
    [InlineData("html-encode", "<b>你好 & 世界</b>", "", "&lt;b&gt;你好 &amp; 世界&lt;/b&gt;")]
    [InlineData("html-decode", "&lt;b&gt;你好&lt;/b&gt;", "", "<b>你好</b>")]
    [InlineData("hex-encode", "你好", "", "E4BDA0E5A5BD")]
    [InlineData("hex-decode", "e4bda0e5a5bd", "", "你好")]
    [InlineData("base64url-encode", "你好", "", "5L2g5aW9")]
    [InlineData("base64url-decode", "5L2g5aW9", "", "你好")]
    [InlineData("unix-seconds-to-date", "0", "", "1970-01-01T00:00:00.0000000+00:00")]
    [InlineData("unix-ms-to-date", "-1", "", "1969-12-31T23:59:59.9990000+00:00")]
    [InlineData("date-to-unix-seconds", "1970-01-01T08:00:01+08:00", "", "1")]
    [InlineData("date-to-unix-ms", "1970-01-01T00:00:00.123Z", "", "123")]
    [InlineData("integer-base", "-FF", "16:2", "-11111111")]
    [InlineData("hex-to-rgb", "#1a2B3c", "", "26,43,60")]
    [InlineData("rgb-to-hex", "26, 43,60", "", "#1A2B3C")]
    [InlineData("gzip-decompress", "H4sIAAAAAAACAwMAAAAAAAAAAAA=", "", "")]
    public void Execute_HasDeterministicExamples(string id, string input, string option, string expected)
    {
        _service.Execute(id, input, option).Should().Be(expected);
    }

    [Fact]
    public void Catalog_ContainsExactlyFiftyFourUniqueToolsWithChineseMetadata()
    {
        var expected = "json-validate,json-minify,json-sort,json-flatten,json-unflatten,json-to-csv,csv-to-json,json-escape,json-unescape,url-encode,url-decode,query-parse,query-build,html-encode,html-decode,hex-encode,hex-decode,base64url-encode,base64url-decode,unix-seconds-to-date,unix-ms-to-date,date-to-unix-seconds,date-to-unix-ms,guid-batch,password-generate,integer-base,hex-to-rgb,rgb-to-hex,gzip-compress,gzip-decompress,text-stats,reverse-graphemes,rot13,morse-encode,morse-decode,regex-escape,text-diff,hash-digest,hmac-sha256,jwt-decode,seconds-to-duration,byte-size,bin-encode,bin-decode,crc32,base32-encode,base32-decode,number-format,rgb-to-hsl,hsl-to-rgb,text-case,password-strength,csv-validate,cron-describe".Split(',');
        _service.Tools.Select(t => t.Id).Should().Equal(expected);
        foreach (var tool in _service.Tools)
        {
            tool.Name.Should().MatchRegex("[\u4e00-\u9fff]");
            tool.Category.Should().MatchRegex("[\u4e00-\u9fff]");
            tool.Description.Should().MatchRegex("[\u4e00-\u9fff]");
            _service.Execute(tool.Id, tool.Example, tool.DefaultOption).Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("guid-batch", "0")]
    [InlineData("password-generate", "3")]
    public void RandomTools_HaveDeterministicInvalidCases(string id, string option)
    {
        Action action = () => _service.Execute(id, "", option);
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("", 1)]
    [InlineData("1000", 1000)]
    public void GuidBatch_ProducesRequestedNumberOfVersionFourGuids(string option, int count)
    {
        var values = _service.Execute("guid-batch", "", option).Split('\n');
        values.Should().HaveCount(count);
        foreach (var value in values)
        {
            Guid.TryParseExact(value, "D", out _).Should().BeTrue();
            value[14].Should().Be('4');
            "89ab".Should().Contain(value[19].ToString());
        }
    }

    [Theory]
    [InlineData("4", 4)]
    [InlineData("", 20)]
    [InlineData("1024", 1024)]
    public void PasswordGeneration_AlwaysMeetsLengthAndCharacterClasses(string option, int length)
    {
        var value = _service.Execute("password-generate", "", option);
        value.Should().HaveLength(length);
        value.Should().MatchRegex("[A-Z]").And.MatchRegex("[a-z]").And.MatchRegex("[0-9]").And.MatchRegex("[!@#$%^&*()_=+\\-]");
    }

    [Fact]
    public void GzipCompression_ProducesAnIndependentlyDecodablePayload()
    {
        var bytes = Convert.FromBase64String(_service.Execute("gzip-compress", "你好\nworld"));
        using var source = new MemoryStream(bytes);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, new UTF8Encoding(false, true));
        reader.ReadToEnd().Should().Be("你好\nworld");
    }

    [Theory]
    [InlineData("json-escape", "json-unescape")]
    [InlineData("url-encode", "url-decode")]
    [InlineData("html-encode", "html-decode")]
    [InlineData("hex-encode", "hex-decode")]
    [InlineData("base64url-encode", "base64url-decode")]
    [InlineData("gzip-compress", "gzip-decompress")]
    public void TextCodecs_RoundTripUnicodeAndControls(string encode, string decode)
    {
        const string text = "中文\U0001F600 e\u0301\0\r\n\t\"<>&%+/";
        _service.Execute(decode, _service.Execute(encode, text)).Should().Be(text);
        _service.Execute(decode, _service.Execute(encode, "")).Should().BeEmpty();
    }

    [Theory]
    [InlineData("{\"\":{\"0\":1},\"a/b~c\":[{},[],null,{\"~1/\":\"中文\"}]}")]
    [InlineData("[0,false,null,\"a\",[1,2],{\"01\":true}]")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"中文\"")]
    [InlineData("123.4500")]
    public void JsonPointers_RoundTripAllNodeTypesAndSpecialKeys(string input)
    {
        var restored = _service.Execute("json-unflatten", _service.Execute("json-flatten", input));
        JsonNode.DeepEquals(JsonNode.Parse(restored), JsonNode.Parse(input)).Should().BeTrue();
    }

    [Fact]
    public void JsonPointers_RestoreUnorderedArrayEntries()
    {
        _service.Execute("json-unflatten", "{\"/1\":2,\"/0\":1,\"\":[]}").Should().Be("[1,2]");
    }

    [Fact]
    public void Csv_RoundTripsQuotedCommasQuotesAndLineBreaks()
    {
        const string input = "[{\"a,b\":\"第一行\\r\\n第二行\",\"q\":\"a\\\"b\",\"\":\"\"}]";
        var restored = _service.Execute("csv-to-json", _service.Execute("json-to-csv", input));
        JsonNode.DeepEquals(JsonNode.Parse(restored), JsonNode.Parse(input)).Should().BeTrue();
    }

    [Theory]
    [InlineData("a,b\n1,2\n", "[{\"a\":\"1\",\"b\":\"2\"}]")]
    [InlineData("a,b\r1,\r", "[{\"a\":\"1\",\"b\":\"\"}]")]
    [InlineData("\"\"\n\"\"", "[{\"\":\"\"}]")]
    [InlineData("a\n\n", "[{\"a\":\"\"}]")]
    [InlineData("a,b", "[]")]
    [InlineData("", "[]")]
    public void Csv_HandlesEmptyFieldsAndLineEndings(string input, string expected)
    {
        _service.Execute("csv-to-json", input).Should().Be(expected);
    }

    [Fact]
    public void JsonToCsv_UsesUnionOfColumnsAndDocumentsLossyScalarConversion()
    {
        _service.Execute("json-to-csv", "[{\"a\":null},{\"b\":true},{\"a\":12}]")
            .Should().Be("\"a\",\"b\"\r\n\"\",\"\"\r\n\"\",\"true\"\r\n\"12\",\"\"");
    }

    [Fact]
    public void Query_RoundTripsRepeatedAndEmptyValues()
    {
        const string input = "{\"\":[\"\"],\"中文\":[\"a+b\",\"a b\",\"&=\"],\"flag\":[\"\"]}";
        _service.Execute("query-parse", _service.Execute("query-build", input)).Should().Be(input);
        _service.Execute("query-parse", "flag").Should().Be("{\"flag\":[\"\"]}");
    }

    [Theory]
    [InlineData("date-to-unix-seconds", "1969-12-31T23:59:59.9999999Z", "-1")]
    [InlineData("date-to-unix-ms", "1969-12-31T23:59:59.9999999Z", "-1")]
    [InlineData("integer-base", "-0", "0")]
    [InlineData("hex-to-rgb", "#aBc", "170,187,204")]
    [InlineData("rgb-to-hex", "0,255,0", "#00FF00")]
    public void Conversion_HandlesBoundaries(string id, string input, string expected)
    {
        _service.Execute(id, input).Should().Be(expected);
    }

    [Theory]
    [InlineData("-62135596800")]
    [InlineData("253402300799")]
    [InlineData("-1")]
    public void Dates_RoundTripSupportedSecondBoundaries(string seconds)
    {
        _service.Execute("date-to-unix-seconds", _service.Execute("unix-seconds-to-date", seconds)).Should().Be(seconds);
    }

    [Fact]
    public void IntegerConversion_RoundTripsLargeSignedIntegers()
    {
        var input = "-" + new string('Z', 4096);
        var hex = _service.Execute("integer-base", input, "36:36");
        hex.Should().Be(input);
        _service.Execute("integer-base", _service.Execute("integer-base", "-123456789012345678901234567890", "10:36"), "36:10")
            .Should().Be("-123456789012345678901234567890");
    }

    [Theory]
    [InlineData("json-validate", "{\"a\":1,\"a\":2}", "")]
    [InlineData("json-minify", "{", "")]
    [InlineData("json-sort", "[1,]", "")]
    [InlineData("json-flatten", "{\"a\":{\"b\":0,\"b\":1}}", "")]
    [InlineData("json-unflatten", "{}", "")]
    [InlineData("json-unflatten", "{\"\":{},\"/a~2\":0}", "")]
    [InlineData("json-unflatten", "{\"\":{},\"/a/b\":0}", "")]
    [InlineData("json-unflatten", "{\"\":0,\"/a\":0}", "")]
    [InlineData("json-unflatten", "{\"\":[],\"/1\":0}", "")]
    [InlineData("json-unflatten", "{\"\":[],\"/00\":0}", "")]
    [InlineData("json-unflatten", "{\"\":[],\"/2147483647\":0}", "")]
    [InlineData("json-unflatten", "{\"\":{\"a\":0}}", "")]
    [InlineData("json-unflatten", "{\"\":{},\"a\":0}", "")]
    [InlineData("json-to-csv", "[{}]", "")]
    [InlineData("json-to-csv", "[1]", "")]
    [InlineData("json-to-csv", "[{\"a\":[]}]", "")]
    [InlineData("csv-to-json", "a,a\n1,2", "")]
    [InlineData("csv-to-json", "a,\"a\"\n1,2", "")]
    [InlineData("csv-to-json", "a,b\n1", "")]
    [InlineData("csv-to-json", "a\n\"x", "")]
    [InlineData("csv-to-json", "a\nx\"y", "")]
    [InlineData("csv-to-json", "a\n\"x\"y", "")]
    [InlineData("json-unescape", "null", "")]
    [InlineData("url-decode", "%", "")]
    [InlineData("url-decode", "%GG", "")]
    [InlineData("url-decode", "%FF", "")]
    [InlineData("url-decode", "%C0%AF", "")]
    [InlineData("query-parse", "a=%ED%A0%80", "")]
    [InlineData("query-parse", "a=1&&b=2", "")]
    [InlineData("query-build", "{\"a\":[]}", "")]
    [InlineData("query-build", "{\"a\":1}", "")]
    [InlineData("hex-decode", "0", "")]
    [InlineData("hex-decode", "GG", "")]
    [InlineData("hex-decode", "FF", "")]
    [InlineData("hex-decode", "EDA080", "")]
    [InlineData("base64url-decode", "YQ==", "")]
    [InlineData("base64url-decode", "Y Q", "")]
    [InlineData("base64url-decode", "Y+", "")]
    [InlineData("base64url-decode", "Y/", "")]
    [InlineData("base64url-decode", "A", "")]
    [InlineData("base64url-decode", "YR", "")]
    [InlineData("base64url-decode", "_w", "")]
    [InlineData("unix-seconds-to-date", "253402300800", "")]
    [InlineData("unix-ms-to-date", "9223372036854775807", "")]
    [InlineData("date-to-unix-seconds", "1970-01-01T00:00:00", "")]
    [InlineData("date-to-unix-ms", "2023-02-29T00:00:00Z", "")]
    [InlineData("guid-batch", "", "1001")]
    [InlineData("guid-batch", "ignored", "1")]
    [InlineData("password-generate", "", "1025")]
    [InlineData("password-generate", "", "-1")]
    [InlineData("integer-base", "12", "2:10")]
    [InlineData("integer-base", "12", "1:10")]
    [InlineData("integer-base", "12", "10:37")]
    [InlineData("integer-base", "12", "10")]
    [InlineData("integer-base", "-", "10:2")]
    [InlineData("hex-to-rgb", "#1234", "")]
    [InlineData("rgb-to-hex", "256,0,0", "")]
    [InlineData("rgb-to-hex", "0,0", "")]
    [InlineData("gzip-decompress", "", "")]
    [InlineData("gzip-decompress", "AAAA", "")]
    [InlineData("gzip-decompress", "!", "")]
    [InlineData("json-escape", "a", "unexpected")]
    [InlineData("unknown", "", "")]
    public void InvalidInputs_ThrowDocumentedExceptions(string id, string input, string option)
    {
        var exception = Record.Exception(() => _service.Execute(id, input, option));
        exception.Should().NotBeNull();
        (exception is ArgumentException or FormatException).Should().BeTrue();
        exception!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Limits_AllowExactInputByteLimitAndRejectOverflow()
    {
        var text = new string('a', UtilityWorkbenchService.MaxInputBytes);
        _service.Execute("url-encode", text).Should().Be(text);
        Action asciiOverflow = () => _service.Execute("url-encode", text + "a");
        asciiOverflow.Should().Throw<ArgumentException>();
        Action unicodeOverflow = () => _service.Execute("url-encode", new string('中', UtilityWorkbenchService.MaxInputBytes / 3 + 1));
        unicodeOverflow.Should().Throw<ArgumentException>();
        Action optionOverflow = () => _service.Execute("guid-batch", "", new string('1', 65));
        optionOverflow.Should().Throw<ArgumentException>();
        Action integerOverflow = () => _service.Execute("integer-base", new string('1', 4097));
        integerOverflow.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Limits_RejectExcessiveNodesDepthCellsAndQueryItems()
    {
        Action nodes = () => _service.Execute("json-minify", "[" + string.Join(",", Enumerable.Repeat("0", UtilityWorkbenchService.MaxItems)) + "]");
        nodes.Should().Throw<ArgumentException>();
        Action depth = () => _service.Execute("json-minify", new string('[', 33) + "0" + new string(']', 33));
        depth.Should().Throw<FormatException>();
        Action cells = () => _service.Execute("csv-to-json", "a\n" + string.Join("\n", Enumerable.Repeat("x", UtilityWorkbenchService.MaxItems)));
        cells.Should().Throw<ArgumentException>();
        Action query = () => _service.Execute("query-parse", string.Join("&", Enumerable.Repeat("a=1", UtilityWorkbenchService.MaxItems + 1)));
        query.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Limits_RejectSparseCsvExpansion()
    {
        var input = "[" + string.Join(",", Enumerable.Range(0, 101).Select(i => "{\"c" + i.ToString(CultureInfo.InvariantCulture) + "\":0}")) + "]";
        Action action = () => _service.Execute("json-to-csv", input);
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(UtilityWorkbenchService.MaxOutputBytes, true)]
    [InlineData(UtilityWorkbenchService.MaxOutputBytes + 1, false)]
    public void Gzip_DecompressionHonorsExactOutputLimit(int size, bool allowed)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true)) gzip.Write(new byte[size]);
        var input = Convert.ToBase64String(output.ToArray());
        if (allowed) _service.Execute("gzip-decompress", input).Should().HaveLength(size);
        else
        {
            Action action = () => _service.Execute("gzip-decompress", input);
            action.Should().Throw<ArgumentException>();
        }
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("crc")]
    [InlineData("length")]
    [InlineData("trailing")]
    public void Gzip_RejectsDamagedPayloads(string damage)
    {
        var bytes = Convert.FromBase64String(_service.Execute("gzip-compress", "hello"));
        if (damage == "truncated") bytes = bytes[..^1];
        if (damage == "crc") bytes[^8] ^= 1;
        if (damage == "length") bytes[^4] ^= 1;
        if (damage == "trailing") bytes = bytes.Concat(new byte[] { 1 }).ToArray();
        Action action = () => _service.Execute("gzip-decompress", Convert.ToBase64String(bytes));
        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void Gzip_RejectsInvalidUtf8()
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true)) gzip.Write(new byte[] { 0xff });
        Action action = () => _service.Execute("gzip-decompress", Convert.ToBase64String(output.ToArray()));
        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void NullAndUnpairedSurrogates_AreRejected()
    {
        Action nullInput = () => _service.Execute("json-escape", null!);
        Action nullOption = () => _service.Execute("guid-batch", "", null!);
        Action surrogate = () => _service.Execute("hex-encode", "\ud800");
        nullInput.Should().Throw<ArgumentException>();
        nullOption.Should().Throw<ArgumentException>();
        surrogate.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("text-stats", "你好\nworld", "",
        "{\"characters\":8,\"bytes\":12,\"graphemes\":8,\"lines\":2,\"words\":2,\"nonWhitespace\":7}")]
    [InlineData("text-stats", "", "", "{\"characters\":0,\"bytes\":0,\"graphemes\":0,\"lines\":0,\"words\":0,\"nonWhitespace\":0}")]
    [InlineData("text-stats", "   \n\t", "", "{\"characters\":5,\"bytes\":5,\"graphemes\":5,\"lines\":2,\"words\":0,\"nonWhitespace\":0}")]
    [InlineData("reverse-graphemes", "abc", "", "cba")]
    [InlineData("reverse-graphemes", "你好", "", "好你")]
    [InlineData("reverse-graphemes", "e\u0301x", "", "xe\u0301")]
    [InlineData("rot13", "Hello, World! 123 中文", "", "Uryyb, Jbeyq! 123 中文")]
    [InlineData("rot13", "", "", "")]
    [InlineData("morse-encode", "SOS 1", "", "... --- ... / .----")]
    [InlineData("morse-decode", "... --- ... / .----", "", "SOS 1")]
    [InlineData("regex-escape", "a.b*(c)[d]{e}|^$?f+\\g", "", @"a\.b\*\(c\)\[d]\{e}\|\^\$\?f\+\\g")]
    [InlineData("seconds-to-duration", "90061", "", "1 天 01:01:01")]
    [InlineData("seconds-to-duration", "-3600", "", "-01:00:00")]
    [InlineData("seconds-to-duration", "0", "", "00:00:00")]
    [InlineData("seconds-to-duration", "59", "", "00:00:59")]
    [InlineData("byte-size", "1536", "KB", "1.5 MB")]
    [InlineData("byte-size", "1", "B", "1 B")]
    [InlineData("byte-size", "0", "MB", "0 B")]
    [InlineData("byte-size", "999", "", "999 KB")]
    [InlineData("byte-size", "1073741824", "KB", "1 TB")]
    public void NewTextAndConversionTools_HaveDeterministicExamples(string id, string input, string option, string expected)
    {
        _service.Execute(id, input, option).Should().Be(expected);
    }

    [Theory]
    [InlineData("hash-digest", "hello", "SHA256", "2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824")]
    [InlineData("hash-digest", "hello", "sha256", "2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824")]
    [InlineData("hash-digest", "hello", "MD5", "5D41402ABC4B2A76B9719D911017C592")]
    [InlineData("hash-digest", "hello", "SHA1", "AAF4C61DDCC5E8A2DABEDE0F3B482CD9AEA9434D")]
    [InlineData("hash-digest", "hello", "SHA384", "59E1748777448C69DE6B800D7A33BBFB9FF1B463E44354C3553BCDB9C666FA90125A3C79F90397BDF5F6A13DE828684F")]
    [InlineData("hash-digest", "hello", "SHA512", "9B71D224BD62F3785D96D46AD3EA3D73319BFBC2890CAADAE2DFF72519673CA72323C3D99BA5C11D7C7ACC6E14B8C5DA0C4663475C2E5C3ADEF46F73BCDEC043")]
    [InlineData("hmac-sha256", "hello", "key", "9307B3B915EFB5171FF14D8CB55FBCC798C6C0EF1456D66DED1A6AA723A58B7B")]
    [InlineData("jwt-decode", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk", "", """{"header":{"alg":"HS256"},"payload":{"sub":"1"}}""")]
    public void HashTools_ProduceKnownDigests(string id, string input, string digestOption, string expected)
    {
        _service.Execute(id, input, digestOption).Should().Be(expected);
    }

    [Fact]
    public void JwtDecode_ExtractsHeaderAndPayloadWithoutVerification()
    {
        const string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IuWwj-aYjiIsImlhdCI6MTUxNjIzOTAyMn0.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        _service.Execute("jwt-decode", token).Should()
            .Be("""{"header":{"alg":"HS256","typ":"JWT"},"payload":{"sub":"1234567890","name":"小明","iat":1516239022}}""");
    }

    [Theory]
    [InlineData("jwt-decode", "abc", "")]
    [InlineData("jwt-decode", "a.b.c.d", "")]
    [InlineData("jwt-decode", "YQ.YQ.YQ", "")]
    [InlineData("morse-encode", "a=1", "")]
    [InlineData("morse-decode", "...x", "")]
    [InlineData("hash-digest", "hello", "CRC32")]
    [InlineData("byte-size", "-1", "KB")]
    [InlineData("byte-size", "99999999999999999999", "TB")]
    [InlineData("seconds-to-duration", "abc", "")]
    [InlineData("text-diff", "a\n---\na\nb", "21")]
    public void NewTools_ValidateInputsAndOptions(string id, string input, string option)
    {
        var exception = Record.Exception(() => _service.Execute(id, input, option));
        exception.Should().NotBeNull();
        (exception is ArgumentException or FormatException).Should().BeTrue();
        exception!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void TextDiff_ContextModeIncludesHeaderAndSurroundingLines(int context)
    {
        var input = "apple\nbanana\ncherry\n---\napple\nbanana\ndate\ncherry";
        var output = _service.Execute("text-diff", input, context.ToString(CultureInfo.InvariantCulture));
        if (context == 0)
        {
            output.Should().Be("+ date");
        }
        else
        {
            output.Should().StartWith("@@ 左侧 1 / 右侧 1 @@");
            output.Should().Contain("+ date").And.Contain("apple").And.Contain("cherry");
        }
    }

    [Fact]
    public void TextDiff_IdenticalTextsReportNoChanges()
    {
        _service.Execute("text-diff", "same\nlines\n---\nsame\nlines").Should().Be("文本相同");
    }

    [Fact]
    public void TextDiff_RoundTripsDeletionsAndAdditionsWithLineNumbers()
    {
        var output = _service.Execute("text-diff", "one\ntwo\nthree\n---\none\nTWO\nthree\nfour", "2");
        output.Should().StartWith("@@ 左侧 1 / 右侧 1 @@");
        output.Should().Contain("- two");
        output.Should().Contain("+ TWO");
        output.Should().Contain("+ four");
    }

    [Theory]
    [InlineData("中文\U0001F600\U0001F469\u200d\U0001F4BB")]
    public void GraphemeTools_HandleEmojiAndCombiningMarks(string input)
    {
        var reversed = _service.Execute("reverse-graphemes", input);
        // 反转后按字符数一致，且 ZWJ 序列的字节数保持不变（簇未被拆散）。
        reversed.Should().HaveLength(input.Length);
        var original = JsonNode.Parse(_service.Execute("text-stats", input))!;
        var after = JsonNode.Parse(_service.Execute("text-stats", reversed))!;
        original["bytes"]!.GetValue<int>().Should().Be(after["bytes"]!.GetValue<int>());
        original["graphemes"].Should().NotBeNull();
    }

    [Fact]
    public void Morse_RoundTripsLettersDigitsAndWordBreaks()
    {
        const string text = "Attack at Dawn 42";
        var encoded = _service.Execute("morse-encode", text);
        _service.Execute("morse-decode", encoded).Should().Be(text.ToUpperInvariant());
    }

    [Fact]
    public void ByteSize_LimitsRejectOversizedValues()
    {
        _service.Execute("byte-size", long.MaxValue.ToString(CultureInfo.InvariantCulture), "B").Should().NotBeNullOrWhiteSpace();
        Action overflow = () => _service.Execute("byte-size", long.MaxValue.ToString(CultureInfo.InvariantCulture), "KB");
        overflow.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("bin-encode", "Hi", "", "01001000 01101001")]
    [InlineData("bin-encode", "你好", "", "11100100 10111101 10100000 11100101 10100101 10111101")]
    [InlineData("bin-decode", "01001000 01101001", "", "Hi")]
    [InlineData("bin-decode", "", "", "")]
    [InlineData("crc32", "123456789", "", "CBF43926")]
    [InlineData("crc32", "", "", "00000000")]
    [InlineData("crc32", "hi", "", "D8932AAC")]
    [InlineData("base32-encode", "", "", "")]
    [InlineData("base32-encode", "Hello", "", "JBSWY3DP")]
    [InlineData("base32-encode", "hi", "", "NBUQ====")]
    [InlineData("base32-decode", "jbswy3dp", "", "Hello")]
    [InlineData("base32-decode", "NBUQ====", "", "hi")]
    [InlineData("number-format", "1500.005", ",:2", "1,500.00")]
    [InlineData("number-format", "1500.5", "n:0", "1500")]
    [InlineData("number-format", "1234567.891", ",:2", "1,234,567.89")]
    [InlineData("number-format", "-987.6543", ",:3", "-987.654")]
    [InlineData("number-format", "0.0005", ",:2", "0.00")]
    [InlineData("number-format", "12345678", ",:0", "12,345,678")]
    [InlineData("rgb-to-hsl", "#8080FF", "", "240,100,75")]
    [InlineData("rgb-to-hsl", "#800000", "", "0,100,25")]
    [InlineData("rgb-to-hsl", "0,255,0", "", "120,100,50")]
    [InlineData("rgb-to-hsl", "#3c3c3c", "", "0,0,24")]
    [InlineData("rgb-to-hsl", "#FFFFFF", "", "0,0,100")]
    [InlineData("hsl-to-rgb", "0,100,25", "", "#800000")]
    [InlineData("hsl-to-rgb", "120,100,50", "", "#00FF00")]
    [InlineData("hsl-to-rgb", "240,100,75", "", "#8080FF")]
    [InlineData("hsl-to-rgb", "0,0,50", "", "#808080")]
    [InlineData("hsl-to-rgb", "240°,100%,75%", "", "#8080FF")]
    [InlineData("text-case", "hello world_foo-bar", "camel", "helloWorldFooBar")]
    [InlineData("text-case", "hello world_foo-bar", "pascal", "HelloWorldFooBar")]
    [InlineData("text-case", "HelloWorld", "snake", "hello_world")]
    [InlineData("text-case", "hello_world", "kebab", "hello-world")]
    [InlineData("text-case", "helloWorld", "title", "Hello World")]
    [InlineData("text-case", "helloWorld", "upper", "HELLO WORLD")]
    public void BatchThreeTools_ProduceDeterministicResults(string id, string input, string option, string expected)
    {
        _service.Execute(id, input, option).Should().Be(expected);
    }

    [Fact]
    public void TextCase_SnakeUsesFullLoweredTokens()
    {
        _service.Execute("text-case", "hello world_foo-bar", "snake").Should().Be("hello_world_foo_bar");
    }

    [Fact]
    public void BinaryAndBase32_RoundTripAndRejectMalformed()
    {
        _service.Execute("bin-decode", _service.Execute("bin-encode", "你好 2026")).Should().Be("你好 2026");
        _service.Execute("base32-decode", _service.Execute("base32-encode", "工具 Test9")).Should().Be("工具 Test9");
        Action lenBad = () => _service.Execute("bin-decode", "10101");
        lenBad.Should().Throw<FormatException>();
        Action bitsBad = () => _service.Execute("bin-decode", "0100100X");
        bitsBad.Should().Throw<FormatException>();
        Action padBad = () => _service.Execute("base32-decode", "JBSW=Y3D");
        padBad.Should().Throw<FormatException>();
        Action tailBad = () => _service.Execute("base32-decode", "ML====");
        tailBad.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("abcdefgh", "弱")]
    [InlineData("Abcd1234!", "中")]
    [InlineData("Xy9#kL2@pQv7&NM!", "强")]
    public void PasswordStrength_RatesByRules(string password, string level)
    {
        _service.Execute("password-strength", password).Should().StartWith("评级：" + level);
    }

    [Fact]
    public void CsvValidate_ReportsStatsAndSharesErrorRules()
    {
        var json = JsonNode.Parse(_service.Execute("csv-validate", "姓名,年龄\r\n小明,20\r\n小红,20"))!;
        json["rowCount"]!.GetValue<int>().Should().Be(2);
        json["columnCount"]!.GetValue<int>().Should().Be(2);
        json["distinctValueCount"]!["年龄"]!.GetValue<int>().Should().Be(1);
        json["emptyCellCount"]!["姓名"]!.GetValue<int>().Should().Be(0);
        _service.Execute("csv-validate", "").Should().Contain("\"rowCount\":0");
        Action dup = () => _service.Execute("csv-validate", "a,a\r\n1,2");
        dup.Should().Throw<FormatException>();
        Action width = () => _service.Execute("csv-validate", "a,b\r\n1");
        width.Should().Throw<FormatException>();
    }

    [Fact]
    public void CronDescribe_ExpandsFieldsAndNormalizesDow()
    {
        var json = JsonNode.Parse(_service.Execute("cron-describe", "*/5 9-18 * * 1-5"))!;
        json["分"]!.AsArray().Select(n => n!.GetValue<int>()).Should().Equal(0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55);
        json["时"]!.AsArray().Select(n => n!.GetValue<int>()).Should().Equal(9, 10, 11, 12, 13, 14, 15, 16, 17, 18);
        json["日"]!.AsArray().Should().HaveCount(31);
        json["周"]!.AsArray().Select(n => n!.GetValue<int>()).Should().Equal(1, 2, 3, 4, 5);
        _service.Execute("cron-describe", "0 0 1 1 7").Should().Contain("\"周\":[0]");
        _service.Execute("cron-describe", "5-11/2,30 * * * *").Should().Contain("\"分\":[5,7,9,11,30]");
    }

    [Theory]
    [InlineData("61 * * * *")]
    [InlineData("0-4 * * *")]
    [InlineData("* * * * 10")]
    [InlineData("*/0 * * * *")]
    [InlineData("10-2 * * * *")]
    [InlineData("5/10 * * * *")]
    [InlineData("a * * * *")]
    public void CronDescribe_RejectsInvalidExpressions(string expression)
    {
        var act = () => _service.Execute("cron-describe", expression);
        act.Should().Throw<Exception>().Where(e => e.GetType() == typeof(ArgumentException) || e.GetType() == typeof(FormatException));
    }

    [Theory]
    [InlineData("number-format", "abc", ",:2")]
    [InlineData("number-format", "1", ",:13")]
    [InlineData("number-format", "1", "x:2")]
    [InlineData("rgb-to-hsl", "#12345")]
    [InlineData("hsl-to-rgb", "0,101,50")]
    [InlineData("hsl-to-rgb", "361,0,0")]
    [InlineData("text-case", "hello", "scREAMing")]
    [InlineData("password-strength", "")]
    public void BatchThreeTools_RejectInvalidInputs(string id, string input, string option = "")
    {
        var act = () => _service.Execute(id, input, option);
        act.Should().Throw<Exception>().Where(e => e.GetType() == typeof(ArgumentException) || e.GetType() == typeof(FormatException));
    }
}
