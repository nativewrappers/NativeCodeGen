using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.TypeSystem;

namespace NativeCodeGen.Core.Generation;

public enum BindingStyle
{
    Global,      // globalThis.X = function() / function X()
    Export,      // export function X()
    Module       // ModuleName.X = function() / function ModuleName.X()
}

public enum Language
{
    TypeScript,
    Lua
}

public class RawNativeBuilder
{
    private readonly CodeBuilder _cb;
    private readonly Language _lang;
    private readonly ITypeMapper _typeMapper;
    private readonly LanguageConfig _config;

    public RawNativeBuilder(Language lang, ITypeMapper typeMapper)
    {
        _cb = new CodeBuilder();
        _lang = lang;
        _typeMapper = typeMapper;
        _config = lang == Language.TypeScript ? LanguageConfig.TypeScript : LanguageConfig.Lua;
    }

    public void Clear() => _cb.Clear();
    public override string ToString() => _cb.ToString();

    /// <summary>
    /// Emits the native aliases that should appear at the top of each file.
    /// Call this once per file before emitting functions.
    /// </summary>
    public void EmitImports(bool singleFile = false)
    {
        if (_lang == Language.TypeScript)
        {
            var prefix = singleFile ? "./" : "../";
            _cb.AppendLine($"import {{ Vector3 }} from '{prefix}types/Vector3';");
            _cb.AppendLine();
            EmitTypeScriptAliases();
        }
        else
        {
            EmitLuaAliases();
        }
    }

    /// <summary>
    /// Emits just the alias definitions without imports.
    /// Used by class-based emitters that handle imports separately.
    /// </summary>
    public static void EmitTypeScriptAliases(CodeBuilder cb)
    {
        var cfg = LanguageConfig.TypeScript;
        cb.AppendLine($"const {cfg.InvokeAlias} = Citizen.invokeNative;");
        cb.AppendLine($"const {cfg.ResultAsIntAlias} = Citizen.resultAsInteger;");
        cb.AppendLine($"const {cfg.ResultAsFloatAlias} = Citizen.resultAsFloat;");
        cb.AppendLine($"const {cfg.ResultAsStringAlias} = Citizen.resultAsString;");
        cb.AppendLine($"const {cfg.ResultAsVectorAlias} = Citizen.resultAsVector;");
        cb.AppendLine($"const {cfg.PointerIntAlias} = Citizen.pointerValueInt;");
        cb.AppendLine($"const {cfg.PointerFloatAlias} = Citizen.pointerValueFloat;");
        cb.AppendLine($"const {cfg.PointerVectorAlias} = Citizen.pointerValueVector;");
        cb.AppendLine($"const {cfg.PointerIntInitAlias} = Citizen.pointerValueIntInitialized;");
        cb.AppendLine($"const {cfg.PointerFloatInitAlias} = Citizen.pointerValueFloatInitialized;");
        cb.AppendLine($"const {cfg.FloatWrapperAlias} = (v: number) => v + 0.0000000001;");
        cb.AppendLine($"const {cfg.HashWrapperAlias} = (v: string | number) => (typeof v === 'string' ? GetHashKey(v) : v) & 0xFFFFFFFF;");
        cb.AppendLine();
    }

    private void EmitTypeScriptAliases() => EmitTypeScriptAliases(_cb);

