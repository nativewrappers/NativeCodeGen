using NativeCodeGen.Core.Export;
using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;
using NativeCodeGen.Core.TypeSystem;
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

    public void Generate(NativeDatabase db, string outputPath, bool useClasses)
    {
        Directory.CreateDirectory(outputPath);

        if (useClasses)
        {
            GenerateClassOutput(db, outputPath);
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

        foreach (var ns in db.Namespaces)
        {
            var content = GenerateRawNamespace(ns);
            File.WriteAllText(Path.Combine(nativesDir, $"{ns.Name}.ts"), content);
        }

        GenerateRawIndex(db, outputPath);
    }

    private void GenerateClassOutput(NativeDatabase db, string outputPath)
    {
        GenerateCommonFiles(db, outputPath);

        var classNatives = _classifier.Classify(db);

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
            var content = _classGenerator.GenerateNamespaceClass(namespaceName, natives);
            var className = NameConverter.NamespaceToClassName(namespaceName);
            File.WriteAllText(Path.Combine(namespacesDir, $"{className}.ts"), content);
        }

        GenerateClassIndex(db, classNatives, outputPath);
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
                  // Nested mode: share parent's buffer with offset
                  this.buffer = existingBuffer;
                  this.view = new DataView(existingBuffer, offset, bufferSize);
                } else {
                  // Standalone mode: create own buffer
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

    private string GenerateRawNamespace(NativeNamespace ns)
    {
        var cb = new CodeBuilder();

        cb.AppendLine("import { Vector3 } from '../types/Vector3';");
        cb.AppendLine();

        foreach (var native in ns.Natives)
        {
            GenerateRawFunction(cb, native);
        }

        return cb.ToString();
    }

    private void GenerateRawFunction(CodeBuilder cb, NativeDefinition native)
    {
        var functionName = NameConverter.ToPascalCase(native.Name.TrimStart('_'));
        var inputParams = native.Parameters.Where(p => !p.IsOutput).ToList();

        var doc = new JsDocBuilder()
            .AddDescription(native.Description);
        foreach (var param in inputParams)
        {
            var tsType = _emitter.TypeMapper.MapType(param.Type, param.Attributes.IsNotNull);
            doc.AddParam(param.Name, tsType, param.Description);
        }
        if (!string.IsNullOrWhiteSpace(native.ReturnDescription))
        {
            var returnType = _emitter.TypeMapper.MapType(native.ReturnType);
            doc.AddReturn(returnType, native.ReturnDescription);
        }
        doc.Render(cb);

        var returnType2 = _emitter.TypeMapper.MapType(native.ReturnType);
        var paramString = BuildRawParameterString(inputParams);

        cb.AppendLine($"export function {functionName}({paramString}): {returnType2} {{");
        cb.Indent();

        // Pass ALL parameters (ArgumentBuilder handles output pointers)
        var args = ArgumentBuilder.BuildInvokeArgs(native, native.Parameters, _emitter.TypeMapper);

        var invokeType = _emitter.TypeMapper.GetInvokeReturnType(native.ReturnType);
        cb.AppendLine($"return Citizen.invokeNative<{invokeType}>({string.Join(", ", args)});");

        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    private string BuildRawParameterString(List<NativeParameter> parameters)
    {
        var parts = new List<string>();
        foreach (var param in parameters)
        {
            var tsType = _emitter.TypeMapper.MapType(param.Type, param.Attributes.IsNotNull);
            if (param.Type.Category == TypeCategory.Handle)
            {
                tsType = "number";
            }
            var optional = param.HasDefaultValue ? "?" : "";
            parts.Add($"{param.Name}{optional}: {tsType}");
        }
        return string.Join(", ", parts);
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

    private void GenerateClassIndex(NativeDatabase db, ClassifiedNatives classNatives, string outputPath)
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
            var className = NameConverter.NamespaceToClassName(nsName);
            cb.AppendLine($"export {{ {className} }} from './namespaces/{className}';");
        }

        File.WriteAllText(Path.Combine(outputPath, "index.ts"), cb.ToString());
    }
}
