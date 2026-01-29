using NativeCodeGen.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NativeCodeGen.Core.Registry;

public class SharedExampleFrontmatter
{
    public string? Title { get; set; }
}

public class SharedExampleRegistry
{
    private readonly Dictionary<string, SharedExample> _examples = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDeserializer _deserializer;

    public int Count => _examples.Count;

    public SharedExampleRegistry()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public List<string> Errors { get; } = new();

    public void LoadExamples(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.GetFiles(directory, "*.mdx"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var content = File.ReadAllText(file);

            // Extract frontmatter and code content
            var (frontmatter, body) = ParseFrontmatter(content);

            if (string.IsNullOrWhiteSpace(frontmatter?.Title))
            {
                Errors.Add($"{file}: Missing required 'title' in frontmatter");
                continue;
            }

            var codeBlocks = ParseCodeBlocks(body);

            _examples[name] = new SharedExample
            {
                Name = name,
                Title = frontmatter.Title,
                Examples = codeBlocks,
                SourceFile = file
            };
        }
    }

    private (SharedExampleFrontmatter? frontmatter, string body) ParseFrontmatter(string content)
    {
        var lines = content.Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
            return (null, content);

        int endIndex = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex == -1)
            return (null, content);

        var yamlContent = string.Join('\n', lines.Skip(1).Take(endIndex - 1));
        var body = string.Join("\n", lines.Skip(endIndex + 1));

        try
        {
            var frontmatter = _deserializer.Deserialize<SharedExampleFrontmatter>(yamlContent);
            return (frontmatter, body);
        }
        catch
        {
            return (null, body);
        }
    }

    private static List<SharedExampleCode> ParseCodeBlocks(string content)
    {
        var blocks = new List<SharedExampleCode>();
        var lines = content.Split('\n');
        var codeLines = new List<string>();
        string? language = null;
        bool inCodeBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    // Start of code block - extract language
                    inCodeBlock = true;
                    codeLines.Clear();
                    var langPart = trimmed[3..].Trim();
                    language = !string.IsNullOrEmpty(langPart) ? langPart : null;
                }
                else
                {
                    // End of code block - save it
                    inCodeBlock = false;
                    blocks.Add(new SharedExampleCode
                    {
                        Content = string.Join("\n", codeLines).Trim(),
                        Language = language
                    });
                }
            }
            else if (inCodeBlock)
            {
                codeLines.Add(line);
            }
        }

        return blocks;
    }

    public bool Contains(string name) => _examples.ContainsKey(name);

    public SharedExample? GetExample(string name)
    {
        return _examples.TryGetValue(name, out var example) ? example : null;
    }

    public Dictionary<string, SharedExample> GetAllExamples() => new(_examples);
}