    /// <summary>
    /// Emits Lua aliases. Static so it can be used by other emitters.
    /// </summary>
    public static void EmitLuaAliases(CodeBuilder cb)
    {
        var cfg = LanguageConfig.Lua;
        cb.AppendLine($"local {cfg.InvokeAlias} = Citizen.InvokeNative");
        cb.AppendLine($"local {cfg.ResultAsIntAlias} = Citizen.ResultAsInteger");
        cb.AppendLine($"local {cfg.ResultAsFloatAlias} = Citizen.ResultAsFloat");
        cb.AppendLine($"local {cfg.ResultAsStringAlias} = Citizen.ResultAsString");
        cb.AppendLine($"local {cfg.ResultAsVectorAlias} = Citizen.ResultAsVector");
        cb.AppendLine($"local {cfg.PointerIntAlias} = Citizen.PointerValueInt");
        cb.AppendLine($"local {cfg.PointerFloatAlias} = Citizen.PointerValueFloat");
        cb.AppendLine($"local {cfg.PointerVectorAlias} = Citizen.PointerValueVector");
        cb.AppendLine($"local {cfg.PointerIntInitAlias} = Citizen.PointerValueIntInitialized");
        cb.AppendLine($"local {cfg.PointerFloatInitAlias} = Citizen.PointerValueFloatInitialized");
        cb.AppendLine($"local {cfg.HashWrapperAlias} = function(v) return (type(v) == 'string' and GetHashKey(v) or v) & 0xFFFFFFFF end");
        cb.AppendLine();
    }

    private void EmitLuaAliases() => EmitLuaAliases(_cb);

    public void EmitModuleHeader(string moduleName)
    {
        if (_lang == Language.Lua)
        {
            _cb.AppendLine($"---@class {moduleName}");
            _cb.AppendLine($"local {moduleName} = {{}}");
            _cb.AppendLine();
        }
    }

    public void EmitModuleFooter(string moduleName)
    {
        if (_lang == Language.Lua)
        {
            _cb.AppendLine($"return {moduleName}");
        }
    }

    public void EmitFunction(NativeDefinition native, BindingStyle binding, string? moduleName = null, string? nameOverride = null)
    {
        var name = nameOverride ?? GetFunctionName(native.Name);
        var inputParams = native.Parameters.Where(p => !p.IsOutput).ToList();
        var outputParams = native.Parameters.Where(p => p.IsOutput).ToList();

        // Emit doc comment
        EmitDoc(native, inputParams, outputParams);

        // Emit function signature
        var paramList = BuildParamList(inputParams);
        var returnType = BuildReturnType(native.ReturnType, outputParams);

        EmitFunctionStart(name, paramList, returnType, binding, moduleName);

        // Emit body
        _cb.Indent();
        EmitInvokeNative(native, outputParams);
        _cb.Dedent();

        EmitFunctionEnd(binding);
        _cb.AppendLine();
    }

    private string GetFunctionName(string nativeName)
    {
        var trimmed = nativeName.TrimStart('_');
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return "N_" + trimmed;
        return ToPascalCase(trimmed);
    }

    private void EmitDoc(NativeDefinition native, List<NativeParameter> inputParams, List<NativeParameter> outputParams)
    {
        if (_lang == Language.TypeScript)
        {
            if (!string.IsNullOrWhiteSpace(native.Description))
            {
                _cb.AppendLine("/**");
                foreach (var line in native.Description.Split('\n'))
                    _cb.AppendLine($" * {line.TrimEnd()}");
            }
            else
            {
                _cb.AppendLine("/**");
            }

            foreach (var p in inputParams)
            {
                _cb.AppendLine($" * @param {p.Name} {p.Description ?? ""}".TrimEnd());
            }

            if (native.ReturnType.Category != TypeCategory.Void || outputParams.Count > 0)
            {
                _cb.AppendLine($" * @returns {native.ReturnDescription ?? ""}".TrimEnd());
            }

            _cb.AppendLine(" */");
        }
        else // Lua
        {
            if (!string.IsNullOrWhiteSpace(native.Description))
            {
                foreach (var line in native.Description.Split('\n'))
                    _cb.AppendLine($"--- {line.TrimEnd()}");
            }

            foreach (var p in inputParams)
            {
                var type = MapParamType(p.Type);
                _cb.AppendLine($"---@param {p.Name} {type}");
            }

            var returnType = BuildReturnType(native.ReturnType, outputParams);
            if (returnType != "void" && returnType != "nil")
            {
                _cb.AppendLine($"---@return {returnType}");
            }
        }
    }

