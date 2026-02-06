using NativeCodeGen.Core.Export;
using NativeCodeGen.CSharp.Generation;

namespace NativeCodeGen.CSharp;

public class CSharpExporter : BaseExporter
{
    private readonly CSharpGenerator _generator = new();

    protected override ICodeGenerator Generator => _generator;
}
