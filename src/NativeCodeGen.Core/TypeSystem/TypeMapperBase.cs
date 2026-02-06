using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.TypeSystem;

/// <summary>
/// Language configuration for type mappers.
/// </summary>
public record LanguageConfig
{
    // Type names
    public required string VoidType { get; init; }
    public required string NumberType { get; init; }
    public required string BooleanType { get; init; }
    public required string StringType { get; init; }
    public required string Vector2Type { get; init; }
    public required string Vector3Type { get; init; }
    public required string Vector4Type { get; init; }
    public required string ColorType { get; init; }
    public required string AnyType { get; init; }
    public required string NullableSuffix { get; init; }  // " | null" or "|nil"
    public required string HashType { get; init; }  // "string | number" or "number"

    // Handles are typed in TS but just numbers in Lua
    public required bool UseTypedHandles { get; init; }

    // Short aliases (these are used after being defined at file top)
    public required string InvokeAlias { get; init; }         // inv
    public required string ResultAsIntAlias { get; init; }    // rai
    public required string ResultAsFloatAlias { get; init; }  // raf
    public required string ResultAsStringAlias { get; init; } // ras
    public required string ResultAsVectorAlias { get; init; } // rav
    public required string PointerIntAlias { get; init; }     // pvi
    public required string PointerFloatAlias { get; init; }   // pvf
    public required string PointerVectorAlias { get; init; }  // pvv
    public required string PointerIntInitAlias { get; init; } // pvii
    public required string PointerFloatInitAlias { get; init; } // pvfi
    public required string FloatWrapperAlias { get; init; }   // f
    public required string HashWrapperAlias { get; init; }    // h

    // Whether to use wrappers (Lua doesn't need float wrapper since no bundler issue)
    public required bool UseFloatWrapper { get; init; }
    public required bool UseHashWrapper { get; init; }

    // Whether the language supports getter properties (TypeScript does, Lua doesn't)
    public required bool SupportsGetters { get; init; }

    // Whether to use inline default values in invoke args (Lua needs this, TS uses param defaults)
    public required bool UseInlineDefaults { get; init; }

    // Whether to use primitive type aliases (float, u8, etc.) for better documentation
    public required bool UsePrimitiveAliases { get; init; }

    // Whether the language supports optional chaining (?.) and nullish coalescing (??)
    public required bool UseOptionalChaining { get; init; }

    public static readonly LanguageConfig TypeScript = new()
    {
        VoidType = "void",
        NumberType = "number",
        BooleanType = "boolean",
        StringType = "string",
        Vector2Type = "Vector2",
        Vector3Type = "Vector3",
        Vector4Type = "Vector4",
        ColorType = "Color",
        AnyType = "any",
        NullableSuffix = " | null",
        HashType = "string | number",
        UseTypedHandles = true,

        InvokeAlias = "inv",
        ResultAsIntAlias = "rai",
        ResultAsFloatAlias = "raf",
        ResultAsStringAlias = "ras",
        ResultAsVectorAlias = "rav",
        PointerIntAlias = "pvi",
        PointerFloatAlias = "pvf",
        PointerVectorAlias = "pvv",
        PointerIntInitAlias = "pvii",
        PointerFloatInitAlias = "pvfi",
        FloatWrapperAlias = "f",
        HashWrapperAlias = "_h",
        UseFloatWrapper = true,
        UseHashWrapper = true,
        SupportsGetters = true,
        UseInlineDefaults = false,
        UsePrimitiveAliases = true,
        UseOptionalChaining = true
    };

    public static readonly LanguageConfig Lua = new()
    {
        VoidType = "nil",
        NumberType = "number",
        BooleanType = "boolean",
        StringType = "string",
        Vector2Type = "vector2",
        Vector3Type = "vector3",
        Vector4Type = "vector4",
        ColorType = "Color",
        AnyType = "any",
        NullableSuffix = "|nil",
        HashType = "string|number",
        UseTypedHandles = true,

        InvokeAlias = "inv",
        ResultAsIntAlias = "rai",
        ResultAsFloatAlias = "raf",
        ResultAsStringAlias = "ras",
        ResultAsVectorAlias = "rav",
        PointerIntAlias = "pvi",
        PointerFloatAlias = "pvf",
        PointerVectorAlias = "pvv",
        PointerIntInitAlias = "pvii",
        PointerFloatInitAlias = "pvfi",
        FloatWrapperAlias = "f",
        HashWrapperAlias = "_h",
        UseFloatWrapper = false,
        UseHashWrapper = true,
        SupportsGetters = false,
        UseInlineDefaults = true,
        UsePrimitiveAliases = false,
        UseOptionalChaining = false
    };