    private string BuildParamList(List<NativeParameter> inputParams)
    {
        if (_lang == Language.TypeScript)
        {
            var parts = inputParams.Select(p =>
            {
                var type = MapParamType(p.Type);
                var opt = p.HasDefaultValue ? "?" : "";
                return $"{p.Name}{opt}: {type}";
            });
            return string.Join(", ", parts);
        }
        else
        {
            return string.Join(", ", inputParams.Select(p => p.Name));
        }
    }

    private string BuildReturnType(TypeInfo returnType, List<NativeParameter> outputParams)
    {
        var types = new List<string>();

        if (returnType.Category != TypeCategory.Void)
            types.Add(MapReturnType(returnType));

        foreach (var p in outputParams)
            types.Add(MapOutputType(p.Type));

        if (types.Count == 0)
            return _config.VoidType;

        if (types.Count == 1)
            return types[0];

        return _lang == Language.TypeScript
            ? $"[{string.Join(", ", types)}]"
            : string.Join(", ", types);
    }

    private void EmitFunctionStart(string name, string paramList, string returnType, BindingStyle binding, string? moduleName)
    {
        if (_lang == Language.TypeScript)
        {
            var signature = $"function({paramList}): {returnType}";
            switch (binding)
            {
                case BindingStyle.Global:
                    _cb.AppendLine($"globalThis.{name} = {signature} {{");
                    break;
                case BindingStyle.Export:
                    _cb.AppendLine($"export function {name}({paramList}): {returnType} {{");
                    break;
                case BindingStyle.Module:
                    _cb.AppendLine($"{moduleName}.{name} = {signature} {{");
                    break;
            }
        }
        else // Lua
        {
            switch (binding)
            {
                case BindingStyle.Global:
                    _cb.AppendLine($"function {name}({paramList})");
                    break;
                case BindingStyle.Module:
                    _cb.AppendLine($"function {moduleName}.{name}({paramList})");
                    break;
                case BindingStyle.Export:
                    _cb.AppendLine($"function {name}({paramList})");
                    break;
            }
        }
    }

    private void EmitFunctionEnd(BindingStyle binding)
    {
        if (_lang == Language.TypeScript)
        {
            _cb.AppendLine(binding == BindingStyle.Export ? "}" : "};");
        }
        else
        {
            _cb.AppendLine("end");
        }
    }

    private void EmitInvokeNative(NativeDefinition native, List<NativeParameter> outputParams)
    {
        var args = new List<string>();

        // Hash
        if (_lang == Language.TypeScript)
            args.Add($"'{native.Hash}'");
        else
            args.Add(native.Hash);

        // Input params - use ArgumentBuilder shared logic
        foreach (var p in native.Parameters.Where(p => !p.IsOutput))
        {
            args.Add(GetArgumentExpression(p));
        }

        // Output param pointers - use the type mapper
        foreach (var p in outputParams)
        {
            args.Add(_typeMapper.GetPointerPlaceholder(p.Type));
        }

        // Result marker - use the type mapper
        if (_typeMapper.NeedsResultMarker(native.ReturnType))
        {
            args.Add(_typeMapper.GetResultMarker(native.ReturnType));
        }

        var hasReturn = native.ReturnType.Category != TypeCategory.Void || outputParams.Count > 0;

        // Check if we need complex return handling (Vector3 wrapping or Hash unsigned conversion)
        var hasVector3Output = _lang == Language.TypeScript &&
            outputParams.Any(p => p.Type.Category == TypeCategory.Vector3);
        var hasHashReturn = native.ReturnType.Category == TypeCategory.Hash;
        var needsComplexReturn = hasVector3Output || (hasHashReturn && outputParams.Count > 0);

        if (needsComplexReturn)
        {
            EmitComplexReturn(native, outputParams, args);
        }
        else if (hasReturn)
        {
            var invokeExpr = $"{_config.InvokeAlias}({string.Join(", ", args)})";

            // Wrap hash returns to ensure unsigned (simple case with no output params)
            if (hasHashReturn)
            {
                invokeExpr = $"({invokeExpr}) & 0xFFFFFFFF";
            }

            _cb.AppendLine($"return {invokeExpr};");
        }
        else
        {
            _cb.AppendLine($"{_config.InvokeAlias}({string.Join(", ", args)});");
        }
    }

