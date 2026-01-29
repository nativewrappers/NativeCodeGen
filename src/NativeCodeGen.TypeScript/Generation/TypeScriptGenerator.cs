using NativeCodeGen.Core.Export;
using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;
using NativeCodeGen.Core.Utilities;

namespace NativeCodeGen.TypeScript.Generation;

public class TypeScriptGenerator : ICodeGenerator
{
    private readonly TypeScriptEmitter _emitter = new();
    private readonly SharedClassGenerator _classGenerator;
    private readonly SharedStructGenerator _structGenerator;
    private readonly SharedEnumGenerator _enumGenerator;
    private readonly NativeClassifier _classifier = new();

    public IReadOnlyList<string> Warnings => _structGenerator.Warnings;

    public TypeScriptGenerator()
    {
        _classGenerator = new SharedClassGenerator(_emitter);
        _structGenerator = new SharedStructGenerator(_emitter);
        _enumGenerator = new SharedEnumGenerator(_emitter);
    }

    public void Generate(NativeDatabase db, string outputPath, GeneratorOptions options)
    {
        Directory.CreateDirectory(outputPath);

        if (options.UseClasses)
        {
            GenerateClassOutput(db, outputPath);
        }
        else if (options.SingleFile)
        {
            GenerateSingleFileOutput(db, outputPath);
        }
        else
        {
            GenerateRawOutput(db, outputPath);
        }
    }

    private void GenerateRawOutput(NativeDatabase db, string outputPath)
    {
        GenerateCommonFiles(db, outputPath);

        var nativesDir = Path.Combine(outputPath, "natives");
        Directory.CreateDirectory(nativesDir);

        var builder = new RawNativeBuilder(Language.TypeScript, _emitter.TypeMapper);

        foreach (var ns in db.Namespaces)
        {
            builder.Clear();
            builder.EmitImports();

            foreach (var native in ns.Natives)
            {
                builder.EmitFunction(native, BindingStyle.Export);
            }

            File.WriteAllText(Path.Combine(nativesDir, $"{ns.Name}.ts"), builder.ToString());
        }

        GenerateRawIndex(db, outputPath);
    }

    private void GenerateSingleFileOutput(NativeDatabase db, string outputPath)
    {
        GenerateCommonFiles(db, outputPath);

        // Build a map of function names to detect duplicates
        var nameCount = new Dictionary<string, int>();
        foreach (var ns in db.Namespaces)
        {
            foreach (var native in ns.Natives)
            {
                var name = GetFunctionName(native.Name);
                nameCount[name] = nameCount.GetValueOrDefault(name, 0) + 1;
            }
        }

        var builder = new RawNativeBuilder(Language.TypeScript, _emitter.TypeMapper);
        builder.EmitImports(singleFile: true);

        foreach (var ns in db.Namespaces.OrderBy(n => n.Name))
        {
            foreach (var native in ns.Natives)
            {
                var baseName = GetFunctionName(native.Name);
                var finalName = nameCount.TryGetValue(baseName, out var count) && count > 1
                    ? ToPascalCase(ns.Name.ToLowerInvariant()) + baseName
                    : baseName;

                builder.EmitFunction(native, BindingStyle.Global, nameOverride: finalName);
            }
        }

        File.WriteAllText(Path.Combine(outputPath, "natives.ts"), builder.ToString());
        GenerateSingleFileIndex(db, outputPath);
    }

    private void GenerateSingleFileIndex(NativeDatabase db, string outputPath)
    {
        var cb = new CodeBuilder();

        cb.AppendLine("// Types");
        cb.AppendLine("export { IHandle } from './types/IHandle';");
        cb.AppendLine("export { Vector3 } from './types/Vector3';");
        cb.AppendLine("export { BufferedClass } from './types/BufferedClass';");
        cb.AppendLine();

        cb.AppendLine("// Enums");
        foreach (var enumName in db.Enums.Keys.OrderBy(k => k))
        {
            cb.AppendLine($"export {{ {enumName} }} from './enums/{enumName}';");
        }
        cb.AppendLine();

        cb.AppendLine("// Structs");
        foreach (var structName in db.Structs.Keys.OrderBy(k => k))
        {
            cb.AppendLine($"export {{ {structName} }} from './structs/{structName}';");
        }
        cb.AppendLine();

        cb.AppendLine("// Natives");
        cb.AppendLine("export * from './natives';");

        File.WriteAllText(Path.Combine(outputPath, "index.ts"), cb.ToString());
    }

