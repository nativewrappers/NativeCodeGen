using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Core.Registry;

public class EnumRegistry
{
    private readonly Dictionary<string, EnumDefinition> _enums = new(StringComparer.OrdinalIgnoreCase);
    private readonly EnumParser _parser = new();

    public void LoadEnums(string enumsDirectory)
    {
        if (!Directory.Exists(enumsDirectory))
            return;

        var files = Directory.GetFiles(enumsDirectory, "*.mdx")
            .Concat(Directory.GetFiles(enumsDirectory, "*.c"))
            .Concat(Directory.GetFiles(enumsDirectory, "*.h"));

        foreach (var file in files)
        {
            var result = _parser.ParseFile(file);
            if (result.IsSuccess && result.Value != null)
            {
                _enums[result.Value.Name] = result.Value;
            }
        }
    }

    public void TrackUsage(string enumName, string nativeHash)
    {
        if (_enums.TryGetValue(enumName, out var enumDef))
        {
            if (!enumDef.UsedByNatives.Contains(nativeHash))
            {
                enumDef.UsedByNatives.Add(nativeHash);
            }
        }
    }

    public EnumDefinition? GetEnum(string name)
    {
        return _enums.TryGetValue(name, out var enumDef) ? enumDef : null;
    }

    public bool Contains(string name) => _enums.ContainsKey(name);

    /// <summary>
    /// Gets the base type of an enum by name. Returns null if not found.
    /// Used for resolving enum types in signatures.
    /// </summary>
    public string? GetBaseType(string name)
    {
        if (_enums.TryGetValue(name, out var enumDef))
        {
            // Default to "int" if no base type specified
            return enumDef.BaseType ?? "int";
        }
        return null;
    }

    public Dictionary<string, EnumDefinition> GetAllEnums() => new(_enums);

    public int Count => _enums.Count;
}
