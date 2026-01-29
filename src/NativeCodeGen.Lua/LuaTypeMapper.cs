using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Lua;

/// <summary>
/// Lua type mapper implementation.
/// </summary>
public class LuaTypeMapper : ITypeMapper
{
    public string MapType(TypeInfo type, bool isNotNull = false)
    {
        if (type.IsPointer)
        {
            if (type.Name == "char" || type.Name == "string")
            {
                return isNotNull ? "string" : "string|nil";
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
            TypeCategory.Void => "nil",
            TypeCategory.Primitive => MapPrimitive(type.Name),
            TypeCategory.Handle => "number", // Handles are just integers in Lua
            TypeCategory.Hash => "number",
            TypeCategory.String => isNotNull ? "string" : "string|nil",
            TypeCategory.Vector3 => "vector3",
            TypeCategory.Any => "any",
            TypeCategory.Struct => type.Name,
            _ => "any"
        };
    }

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
        // CFX Lua uses Citizen.ResultAs* similar to JS
        return type.Category switch
        {
            TypeCategory.Vector3 => "Citizen.ResultAsVector()",
            TypeCategory.String => "Citizen.ResultAsString()",
            TypeCategory.Primitive when type.Name is "float" or "double" or "f32" or "f64" => "Citizen.ResultAsFloat()",
            _ => "Citizen.ResultAsInteger()"
        };
    }

    public bool NeedsResultMarker(TypeInfo type)
    {
        return type.Category != TypeCategory.Void &&
               type.Category != TypeCategory.Any;
    }

    public bool IsHandleType(TypeInfo type) => type.Category == TypeCategory.Handle;

    public bool IsVector3(TypeInfo type) => type.Category == TypeCategory.Vector3;

    public string GetInvokeReturnType(TypeInfo type)
    {
        // Lua is dynamically typed, but we track this for documentation
        return type.Category switch
        {
            TypeCategory.Void => "nil",
            TypeCategory.Vector3 => "vector3",
            TypeCategory.String => "string",
            TypeCategory.Primitive when type.Name is "BOOL" or "bool" => "boolean",
            _ => "number"
        };
    }

    public string GetPointerPlaceholder(TypeInfo type)
    {
        if (type.Category == TypeCategory.Vector3 || type.Name == "Vector3")
        {
            return "Citizen.PointerValueVector()";
        }

        return type.Name switch
        {
            "float" or "f32" or "f64" or "double" => "Citizen.PointerValueFloat()",
            _ => "Citizen.PointerValueInt()"
        };
    }

    public string GetInitializedPointerFormat(TypeInfo type)
    {
        if (type.Category == TypeCategory.Vector3 || type.Name == "Vector3")
        {
            // Vector3 expands to 3 floats: x, y, z
            return "Citizen.PointerValueFloatInitialized({0}.x), Citizen.PointerValueFloatInitialized({0}.y), Citizen.PointerValueFloatInitialized({0}.z)";
        }

        return type.Name switch
        {
            "float" or "f32" or "f64" or "double" => "Citizen.PointerValueFloatInitialized({0})",
            _ => "Citizen.PointerValueIntInitialized({0})"
        };
    }

    public (string LanguageType, string GetMethod, string SetMethod) GetDataViewAccessor(TypeInfo type)
    {
        return type.Name switch
        {
            "bool" or "BOOL" => ("boolean", "GetInt8", "SetInt8"),
            "i8" => ("number", "GetInt8", "SetInt8"),
            "u8" => ("number", "GetUint8", "SetUint8"),
            "i16" => ("number", "GetInt16", "SetInt16"),
            "u16" => ("number", "GetUint16", "SetUint16"),
            "i32" or "int" => ("number", "GetInt32", "SetInt32"),
            "u32" or "uint" or "Hash" => ("number", "GetUint32", "SetUint32"),
            "f32" or "float" => ("number", "GetFloat32", "SetFloat32"),
            "f64" or "double" => ("number", "GetFloat64", "SetFloat64"),
            _ => ("number", "GetUint32", "SetUint32")
        };
    }
}
