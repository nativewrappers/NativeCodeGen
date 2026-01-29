using System.Text.Json.Serialization;
using ProtoBuf;

namespace NativeCodeGen.Core.Export;

/// <summary>
/// Export models for both JSON and Protobuf serialization.
/// Use [JsonIgnore] for fields that should be skipped in JSON (e.g., redundant keys).
/// Prefixed with "Export" to distinguish from parsing models.
/// </summary>

[ProtoContract]
public class ExportDatabase
{
    [ProtoMember(1)]
    public List<ExportNamespace> Namespaces { get; set; } = new();

    [ProtoMember(2)]
    public List<ExportEnum> Enums { get; set; } = new();

    [ProtoMember(3)]
    public List<ExportStruct> Structs { get; set; } = new();

    [ProtoMember(4)]
    public List<ExportSharedExample> SharedExamples { get; set; } = new();
}

[ProtoContract]
public class ExportNamespace
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public List<ExportNative> Natives { get; set; } = new();
}

[ProtoContract]
public class ExportNative
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Hash { get; set; } = string.Empty;

    [ProtoMember(3)]
    [JsonPropertyName("ns")]
    public string Namespace { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string? Description { get; set; }

    [ProtoMember(5)]
    public List<ExportParameter> Parameters { get; set; } = new();

    [ProtoMember(6)]
    public string ReturnType { get; set; } = string.Empty;

    [ProtoMember(7)]
    public string? ReturnDescription { get; set; }

    [ProtoMember(8)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Aliases { get; set; }

    [ProtoMember(9)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? UsedEnums { get; set; }

    [ProtoMember(10)]
    [JsonPropertyName("apiset")]
    public string ApiSet { get; set; } = "client";

    [ProtoMember(11)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RelatedExamples { get; set; }
}

[ProtoContract]
public class ExportParameter
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Type { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string? Description { get; set; }

    [ProtoMember(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Attributes { get; set; }

    [ProtoMember(5)]
    public string? DefaultValue { get; set; }

    [ProtoMember(6)]
    public bool IsOutput { get; set; }
}

[ProtoContract]
public class ExportEnum
{
    [ProtoMember(1)]
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string? BaseType { get; set; }

    [ProtoMember(3)]
    public List<ExportEnumMember> Members { get; set; } = new();

    [ProtoMember(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? UsedByNatives { get; set; }
}

[ProtoContract]
public class ExportEnumMember
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string? Value { get; set; }
}

[ProtoContract]
public class ExportStruct
{
    [ProtoMember(1)]
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public List<ExportStructField> Fields { get; set; } = new();

    [ProtoMember(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ExportNativeReference>? UsedByNatives { get; set; }

    [ProtoMember(4)]
    public int? DefaultAlignment { get; set; }
}

[ProtoContract]
public class ExportStructField
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Type { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string? Comment { get; set; }

    [ProtoMember(4)]
    public bool IsInput { get; set; }

    [ProtoMember(5)]
    public bool IsOutput { get; set; }

    [ProtoMember(6)]
    public int ArraySize { get; set; }

    [ProtoMember(7)]
    public bool IsArray { get; set; }

    [ProtoMember(8)]
    public bool IsNestedStruct { get; set; }

    [ProtoMember(9)]
    public string? NestedStructName { get; set; }

    [ProtoMember(10)]
    public bool IsPadding { get; set; }

    [ProtoMember(11)]
    public int? Alignment { get; set; }
}

[ProtoContract]
public class ExportNativeReference
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Hash { get; set; } = string.Empty;
}

[ProtoContract]
public class ExportSharedExample
{
    [ProtoMember(1)]
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Content { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string? Language { get; set; }
}
