using System.Collections.Frozen;

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

    /// <summary>
    /// For fixed-size array types (e.g., int[3]), the array size. Null for non-arrays.
    /// </summary>
    public int? ArraySize { get; set; }

    /// <summary>
    /// True if this is a fixed-size array type (e.g., int[3]).
    /// </summary>
    public bool IsFixedArray => ArraySize.HasValue && ArraySize.Value > 0;

    /// <summary>True if this is a boolean type (bool or BOOL).</summary>
    public bool IsBool => Name is "bool" or "BOOL";

    /// <summary>True if this is a floating-point type.</summary>
    public bool IsFloat => Name is "float" or "f32" or "f64" or "double";

    /// <summary>True if this is a vector type (Vector2, Vector3, Vector4).</summary>
    public bool IsVector => Category is TypeCategory.Vector2 or TypeCategory.Vector3 or TypeCategory.Vector4;

    /// <summary>True if this is a Vector3 type (by category or name).</summary>
    public bool IsVector3 => Category == TypeCategory.Vector3 || Name == "Vector3";

    /// <summary>Gets the component names for vector types.</summary>
    public static readonly string[] VectorComponents = ["x", "y", "z", "w"];

    /// <summary>Gets the number of components for this vector type, or 0 if not a vector.</summary>
    public int VectorComponentCount => Category switch
    {
        TypeCategory.Vector2 => 2,
        TypeCategory.Vector3 => 3,
        TypeCategory.Vector4 => 4,
        _ => 0
    };

    // Static frozen dictionaries for O(1) lookup
    private static readonly FrozenSet<string> Primitives = new HashSet<string>
    {
        "int", "uint", "float", "double", "BOOL", "bool",
        "u8", "u16", "u32", "u64",
        "i8", "i16", "i32", "i64",
        "f32", "f64", "long", "variadic"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> Handles = new HashSet<string>
    {
        "Entity", "Ped", "Vehicle", "Object", "Pickup",
        "Player", "Cam", "Blip", "Interior", "FireId",
        "AnimScene", "ItemSet", "PersChar", "PopZone",
        "PropSet", "Volume", "ScrHandle", "PedGroup", "Prompt"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ClassHandles = new HashSet<string>
    {
        // Core entity types
        "Entity", "Ped", "Vehicle", "Object", "Pickup",
        // Other types with dedicated namespaces
        "Player", "Cam", "Interior",
        "AnimScene", "ItemSet", "PersChar",
        "PropSet", "Volume"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, TypeCategory> SpecialTypes =
        new Dictionary<string, TypeCategory>
        {
            ["void"] = TypeCategory.Void,
            ["Hash"] = TypeCategory.Hash,
            ["Vector2"] = TypeCategory.Vector2,
            ["Vector3"] = TypeCategory.Vector3,
            ["Vector4"] = TypeCategory.Vector4,
            ["Color"] = TypeCategory.Color,
            ["Any"] = TypeCategory.Any,
            ["string"] = TypeCategory.String
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Known valid attributes for parameters.
    /// </summary>
    public static readonly FrozenSet<string> ValidAttributes = new HashSet<string>
    {
        "@this", "@nullable", "@in"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Gets valid attributes as a comma-separated string for error messages.
    /// </summary>
    public static string ValidAttributesList => string.Join(", ", ValidAttributes);

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

    /// <summary>
    /// Categorizes a type name into a TypeCategory. This is the single source of truth
    /// for type categorization - do not duplicate this logic.
    /// </summary>
    public static TypeCategory CategorizeType(string name, bool isPointer)
    {
        // Handle void (both void and void*)
        if (name == "void")
            return TypeCategory.Void;

        // Handle string types (char* or string)
        if (isPointer && (name == "char" || name == "string"))
            return TypeCategory.String;

        // Check special types dictionary first (includes string, Hash, Vector*, Color, Any)
        if (SpecialTypes.TryGetValue(name, out var category))
            return category;

        // Check primitives
        if (Primitives.Contains(name))
            return TypeCategory.Primitive;

        // Check handles
        if (Handles.Contains(name))
            return TypeCategory.Handle;

        // Default to Struct (may be resolved to Enum later)
        return TypeCategory.Struct;
    }

    public static bool IsPrimitive(string name) => Primitives.Contains(name);

    public static bool IsHandle(string name) => Handles.Contains(name);

    /// <summary>
    /// Handle types that have their own generated class (with a namespace of methods).
    /// Must match types in NativeClassifier.TypeToNamespace.
    /// Other handles like Prompt, ScrHandle, FireId, PopZone, Blip are just typed as number.
    /// </summary>
    public static bool IsClassHandle(string name) => ClassHandles.Contains(name);

    /// <summary>
    /// Normalizes handle type names for code generation (Object → Prop).
    /// </summary>
    public static string NormalizeHandleName(string name) => name == "Object" ? "Prop" : name;

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
    Vector2,
    Vector3,
    Vector4,
    Color,
    Any,
    Struct,
    Enum
}
