using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.TypeSystem;

/// <summary>
/// TypeScript type mapper implementation.
/// </summary>
public class TypeMapper : ITypeMapper
{
    public string MapType(TypeInfo type, bool isNotNull = false)
    {
        if (type.IsPointer)
        {
            if (type.Name == "char" || type.Name == "string")
            {
                return isNotNull ? "string" : "string | null";
            }
            // Struct pointers are passed as the struct type (we pass the buffer)
            if (type.Category == TypeCategory.Struct)
            {
                return type.Name;
            }
            return MapPrimitive(type.Name);
        }

        return type.Category switch
        {
            TypeCategory.Void => "void",
            TypeCategory.Primitive => MapPrimitive(type.Name),
            TypeCategory.Handle => type.Name == "Object" ? "Prop" : type.Name,
            TypeCategory.Hash => "number",
            TypeCategory.String => isNotNull ? "string" : "string | null",
            TypeCategory.Vector3 => "Vector3",
            TypeCategory.Any => "any",
            TypeCategory.Struct => type.Name,
            _ => type.Name
        };
    }

    // Keep old method name for backwards compatibility
    public string MapToTypeScript(TypeInfo type, bool isNotNull = false) => MapType(type, isNotNull);

    private static string MapPrimitive(string name) => name switch
    {
        "int" or "uint" => "number",
        "float" or "double" => "number",
        "BOOL" or "bool" => "boolean",
        "u8" or "u16" or "u32" or "u64" => "number",
        "i8" or "i16" or "i32" or "i64" => "number",
        "f32" or "f64" => "number",
        "Hash" => "number",
        _ => "number"
    };

    public string GetResultMarker(TypeInfo type)
    {
        return type.Category switch
        {
            TypeCategory.Vector3 => "Citizen.resultAsVector()",
            TypeCategory.String => "Citizen.resultAsString()",
            TypeCategory.Primitive when type.Name is "float" or "double" or "f32" or "f64" => "Citizen.resultAsFloat()",
            TypeCategory.Primitive when type.Name is "BOOL" or "bool" => "Citizen.resultAsInteger()",
            TypeCategory.Handle => "Citizen.resultAsInteger()",
            TypeCategory.Hash => "Citizen.resultAsInteger()",
            _ => "Citizen.resultAsInteger()"
        };
    }

    // Keep old method name for backwards compatibility
    public string GetCitizenResultType(TypeInfo type) => GetResultMarker(type);

    public bool NeedsResultMarker(TypeInfo type)
    {
        return type.Category != TypeCategory.Void &&
               type.Category != TypeCategory.Any;
    }

    public bool IsHandleType(TypeInfo type)
    {
        return type.Category == TypeCategory.Handle;
    }

    public bool IsVector3(TypeInfo type)
    {
        return type.Category == TypeCategory.Vector3;
    }

    public string GetInvokeReturnType(TypeInfo type)
    {
        return type.Category switch
        {
            TypeCategory.Void => "void",
            TypeCategory.Vector3 => "number[]",
            TypeCategory.String => "string",
            _ => "number"
        };
    }

    public string GetPointerPlaceholder(TypeInfo type)
    {
        if (type.Category == TypeCategory.Vector3 || type.Name == "Vector3")
        {
            return "Citizen.pointerValueVector()";
        }

        return type.Name switch
        {
            "float" or "f32" or "f64" or "double" => "Citizen.pointerValueFloat()",
            _ => "Citizen.pointerValueInt()"
        };
    }

    public string GetInitializedPointerFormat(TypeInfo type)
    {
        if (type.Category == TypeCategory.Vector3 || type.Name == "Vector3")
        {
            // Vector3 expands to 3 floats: x, y, z
            return "Citizen.pointerValueFloatInitialized({0}.x), Citizen.pointerValueFloatInitialized({0}.y), Citizen.pointerValueFloatInitialized({0}.z)";
        }

        return type.Name switch
        {
            "float" or "f32" or "f64" or "double" => "Citizen.pointerValueFloatInitialized({0})",
            _ => "Citizen.pointerValueIntInitialized({0})"
        };
    }

    public (string LanguageType, string GetMethod, string SetMethod) GetDataViewAccessor(TypeInfo type)
    {
        return type.Name switch
        {
            "u8" => ("number", "getUint8", "setUint8"),
            "u16" => ("number", "getUint16", "setUint16"),
            "u32" or "uint" => ("number", "getUint32", "setUint32"),
            "u64" => ("bigint", "getBigUint64", "setBigUint64"),
            "i8" => ("number", "getInt8", "setInt8"),
            "i16" => ("number", "getInt16", "setInt16"),
            "i32" or "int" => ("number", "getInt32", "setInt32"),
            "i64" => ("bigint", "getBigInt64", "setBigInt64"),
            "f32" or "float" => ("number", "getFloat32", "setFloat32"),
            "f64" or "double" => ("number", "getFloat64", "setFloat64"),
            "bool" or "BOOL" => ("boolean", "getInt8", "setInt8"),
            "Hash" => ("number", "getUint32", "setUint32"),
            _ => ("number", "getInt32", "setInt32")
        };
    }
}
