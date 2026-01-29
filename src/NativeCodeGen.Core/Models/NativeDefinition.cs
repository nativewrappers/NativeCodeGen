namespace NativeCodeGen.Core.Models;

public class NativeDefinition
{
    public string Namespace { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
    public TypeInfo ReturnType { get; set; } = new();
    public List<NativeParameter> Parameters { get; set; } = new();
    public string? Description { get; set; }
    public string? ReturnDescription { get; set; }
    public NativeAttributes Attributes { get; set; } = new();
    public string ApiSet { get; set; } = "client";
    public List<string> UsedEnums { get; set; } = new();
    public List<string> RelatedExamples { get; set; } = new();
    public List<Callout> Callouts { get; set; } = new();
    public string? SourceFile { get; set; }
}

public class NativeAttributes
{
    public bool IsDeprecated { get; set; }
    public string? DeprecatedMessage { get; set; }
    public List<string> CustomAttributes { get; set; } = new();
}
