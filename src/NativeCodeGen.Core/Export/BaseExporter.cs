using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Core.Export;

/// <summary>
/// Base class for code exporters that handles common filtering and warning output.
/// </summary>
public abstract class BaseExporter : IExporter
{
    protected abstract ICodeGenerator Generator { get; }

    public void Export(NativeDatabase db, string outputPath, ExportOptions options)
    {
        // Filter namespaces if specified
        if (options.Namespaces != null && options.Namespaces.Count > 0)
        {
            db = new NativeDatabase
            {
                Namespaces = db.Namespaces
                    .Where(ns => options.Namespaces.Contains(ns.Name, StringComparer.OrdinalIgnoreCase))
                    .ToList(),
                Enums = options.IncludeEnums ? db.Enums : new(),
                Structs = options.IncludeStructs ? db.Structs : new()
            };
        }

        var generatorOptions = new GeneratorOptions
        {
            UseClasses = !options.Raw,
            SingleFile = options.SingleFile,
            UseExports = options.UseExports,
            Package = options.Package,
            PackageName = options.PackageName,
            PackageVersion = options.PackageVersion
        };
        Generator.Generate(db, outputPath, generatorOptions);

        // Output any warnings from code generation
        foreach (var warning in Generator.Warnings)
        {
            Console.WriteLine($"WARNING: {warning}");
        }
    }
}
