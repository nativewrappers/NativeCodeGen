using System.Text.RegularExpressions;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Utilities;
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

    /// <summary>
    /// Auto-links shared examples to natives by scanning example code for function calls
    /// that match native names (PascalCase, normalized, or original form).
    /// </summary>
    public int AutoLinkExamples(IEnumerable<NativeDefinition> natives)
    {
        // Build reverse map: function name variant -> natives
        var nameToNatives = new Dictionary<string, List<NativeDefinition>>(StringComparer.OrdinalIgnoreCase);

        void AddMapping(string key, NativeDefinition native)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!nameToNatives.TryGetValue(key, out var list))
            {
                list = new List<NativeDefinition>();
                nameToNatives[key] = list;
            }
            if (!list.Contains(native))
                list.Add(native);
        }

        foreach (var native in natives)
        {
            // Original name (e.g., _DATABINDING_ADD_DATA_BOOL)
            AddMapping(native.Name, native);

            // Normalized name without leading underscores (e.g., DATABINDING_ADD_DATA_BOOL)
            var normalized = NameConverter.NormalizeNativeName(native.Name);
            AddMapping(normalized, native);

            // PascalCase name (e.g., DatabindingAddDataBool) - matches raw function output
            if (!normalized.StartsWith("N_"))
            {
                var pascalName = NameConverter.ToPascalCase(normalized);
                AddMapping(pascalName, native);
            }
        }

        int linkedCount = 0;

        foreach (var (name, example) in _examples)
        {
            var referencedNatives = new HashSet<NativeDefinition>();

            foreach (var codeBlock in example.Examples)
            {
                if (string.IsNullOrWhiteSpace(codeBlock.Content))
                    continue;

                // Find identifiers followed by ( - these are function calls
                foreach (Match match in FunctionCallPattern.Matches(codeBlock.Content))
                {
                    var funcName = match.Groups[1].Value;
                    if (nameToNatives.TryGetValue(funcName, out var matchedNatives))
                    {
                        foreach (var native in matchedNatives)
                            referencedNatives.Add(native);
                    }
                }
            }

            // Add example reference to each matched native
            foreach (var native in referencedNatives)
            {
                if (!native.RelatedExamples.Exists(e => e.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    native.RelatedExamples.Add(name);
                    linkedCount++;
                }
            }
        }

        return linkedCount;
    }

    private static readonly Regex FunctionCallPattern = new(@"\b(\w+)\s*\(", RegexOptions.Compiled);
}
