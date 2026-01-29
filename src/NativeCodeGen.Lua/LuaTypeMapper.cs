using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.TypeSystem;

namespace NativeCodeGen.Lua;

/// <summary>
/// Lua type mapper implementation.
/// </summary>
public class LuaTypeMapper : TypeMapperBase
{
    public LuaTypeMapper() : base(LanguageConfig.Lua) { }
}
