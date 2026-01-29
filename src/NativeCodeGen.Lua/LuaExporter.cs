using NativeCodeGen.Core.Export;
using NativeCodeGen.Lua.Generation;

namespace NativeCodeGen.Lua;

public class LuaExporter : BaseExporter
{
    private readonly LuaGenerator _generator = new();

    protected override ICodeGenerator Generator => _generator;
}
