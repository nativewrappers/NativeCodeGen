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
    public required string NullableStringSuffix { get; init; }  // "| null" or "|nil"
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

    public static readonly LanguageConfig TypeScript = new()
    {
        VoidType = "void",
        NumberType = "number",
        BooleanType = "boolean",
        StringType = "string",
        NullableStringSuffix = " | null",
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
        UseHashWrapper = true
    };

    public static readonly LanguageConfig Lua = new()
    {
        VoidType = "nil",
        NumberType = "number",
        BooleanType = "boolean",
        StringType = "string",
        NullableStringSuffix = "|nil",
        Vector2Type = "vector2",
        Vector3Type = "vector3",
        Vector4Type = "vector4",
        ColorType = "Color",
        AnyType = "any",
        NullableSuffix = "|nil",
        HashType = "string|number",
        UseTypedHandles = false,

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
        UseHashWrapper = true
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

    public virtual string MapType(TypeInfo type, bool isNotNull = false)
    {
        if (type.IsPointer)
        {
            if (type.Name == "char" || type.Name == "string")
            {
                return isNotNull ? Config.StringType : Config.StringType + Config.NullableStringSuffix;
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
                ? (type.Name == "Object" ? "Prop" : type.Name)
                : Config.NumberType,
            TypeCategory.Hash => Config.HashType,
            TypeCategory.String => isNotNull ? Config.StringType : Config.StringType + Config.NullableStringSuffix,
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
        "int" or "uint" => Config.NumberType,
        "float" or "double" => Config.NumberType,
        "BOOL" or "bool" => Config.BooleanType,
        "u8" or "u16" or "u32" or "u64" => Config.NumberType,
        "i8" or "i16" or "i32" or "i64" => Config.NumberType,
        "f32" or "f64" => Config.NumberType,
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
        if (type.Category == TypeCategory.Vector3 || type.Name == "Vector3")
            return $"{Config.PointerVectorAlias}()";

        return type.IsFloat ? $"{Config.PointerFloatAlias}()" : $"{Config.PointerIntAlias}()";
    }

    public virtual string GetInitializedPointerFormat(TypeInfo type)
    {
        var f = Config.FloatWrapperAlias;
        var pvfi = Config.PointerFloatInitAlias;
        var pvii = Config.PointerIntInitAlias;
        var useFloat = Config.UseFloatWrapper;

        if (type.Category == TypeCategory.Vector3 || type.Name == "Vector3")
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

    public virtual string GetOutputParamType(TypeInfo type)
    {
        if (type.Category == TypeCategory.Vector3 || type.Name == "Vector3")
        {
            return Config.Vector3Type;
        }

        if (type.Category == TypeCategory.Handle)
        {
            return Config.UseTypedHandles
                ? $"{(type.Name == "Object" ? "Prop" : type.Name)}{Config.NullableSuffix}"
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
            var mapped = MapType(type);
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

        // TypeScript uses [a, b], Lua uses comma-separated
        return Config == LanguageConfig.TypeScript
            ? $"[{string.Join(", ", tupleTypes)}]"
            : string.Join(", ", tupleTypes);
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
            if (outputType.Category == TypeCategory.Vector3 || outputType.Name == "Vector3")
            {
                invokeTypes.Add(Config == LanguageConfig.TypeScript ? "number[]" : Config.Vector3Type);
            }
            else
            {
                invokeTypes.Add(Config.NumberType);
            }
        }

        if (invokeTypes.Count == 1)
        {
            return invokeTypes[0];
        }

        return Config == LanguageConfig.TypeScript
            ? $"[{string.Join(", ", invokeTypes)}]"
            : string.Join(", ", invokeTypes);
    }
}
