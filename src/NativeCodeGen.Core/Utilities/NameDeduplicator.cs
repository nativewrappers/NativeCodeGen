namespace NativeCodeGen.Core.Utilities;

/// <summary>
/// Removes redundant namespace/class prefixes from method names.
/// </summary>
public static class NameDeduplicator
{
    private static readonly string[] CommonPrefixes = { "GET_", "SET_", "IS_", "HAS_", "CAN_", "DOES_", "CREATE_", "DELETE_", "CLEAR_" };

    /// <summary>
    /// Removes namespace prefix and converts to method name.
    /// E.g., "TASK_START_SCENARIO" with namespace "TASK" becomes "startScenario" (camelCase).
    /// </summary>
    public static string DeduplicateForNamespace(string nativeName, string namespaceName, NamingConvention convention)
    {
        var deduplicated = RemoveNamespacePrefix(nativeName, namespaceName);
        return NameConverter.NativeToMethodName(deduplicated, convention);
    }

    /// <summary>
    /// Removes class name and converts to method name.
    /// E.g., "GET_ENTITY_COORDS" on Entity class becomes "getCoords" (camelCase).
    /// </summary>
    public static string DeduplicateForClass(string nativeName, string className, NamingConvention convention)
    {
        var deduplicated = RemoveClassPrefix(nativeName, className);
        return NameConverter.NativeToMethodName(deduplicated, convention);
    }

    /// <summary>
    /// Removes namespace prefix from a native name.
    /// </summary>
    public static string RemoveNamespacePrefix(string nativeName, string namespaceName)
    {
        var trimmedName = nativeName.TrimStart('_');
        var nsUpper = namespaceName.ToUpperInvariant();

        if (trimmedName.StartsWith(nsUpper, StringComparison.OrdinalIgnoreCase))
        {
            var remaining = trimmedName[nsUpper.Length..];
            if (remaining.StartsWith('_'))
            {
                remaining = remaining[1..];
            }

            if (!string.IsNullOrEmpty(remaining))
            {
                return remaining;
            }
        }

        return trimmedName;
    }

    /// <summary>
    /// Removes class name from a native name for instance methods.
    /// </summary>
    public static string RemoveClassPrefix(string nativeName, string className)
    {
        var trimmedName = nativeName.TrimStart('_');
        var upperName = trimmedName.ToUpperInvariant();
        var upperClass = className.ToUpperInvariant();

        // Check for patterns like GET_CLASSNAME_*, SET_CLASSNAME_*, IS_CLASSNAME_*
        foreach (var prefix in CommonPrefixes)
        {
            var pattern = prefix + upperClass + "_";
            if (upperName.StartsWith(pattern))
            {
                return prefix + trimmedName[pattern.Length..];
            }
        }

        // Check for just CLASSNAME_ prefix without verb
        if (upperName.StartsWith(upperClass + "_"))
        {
            return trimmedName[(className.Length + 1)..];
        }

        // Check for _CLASSNAME_ in middle
        var classPattern = "_" + upperClass + "_";
        var idx = upperName.IndexOf(classPattern);
        if (idx > 0)
        {
            var beforeClass = trimmedName[..idx];
            var afterClass = trimmedName[(idx + classPattern.Length)..];
            return beforeClass + "_" + afterClass;
        }

        return trimmedName;
    }
}
