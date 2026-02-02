using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.TypeSystem;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Shared logic for building native invoke arguments across language generators.
/// </summary>
public static class ArgumentBuilder
{
    /// <summary>
    /// Gets the argument expression for a parameter, handling all parameter types:
    /// - Regular inputs: pass their value directly
    /// - Vector3: expand to x, y, z
    /// - Structs: pass .buffer
    /// - Handles: pass .handle (unless rawMode)
    /// - Output-only pointers: pass pointer placeholder
    /// - Input+output pointers (@in): pass initialized pointer
    /// </summary>
    public static string GetArgumentExpression(NativeParameter param, ITypeMapper typeMapper, LanguageConfig config, bool rawMode = false, string? mappedDefaultValue = null)
    {
        var f = config.FloatWrapperAlias;
        var h = config.HashWrapperAlias;
        var useFloat = config.UseFloatWrapper;
        var useHash = config.UseHashWrapper;

        // Pure output pointer (int*, float*, Vector3* without @in)
        if (param.IsPureOutput)
        {
            return typeMapper.GetPointerPlaceholder(param.Type);
        }

        // Input+output pointer (int*, float*, Vector3*, Entity* with @in)
        if (param.IsInOut)
        {
            var format = typeMapper.GetInitializedPointerFormat(param.Type);
            // Only class handles (with generated classes) have .handle property
            var value = typeMapper.IsHandleType(param.Type) && TypeInfo.IsClassHandle(param.Type.Name) && !rawMode
                ? $"{param.Name}.handle"
                : param.Name;
            return string.Format(format, value);
        }

        // Vector expansion (non-pointer Vector2/Vector3/Vector4) - components are floats
        if (param.Type.IsVector && !param.Type.IsPointer)
        {
            return ExpandVector(param.Name, param.Type.VectorComponentCount, useFloat, f);
        }

        // Color expansion (non-pointer Color) - components are integers (r, g, b, a)
        if (param.Type.Category == TypeCategory.Color && !param.Type.IsPointer)
        {
            return $"{param.Name}.r, {param.Name}.g, {param.Name}.b, {param.Name}.a";
        }

        // Struct buffer
        if (param.Type.Category == TypeCategory.Struct)
        {
            return $"{param.Name}.buffer";
        }

        // Handle types - only class handles have a .handle property
        // Non-class handles (ScrHandle, Prompt) are just numbers
        if (typeMapper.IsHandleType(param.Type) && TypeInfo.IsClassHandle(param.Type.Name) && !rawMode)
        {
            return $"{param.Name}.handle";
        }

        // Hash type - wrap with h() for string conversion and unsigned
        if (param.Type.Category == TypeCategory.Hash || param.Type.Name == "Hash")
        {
            if (useHash)
                return $"{h}({param.Name})";
            else
                return param.Name;
        }

        // Float type - wrap with f() to prevent bundler optimization
        if (param.Type.IsFloat)
        {
            var expr = useFloat ? $"{f}({param.Name})" : param.Name;
            return WrapWithInlineDefault(expr, param.Name, mappedDefaultValue, config);
        }

        // Regular value
        return WrapWithInlineDefault(param.Name, param.Name, mappedDefaultValue, config);
    }

    /// <summary>
    /// Wraps an expression with inline default value check for Lua.
    /// Pattern: paramName == nil and defaultValue or expression
    /// </summary>
    private static string WrapWithInlineDefault(string expression, string paramName, string? mappedDefaultValue, LanguageConfig config)
    {
        if (!config.UseInlineDefaults || mappedDefaultValue == null)
            return expression;

        // For simple param references, use: param == nil and default or param
        // For complex expressions (like f(param)), we need parentheses
        if (expression == paramName)
            return $"{paramName} == nil and {mappedDefaultValue} or {paramName}";

        return $"({paramName} == nil and {mappedDefaultValue} or {expression})";
    }

    /// <summary>
    /// Builds the list of arguments for a native invoke call.
    /// Iterates over ALL parameters to include output pointer placeholders.
    /// </summary>
    public static List<string> BuildInvokeArgs(
        NativeDefinition native,
        IEnumerable<NativeParameter> allParams,
        ITypeMapper typeMapper,
        LanguageConfig config,
        string hashFormat = "'{0}'",
        bool rawMode = false)
    {
        var args = new List<string> { string.Format(hashFormat, native.Hash) };

        foreach (var param in allParams)
        {
            args.Add(GetArgumentExpression(param, typeMapper, config, rawMode));
        }

        if (typeMapper.NeedsResultMarker(native.ReturnType))
        {
            args.Add(typeMapper.GetResultMarker(native.ReturnType));
        }

        return args;
    }

    /// <summary>
    /// Builds the list of arguments with a custom first argument (e.g., "this.handle" for instance methods).
    /// The first parameter from allParams is skipped (assumed to be the "this" parameter).
    /// </summary>
    public static List<string> BuildInvokeArgsWithFirst(
        NativeDefinition native,
        string firstArg,
        IEnumerable<NativeParameter> remainingParams,
        ITypeMapper typeMapper,
        LanguageConfig config,
        string hashFormat = "'{0}'")
    {
        var args = new List<string> { string.Format(hashFormat, native.Hash), firstArg };

        foreach (var param in remainingParams)
        {
            args.Add(GetArgumentExpression(param, typeMapper, config));
        }

        if (typeMapper.NeedsResultMarker(native.ReturnType))
        {
            args.Add(typeMapper.GetResultMarker(native.ReturnType));
        }

        return args;
    }

    /// <summary>
    /// Expands a vector parameter to its component arguments (x, y, [z], [w]).
    /// </summary>
    private static string ExpandVector(string paramName, int componentCount, bool useFloat, string floatAlias)
    {
        var components = TypeInfo.VectorComponents[..componentCount];
        return useFloat
            ? string.Join(", ", components.Select(c => $"{floatAlias}({paramName}.{c})"))
            : string.Join(", ", components.Select(c => $"{paramName}.{c}"));
    }
}
