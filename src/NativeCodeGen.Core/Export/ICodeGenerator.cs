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
    /// <param name="useClasses">If true, generate class-based output; if false, generate raw/namespace output.</param>
    void Generate(NativeDatabase db, string outputPath, bool useClasses);

    /// <summary>
    /// Gets any warnings produced during generation.
    /// </summary>
    IReadOnlyList<string> Warnings { get; }
}
