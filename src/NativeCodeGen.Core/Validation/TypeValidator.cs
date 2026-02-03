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

            // Validate default values match parameter types
            if (param.HasDefaultValue)
            {
                ValidateDefaultValue(param, filePath, errors);
            }
        }

        // Validate and resolve return type
        ValidateType(native.ReturnType, "return type", filePath, errors);

        return errors;
    }

    /// <summary>
    /// Validates that a default value is compatible with the parameter type.
    /// </summary>
    private static void ValidateDefaultValue(NativeParameter param, string filePath, List<ParseError> errors)
    {
        var type = param.Type;
        var defaultValue = param.DefaultValue!;
        var context = $"parameter '{param.Name}'";

        var error = ValidateDefaultForType(type, defaultValue, context);
        if (error != null)
        {
            errors.Add(new ParseError
            {
                FilePath = filePath,
                Line = 1,
                Column = 1,
                Message = error
            });
        }
    }

    /// <summary>
    /// Validates a default value against a type and returns an error message if invalid.
    /// </summary>
    public static string? ValidateDefaultForType(TypeInfo type, string defaultValue, string context)
    {
        // Handle pointers - string pointers can be null, other pointers shouldn't have defaults
        if (type.IsPointer && type.Name != "char")
        {
            return $"Pointer type '{type}' in {context} should not have a default value '{defaultValue}'";
        }

        // Validate based on category
        return type.Category switch
        {
            TypeCategory.Primitive => ValidatePrimitiveDefault(type, defaultValue, context),
            TypeCategory.Handle => ValidateHandleDefault(type, defaultValue, context),
            TypeCategory.Hash => ValidateHashDefault(defaultValue, context),
            TypeCategory.String => ValidateStringDefault(defaultValue, context),
            TypeCategory.Enum => ValidateEnumDefault(defaultValue, context),
            TypeCategory.Vector2 or TypeCategory.Vector3 or TypeCategory.Vector4
                => $"Vector type '{type}' in {context} should not have a default value",
            TypeCategory.Color => $"Color type in {context} should not have a default value",
            TypeCategory.Struct => $"Struct type '{type}' in {context} should not have a default value",
            TypeCategory.Any => null, // Any type can have any default
            TypeCategory.Void => $"Void type in {context} cannot have a default value",
            _ => null
        };
    }

    private static string? ValidatePrimitiveDefault(TypeInfo type, string defaultValue, string context)
    {
        // Boolean types
        if (type.IsBool)
        {
            if (defaultValue is "true" or "false" or "TRUE" or "FALSE" or "0" or "1")
                return null;
            return $"Boolean {context} has invalid default '{defaultValue}'. Expected: true, false, 0, or 1";
        }

        // Float types - allow numeric literals with optional decimal/scientific notation
        if (type.IsFloat)
        {
            if (IsNumericLiteral(defaultValue, allowDecimal: true))
                return null;
            return $"Float {context} has invalid default '{defaultValue}'. Expected: numeric literal (e.g., 0, 1.0, -3.14)";
        }

        // Integer types - allow integer literals only
        if (IsNumericLiteral(defaultValue, allowDecimal: false))
            return null;

        return $"Integer {context} has invalid default '{defaultValue}'. Expected: integer literal (e.g., 0, 1, -1)";
    }

    private static string? ValidateHandleDefault(TypeInfo type, string defaultValue, string context)
    {
        // Handle types can have 0, nullptr, or NULL as default (null handle)
        if (defaultValue is "0" or "nullptr" or "NULL")
            return null;

        // Provide helpful error for class handle vs non-class handle
        if (TypeInfo.IsClassHandle(type.Name))
        {
            return $"Class handle type '{type.Name}' in {context} has invalid default '{defaultValue}'. " +
                   $"Only '0', 'nullptr', or 'NULL' is allowed (will be converted to null). Did you mean: {type.Name} {GetParamNameFromContext(context)} = 0";
        }

        return $"Handle type '{type.Name}' in {context} has invalid default '{defaultValue}'. " +
               $"Only '0', 'nullptr', or 'NULL' is allowed for null handle";
    }

    private static string? ValidateHashDefault(string defaultValue, string context)
    {
        // Hash can be a string literal or numeric
        if (IsStringLiteral(defaultValue) || IsNumericLiteral(defaultValue, allowDecimal: false))
            return null;

        return $"Hash {context} has invalid default '{defaultValue}'. Expected: string literal (e.g., \"model\") or numeric hash";
    }

    private static string? ValidateStringDefault(string defaultValue, string context)
    {
        // String can be a string literal or null pointer
        if (IsStringLiteral(defaultValue) || defaultValue is "nullptr" or "NULL" or "0")
            return null;

        return $"String {context} has invalid default '{defaultValue}'. Expected: string literal (e.g., \"text\"), nullptr, or NULL";
    }

    private static string? ValidateEnumDefault(string defaultValue, string context)
    {
        // Enum can be numeric (cast) or an identifier (enum member name)
        if (IsNumericLiteral(defaultValue, allowDecimal: false) || IsIdentifier(defaultValue))
            return null;

        return $"Enum {context} has invalid default '{defaultValue}'. Expected: numeric value or enum member name";
    }

    private static bool IsNumericLiteral(string value, bool allowDecimal)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var span = value.AsSpan();
        var start = 0;

        // Allow leading minus for negative numbers
        if (span[0] == '-')
        {
            if (span.Length == 1)
                return false;
            start = 1;
        }

        // Check for hex literal (0x...)
        if (span.Length > start + 2 && span[start] == '0' && (span[start + 1] == 'x' || span[start + 1] == 'X'))
        {
            for (var i = start + 2; i < span.Length; i++)
            {
                if (!char.IsAsciiHexDigit(span[i]))
                    return false;
            }
            return span.Length > start + 2;
        }

        var hasDecimal = false;
        var hasExponent = false;

        for (var i = start; i < span.Length; i++)
        {
            var c = span[i];

            if (char.IsAsciiDigit(c))
                continue;

            if (allowDecimal && c == '.' && !hasDecimal && !hasExponent)
            {
                hasDecimal = true;
                continue;
            }

            if (allowDecimal && (c == 'e' || c == 'E') && !hasExponent && i > start)
            {
                hasExponent = true;
                // Allow optional sign after exponent
                if (i + 1 < span.Length && (span[i + 1] == '+' || span[i + 1] == '-'))
                    i++;
                continue;
            }

            // Allow 'f' suffix for float literals
            if (allowDecimal && (c == 'f' || c == 'F') && i == span.Length - 1)
                continue;

            return false;
        }

        return true;
    }

    private static bool IsStringLiteral(string value)
    {
        return value.Length >= 2 &&
               ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')));
    }

    private static bool IsIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        // First char must be letter or underscore
        if (!char.IsLetter(value[0]) && value[0] != '_')
            return false;

        // Rest can be letter, digit, or underscore
        for (var i = 1; i < value.Length; i++)
        {
            if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                return false;
        }

        return true;
    }

    private static string GetParamNameFromContext(string context)
    {
        // Extract param name from "parameter 'name'" format
        var start = context.IndexOf('\'');
        var end = context.LastIndexOf('\'');
        if (start >= 0 && end > start)
            return context[(start + 1)..end];
        return "param";
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
