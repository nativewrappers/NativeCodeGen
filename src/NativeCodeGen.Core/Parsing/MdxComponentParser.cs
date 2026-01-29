namespace NativeCodeGen.Core.Parsing;

public class EmbeddedEnumRef
{
    public string Name { get; set; } = string.Empty;
}

public class SharedExampleRef
{
    public string Name { get; set; } = string.Empty;
}

public class MdxComponentParser
{
    public List<EmbeddedEnumRef> ParseEmbeddedEnums(string content)
    {
        var results = new List<EmbeddedEnumRef>();
        var index = 0;

        while (index < content.Length)
        {
            // Find <EmbeddedEnum
            var tagStart = content.IndexOf("<EmbeddedEnum", index, StringComparison.OrdinalIgnoreCase);
            if (tagStart == -1)
                break;

            // Find the closing />
            var tagEnd = content.IndexOf("/>", tagStart);
            if (tagEnd == -1)
                break;

            // Extract the tag content
            var tagContent = content[tagStart..(tagEnd + 2)];

            // Parse name attribute
            var name = ExtractAttribute(tagContent, "name");
            if (name != null)
            {
                results.Add(new EmbeddedEnumRef { Name = name });
            }

            index = tagEnd + 2;
        }

        return results;
    }

    public List<SharedExampleRef> ParseSharedExamples(string content)
    {
        var results = new List<SharedExampleRef>();
        var index = 0;

        while (index < content.Length)
        {
            // Find <SharedExample
            var tagStart = content.IndexOf("<SharedExample", index, StringComparison.OrdinalIgnoreCase);
            if (tagStart == -1)
                break;

            // Find the closing />
            var tagEnd = content.IndexOf("/>", tagStart);
            if (tagEnd == -1)
                break;

            // Extract the tag content
            var tagContent = content[tagStart..(tagEnd + 2)];

            // Parse name attribute
            var name = ExtractAttribute(tagContent, "name");
            if (name != null)
            {
                results.Add(new SharedExampleRef { Name = name });
            }

            index = tagEnd + 2;
        }

        return results;
    }

    private static string? ExtractAttribute(string tag, string attributeName)
    {
        // Find name= or name =
        var attrIndex = FindAttribute(tag, attributeName);
        if (attrIndex == -1)
            return null;

        // Find the = sign
        var equalsIndex = tag.IndexOf('=', attrIndex);
        if (equalsIndex == -1)
            return null;

        // Skip whitespace after =
        var valueStart = equalsIndex + 1;
        while (valueStart < tag.Length && char.IsWhiteSpace(tag[valueStart]))
        {
            valueStart++;
        }

        if (valueStart >= tag.Length)
            return null;

        // Check for quote character
        var quote = tag[valueStart];
        if (quote != '"' && quote != '\'')
            return null;

        valueStart++;

        // Find closing quote
        var valueEnd = tag.IndexOf(quote, valueStart);
        if (valueEnd == -1)
            return null;

        return tag[valueStart..valueEnd];
    }

    private static int FindAttribute(string tag, string attributeName)
    {
        var index = 0;
        while (index < tag.Length)
        {
            var found = tag.IndexOf(attributeName, index, StringComparison.OrdinalIgnoreCase);
            if (found == -1)
                return -1;

            // Check that this is a standalone attribute name (not part of another word)
            var beforeOk = found == 0 || !char.IsLetterOrDigit(tag[found - 1]);
            var afterOk = found + attributeName.Length >= tag.Length ||
                          !char.IsLetterOrDigit(tag[found + attributeName.Length]);

            if (beforeOk && afterOk)
            {
                // Skip whitespace to find =
                var checkIndex = found + attributeName.Length;
                while (checkIndex < tag.Length && char.IsWhiteSpace(tag[checkIndex]))
                {
                    checkIndex++;
                }
                if (checkIndex < tag.Length && tag[checkIndex] == '=')
                {
                    return found;
                }
            }

            index = found + 1;
        }

        return -1;
    }
}
