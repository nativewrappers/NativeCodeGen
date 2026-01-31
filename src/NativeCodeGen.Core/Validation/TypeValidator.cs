using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;
using NativeCodeGen.Core.Registry;

namespace NativeCodeGen.Core.Validation;

/// <summary>
/// Validates types used in native definitions against known registries.
/// </summary>
public class TypeValidator
{
    private readonly EnumRegistry _enumRegistry;
    private readonly StructRegistry _structRegistry;

    public TypeValidator(EnumRegistry enumRegistry, StructRegistry structRegistry)
    {
        _enumRegistry = enumRegistry;
        _structRegistry = structRegistry;
    }

    /// <summary>
    /// Resolves and validates all types in a native definition.
    /// Returns a list of validation errors.
    /// </summary>
    public List<ParseError> ValidateNative(NativeDefinition native, string filePath)
    {
        var errors = new List<ParseError>();

        // Validate and resolve parameter types
        foreach (var param in native.Parameters)
        {
            ValidateType(param.Type, $"parameter '{param.Name}'", filePath, errors);
        }

        // Validate and resolve return type
        ValidateType(native.ReturnType, "return type", filePath, errors);

        return errors;
    }

    private void ValidateType(TypeInfo type, string context, string filePath, List<ParseError> errors)
    {
        // First, try to resolve enum types
        type.ResolveEnumType(_enumRegistry.GetBaseType);

        // Validate based on category
        switch (type.Category)
        {
            case TypeCategory.Enum:
                if (!_enumRegistry.Contains(type.Name))
                {
                    errors.Add(new ParseError
                    {
                        FilePath = filePath,
                        Line = 1,
                        Column = 1,
                        Message = $"Unknown enum type '{type.Name}' in {context}"
                    });
                }
                break;

            case TypeCategory.Struct:
                if (!_structRegistry.Contains(type.Name))
                {
                    errors.Add(new ParseError
                    {
                        FilePath = filePath,
                        Line = 1,
                        Column = 1,
                        Message = $"Unknown struct type '{type.Name}' in {context}. Did you mean one of: {GetSuggestions(type.Name)}"
                    });
                }
                break;
        }
    }

    /// <summary>
    /// Get suggestions for a misspelled type name.
    /// </summary>
    private string GetSuggestions(string typeName)
    {
        var suggestions = new List<string>();

        // Check for common typos in built-in types
        var typoSuggestions = CheckCommonTypos(typeName);
        if (typoSuggestions != null)
        {
            suggestions.Add(typoSuggestions);
        }

        // Check for similar struct names
        foreach (var structName in _structRegistry.GetAllStructs().Keys)
        {
            if (IsSimilar(typeName, structName))
            {
                suggestions.Add(structName);
            }
        }

        // Check for similar enum names
        foreach (var enumName in _enumRegistry.GetAllEnums().Keys)
        {
            if (IsSimilar(typeName, enumName))
            {
                suggestions.Add(enumName);
            }
        }

        if (suggestions.Count == 0)
        {
            return "(no suggestions)";
        }

        return string.Join(", ", suggestions.Take(3));
    }

    /// <summary>
    /// Check for common typos that would result in a type being categorized as Struct.
    /// </summary>
    private static string? CheckCommonTypos(string typeName)
    {
        return typeName.ToLowerInvariant() switch
        {
            // Entity typos
            "entitty" or "enity" or "entiity" or "entiy" => "Entity",

            // Vector typos
            "vestor3" or "vector" or "vec3" or "vectror3" => "Vector3",
            "vestor2" or "vec2" or "vectror2" => "Vector2",
            "vestor4" or "vec4" or "vectror4" => "Vector4",

            // Primitive typos (these should be primitives, not structs)
            "int32" or "integer" or "interger" => "int",
            "int64" => "long",
            "uint32" => "uint",
            "boolean" or "boolen" or "bolean" => "BOOL",
            "float32" => "float",
            "float64" => "double",
            "string" => "char* (string)",

            // Handle typos
            "player" => "Player (handle)",
            "vehicle" or "vehcile" or "vehicel" => "Vehicle",
            "pedistrian" or "pedestrian" => "Ped",
            "object" or "obejct" => "Object",
            "camera" => "Cam",

            // Hash typos
            "hash" => "Hash",

            _ => null
        };
    }

    /// <summary>
    /// Simple similarity check using Levenshtein-like comparison.
    /// </summary>
    private static bool IsSimilar(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return false; // Exact match, not a suggestion

        // Simple length-based early exit
        if (Math.Abs(a.Length - b.Length) > 3)
            return false;

        // Check if one contains the other
        if (a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
            b.Contains(a, StringComparison.OrdinalIgnoreCase))
            return true;

        // Simple edit distance approximation: count mismatches
        var shorter = a.Length < b.Length ? a.ToLowerInvariant() : b.ToLowerInvariant();
        var longer = a.Length < b.Length ? b.ToLowerInvariant() : a.ToLowerInvariant();

        int mismatches = longer.Length - shorter.Length;
        for (int i = 0; i < shorter.Length && mismatches <= 2; i++)
        {
            if (i < longer.Length && shorter[i] != longer[i])
                mismatches++;
        }

        return mismatches <= 2;
    }
}