    public static readonly LanguageConfig CSharp = new()
    {
        VoidType = "void",
        NumberType = "int",
        BooleanType = "bool",
        StringType = "string",
        Vector2Type = "Vector2",
        Vector3Type = "Vector3",
        Vector4Type = "Vector4",
        ColorType = "Color",
        AnyType = "object",
        NullableSuffix = "?",
        HashType = "uint",  // C# uses uint for hashes
        UseTypedHandles = true,

        // C# doesn't use aliases - direct Function.Call<T>()
        InvokeAlias = "Function.Call",
        ResultAsIntAlias = "",
        ResultAsFloatAlias = "",
        ResultAsStringAlias = "",
        ResultAsVectorAlias = "",
        PointerIntAlias = "",
        PointerFloatAlias = "",
        PointerVectorAlias = "",
        PointerIntInitAlias = "",
        PointerFloatInitAlias = "",
        FloatWrapperAlias = "",
        HashWrapperAlias = "",
        UseFloatWrapper = false,
        UseHashWrapper = false,
        SupportsGetters = true,
        UseInlineDefaults = false,
        UsePrimitiveAliases = true,
        UseOptionalChaining = false  // C# uses null-conditional ?. but we handle it differently
    };
}

/// <summary>
/// Base class for language-specific type mappers with shared logic.
/// </summary>
public abstract class TypeMapperBase : ITypeMapper
{
    protected readonly LanguageConfig Config;

    protected TypeMapperBase(LanguageConfig config)
    {
        Config = config;
    }

    public virtual string MapType(TypeInfo type, bool isNullable = false, bool forReturn = false)
    {
        // Handle fixed-size arrays: int[3] -> [number, number, number]
        if (type.IsFixedArray)
        {
            var elementType = MapPrimitive(type.Name);
            return FormatTuple(Enumerable.Repeat(elementType, type.ArraySize!.Value));
        }

        if (type.IsPointer)
        {
            if (type.Name == "char" || type.Name == "string")
            {
                return MapStringType(isNullable);
            }
            if (type.Category == TypeCategory.Struct)
            {
                return type.Name;
            }
            return MapPrimitive(type.Name);
        }

        return type.Category switch
        {
            TypeCategory.Void => Config.VoidType,
            TypeCategory.Primitive => MapPrimitive(type.Name),
            TypeCategory.Handle => Config.UseTypedHandles && TypeInfo.IsClassHandle(type.Name)
                ? TypeInfo.NormalizeHandleName(type.Name)
                : Config.NumberType,
            // Hash parameters accept string | number, but return type is always number
            TypeCategory.Hash => forReturn ? Config.NumberType : Config.HashType,
            TypeCategory.String => MapStringType(isNullable),
            TypeCategory.Vector2 => Config.Vector2Type,
            TypeCategory.Vector3 => Config.Vector3Type,
            TypeCategory.Vector4 => Config.Vector4Type,
            TypeCategory.Color => Config.ColorType,
            TypeCategory.Any => Config.AnyType,
            TypeCategory.Struct => type.Name,
            // Enums: use the enum name as type (will be defined in generated code)
            TypeCategory.Enum => type.Name,
            _ => Config.UseTypedHandles ? type.Name : Config.AnyType
        };
    }

    /// <summary>
    /// Maps a string type, optionally nullable.
    /// </summary>
    protected string MapStringType(bool isNullable) =>
        isNullable ? Config.StringType + Config.NullableSuffix : Config.StringType;

    /// <summary>
    /// Formats a list of types as a tuple. TypeScript uses [a, b], Lua uses a, b.
    /// </summary>
    protected string FormatTuple(IEnumerable<string> types) =>
        Config == LanguageConfig.TypeScript
            ? $"[{string.Join(", ", types)}]"
            : string.Join(", ", types);

