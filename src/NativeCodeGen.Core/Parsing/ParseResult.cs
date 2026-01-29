using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Parsing;

public class ParseResult<T>
{
    public T? Value { get; set; }
    public List<ParseError> Errors { get; set; } = new();
    public List<ParseWarning> Warnings { get; set; } = new();

    public bool HasErrors => Errors.Count > 0;
    public bool HasWarnings => Warnings.Count > 0;
    public bool IsSuccess => !HasErrors && Value != null;
}

public class ParseError
{
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = string.Empty;

    public override string ToString() =>
        $"{FilePath}:{Line}:{Column}: error: {Message}";
}

public class ParseWarning
{
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = string.Empty;

    public override string ToString() =>
        $"{FilePath}:{Line}:{Column}: warning: {Message}";
}

public class NativeDatabase
{
    public List<NativeNamespace> Namespaces { get; set; } = new();
    public Dictionary<string, EnumDefinition> Enums { get; set; } = new();
    public Dictionary<string, StructDefinition> Structs { get; set; } = new();
    public Dictionary<string, SharedExample> SharedExamples { get; set; } = new();
}

public class NativeNamespace
{
    public string Name { get; set; } = string.Empty;
    public List<NativeDefinition> Natives { get; set; } = new();
}
