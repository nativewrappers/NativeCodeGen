using System.Text;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Base documentation builder that collects doc elements.
/// Language-specific builders override Render() to output their format.
/// </summary>
public abstract class DocBuilder
{
    protected readonly List<string> DescriptionLines = new();
    protected readonly List<DocParam> Params = new();
    protected readonly List<DocThrows> Throws = new();
    protected DocReturn? Return;

    public DocBuilder AddDescription(string? description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            foreach (var line in description.Split('\n'))
            {
                DescriptionLines.Add(line.Trim());
            }
        }
        return this;
    }

    public DocBuilder AddParam(string name, string? description = null)
    {
        Params.Add(new DocParam(name, "", description ?? ""));
        return this;
    }

    public DocBuilder AddParam(string name, string type, string? description = null)
    {
        Params.Add(new DocParam(name, type, description ?? ""));
        return this;
    }

    public DocBuilder AddThrows(string type, string description)
    {
        Throws.Add(new DocThrows(type, description));
        return this;
    }

    public DocBuilder AddReturn(string type, string? description = null)
    {
        Return = new DocReturn(type, description);
        return this;
    }

    public bool IsEmpty => DescriptionLines.Count == 0 && Params.Count == 0 && Throws.Count == 0 && Return == null;

    /// <summary>
    /// Renders the documentation to the CodeBuilder.
    /// </summary>
    public abstract void Render(CodeBuilder cb);

    /// <summary>
    /// Renders to a StringBuilder (for backwards compatibility).
    /// </summary>
    public void Build(StringBuilder sb)
    {
        var cb = new CodeBuilder();
        Render(cb);
        sb.Append(cb.ToString());
    }

    /// <summary>
    /// Alias for Build(StringBuilder) for backwards compatibility.
    /// </summary>
    public void Build()
    {
        // No-op if no target - subclasses with their own StringBuilder can override
    }
}

/// <summary>
/// JSDoc format documentation (/** ... */)
/// </summary>
public class JsDocBuilder : DocBuilder
{
    /// <summary>
    /// Escapes text that could break JSDoc comments (e.g., */ would close the comment prematurely).
    /// </summary>
    private static string EscapeJsDoc(string text)
    {
        // Replace */ with *\/ to prevent closing the JSDoc comment
        return text.Replace("*/", "*\\/");
    }

    public override void Render(CodeBuilder cb)
    {
        if (IsEmpty) return;

        cb.AppendLine("/**");

        foreach (var line in DescriptionLines)
        {
            cb.AppendLine($" * {EscapeJsDoc(line)}");
        }

        if (DescriptionLines.Count > 0 && (Params.Count > 0 || Throws.Count > 0 || Return != null))
        {
            cb.AppendLine(" *");
        }

        foreach (var param in Params)
        {
            cb.AppendLine($" * @param {param.Name} {EscapeJsDoc(param.Description)}");
        }

        foreach (var throws in Throws)
        {
            cb.AppendLine($" * @throws {{{throws.Type}}} {EscapeJsDoc(throws.Description)}");
        }

        if (Return != null)
        {
            var desc = string.IsNullOrEmpty(Return.Description) ? "" : $" {EscapeJsDoc(Return.Description)}";
            cb.AppendLine($" * @returns{desc}");
        }

        cb.AppendLine(" */");
    }
}

/// <summary>
/// EmmyLua/LuaLS annotation format (---@param, ---@return)
/// </summary>
public class LuaDocBuilder : DocBuilder
{
    public override void Render(CodeBuilder cb)
    {
        foreach (var line in DescriptionLines)
        {
            cb.AppendLine($"--- {line}");
        }

        foreach (var param in Params)
        {
            var desc = string.IsNullOrEmpty(param.Description) ? "" : $" {param.Description}";
            cb.AppendLine($"---@param {param.Name} {param.Type}{desc}");
        }

        if (Return != null)
        {
            var desc = string.IsNullOrEmpty(Return.Description) ? "" : $" {Return.Description}";
            cb.AppendLine($"---@return {Return.Type}{desc}");
        }
    }
}

/// <summary>
/// C# XML doc format (/// <summary>)
/// </summary>
public class XmlDocBuilder : DocBuilder
{
    public override void Render(CodeBuilder cb)
    {
        if (DescriptionLines.Count > 0)
        {
            cb.AppendLine("/// <summary>");
            foreach (var line in DescriptionLines)
            {
                cb.AppendLine($"/// {line}");
            }
            cb.AppendLine("/// </summary>");
        }

        foreach (var param in Params)
        {
            cb.AppendLine($"/// <param name=\"{param.Name}\">{param.Description}</param>");
        }

        if (Return != null && !string.IsNullOrEmpty(Return.Description))
        {
            cb.AppendLine($"/// <returns>{Return.Description}</returns>");
        }
    }
}

public record DocParam(string Name, string Type, string Description);
public record DocReturn(string Type, string? Description);
public record DocThrows(string Type, string Description);
