namespace NativeCodeGen.Core.Models;

public class TypeInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsPointer { get; set; }
    public TypeCategory Category { get; set; }
    public string? GenericParameter { get; set; }

    /// <summary>
    /// For enum types, the base type (e.g., "Hash", "int"). Null for non-enums.
    /// </summary>
    public string? EnumBaseType { get; set; }

    public static TypeInfo Parse(string typeString)
    {
        var trimmed = typeString.Trim();
        var isPointer = trimmed.EndsWith('*');
        var name = isPointer ? trimmed[..^1].Trim() : trimmed;

        return new TypeInfo
        {
            Name = name,
            IsPointer = isPointer,
            Category = CategorizeType(name, isPointer)
        };
    }

    /// <summary>
    /// Resolves enum types using the provided lookup. Call after parsing to upgrade
    /// Struct category to Enum if the type name matches a known enum.
    /// </summary>
    public void ResolveEnumType(Func<string, string?> enumBaseTypeLookup)
    {
        if (Category == TypeCategory.Struct)
        {
            var baseType = enumBaseTypeLookup(Name);
            if (baseType != null)
            {
                Category = TypeCategory.Enum;
                EnumBaseType = baseType;
            }
        }
    }

    private static TypeCategory CategorizeType(string name, bool isPointer)
    {
        if (name == "void" && !isPointer)
            return TypeCategory.Void;

        if (isPointer && (name == "char" || name == "string"))
            return TypeCategory.String;

        if (name == "string")
            return TypeCategory.String;

        if (name == "Hash")
            return TypeCategory.Hash;

        if (name == "Vector3")
            return TypeCategory.Vector3;

        if (name == "Any")
            return TypeCategory.Any;

        if (IsPrimitive(name))
            return TypeCategory.Primitive;

        if (IsHandle(name))
            return TypeCategory.Handle;

        return TypeCategory.Struct;
    }

    private static bool IsPrimitive(string name) => name switch
    {
        "int" or "uint" or "float" or "double" or "BOOL" or "bool" => true,
        "u8" or "u16" or "u32" or "u64" => true,
        "i8" or "i16" or "i32" or "i64" => true,
        "f32" or "f64" => true,
        _ => false
    };

    private static bool IsHandle(string name) => name switch
    {
        "Entity" or "Ped" or "Vehicle" or "Object" or "Pickup" => true,
        "Player" or "Cam" or "Blip" or "Interior" or "FireId" => true,
        "AnimScene" or "ItemSet" or "PersChar" or "PopZone" => true,
        "PropSet" or "Volume" or "ScrHandle" or "PedGroup" => true,
        _ => false
    };

    public override string ToString()
    {
        return IsPointer ? $"{Name}*" : Name;
    }
}

public enum TypeCategory
{
    Void,
    Primitive,
    Handle,
    Hash,
    String,
    Vector3,
    Any,
    Struct,
    Enum
}
