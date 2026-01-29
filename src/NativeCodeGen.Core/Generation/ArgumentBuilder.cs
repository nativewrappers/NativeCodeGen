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
    public static string GetArgumentExpression(NativeParameter param, ITypeMapper typeMapper, LanguageConfig config, bool rawMode = false)
    {
        var f = config.FloatWrapperAlias;
        var h = config.HashWrapperAlias;
        var useFloat = config.UseFloatWrapper;
        var useHash = config.UseHashWrapper;

        // Output-only pointer (int*, float*, Vector3* without @in)
        if (param.IsOutput)
        {
            return typeMapper.GetPointerPlaceholder(param.Type);
        }

        // Input+output pointer (int*, float*, Vector3*, Entity* with @in)
        if (param.Type.IsPointer && param.Attributes.IsIn)
        {
            var format = typeMapper.GetInitializedPointerFormat(param.Type);
            // Handle types need to pass .handle (unless raw mode)
            var value = typeMapper.IsHandleType(param.Type) && !rawMode ? $"{param.Name}.handle" : param.Name;
            return string.Format(format, value);
        }

        // Vector3 expansion (non-pointer Vector3) - components are floats
        if (typeMapper.IsVector3(param.Type) && !param.Type.IsPointer)
        {
            if (useFloat)
                return $"{f}({param.Name}.x), {f}({param.Name}.y), {f}({param.Name}.z)";
            else
                return $"{param.Name}.x, {param.Name}.y, {param.Name}.z";
        }

        // Struct buffer
        if (param.Type.Category == TypeCategory.Struct)
        {
            return $"{param.Name}.buffer";
        }

        // Handle types - in raw mode, just pass the number directly
        if (typeMapper.IsHandleType(param.Type) && !rawMode)
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
        if (param.Type.Name is "float" or "f32" or "f64" or "double")
        {
            if (useFloat)
                return $"{f}({param.Name})";
            else
                return param.Name;
        }

        // Regular value
        return param.Name;
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
}
