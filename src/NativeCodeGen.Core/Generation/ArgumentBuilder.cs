using NativeCodeGen.Core.Models;

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
    /// - Handles: pass .handle
    /// - Output-only pointers: pass pointer placeholder
    /// - Input+output pointers (@in): pass initialized pointer
    /// </summary>
    public static string GetArgumentExpression(NativeParameter param, ITypeMapper typeMapper)
    {
        // Output-only pointer (int*, float*, Vector3* without @in)
        if (param.IsOutput)
        {
            return typeMapper.GetPointerPlaceholder(param.Type);
        }

        // Input+output pointer (int*, float*, Vector3*, Entity* with @in)
        if (param.Type.IsPointer && param.Attributes.IsIn)
        {
            var format = typeMapper.GetInitializedPointerFormat(param.Type);
            // Handle types need to pass .handle
            var value = typeMapper.IsHandleType(param.Type) ? $"{param.Name}.handle" : param.Name;
            return string.Format(format, value);
        }

        // Vector3 expansion (non-pointer Vector3)
        if (typeMapper.IsVector3(param.Type) && !param.Type.IsPointer)
        {
            return $"{param.Name}.x, {param.Name}.y, {param.Name}.z";
        }

        // Struct buffer
        if (param.Type.Category == TypeCategory.Struct)
        {
            return $"{param.Name}.buffer";
        }

        // Handle types
        if (typeMapper.IsHandleType(param.Type))
        {
            return $"{param.Name}.handle";
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
        string hashFormat = "'{0}'")
    {
        var args = new List<string> { string.Format(hashFormat, native.Hash) };

        foreach (var param in allParams)
        {
            args.Add(GetArgumentExpression(param, typeMapper));
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
        string hashFormat = "'{0}'")
    {
        var args = new List<string> { string.Format(hashFormat, native.Hash), firstArg };

        foreach (var param in remainingParams)
        {
            args.Add(GetArgumentExpression(param, typeMapper));
        }

        if (typeMapper.NeedsResultMarker(native.ReturnType))
        {
            args.Add(typeMapper.GetResultMarker(native.ReturnType));
        }

        return args;
    }
}
