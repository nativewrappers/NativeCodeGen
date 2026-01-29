using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Registry;

public class SharedExampleRegistry
{
    private readonly Dictionary<string, SharedExample> _examples = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _examples.Count;

    public void LoadExamples(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.GetFiles(directory, "*.mdx"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var content = File.ReadAllText(file);

            // Extract language and code content from markdown code block
            var (language, code) = ParseCodeBlock(content);

            _examples[name] = new SharedExample
            {
                Name = name,
                Content = code,
                Language = language,
                SourceFile = file
            };
        }
    }

    private static (string? language, string content) ParseCodeBlock(string content)
    {
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
                    var langPart = trimmed[3..].Trim();
                    if (!string.IsNullOrEmpty(langPart))
                        language = langPart;
                }
                else
                {
                    // End of code block
                    inCodeBlock = false;
                }
            }
            else if (inCodeBlock)
            {
                codeLines.Add(line);
            }
        }

        return (language, string.Join("\n", codeLines).Trim());
    }

    public bool Contains(string name) => _examples.ContainsKey(name);

    public SharedExample? GetExample(string name)
    {
        return _examples.TryGetValue(name, out var example) ? example : null;
    }

    public Dictionary<string, SharedExample> GetAllExamples() => new(_examples);
}
