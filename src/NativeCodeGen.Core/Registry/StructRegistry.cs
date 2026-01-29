using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Core.Registry;

public class StructRegistry
{
    private readonly Dictionary<string, StructDefinition> _structs = new(StringComparer.OrdinalIgnoreCase);
    private readonly StructParser _parser = new();

    public void LoadStructs(string structsDirectory)
    {
        if (!Directory.Exists(structsDirectory))
            return;

        var files = Directory.GetFiles(structsDirectory, "*.c")
            .Concat(Directory.GetFiles(structsDirectory, "*.h"));

        foreach (var file in files)
        {
            // Skip empty files
            var fileInfo = new FileInfo(file);
            if (fileInfo.Length == 0)
                continue;

            var results = _parser.ParseFileAll(file);
            foreach (var result in results)
            {
                if (result.IsSuccess && result.Value != null)
                {
                    _structs[result.Value.Name] = result.Value;
                }
            }
        }
    }

    public StructDefinition? GetStruct(string name)
    {
        return _structs.TryGetValue(name, out var structDef) ? structDef : null;
    }

    public bool Contains(string name) => _structs.ContainsKey(name);

    public Dictionary<string, StructDefinition> GetAllStructs() => new(_structs);

    public int Count => _structs.Count;
}
