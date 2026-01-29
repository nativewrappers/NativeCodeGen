namespace NativeCodeGen.Core.Models;

public class SharedExample
{
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? SourceFile { get; set; }
}
