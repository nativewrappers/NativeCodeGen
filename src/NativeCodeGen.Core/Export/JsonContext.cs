using System.Text.Json.Serialization;
using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Export;

/// <summary>
/// Source generator context for JSON serialization.
/// Required for trimming/AOT compatibility.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsonOutput))]
[JsonSerializable(typeof(ExportDatabase))]
[JsonSerializable(typeof(ExportNamespace))]
[JsonSerializable(typeof(ExportNative))]
[JsonSerializable(typeof(ExportParameter))]
[JsonSerializable(typeof(ExportEnum))]
[JsonSerializable(typeof(ExportEnumMember))]
[JsonSerializable(typeof(ExportStruct))]
[JsonSerializable(typeof(ExportStructField))]
[JsonSerializable(typeof(ExportNativeReference))]
[JsonSerializable(typeof(ExportSharedExample))]
[JsonSerializable(typeof(ExportSharedExampleCode))]
[JsonSerializable(typeof(List<ExportSharedExampleCode>))]
[JsonSerializable(typeof(ExportCallout))]
[JsonSerializable(typeof(List<ExportCallout>))]
[JsonSerializable(typeof(CalloutType))]
[JsonSerializable(typeof(ParamFlags))]
[JsonSerializable(typeof(FieldFlags))]
[JsonSerializable(typeof(List<ExportNative>))]
[JsonSerializable(typeof(List<ExportParameter>))]
[JsonSerializable(typeof(List<ExportEnumMember>))]
[JsonSerializable(typeof(List<ExportStructField>))]
[JsonSerializable(typeof(Dictionary<string, ExportEnum>))]
[JsonSerializable(typeof(Dictionary<string, ExportStruct>))]
[JsonSerializable(typeof(Dictionary<string, ExportSharedExample>))]
[JsonSerializable(typeof(Dictionary<string, ExportTypeInfo>))]
[JsonSerializable(typeof(ExportTypeInfo))]
[JsonSerializable(typeof(ExportTypeCategory))]
[JsonSerializable(typeof(ExportTypeEntry))]
[JsonSerializable(typeof(List<ExportTypeEntry>))]
public partial class ExportJsonContext : JsonSerializerContext
{
}
