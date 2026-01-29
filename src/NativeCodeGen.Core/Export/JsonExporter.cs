using System.Text.Json;
using System.Text.Json.Serialization;
using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Core.Export;

public class JsonExporter : IExporter
{
    public void Export(NativeDatabase db, string outputPath, ExportOptions options)
    {
        var exportDb = DatabaseConverter.Convert(db, options);

        // For JSON, we want dictionaries keyed by name for enums, structs, and examples
        var output = new JsonOutput
        {
            Natives = exportDb.Namespaces.SelectMany(ns => ns.Natives).ToList(),
            Enums = exportDb.Enums.ToDictionary(e => e.Name, e => e),
            Structs = exportDb.Structs.ToDictionary(s => s.Name, s => s),
            SharedExamples = exportDb.SharedExamples.ToDictionary(e => e.Name, e => e)
        };

        var json = JsonSerializer.Serialize(output, ExportJsonContext.Default.JsonOutput);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        File.WriteAllText(outputPath, json);
    }
}

/// <summary>
/// JSON-specific output structure with dictionaries for lookup by name.
/// </summary>
public class JsonOutput
{
    public List<ExportNative> Natives { get; set; } = new();
    public Dictionary<string, ExportEnum> Enums { get; set; } = new();
    public Dictionary<string, ExportStruct> Structs { get; set; } = new();
    public Dictionary<string, ExportSharedExample> SharedExamples { get; set; } = new();
}
