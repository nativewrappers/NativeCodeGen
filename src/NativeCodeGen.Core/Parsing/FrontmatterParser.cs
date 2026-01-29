using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NativeCodeGen.Core.Parsing;

public class Frontmatter
{
    public string Ns { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
    public string Apiset { get; set; } = "client";
}

public class FrontmatterParser
{
    private readonly IDeserializer _deserializer;

    public FrontmatterParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public (Frontmatter? frontmatter, int endLine, ParseError? error) Parse(string content, string filePath)
    {
        var lines = content.Split('\n');

        // Check for opening ---
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return (null, 0, new ParseError
            {
                FilePath = filePath,
                Line = 1,
                Column = 1,
                Message = "Missing frontmatter. File must start with '---'"
            });
        }

        // Find closing ---
        int endLine = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                endLine = i;
                break;
            }
        }

        if (endLine == -1)
        {
            return (null, 0, new ParseError
            {
                FilePath = filePath,
                Line = 1,
                Column = 1,
                Message = "Unclosed frontmatter. Missing closing '---'"
            });
        }

        // Extract YAML content
        var yamlContent = string.Join('\n', lines.Skip(1).Take(endLine - 1));

        try
        {
            var frontmatter = _deserializer.Deserialize<Frontmatter>(yamlContent);
            return (frontmatter ?? new Frontmatter(), endLine + 1, null);
        }
        catch (Exception ex)
        {
            return (null, 0, new ParseError
            {
                FilePath = filePath,
                Line = 2,
                Column = 1,
                Message = $"Invalid YAML in frontmatter: {ex.Message}"
            });
        }
    }
}