    /// <summary>
    /// Gets the effective category for code generation, treating enums as their base type.
    /// </summary>
    protected TypeCategory GetEffectiveCategory(TypeInfo type)
    {
        if (type.Category == TypeCategory.Enum && type.EnumBaseType != null)
        {
            // Map enum base type to category
            return type.EnumBaseType switch
            {
                "Hash" => TypeCategory.Hash,
                "int" or "u32" or "i32" => TypeCategory.Primitive,
                _ => TypeCategory.Primitive
            };
        }
        return type.Category;
    }

    protected string MapPrimitive(string name) => name switch
    {
        "int" => Config.UsePrimitiveAliases ? "int" : Config.NumberType,
        "uint" => Config.UsePrimitiveAliases ? "uint" : Config.NumberType,
        "float" => Config.UsePrimitiveAliases ? "float" : Config.NumberType,
        "double" => Config.UsePrimitiveAliases ? "float" : Config.NumberType,
        "BOOL" or "bool" => Config.BooleanType,
        "u8" => Config.UsePrimitiveAliases ? "u8" : Config.NumberType,
        "u16" => Config.UsePrimitiveAliases ? "u16" : Config.NumberType,
        "u32" => Config.UsePrimitiveAliases ? "u32" : Config.NumberType,
        "u64" => Config.UsePrimitiveAliases ? "u64" : Config.NumberType,
        "i8" => Config.UsePrimitiveAliases ? "i8" : Config.NumberType,
        "i16" => Config.UsePrimitiveAliases ? "i16" : Config.NumberType,
        "i32" => Config.UsePrimitiveAliases ? "i32" : Config.NumberType,
        "i64" => Config.UsePrimitiveAliases ? "i64" : Config.NumberType,
        "f32" or "f64" => Config.UsePrimitiveAliases ? "float" : Config.NumberType,
        "Hash" => Config.NumberType,
        _ => Config.NumberType
    };

    public virtual string GetResultMarker(TypeInfo type)
    {
        var category = GetEffectiveCategory(type);
        return category switch
        {
            TypeCategory.Vector3 => $"{Config.ResultAsVectorAlias}()",
            TypeCategory.String => $"{Config.ResultAsStringAlias}()",
            TypeCategory.Primitive when type.IsFloat => $"{Config.ResultAsFloatAlias}()",
            _ => $"{Config.ResultAsIntAlias}()"
        };
    }

    public virtual bool NeedsResultMarker(TypeInfo type)
    {
        var category = GetEffectiveCategory(type);
        return category != TypeCategory.Void &&
               category != TypeCategory.Any;
    }

    public bool IsEnumType(TypeInfo type) => type.Category == TypeCategory.Enum;

    public bool IsHandleType(TypeInfo type) => type.Category == TypeCategory.Handle;

    public bool IsVector3(TypeInfo type) => type.Category == TypeCategory.Vector3;

    public virtual string GetInvokeReturnType(TypeInfo type)
    {
        return type.Category switch
        {
            TypeCategory.Void => Config.VoidType,
            TypeCategory.Vector3 => Config == LanguageConfig.TypeScript ? "number[]" : Config.Vector3Type,
            TypeCategory.String => Config.StringType,
            TypeCategory.Primitive when type.IsBool => Config.BooleanType,
            _ => Config.NumberType
        };
    }

    public virtual string GetPointerPlaceholder(TypeInfo type)
    {
        if (type.IsVector3)
            return $"{Config.PointerVectorAlias}()";

        return type.IsFloat ? $"{Config.PointerFloatAlias}()" : $"{Config.PointerIntAlias}()";
    }

    public virtual string GetInitializedPointerFormat(TypeInfo type)
    {
        var f = Config.FloatWrapperAlias;
        var pvfi = Config.PointerFloatInitAlias;
        var pvii = Config.PointerIntInitAlias;
        var useFloat = Config.UseFloatWrapper;

        if (type.IsVector3)
        {
            // Vector3 expands to 3 floats: x, y, z - wrapped with f() for float safety if needed
            return useFloat
                ? $"{pvfi}({f}({{0}}.x)), {pvfi}({f}({{0}}.y)), {pvfi}({f}({{0}}.z))"
                : $"{pvfi}({{0}}.x), {pvfi}({{0}}.y), {pvfi}({{0}}.z)";
        }

        if (type.IsFloat)
            return useFloat ? $"{pvfi}({f}({{0}}))" : $"{pvfi}({{0}})";

        return $"{pvii}({{0}})";
    }