    /// <summary>
    /// Gets the argument expression for a parameter.
    /// Handles float wrapping, hash wrapping, Vector3 expansion.
    /// </summary>
    private string GetArgumentExpression(NativeParameter p)
    {
        var f = _config.FloatWrapperAlias;
        var h = _config.HashWrapperAlias;
        var useFloat = _config.UseFloatWrapper;
        var useHash = _config.UseHashWrapper;

        if (p.Type.Category == TypeCategory.Vector3)
        {
            // Vector3 components are floats - wrap if language needs it
            if (useFloat)
                return $"{f}({p.Name}.x), {f}({p.Name}.y), {f}({p.Name}.z)";
            else
                return $"{p.Name}.x, {p.Name}.y, {p.Name}.z";
        }
        else if (p.Type.Category == TypeCategory.Hash || p.Type.Name == "Hash")
        {
            if (useHash)
                return $"{h}({p.Name})";
            else
                return p.Name;
        }
        else if (p.Type.Name is "float" or "f32" or "f64" or "double")
        {
            if (useFloat)
                return $"{f}({p.Name})";
            else
                return p.Name;
        }
        else
        {
            return p.Name;
        }
    }

    private void EmitComplexReturn(NativeDefinition native, List<NativeParameter> outputParams, List<string> args)
    {
        var isSingleVector3 = outputParams.Count == 1 &&
            native.ReturnType.Category == TypeCategory.Void &&
            outputParams[0].Type.Category == TypeCategory.Vector3;

        var resultVar = _lang == Language.TypeScript ? "const result" : "local result";
        _cb.AppendLine($"{resultVar} = {_config.InvokeAlias}({string.Join(", ", args)});");

        if (isSingleVector3)
        {
            _cb.AppendLine("return Vector3.fromArray(result);");
        }
        else
        {
            var parts = new List<string>();
            int idx = 0;

            if (native.ReturnType.Category != TypeCategory.Void)
            {
                var expr = $"result[{idx}]";
                // Wrap hash return to ensure unsigned
                if (native.ReturnType.Category == TypeCategory.Hash)
                    expr = $"{expr} & 0xFFFFFFFF";
                parts.Add(expr);
                idx++;
            }

            foreach (var p in outputParams)
            {
                if (p.Type.Category == TypeCategory.Vector3)
                    parts.Add($"Vector3.fromArray(result[{idx}])");
                else
                    parts.Add($"result[{idx}]");
                idx++;
            }

            if (parts.Count == 1)
                _cb.AppendLine($"return {parts[0]};");
            else
                _cb.AppendLine($"return [{string.Join(", ", parts)}];");
        }
    }

    private string MapParamType(TypeInfo type)
    {
        if (type.Category == TypeCategory.Handle)
            return _config.NumberType;

        return _typeMapper.MapType(type);
    }

    private string MapReturnType(TypeInfo type)
    {
        if (type.Category == TypeCategory.Handle)
            return _config.NumberType;

        if (type.Category == TypeCategory.Vector3)
            return _config.Vector3Type;

        return _typeMapper.MapType(type);
    }

    private string MapOutputType(TypeInfo type)
    {
        if (type.Category == TypeCategory.Vector3)
            return _config.Vector3Type;

        return type.Name switch
        {
            "float" or "f32" or "f64" or "double" => _config.NumberType,
            "BOOL" or "bool" => _config.BooleanType,
            _ => _config.NumberType
        };
    }

    private static string ToPascalCase(string name)
    {
        var sb = new System.Text.StringBuilder();
        bool capitalizeNext = true;

        foreach (var c in name)
        {
            if (c == '_')
            {
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}
