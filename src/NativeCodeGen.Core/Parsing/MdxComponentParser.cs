using System.Text.RegularExpressions;
using NativeCodeGen.Core.Models;

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
    public string? Game { get; set; }
}

public partial class MdxComponentParser
{
    // Attribute format patterns: [type: name]
    [GeneratedRegex(@"\[enum:\s*(\w+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex EnumAttributeRegex();

    [GeneratedRegex(@"\[example:\s*(\w+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex ExampleAttributeRegex();

    [GeneratedRegex(@"\[struct:\s*(\w+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex StructAttributeRegex();

    // Native refs: [native: NAME] or [native: NAME | game]
    [GeneratedRegex(@"\[native:\s*([\w]+)(?:\s*\|\s*(\w+))?\]", RegexOptions.IgnoreCase)]
    private static partial Regex NativeAttributeRegex();

    // Callout patterns: [note: Description] or [note: Title | Description]
    [GeneratedRegex(@"\[note:\s*(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NoteAttributeRegex();

    [GeneratedRegex(@"\[warning:\s*(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WarningAttributeRegex();

    [GeneratedRegex(@"\[info:\s*(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InfoAttributeRegex();

    [GeneratedRegex(@"\[danger:\s*(.+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DangerAttributeRegex();

    /// <summary>
    /// Normalizes description text by cleaning up whitespace.
    /// Preserves [enum:], [struct:] markers for downstream processing.
    /// </summary>
    public string NormalizeDescription(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Remove markers that don't make sense in final output
        var result = NativeAttributeRegex().Replace(content, "");
        result = ExampleAttributeRegex().Replace(result, "");

        // Clean up double spaces
        result = Regex.Replace(result, @"\s{2,}", " ");

        return result.Trim();
    }

    /// <summary>
    /// Converts description text for code generation output.
    /// Transforms [enum: Name] and [struct: Name] to {@link Name} for JSDoc.
    /// Formats [note:], [warning:], [info:], [danger:] as readable callouts.
    /// </summary>
    public static string FormatDescriptionForCodeGen(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return content ?? string.Empty;

        // Convert [enum: Name] to {@link Name} for inline JSDoc references
        var result = EnumAttributeRegex().Replace(content, "{@link $1}");

        // Convert [struct: Name] to {@link Name}
        result = StructAttributeRegex().Replace(result, "{@link $1}");

        // Format callouts as @remarks tags (JSDoc/TypeDoc compatible)
        result = NoteAttributeRegex().Replace(result, m => FormatCalloutAsRemarks(m.Groups[1].Value));
        result = WarningAttributeRegex().Replace(result, m => FormatCalloutAsRemarks(m.Groups[1].Value, "Warning"));
        result = InfoAttributeRegex().Replace(result, m => FormatCalloutAsRemarks(m.Groups[1].Value));
        result = DangerAttributeRegex().Replace(result, m => FormatCalloutAsRemarks(m.Groups[1].Value, "Danger"));

        return result;
    }

    private static string FormatCalloutAsRemarks(string content, string? prefix = null)
    {
        var trimmed = content.Trim();
        // Check for title | description format
        var pipeIndex = trimmed.IndexOf('|');
        if (pipeIndex >= 0)
        {
            var title = trimmed[..pipeIndex].Trim();
            var description = trimmed[(pipeIndex + 1)..].Trim();
            trimmed = $"{title}: {description}";
        }

        // Add newlines before and after to separate from surrounding text
        return prefix != null
            ? $"\n@remarks **{prefix}:** {trimmed}\n"
            : $"\n@remarks {trimmed}\n";
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
            var game = match.Groups[2].Success ? match.Groups[2].Value : null;

            // Check for duplicates (same name and game)
            if (!results.Any(r => r.Name == name && r.Game == game))
                results.Add(new NativeRef { Name = name, Game = game });
        }

        return results;
    }

    public List<Callout> ParseCallouts(string content)
    {
        var results = new List<Callout>();

        ParseCalloutType(content, NoteAttributeRegex(), CalloutType.Note, results);
        ParseCalloutType(content, WarningAttributeRegex(), CalloutType.Warning, results);
        ParseCalloutType(content, InfoAttributeRegex(), CalloutType.Info, results);
        ParseCalloutType(content, DangerAttributeRegex(), CalloutType.Danger, results);

        return results;
    }

    private static void ParseCalloutType(string content, Regex regex, CalloutType type, List<Callout> results)
    {
        foreach (Match match in regex.Matches(content))
        {
            var value = match.Groups[1].Value.Trim();
            var pipeIndex = value.IndexOf('|');

            if (pipeIndex >= 0)
            {
                results.Add(new Callout
                {
                    Type = type,
                    Title = value[..pipeIndex].Trim(),
                    Description = value[(pipeIndex + 1)..].Trim()
                });
            }
            else
            {
                results.Add(new Callout
                {
                    Type = type,
                    Title = null,
                    Description = value
                });
            }
        }
    }
}
