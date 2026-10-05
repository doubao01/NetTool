namespace SystemToolkit.Core.Services;
using SystemToolkit.Core.Models;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

public interface IDevToolService
{
    string GenerateCode(string templateName, Dictionary<string, string> parameters);
    List<CodeTemplate> GetAvailableTemplates();
    FormatResult FormatJson(string input);
    FormatResult FormatXml(string input);
    string EncodeBase64(string input);
    string DecodeBase64(string input);
    RegexTestResult TestRegex(string pattern, string input, string options);
    bool ValidateRegex(string pattern);
}

public class DevToolService : IDevToolService
{
    private static readonly Dictionary<string, CodeTemplate> DefaultTemplates = new()
    {
        ["csharp_class"] = new CodeTemplate
        {
            Name = "C# Class",
            Language = "C#",
            Description = "Basic C# class template",
            Template = @"public class {{ClassName}}
{
    {{Properties}}
    
    public {{ClassName}}()
    {
        {{Constructor}}
    }
}",
            Parameters = new List<TemplateParameter>
            {
                new() { Name = "ClassName", Type = "string", Description = "类名" },
                new() { Name = "Properties", Type = "string", Description = "属性定义", DefaultValue = "" },
                new() { Name = "Constructor", Type = "string", Description = "构造函数代码", DefaultValue = "" }
            }
        },
        ["csharp_controller"] = new CodeTemplate
        {
            Name = "ASP.NET Controller",
            Language = "C#",
            Description = "ASP.NET Core API Controller",
            Template = """
[ApiController]
[Route("api/[controller]")]
public class {{ControllerName}}Controller : ControllerBase
{
    private readonly ILogger<{{ControllerName}}Controller> _logger;

    public {{ControllerName}}Controller(ILogger<{{ControllerName}}Controller> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }
}
""",
            Parameters = new List<TemplateParameter>
            {
                new() { Name = "ControllerName", Type = "string", Description = "控制器名称" }
            }
        },
        ["python_function"] = new CodeTemplate
        {
            Name = "Python Function",
            Language = "Python",
            Description = "Python function with docstring",
            Template = """"
def {{function_name}}({{params}}):
    """
    {{description}}
    
    Args:
        {{args_doc}}
    
    Returns:
        {{return_doc}}
    """
    {{body}}
"""",
            Parameters = new List<TemplateParameter>
            {
                new() { Name = "function_name", Type = "string", Description = "函数名" },
                new() { Name = "params", Type = "string", Description = "参数列表", DefaultValue = "" },
                new() { Name = "description", Type = "string", Description = "函数描述" },
                new() { Name = "body", Type = "string", Description = "函数体", DefaultValue = "pass" }
            }
        },
        ["json_config"] = new CodeTemplate
        {
            Name = "JSON Config",
            Language = "JSON",
            Description = "Configuration file template",
            Template = @"{
    ""appSettings"": {
        ""name"": ""{{app_name}}"",
        ""version"": ""{{version}}"",
        ""debug"": {{debug}}
    },
    ""connectionStrings"": {
        ""default"": ""{{connection_string}}""
    }
}",
            Parameters = new List<TemplateParameter>
            {
                new() { Name = "app_name", Type = "string", Description = "应用名称" },
                new() { Name = "version", Type = "string", Description = "版本号", DefaultValue = "1.0.0" },
                new() { Name = "debug", Type = "boolean", Description = "调试模式", DefaultValue = "false" },
                new() { Name = "connection_string", Type = "string", Description = "连接字符串" }
            }
        }
    };

    public string GenerateCode(string templateName, Dictionary<string, string> parameters)
    {
        if (!DefaultTemplates.TryGetValue(templateName, out var template))
        {
            throw new ArgumentException($"Template '{templateName}' not found");
        }

        var result = template.Template;
        foreach (var param in parameters)
        {
            result = result.Replace($"{{{{{param.Key}}}}}", param.Value);
        }

        return result;
    }

    public List<CodeTemplate> GetAvailableTemplates()
    {
        return DefaultTemplates.Values.ToList();
    }

    public FormatResult FormatJson(string input)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(input);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var formatted = JsonSerializer.Serialize(jsonDoc, options);
            return new FormatResult { FormattedText = formatted };
        }
        catch (JsonException ex)
        {
            return new FormatResult { Error = $"JSON 解析错误：{ex.Message}" };
        }
    }

    public FormatResult FormatXml(string input)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(input);

            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = Environment.NewLine,
                NewLineHandling = NewLineHandling.Replace
            };

            using (var writer = XmlWriter.Create(sb, settings))
            {
                xmlDoc.Save(writer);
            }

            return new FormatResult { FormattedText = sb.ToString() };
        }
        catch (XmlException ex)
        {
            return new FormatResult { Error = $"XML 解析错误：{ex.Message}" };
        }
    }

    public string EncodeBase64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes);
    }

    public string DecodeBase64(string input)
    {
        try
        {
            var bytes = Convert.FromBase64String(input);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            return $"解码失败：{ex.Message}";
        }
    }

    public RegexTestResult TestRegex(string pattern, string input, string options)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var matches = new List<MatchInfo>();

        try
        {
            var regexOptions = ParseRegexOptions(options);
            var regex = new Regex(pattern, regexOptions, TimeSpan.FromSeconds(5));

            var matchCollection = regex.Matches(input);
            foreach (Match match in matchCollection)
            {
                var matchInfo = new MatchInfo
                {
                    Index = match.Index,
                    Length = match.Length,
                    Value = match.Value
                };

                foreach (Group group in match.Groups)
                {
                    if (!string.IsNullOrEmpty(group.Name) && group.Name != "0")
                    {
                        matchInfo.Groups[group.Name] = group.Value;
                    }
                }

                matches.Add(matchInfo);
            }

            stopwatch.Stop();

            return new RegexTestResult
            {
                IsMatch = matches.Count > 0,
                Matches = matches,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new RegexTestResult
            {
                IsMatch = false,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public bool ValidateRegex(string pattern)
    {
        try
        {
            new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static RegexOptions ParseRegexOptions(string options)
    {
        var result = RegexOptions.None;

        if (options.Contains("i", StringComparison.OrdinalIgnoreCase))
            result |= RegexOptions.IgnoreCase;
        if (options.Contains("m", StringComparison.OrdinalIgnoreCase))
            result |= RegexOptions.Multiline;
        if (options.Contains("s", StringComparison.OrdinalIgnoreCase))
            result |= RegexOptions.Singleline;
        if (options.Contains("x", StringComparison.OrdinalIgnoreCase))
            result |= RegexOptions.IgnorePatternWhitespace;
        if (options.Contains("r", StringComparison.OrdinalIgnoreCase))
            result |= RegexOptions.RightToLeft;

        return result;
    }
}
