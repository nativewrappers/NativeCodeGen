namespace NativeCodeGen.Core.Utilities;

/// <summary>
/// Extension methods for string operations.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Performs a case-insensitive equality comparison.
    /// </summary>
    public static bool EqualsIgnoreCase(this string str, string other) =>
        str.Equals(other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Performs a case-insensitive starts-with check.
    /// </summary>
    public static bool StartsWithIgnoreCase(this string str, string value) =>
        str.StartsWith(value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Performs a case-insensitive ends-with check.
    /// </summary>
    public static bool EndsWithIgnoreCase(this string str, string value) =>
        str.EndsWith(value, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Performs a case-insensitive contains check.
    /// </summary>
    public static bool ContainsIgnoreCase(this string str, string value) =>
        str.Contains(value, StringComparison.OrdinalIgnoreCase);
}
