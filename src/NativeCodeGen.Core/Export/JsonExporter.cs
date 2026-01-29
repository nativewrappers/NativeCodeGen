using System.Text.Json;
using System.Text.Json.Serialization;
using NativeCodeGen.Core.Parsing;
using ProtoBuf;

namespace NativeCodeGen.Core.Export;

public class JsonExporter : IExporter
{
    public void Export(NativeDatabase db, string outputPath, ExportOptions options)
    {
        var exportDb = DatabaseConverter.Convert(db, options);

        // For JSON, we want dictionaries keyed by name for enums, structs, and examples
        var output = new JsonOutput
        {
            Types = TypeRegistry.GetTypeDefinitions(),
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
    public Dictionary<string, ExportTypeInfo> Types { get; set; } = new();
    public List<ExportNative> Natives { get; set; } = new();
    public Dictionary<string, ExportEnum> Enums { get; set; } = new();
    public Dictionary<string, ExportStruct> Structs { get; set; } = new();
    public Dictionary<string, ExportSharedExample> SharedExamples { get; set; } = new();
}

/// <summary>
/// Type information for JSON/Protobuf export.
/// </summary>
[ProtoContract]
public class ExportTypeInfo
{
    [ProtoMember(1)]
    [JsonConverter(typeof(JsonStringEnumConverter<ExportTypeCategory>))]
    public ExportTypeCategory Category { get; set; }

    [ProtoMember(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NativeType { get; set; }

    [ProtoMember(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

/// <summary>
/// Type categories for the type registry.
/// </summary>
[ProtoContract]
public enum ExportTypeCategory
{
    Primitive = 0,
    Handle = 1,
    Hash = 2,
    Vector3 = 3,
    String = 4,
    Void = 5,
    Any = 6
}

/// <summary>
/// Type entry for protobuf serialization (since protobuf doesn't support dictionaries directly).
/// </summary>
[ProtoContract]
public class ExportTypeEntry
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public ExportTypeInfo Type { get; set; } = new();
}

/// <summary>
/// Registry of all supported types and their mappings.
/// </summary>
public static class TypeRegistry
{
    public static Dictionary<string, ExportTypeInfo> GetTypeDefinitions()
    {
        var types = new Dictionary<string, ExportTypeInfo>();

        // Primitives - integers
        types["int"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "32-bit signed integer" };
        types["uint"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "32-bit unsigned integer" };
        types["i8"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "8-bit signed integer" };
        types["i16"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "16-bit signed integer" };
        types["i32"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "32-bit signed integer" };
        types["i64"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "64-bit signed integer" };
        types["u8"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "8-bit unsigned integer" };
        types["u16"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "16-bit unsigned integer" };
        types["u32"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "32-bit unsigned integer" };
        types["u64"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "64-bit unsigned integer" };

        // Primitives - floats
        types["float"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "float", Description = "32-bit floating point" };
        types["double"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "float", Description = "64-bit floating point" };
        types["f32"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "float", Description = "32-bit floating point" };
        types["f64"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "float", Description = "64-bit floating point" };

        // Primitives - boolean
        types["bool"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "Boolean (0 or 1)" };
        types["BOOL"] = new ExportTypeInfo { Category = ExportTypeCategory.Primitive, NativeType = "int", Description = "Boolean (0 or 1)" };

        // Special types
        types["void"] = new ExportTypeInfo { Category = ExportTypeCategory.Void, Description = "No return value" };
        types["Any"] = new ExportTypeInfo { Category = ExportTypeCategory.Any, NativeType = "int", Description = "Any type (context-dependent)" };
        types["Hash"] = new ExportTypeInfo { Category = ExportTypeCategory.Hash, NativeType = "int", Description = "32-bit hash value (joaat)" };
        types["Vector3"] = new ExportTypeInfo { Category = ExportTypeCategory.Vector3, Description = "3D vector (x, y, z floats)" };
        types["string"] = new ExportTypeInfo { Category = ExportTypeCategory.String, Description = "Null-terminated string pointer" };
        types["char*"] = new ExportTypeInfo { Category = ExportTypeCategory.String, Description = "Null-terminated string pointer" };

        // Handle types - all map to int at native level
        var handles = new[]
        {
            ("Entity", "Base type for all world entities"),
            ("Ped", "Pedestrian/character handle (extends Entity)"),
            ("Vehicle", "Vehicle handle (extends Entity)"),
            ("Object", "Object/prop handle (extends Entity)"),
            ("Pickup", "Pickup handle"),
            ("Player", "Player handle"),
            ("Cam", "Camera handle"),
            ("Blip", "Map blip handle"),
            ("Interior", "Interior handle"),
            ("FireId", "Fire instance handle"),
            ("AnimScene", "Animation scene handle"),
            ("ItemSet", "Item set handle"),
            ("PersChar", "Persistent character handle"),
            ("PopZone", "Population zone handle"),
            ("PropSet", "Prop set handle"),
            ("Volume", "Volume handle"),
            ("ScrHandle", "Generic script handle"),
            ("PedGroup", "Ped group handle")
        };

        foreach (var (name, desc) in handles)
        {
            types[name] = new ExportTypeInfo { Category = ExportTypeCategory.Handle, NativeType = "int", Description = desc };
        }

        return types;
    }
}
