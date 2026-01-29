using System.Text.Json.Serialization;
using NativeCodeGen.Core.Models;
using ProtoBuf;

namespace NativeCodeGen.Core.Export;

/// <summary>
/// Export models for both JSON and Protobuf serialization.
/// Use [JsonIgnore] for fields that should be skipped in JSON (e.g., redundant keys).
/// Prefixed with "Export" to distinguish from parsing models.
/// Flag enums (ParamFlags, FieldFlags) are defined in Models namespace and shared.
/// </summary>

[ProtoContract]
public partial class ExportDatabase
{
    [ProtoMember(1)]
    public List<ExportNamespace> Namespaces { get; set; } = new();

    [ProtoMember(2)]
    public List<ExportEnum> Enums { get; set; } = new();

    [ProtoMember(3)]
    public List<ExportStruct> Structs { get; set; } = new();

    [ProtoMember(4)]
    public List<ExportSharedExample> SharedExamples { get; set; } = new();

    [ProtoMember(5)]
    public List<ExportTypeEntry> Types { get; set; } = new();
}

[ProtoContract]
public partial class ExportNamespace
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public List<ExportNative> Natives { get; set; } = new();
}

[ProtoContract]
public partial class ExportNative
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


    [ProtoMember(10)]
    [JsonPropertyName("apiset")]
    public string ApiSet { get; set; } = "client";

    [ProtoMember(11)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RelatedExamples { get; set; }
}

[ProtoContract]
public partial class ExportParameter
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Type { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string? Description { get; set; }

    [ProtoMember(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ParamFlags Flags { get; set; }

    [ProtoMember(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultValue { get; set; }
}

[ProtoContract]
public partial class ExportEnum
{
    [ProtoMember(1)]
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string? BaseType { get; set; }

    [ProtoMember(3)]
    public List<ExportEnumMember> Members { get; set; } = new();

}

[ProtoContract]
public partial class ExportEnumMember
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string? Value { get; set; }
}

[ProtoContract]
public partial class ExportStruct
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
public partial class ExportStructField
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Type { get; set; } = string.Empty;

    [ProtoMember(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; set; }

    [ProtoMember(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public FieldFlags Flags { get; set; }

    [ProtoMember(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ArraySize { get; set; }

    [ProtoMember(6)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NestedStructName { get; set; }

    [ProtoMember(7)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Alignment { get; set; }
}

[ProtoContract]
public partial class ExportNativeReference
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Hash { get; set; } = string.Empty;
}

[ProtoContract]
public partial class ExportSharedExample
{
    [ProtoMember(1)]
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Title { get; set; } = string.Empty;

    [ProtoMember(3)]
    public List<ExportSharedExampleCode> Examples { get; set; } = new();
}

[ProtoContract]
public partial class ExportSharedExampleCode
{
    [ProtoMember(1)]
    public string Content { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string? Language { get; set; }
}
