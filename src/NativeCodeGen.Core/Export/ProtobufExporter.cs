using System.Diagnostics.CodeAnalysis;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;
using ProtoBuf;

namespace NativeCodeGen.Core.Export;

/// <summary>
/// Exports the native database to a binary protobuf file.
/// </summary>
public class ProtobufExporter : IExporter
{
    // Preserve all export model types for protobuf-net reflection
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportDatabase))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportNamespace))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportNative))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportParameter))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportEnum))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportEnumMember))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportStruct))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportStructField))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportNativeReference))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportSharedExample))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportSharedExampleCode))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportTypeInfo))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportTypeEntry))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportTypeCategory))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ParamFlags))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FieldFlags))]
    public void Export(NativeDatabase db, string outputPath, ExportOptions options)
    {
        var exportDb = DatabaseConverter.Convert(db, options);

        // Ensure output path has .bin extension or create directory
        string binaryPath;
        if (outputPath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            binaryPath = outputPath;
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }
        else
        {
            Directory.CreateDirectory(outputPath);
            binaryPath = Path.Combine(outputPath, "natives.bin");
        }

        using var file = File.Create(binaryPath);
        Serializer.Serialize(file, exportDb);
    }
}
