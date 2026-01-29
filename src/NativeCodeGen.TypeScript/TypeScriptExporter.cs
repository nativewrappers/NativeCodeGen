using NativeCodeGen.Core.Export;
using NativeCodeGen.TypeScript.Generation;

namespace NativeCodeGen.TypeScript;

public class TypeScriptExporter : BaseExporter
{
    private readonly TypeScriptGenerator _generator = new();

    protected override ICodeGenerator Generator => _generator;
}
