using System.Text.RegularExpressions;

namespace NativeCodeGen.Core.Parsing;

public class EmbeddedEnumRef
{
    public string Name { get; set; } = string.Empty;
}

public class SharedExampleRef
{
    public string Name { get; set; } = string.Empty;
}

public class StructRef
{
    public string Name { get; set; } = string.Empty;
}

public class NativeRef
{
    public string Name { get; set; } = string.Empty;
}

public partial class MdxComponentParser
{
    // Attribute format patterns: [type: name]
    [GeneratedRegex(@"\[enum:\s*[""']?(\w+)[""']?\]", RegexOptions.IgnoreCase)]
    private static partial Regex EnumAttributeRegex();

    [GeneratedRegex(@"\[example:\s*[""']?(\w+)[""']?\]", RegexOptions.IgnoreCase)]
    private static partial Regex ExampleAttributeRegex();

    [GeneratedRegex(@"\[struct:\s*[""']?(\w+)[""']?\]", RegexOptions.IgnoreCase)]
    private static partial Regex StructAttributeRegex();

    [GeneratedRegex(@"\[native:\s*[""']?([\w]+)[""']?\]", RegexOptions.IgnoreCase)]
    private static partial Regex NativeAttributeRegex();

    /// <summary>
    /// Normalizes description text by cleaning up whitespace.
    /// </summary>
    public string NormalizeDescription(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Clean up double spaces
        var result = Regex.Replace(content, @"\s{2,}", " ");

        return result.Trim();
    }

    public List<EmbeddedEnumRef> ParseEmbeddedEnums(string content)
    {
        var results = new List<EmbeddedEnumRef>();

        foreach (Match match in EnumAttributeRegex().Matches(content))
        {
            var name = match.Groups[1].Value;
            if (!results.Any(r => r.Name == name))
                results.Add(new EmbeddedEnumRef { Name = name });
        }

        return results;
    }

    public List<StructRef> ParseStructRefs(string content)
    {
        var results = new List<StructRef>();

        foreach (Match match in StructAttributeRegex().Matches(content))
        {
            var name = match.Groups[1].Value;
            if (!results.Any(r => r.Name == name))
                results.Add(new StructRef { Name = name });
        }

        return results;
    }

    public List<SharedExampleRef> ParseSharedExamples(string content)
    {
        var results = new List<SharedExampleRef>();

        foreach (Match match in ExampleAttributeRegex().Matches(content))
        {
            var name = match.Groups[1].Value;
            if (!results.Any(r => r.Name == name))
                results.Add(new SharedExampleRef { Name = name });
        }

        return results;
    }

    public List<NativeRef> ParseNativeRefs(string content)
    {
        var results = new List<NativeRef>();

        foreach (Match match in NativeAttributeRegex().Matches(content))
        {
            var name = match.Groups[1].Value;
            if (!results.Any(r => r.Name == name))
                results.Add(new NativeRef { Name = name });
        }

        return results;
    }
}
