using System.Text;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Helper for building source code with proper indentation.
/// </summary>
public class CodeBuilder
{
    private readonly StringBuilder _sb = new();
    private readonly string _indentChar;
    private int _indentLevel;

    public CodeBuilder(string indentChar = "  ")
    {
        _indentChar = indentChar;
    }

    public CodeBuilder Indent()
    {
        _indentLevel++;
        return this;
    }

    public CodeBuilder Dedent()
    {
        if (_indentLevel > 0)
            _indentLevel--;
        return this;
    }

    public CodeBuilder AppendLine()
    {
        _sb.AppendLine();
        return this;
    }

    public CodeBuilder AppendLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            _sb.AppendLine();
        }
        else
        {
            _sb.Append(GetIndent());
            _sb.AppendLine(line);
        }
        return this;
    }

    public CodeBuilder Append(string text)
    {
        _sb.Append(text);
        return this;
    }

    public CodeBuilder AppendIndented(string text)
    {
        _sb.Append(GetIndent());
        _sb.Append(text);
        return this;
    }

    /// <summary>
    /// Appends a block with opening brace, executes action, then closes.
    /// </summary>
    public CodeBuilder Block(string header, Action<CodeBuilder> body)
    {
        AppendLine($"{header} {{");
        Indent();
        body(this);
        Dedent();
        AppendLine("}");
        return this;
    }

    /// <summary>
    /// Appends multiple lines, each indented.
    /// </summary>
    public CodeBuilder AppendLines(params string[] lines)
    {
        foreach (var line in lines)
        {
            AppendLine(line);
        }
        return this;
    }

    private string GetIndent() => string.Concat(Enumerable.Repeat(_indentChar, _indentLevel));

    public override string ToString() => _sb.ToString();

    public int Length => _sb.Length;

    public void Clear()
    {
        _sb.Clear();
        _indentLevel = 0;
    }
}