    public virtual (string LanguageType, string GetMethod, string SetMethod) GetDataViewAccessor(TypeInfo type)
    {
        return type.Name switch
        {
            "u8" => (Config.NumberType, "getUint8", "setUint8"),
            "u16" => (Config.NumberType, "getUint16", "setUint16"),
            "u32" or "uint" => (Config.NumberType, "getUint32", "setUint32"),
            "u64" => Config == LanguageConfig.TypeScript ? ("bigint", "getBigUint64", "setBigUint64") : (Config.NumberType, "getUint32", "setUint32"),
            "i8" => (Config.NumberType, "getInt8", "setInt8"),
            "i16" => (Config.NumberType, "getInt16", "setInt16"),
            "i32" or "int" => (Config.NumberType, "getInt32", "setInt32"),
            "i64" => Config == LanguageConfig.TypeScript ? ("bigint", "getBigInt64", "setBigInt64") : (Config.NumberType, "getInt32", "setInt32"),
            "f32" or "float" => (Config.NumberType, "getFloat32", "setFloat32"),
            "f64" or "double" => (Config.NumberType, "getFloat64", "setFloat64"),
            "bool" or "BOOL" => (Config.BooleanType, "getInt8", "setInt8"),
            "Hash" => (Config.NumberType, "getUint32", "setUint32"),
            _ => (Config.NumberType, "getInt32", "setInt32")
        };
    }

    public virtual DataViewAccessorInfo GetDataViewAccessorInfo(TypeInfo type)
    {
        var (langType, getMethod, setMethod) = GetDataViewAccessor(type);
        var needsEndian = StructLayoutCalculator.NeedsEndianArgument(type);
        var endianArg = needsEndian ? ", true" : "";
        return new DataViewAccessorInfo(langType, getMethod, setMethod, endianArg, type.IsBool);
    }

    public virtual string GetOutputParamType(TypeInfo type)
    {
        if (type.IsVector3)
            return Config.Vector3Type;

        if (type.Category == TypeCategory.Handle)
        {
            return Config.UseTypedHandles
                ? $"{TypeInfo.NormalizeHandleName(type.Name)}{Config.NullableSuffix}"
                : Config.NumberType;
        }

        if (type.IsFloat) return Config.NumberType;
        if (type.IsBool) return Config.BooleanType;
        return Config.NumberType;
    }

    public virtual string BuildCombinedReturnType(TypeInfo returnType, IEnumerable<TypeInfo> outputParamTypes)
    {
        var outputTypes = outputParamTypes.ToList();

        // Helper to add nullable suffix for class handle return types
        string MapReturnType(TypeInfo type)
        {
            var mapped = MapType(type, forReturn: true);
            // Class handle return types can be null (invalid handle returns 0)
            // Non-class handles (Prompt, ScrHandle) are just numbers
            if (type.Category == TypeCategory.Handle && Config.UseTypedHandles && TypeInfo.IsClassHandle(type.Name))
            {
                return mapped + Config.NullableSuffix;
            }
            return mapped;
        }

        if (outputTypes.Count == 0)
        {
            return MapReturnType(returnType);
        }

        if (outputTypes.Count == 1 && returnType.Category == TypeCategory.Void)
        {
            return GetOutputParamType(outputTypes[0]);
        }

        var tupleTypes = new List<string>();

        if (returnType.Category != TypeCategory.Void)
        {
            tupleTypes.Add(MapReturnType(returnType));
        }

        foreach (var outputType in outputTypes)
        {
            tupleTypes.Add(GetOutputParamType(outputType));
        }

        return FormatTuple(tupleTypes);
    }

    public virtual string GetInvokeCombinedReturnType(TypeInfo returnType, IEnumerable<TypeInfo> outputParamTypes)
    {
        var outputTypes = outputParamTypes.ToList();

        if (outputTypes.Count == 0)
        {
            return GetInvokeReturnType(returnType);
        }

        var invokeTypes = new List<string>();

        if (returnType.Category != TypeCategory.Void)
        {
            invokeTypes.Add(GetInvokeReturnType(returnType));
        }

        foreach (var outputType in outputTypes)
        {
            if (outputType.IsVector3)
                invokeTypes.Add(Config == LanguageConfig.TypeScript ? "number[]" : Config.Vector3Type);
            else
                invokeTypes.Add(Config.NumberType);
        }

        if (invokeTypes.Count == 1)
        {
            return invokeTypes[0];
        }

        return FormatTuple(invokeTypes);
    }
}
