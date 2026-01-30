namespace NativeCodeGen.Core.Models;

public class EnumDefinition
{
    public string Name { get; set; } = string.Empty;
    public string? BaseType { get; set; }
    public List<EnumMember> Members { get; set; } = new();
    public string? SourceFile { get; set; }

    /// <summary>
    /// Native hashes that use this enum
    /// </summary>
    public List<string> UsedByNatives { get; set; } = new();
}

public class EnumMember
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Comment { get; set; }
}
