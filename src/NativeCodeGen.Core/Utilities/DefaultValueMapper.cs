using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Utilities;

/// <summary>
/// Maps C-style default values to target language values.
/// </summary>
public static class DefaultValueMapper
{
    /// <summary>
    /// Maps a C-style default value to the target language's syntax.
    /// </summary>
    public static string MapDefaultValue(string value, TypeInfo type)
    {
        // Convert C-style boolean literals
        if (type.IsBool)
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                   value == "1"
                ? "true"
                : "false";
        }

        // Numeric values pass through
        if (type.Category == TypeCategory.Primitive && (type.IsFloat || type.Name == "int"))
        {
            return value;
        }

        // String values need quotes if not already quoted
        if (type.Category == TypeCategory.String)
        {
            if (value.StartsWith("\"") && value.EndsWith("\""))
                return value;
            return $"\"{value}\"";
        }

        // Default: pass through as-is
        return value;
    }

    /// <summary>
    /// Maps a C-style default value to C# syntax.
    /// </summary>
    public static string MapDefaultValueCSharp(string value, TypeInfo type)
    {
        // Convert C-style boolean literals
        if (type.IsBool)
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ||
                   value == "1"
                ? "true"
                : "false";
        }

        // Handle null pointers for handle types
        if (type.Category == TypeCategory.Handle && TypeInfo.IsClassHandle(type.Name))
        {
            if (value is "0" or "nullptr" or "NULL" or "null")
            {
                return "null";
            }
        }

        // Numeric values - add f suffix for floats
        if (type.IsFloat)
        {
            var numValue = value.TrimEnd('f', 'F');
            if (!numValue.Contains('.'))
            {
                numValue += ".0";
            }
            return numValue + "f";
        }

        // String values need quotes if not already quoted
        if (type.Category == TypeCategory.String)
        {
            if (value.StartsWith("\"") && value.EndsWith("\""))
                return value;
            return $"\"{value}\"";
        }

        // Hash values
        if (type.Category == TypeCategory.Hash)
        {
            // Already numeric
            if (uint.TryParse(value, out _) || value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            // String hash - will need runtime hashing
            return $"Game.GenerateHash(\"{value}\")";
        }

        // Default: pass through as-is
        return value;
    }
}
