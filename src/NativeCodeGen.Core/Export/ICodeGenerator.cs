using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Core.Export;

/// <summary>
/// Interface for code generators that produce output files.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Generates code output from the native database.
    /// </summary>
    /// <param name="db">The native database to generate from.</param>
    /// <param name="outputPath">The output directory path.</param>
    /// <param name="options">Generator options.</param>
    void Generate(NativeDatabase db, string outputPath, GeneratorOptions options);

    /// <summary>
    /// Gets any warnings produced during generation.
    /// </summary>
    IReadOnlyList<string> Warnings { get; }
}

/// <summary>
/// Options for code generation.
/// </summary>
public class GeneratorOptions
{
    /// <summary>
    /// If true, generate class-based output; if false, generate raw/namespace output.
    /// </summary>
    public bool UseClasses { get; set; } = true;

    /// <summary>
    /// If true with raw mode, generate all natives in a single file.
    /// </summary>
    public bool SingleFile { get; set; }
}