    private void GenerateClassOutput(NativeDatabase db, string outputPath)
    {
        GenerateCommonFiles(db, outputPath);

        var classNatives = _classifier.Classify(db);
        var handleClassNames = classNatives.HandleClasses.Keys.ToHashSet();

        var classesDir = Path.Combine(outputPath, "classes");
        Directory.CreateDirectory(classesDir);
        foreach (var (className, natives) in classNatives.HandleClasses)
        {
            var baseClass = NativeClassifier.HandleClassHierarchy.GetValueOrDefault(className);
            var content = _classGenerator.GenerateHandleClass(className, baseClass, natives);
            File.WriteAllText(Path.Combine(classesDir, $"{className}.ts"), content);
        }

        var namespacesDir = Path.Combine(outputPath, "namespaces");
        Directory.CreateDirectory(namespacesDir);
        foreach (var (namespaceName, natives) in classNatives.NamespaceClasses)
        {
            var className = NameConverter.NamespaceToClassName(namespaceName, handleClassNames);
            var content = _classGenerator.GenerateNamespaceClass(namespaceName, natives, handleClassNames);
            File.WriteAllText(Path.Combine(namespacesDir, $"{className}.ts"), content);
        }

        GenerateClassIndex(db, classNatives, outputPath, handleClassNames);
    }

    private void GenerateCommonFiles(NativeDatabase db, string outputPath)
    {
        GenerateTypeFiles(outputPath);

        var enumsDir = Path.Combine(outputPath, "enums");
        Directory.CreateDirectory(enumsDir);
        foreach (var enumDef in db.Enums.Values)
        {
            _enumGenerator.GenerateFile(enumDef, enumsDir);
        }

        var structsDir = Path.Combine(outputPath, "structs");
        Directory.CreateDirectory(structsDir);
        File.WriteAllText(Path.Combine(structsDir, "BufferedClass.ts"), GenerateBufferedClassBase());
        _structGenerator.SetStructRegistry(db.Structs);
        foreach (var structDef in db.Structs.Values)
        {
            _structGenerator.GenerateFile(structDef, structsDir);
        }
    }

    private string GenerateBufferedClassBase()
    {
        return """
            export abstract class BufferedClass {
              buffer: ArrayBuffer;
              view: DataView;

              constructor(bufferSize: number, existingBuffer?: ArrayBuffer, offset?: number) {
                if (existingBuffer !== undefined && offset !== undefined) {
                  this.buffer = existingBuffer;
                  this.view = new DataView(existingBuffer, offset, bufferSize);
                } else {
                  this.buffer = new ArrayBuffer(bufferSize);
                  this.view = new DataView(this.buffer);
                }
              }
            }
            """;
    }

    private void GenerateTypeFiles(string outputPath)
    {
        var typesDir = Path.Combine(outputPath, "types");
        Directory.CreateDirectory(typesDir);

        File.WriteAllText(Path.Combine(typesDir, "IHandle.ts"), """
            export interface IHandle {
              handle: number;
            }
            """);

        File.WriteAllText(Path.Combine(typesDir, "Vector3.ts"), """
            export class Vector3 {
              constructor(
                public x: number = 0,
                public y: number = 0,
                public z: number = 0
              ) {}

              static fromArray(arr: [number, number, number]): Vector3 {
                return new Vector3(arr[0], arr[1], arr[2]);
              }

              toArray(): [number, number, number] {
                return [this.x, this.y, this.z];
              }

              add(other: Vector3): Vector3 {
                return new Vector3(this.x + other.x, this.y + other.y, this.z + other.z);
              }

              subtract(other: Vector3): Vector3 {
                return new Vector3(this.x - other.x, this.y - other.y, this.z - other.z);
              }

              multiply(scalar: number): Vector3 {
                return new Vector3(this.x * scalar, this.y * scalar, this.z * scalar);
              }

              length(): number {
                return Math.sqrt(this.x * this.x + this.y * this.y + this.z * this.z);
              }

              normalize(): Vector3 {
                const len = this.length();
                if (len === 0) return new Vector3();
                return this.multiply(1 / len);
              }

              distance(other: Vector3): number {
                return this.subtract(other).length();
              }
            }
            """);

        File.WriteAllText(Path.Combine(typesDir, "BufferedClass.ts"), GenerateBufferedClassBase());
    }

