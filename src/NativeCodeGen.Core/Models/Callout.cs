namespace NativeCodeGen.Core.Models;

public enum CalloutType
{
    Note = 0,
    Warning = 1,
    Info = 2,
    Danger = 3
}

public class Callout
{
    public CalloutType Type { get; set; }
    public string? Title { get; set; }
    public required string Description { get; set; }
}
