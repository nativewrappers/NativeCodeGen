using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.TypeSystem;

namespace NativeCodeGen.CSharp;

/// <summary>
/// C# type mapper implementation.
/// </summary>
public class CSharpTypeMapper : TypeMapperBase
{
    public CSharpTypeMapper() : base(LanguageConfig.CSharp) { }

    public override string MapType(TypeInfo type, bool isNullable = false, bool forReturn = false)
    {
        // Handle fixed-size arrays: int[3] -> int[]
        if (type.IsFixedArray)
        {
            var elementType = MapPrimitive(type.Name);
            return $"{elementType}[]";
        }

        if (type.IsPointer)
        {
            if (type.Name == "char" || type.Name == "string")
            {
                return isNullable ? "string?" : "string";
            }
            if (type.Category == TypeCategory.Struct)
            {
                return type.Name;
            }
            return MapPrimitive(type.Name);
        }

        var baseType = type.Category switch
        {
            TypeCategory.Void => Config.VoidType,
            TypeCategory.Primitive => MapPrimitive(type.Name),
            TypeCategory.Handle => Config.UseTypedHandles && TypeInfo.IsClassHandle(type.Name)
                ? TypeInfo.NormalizeHandleName(type.Name)
                : "int",
            TypeCategory.Hash => forReturn ? "uint" : "uint",  // C# uses uint for hashes
            TypeCategory.String => "string",
            TypeCategory.Vector2 => Config.Vector2Type,
            TypeCategory.Vector3 => Config.Vector3Type,
            TypeCategory.Vector4 => Config.Vector4Type,
            TypeCategory.Color => Config.ColorType,
            TypeCategory.Any => Config.AnyType,
            TypeCategory.Struct => type.Name,
            TypeCategory.Enum => type.Name,
            _ => Config.UseTypedHandles ? type.Name : Config.AnyType
        };

        // Add nullable suffix for reference types if needed
        if (isNullable && type.Category == TypeCategory.Handle && TypeInfo.IsClassHandle(type.Name))
        {
            return baseType + "?";
        }

        return baseType;
    }

    protected new string MapPrimitive(string name) => name switch
    {
        "int" => "int",
        "uint" => "uint",
        "float" => "float",
        "double" => "double",
        "BOOL" or "bool" => "bool",
        "u8" => "byte",
        "u16" => "ushort",
        "u32" => "uint",
        "u64" => "ulong",
        "i8" => "sbyte",
        "i16" => "short",
        "i32" => "int",
        "i64" => "long",
        "f32" => "float",
        "f64" => "double",
        "Hash" => "uint",
        _ => "int"
    };

    public override string GetResultMarker(TypeInfo type)
    {
        // C# doesn't use result markers - the generic type parameter handles this
        return "";
    }

    public override bool NeedsResultMarker(TypeInfo type)
    {
        // C# doesn't need result markers
        return false;
    }

    public override string GetInvokeReturnType(TypeInfo type)
    {
        return type.Category switch
        {
            TypeCategory.Void => "void",
            TypeCategory.Vector3 => "Vector3",
            TypeCategory.String => "string",
            TypeCategory.Primitive when type.IsBool => "bool",
            TypeCategory.Primitive when type.IsFloat => "float",
            TypeCategory.Hash => "uint",
            _ => "int"
        };
    }

    public override string GetPointerPlaceholder(TypeInfo type)
    {
        // C# uses OutputArgument for output parameters
        return "new OutputArgument()";
    }

    public override string GetInitializedPointerFormat(TypeInfo type)
    {
        // For @in params, we still use OutputArgument but initialize the value differently
        // The actual initialization happens in the method body
        return "new OutputArgument()";
    }

    public override string GetOutputParamType(TypeInfo type)
    {
        if (type.IsVector3)
            return Config.Vector3Type;

        if (type.Category == TypeCategory.Handle)
        {
            return Config.UseTypedHandles && TypeInfo.IsClassHandle(type.Name)
                ? $"{TypeInfo.NormalizeHandleName(type.Name)}?"
                : "int";
        }

        if (type.IsFloat) return "float";
        if (type.IsBool) return "bool";
        return "int";
    }

    public override string BuildCombinedReturnType(TypeInfo returnType, IEnumerable<TypeInfo> outputParamTypes)
    {
        var outputTypes = outputParamTypes.ToList();

        if (outputTypes.Count == 0)
        {
            if (returnType.Category == TypeCategory.Handle && Config.UseTypedHandles && TypeInfo.IsClassHandle(returnType.Name))
            {
                return MapType(returnType, forReturn: true) + "?";
            }
            return MapType(returnType, forReturn: true);
        }

        // For C#, we use out parameters instead of tuples
        // The return type is just the native return type
        if (returnType.Category == TypeCategory.Void && outputTypes.Count == 1)
        {
            return GetOutputParamType(outputTypes[0]);
        }

        // For multiple outputs or return + outputs, we use tuples
        var tupleTypes = new List<string>();

        if (returnType.Category != TypeCategory.Void)
        {
            var mapped = MapType(returnType, forReturn: true);
            if (returnType.Category == TypeCategory.Handle && TypeInfo.IsClassHandle(returnType.Name))
            {
                mapped += "?";
            }
            tupleTypes.Add(mapped);
        }

        foreach (var outputType in outputTypes)
        {
            tupleTypes.Add(GetOutputParamType(outputType));
        }

        return $"({string.Join(", ", tupleTypes)})";
    }

    public override DataViewAccessorInfo GetDataViewAccessorInfo(TypeInfo type)
    {
        // C# doesn't use DataView - this is for struct generation which is disabled
        var (langType, getMethod, setMethod) = GetDataViewAccessor(type);
        return new DataViewAccessorInfo(langType, getMethod, setMethod, "", type.IsBool);
    }

    public override (string LanguageType, string GetMethod, string SetMethod) GetDataViewAccessor(TypeInfo type)
    {
        return type.Name switch
        {
            "u8" => ("byte", "ReadByte", "WriteByte"),
            "u16" => ("ushort", "ReadUInt16", "WriteUInt16"),
            "u32" or "uint" => ("uint", "ReadUInt32", "WriteUInt32"),
            "u64" => ("ulong", "ReadUInt64", "WriteUInt64"),
            "i8" => ("sbyte", "ReadSByte", "WriteSByte"),
            "i16" => ("short", "ReadInt16", "WriteInt16"),
            "i32" or "int" => ("int", "ReadInt32", "WriteInt32"),
            "i64" => ("long", "ReadInt64", "WriteInt64"),
            "f32" or "float" => ("float", "ReadSingle", "WriteSingle"),
            "f64" or "double" => ("double", "ReadDouble", "WriteDouble"),
            "bool" or "BOOL" => ("bool", "ReadByte", "WriteByte"),
            "Hash" => ("uint", "ReadUInt32", "WriteUInt32"),
            _ => ("int", "ReadInt32", "WriteInt32")
        };
    }
}