    private void GenerateRawIndex(NativeDatabase db, string outputPath)
    {
        var cb = new CodeBuilder();

        cb.AppendLine("// Types");
        cb.AppendLine("export { IHandle } from './types/IHandle';");
        cb.AppendLine("export { Vector3 } from './types/Vector3';");
        cb.AppendLine("export { BufferedClass } from './types/BufferedClass';");
        cb.AppendLine();

        cb.AppendLine("// Enums");
        foreach (var enumName in db.Enums.Keys.OrderBy(k => k))
        {
            cb.AppendLine($"export {{ {enumName} }} from './enums/{enumName}';");
        }
        cb.AppendLine();

        cb.AppendLine("// Structs");
        foreach (var structName in db.Structs.Keys.OrderBy(k => k))
        {
            cb.AppendLine($"export {{ {structName} }} from './structs/{structName}';");
        }
        cb.AppendLine();

        cb.AppendLine("// Natives");
        foreach (var ns in db.Namespaces.OrderBy(n => n.Name))
        {
            cb.AppendLine($"export * from './natives/{ns.Name}';");
        }

        File.WriteAllText(Path.Combine(outputPath, "index.ts"), cb.ToString());
    }

    private void GenerateClassIndex(NativeDatabase db, ClassifiedNatives classNatives, string outputPath, HashSet<string> handleClassNames)
    {
        var cb = new CodeBuilder();

        cb.AppendLine("// Types");
        cb.AppendLine("export { IHandle } from './types/IHandle';");
        cb.AppendLine("export { Vector3 } from './types/Vector3';");
        cb.AppendLine("export { BufferedClass } from './types/BufferedClass';");
        cb.AppendLine();

        cb.AppendLine("// Enums");
        foreach (var enumName in db.Enums.Keys.OrderBy(k => k))
        {
            cb.AppendLine($"export {{ {enumName} }} from './enums/{enumName}';");
        }
        cb.AppendLine();

        cb.AppendLine("// Structs");
        foreach (var structName in db.Structs.Keys.OrderBy(k => k))
        {
            cb.AppendLine($"export {{ {structName} }} from './structs/{structName}';");
        }
        cb.AppendLine();

        cb.AppendLine("// Classes");
        foreach (var className in classNatives.HandleClasses.Keys.OrderBy(k => k))
        {
            cb.AppendLine($"export {{ {className} }} from './classes/{className}';");
        }
        cb.AppendLine();

        cb.AppendLine("// Namespace utilities");
        foreach (var nsName in classNatives.NamespaceClasses.Keys.OrderBy(k => k))
        {
            var className = NameConverter.NamespaceToClassName(nsName, handleClassNames);
            cb.AppendLine($"export {{ {className} }} from './namespaces/{className}';");
        }

        File.WriteAllText(Path.Combine(outputPath, "index.ts"), cb.ToString());
    }

    private static string GetFunctionName(string nativeName)
    {
        var trimmed = nativeName.TrimStart('_');
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return "N_" + trimmed;
        return ToPascalCase(trimmed);
    }

    private static string ToPascalCase(string name)
    {
        var sb = new System.Text.StringBuilder();
        bool capitalizeNext = true;
        foreach (var c in name)
        {
            if (c == '_')
                capitalizeNext = true;
            else if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
