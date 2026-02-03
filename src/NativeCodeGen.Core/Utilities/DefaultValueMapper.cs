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
}
