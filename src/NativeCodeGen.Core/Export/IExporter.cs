using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Core.Export;

public interface IExporter
{
    void Export(NativeDatabase db, string outputPath, ExportOptions options);
}

public class ExportOptions
{
    public bool Raw { get; set; }
    public bool Strict { get; set; }
    public HashSet<string>? Namespaces { get; set; }
    public bool IncludeEnums { get; set; } = true;
    public bool IncludeStructs { get; set; } = true;
}
