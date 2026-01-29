using System.Text;

namespace NativeCodeGen.Core.Utilities;

public enum NamingConvention
{
    PascalCase,
    CamelCase,
    SnakeCase
}

/// <summary>
/// Language-agnostic name conversion utilities.
/// </summary>
public static class NameConverter
{
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var sb = new StringBuilder();
        bool capitalizeNext = true;

        foreach (var c in name)
        {
            if (c == '_')
            {
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }

    public static string ToCamelCase(string name)
    {
        var pascal = ToPascalCase(name);
        if (string.IsNullOrEmpty(pascal))
            return pascal;

        int i = 0;
        while (i < pascal.Length - 1 && char.IsUpper(pascal[i]) && char.IsUpper(pascal[i + 1]))
        {
            i++;
        }

        if (i == 0)
        {
            return char.ToLowerInvariant(pascal[0]) + pascal[1..];
        }
        else
        {
            return pascal[..i].ToLowerInvariant() + pascal[i..];
        }
    }

    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public static string Convert(string name, NamingConvention convention) => convention switch
    {
        NamingConvention.PascalCase => ToPascalCase(name),
        NamingConvention.CamelCase => ToCamelCase(name),
        NamingConvention.SnakeCase => ToSnakeCase(name),
        _ => name
    };

    /// <summary>
    /// Normalizes native names - handles hex prefixes and leading underscores.
    /// </summary>
    public static string NormalizeNativeName(string nativeName)
    {
        var trimmed = nativeName.TrimStart('_');

        if (trimmed.StartsWith("N_0x", StringComparison.OrdinalIgnoreCase))
        {
            return "N_" + trimmed[2..];
        }

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return "N_" + trimmed;
        }

        return trimmed;
    }

    /// <summary>
    /// Converts a native name to a method/function name with the given convention.
    /// </summary>
    public static string NativeToMethodName(string nativeName, NamingConvention convention)
    {
        var normalized = NormalizeNativeName(nativeName);

        // Hex names stay as-is
        if (normalized.StartsWith("N_"))
            return normalized;

        return Convert(normalized, convention);
    }

    /// <summary>
    /// Converts a namespace name to a class/module name.
    /// </summary>
    public static string NamespaceToClassName(string ns)
    {
        return ToPascalCase(ns.ToLowerInvariant());
    }

    /// <summary>
    /// Converts a namespace name to a class/module name, avoiding conflicts with handle class names.
    /// </summary>
    public static string NamespaceToClassName(string ns, HashSet<string> handleClassNames)
    {
        var baseName = ToPascalCase(ns.ToLowerInvariant());

        // If this conflicts with a handle class, add "Statics" suffix
        if (handleClassNames.Contains(baseName))
        {
            return baseName + "Statics";
        }

        return baseName;
    }
}
