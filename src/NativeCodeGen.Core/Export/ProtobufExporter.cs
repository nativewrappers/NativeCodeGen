using NativeCodeGen.Core.Parsing;
using ProtoBuf;

namespace NativeCodeGen.Core.Export;

/// <summary>
/// Exports the native database to a binary protobuf file.
/// The schema is defined in natives.proto (static file in this directory).
/// </summary>
public class ProtobufExporter : IExporter
{
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
