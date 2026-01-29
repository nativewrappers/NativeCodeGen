namespace NativeCodeGen.Core.Models;

public class SharedExample
{
    public string Name { get; set; } = string.Empty;
    public required string Title { get; set; }
    public List<SharedExampleCode> Examples { get; set; } = new();
    public string? SourceFile { get; set; }
}

public class SharedExampleCode
{
    public string Content { get; set; } = string.Empty;
    public string? Language { get; set; }
}
