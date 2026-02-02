namespace NativeCodeGen.Core.Models;

/// <summary>
/// Represents a code example from MDX documentation.
/// </summary>
public class CodeExample
{
    /// <summary>
    /// The language of the code example (e.g., "typescript", "lua", "javascript").
    /// </summary>
    public string Language { get; set; } = "typescript";

    /// <summary>
    /// The code content of the example.
    /// </summary>
    public string Code { get; set; } = string.Empty;
}
