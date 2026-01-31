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

        if (options.Package)
        {
            GeneratePackageOutput(db, outputPath, options);
        }
        else if (options.UseClasses)
        {
            GenerateClassOutput(db, outputPath);
        }
        else if (options.SingleFile)
        {
            GenerateSingleFileOutput(db, outputPath, options.UseExports);
        }
        else
        {
            GenerateRawOutput(db, outputPath);
        }
    }

    private void GeneratePackageOutput(NativeDatabase db, string outputPath, GeneratorOptions options)
    {
        var isRawMode = !options.UseClasses;

        // Generate source files in src/ subdirectory
        var srcDir = Path.Combine(outputPath, "src");
        Directory.CreateDirectory(srcDir);

        // Generate the appropriate output style in src/
        if (isRawMode)
        {
            // Raw single-file mode with exports
            GenerateCommonFiles(db, srcDir);
            GenerateSingleFileOutput(db, srcDir, useExports: true);
        }
        else
        {
            // Class-based mode
            GenerateClassOutput(db, srcDir);
        }

        // Generate package.json - different structure based on mode
        var packageJson = isRawMode
            ? GenerateRawPackageJson(options)
            : GenerateClassPackageJson(options);
        File.WriteAllText(Path.Combine(outputPath, "package.json"), packageJson);

        // Generate tsconfig.json
        var tsconfig = """
            {
              "compilerOptions": {
                "target": "ES2020",
                "module": "ESNext",
                "moduleResolution": "bundler",
                "declaration": true,
                "declarationMap": true,
                "outDir": "./dist",
                "rootDir": "./src",
                "strict": true,
                "skipLibCheck": true,
                "esModuleInterop": true
              },
              "include": ["src/**/*"],
              "exclude": ["node_modules", "dist", "plugin"]
            }
            """;
        File.WriteAllText(Path.Combine(outputPath, "tsconfig.json"), tsconfig);

        // Only include esbuild plugin for raw mode (tree-shaking)
        if (isRawMode)
        {
            var pluginDir = Path.Combine(outputPath, "plugin");
            Directory.CreateDirectory(pluginDir);

            var assembly = typeof(TypeScriptGenerator).Assembly;
            using var stream = assembly.GetManifestResourceStream("NativeCodeGen.TypeScript.Resources.native-treeshake.js");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                File.WriteAllText(Path.Combine(pluginDir, "native-treeshake.js"), reader.ReadToEnd());
            }
        }

        // Generate README
        var readme = isRawMode
            ? GenerateRawReadme(options)
            : GenerateClassReadme(options);
        File.WriteAllText(Path.Combine(outputPath, "README.md"), readme);
    }

    private static string GenerateRawPackageJson(GeneratorOptions options)
    {
        return $$"""
            {
              "name": "{{options.PackageName ?? "@natives/generated"}}",
              "version": "{{options.PackageVersion ?? "0.0.1"}}",
              "description": "Auto-generated native function bindings with tree-shaking support",
              "type": "module",
              "main": "./dist/index.js",
              "types": "./dist/index.d.ts",
              "exports": {
                ".": {
                  "import": "./dist/index.js",
                  "types": "./dist/index.d.ts"
                },
                "./natives": {
                  "import": "./dist/natives.js",
                  "types": "./dist/natives.d.ts"
                },
                "./plugin": {
                  "import": "./plugin/native-treeshake.js"
                }
              },
              "files": [
                "dist",
                "plugin",
                "src"
              ],
              "scripts": {
                "build": "tsc",
                "prepublishOnly": "npm run build"
              },
              "devDependencies": {
                "typescript": "^5.0.0"
              },
              "peerDependencies": {
                "esbuild": ">=0.17.0"
              },
              "peerDependenciesMeta": {
                "esbuild": {
                  "optional": true
                }
              },
              "keywords": [
                "natives",
                "fivem",
                "redm",
                "rdr3",
                "gta5",
                "esbuild",
                "tree-shaking"
              ],
              "license": "MIT"
            }
            """;
    }

    private static string GenerateClassPackageJson(GeneratorOptions options)
    {
        return $$"""
            {
              "name": "{{options.PackageName ?? "@natives/generated"}}",
              "version": "{{options.PackageVersion ?? "0.0.1"}}",
              "description": "Auto-generated native function bindings with class-based API",
              "type": "module",
              "main": "./dist/index.js",
              "types": "./dist/index.d.ts",
              "exports": {
                ".": {
                  "import": "./dist/index.js",
                  "types": "./dist/index.d.ts"
                }
              },
              "files": [
                "dist",
                "src"
              ],
              "scripts": {
                "build": "tsc",
                "prepublishOnly": "npm run build"
              },
              "peerDependencies": {
                "@nativewrappers/common": ">=0.0.1"
              },
              "devDependencies": {
                "@nativewrappers/common": ">=0.0.1",
                "typescript": "^5.0.0"
              },
              "keywords": [
                "natives",
                "fivem",
                "redm",
                "rdr3",
                "gta5"
              ],
              "license": "MIT"
            }
            """;
    }

    private static string GenerateRawReadme(GeneratorOptions options)
    {
        return $$"""
            # {{options.PackageName ?? "@natives/generated"}}

            Auto-generated native function bindings with tree-shaking support.

            ## Installation

            ```bash
            npm install {{options.PackageName ?? "@natives/generated"}}
            ```

            ## Usage

            ### Direct imports (recommended for tree-shaking)

            ```typescript
            import { GetEntityCoords, CreatePed } from '{{options.PackageName ?? "@natives/generated"}}';

            const coords = GetEntityCoords(entity, true);
            ```

            ### With esbuild plugin (auto-import)

            ```javascript
            import { nativeTreeshake } from '{{options.PackageName ?? "@natives/generated"}}/plugin';
            import esbuild from 'esbuild';

            esbuild.build({
              entryPoints: ['src/index.ts'],
              bundle: true,
              plugins: [nativeTreeshake({
                natives: './node_modules/{{options.PackageName ?? "@natives/generated"}}/dist/natives.js'
              })]
            });
            ```

            Then use natives directly without imports:

            ```typescript
            // No import needed - plugin handles it
            const coords = GetEntityCoords(entity, true);
            const ped = CreatePed(model, x, y, z, heading, false, false);
            ```

            ## Tree-shaking

            Both approaches support tree-shaking - only the natives you actually use will be included in your bundle.
            """;
    }

    private static string GenerateClassReadme(GeneratorOptions options)
    {
        return $$"""
            # {{options.PackageName ?? "@natives/generated"}}

            Auto-generated native function bindings with class-based API.

            ## Installation

            ```bash
            npm install {{options.PackageName ?? "@natives/generated"}}
            ```

            ## Usage

            ```typescript
            import { Entity, Ped, Vehicle, World } from '{{options.PackageName ?? "@natives/generated"}}';

            // Use handle classes
            const ped = new Ped(playerPedId);
            const coords = ped.getCoords(true);
            const vehicle = ped.getCurrentVehicle();

            // Use namespace utilities
            const weather = World.getWeatherTypeTransition();
            ```

            ## API

            ### Handle Classes
            - `Entity` - Base class for all game entities
            - `Ped` - Pedestrian/character entities
            - `Vehicle` - Vehicle entities
            - `Object` - Prop/object entities
            - `Player` - Player-specific functions
            - `Blip` - Map blip functions
            - And more...

            ### Namespace Utilities
            Static classes for natives that don't operate on handles:
            - `World` - Weather, time, etc.
            - `Streaming` - Asset loading
            - `Graphics` - Drawing, particles
            - And more...
            """;
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

    private void GenerateSingleFileOutput(NativeDatabase db, string outputPath, bool useExports = false)
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

        var bindingStyle = useExports ? BindingStyle.Export : BindingStyle.Global;
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

                builder.EmitFunction(native, bindingStyle, nameOverride: finalName);
            }
        }

        File.WriteAllText(Path.Combine(outputPath, "natives.ts"), builder.ToString());
        GenerateSingleFileIndex(db, outputPath, useExports);
    }

    private void GenerateSingleFileIndex(NativeDatabase db, string outputPath, bool useExports = false)
    {
        var cb = new CodeBuilder();

        cb.AppendLine("// Types");
        cb.AppendLine("export { IHandle } from './types/IHandle';");
        cb.AppendLine("export { Vector2 } from './types/Vector2';");
        cb.AppendLine("export { Vector3 } from './types/Vector3';");
        cb.AppendLine("export { Color } from './types/Color';");
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
        if (useExports)
        {
            cb.AppendLine("export * from './natives';");
        }
        else
        {
            cb.AppendLine("import './natives';");
        }

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

        File.WriteAllText(Path.Combine(typesDir, "Vector2.ts"), """
            export class Vector2 {
              constructor(
                public x: number = 0,
                public y: number = 0
              ) {}

              static fromArray(arr: number[]): Vector2 {
                return new Vector2(arr[0], arr[1]);
              }

              toArray(): [number, number] {
                return [this.x, this.y];
              }

              add(other: Vector2): Vector2 {
                return new Vector2(this.x + other.x, this.y + other.y);
              }

              subtract(other: Vector2): Vector2 {
                return new Vector2(this.x - other.x, this.y - other.y);
              }

              multiply(scalar: number): Vector2 {
                return new Vector2(this.x * scalar, this.y * scalar);
              }

              length(): number {
                return Math.sqrt(this.x * this.x + this.y * this.y);
              }

              normalize(): Vector2 {
                const len = this.length();
                if (len === 0) return new Vector2();
                return this.multiply(1 / len);
              }

              distance(other: Vector2): number {
                return this.subtract(other).length();
              }
            }
            """);

        File.WriteAllText(Path.Combine(typesDir, "Vector3.ts"), """
            export class Vector3 {
              constructor(
                public x: number = 0,
                public y: number = 0,
                public z: number = 0
              ) {}

              static fromArray(arr: number[]): Vector3 {
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

        File.WriteAllText(Path.Combine(typesDir, "Vector4.ts"), """
            export class Vector4 {
              constructor(
                public x: number = 0,
                public y: number = 0,
                public z: number = 0,
                public w: number = 0
              ) {}

              static fromArray(arr: number[]): Vector4 {
                return new Vector4(arr[0], arr[1], arr[2], arr[3]);
              }

              toArray(): [number, number, number, number] {
                return [this.x, this.y, this.z, this.w];
              }

              add(other: Vector4): Vector4 {
                return new Vector4(this.x + other.x, this.y + other.y, this.z + other.z, this.w + other.w);
              }

              subtract(other: Vector4): Vector4 {
                return new Vector4(this.x - other.x, this.y - other.y, this.z - other.z, this.w - other.w);
              }

              multiply(scalar: number): Vector4 {
                return new Vector4(this.x * scalar, this.y * scalar, this.z * scalar, this.w * scalar);
              }

              length(): number {
                return Math.sqrt(this.x * this.x + this.y * this.y + this.z * this.z + this.w * this.w);
              }

              normalize(): Vector4 {
                const len = this.length();
                if (len === 0) return new Vector4();
                return this.multiply(1 / len);
              }

              distance(other: Vector4): number {
                return this.subtract(other).length();
              }
            }
            """);

        File.WriteAllText(Path.Combine(typesDir, "Color.ts"), """
            export class Color {
              constructor(
                public r: number = 0,
                public g: number = 0,
                public b: number = 0,
                public a: number = 255
              ) {}

              static fromRgb(r: number, g: number, b: number): Color {
                return new Color(r, g, b, 255);
              }

              static fromRgba(r: number, g: number, b: number, a: number): Color {
                return new Color(r, g, b, a);
              }

              static fromHex(hex: string): Color {
                const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})?$/i.exec(hex);
                if (!result) return new Color();
                return new Color(
                  parseInt(result[1], 16),
                  parseInt(result[2], 16),
                  parseInt(result[3], 16),
                  result[4] ? parseInt(result[4], 16) : 255
                );
              }

              toHex(): string {
                const toHex = (n: number) => n.toString(16).padStart(2, '0');
                return `#${toHex(this.r)}${toHex(this.g)}${toHex(this.b)}${this.a < 255 ? toHex(this.a) : ''}`;
              }
            }
            """);

        File.WriteAllText(Path.Combine(typesDir, "BufferedClass.ts"), GenerateBufferedClassBase());

        File.WriteAllText(Path.Combine(typesDir, "HandleRegistry.ts"), """
            type HandleConstructor<T> = { fromHandle(handle: number): T | null };

            const registry = new Map<string, HandleConstructor<unknown>>();

            export function registerHandle<T>(name: string, ctor: HandleConstructor<T>): void {
              registry.set(name, ctor);
            }

            export function createFromHandle<T>(name: string, handle: number): T | null {
              const ctor = registry.get(name) as HandleConstructor<T> | undefined;
              return ctor ? ctor.fromHandle(handle) : null;
            }
            """);

        File.WriteAllText(Path.Combine(typesDir, "NativeAliases.ts"), """
            // CFX runtime globals - these are provided by the FiveM/RedM runtime
            declare const Citizen: {
                invokeNative<T = void>(hash: string, ...args: any[]): T;
                resultAsInteger(): any;
                resultAsFloat(): any;
                resultAsString(): any;
                resultAsVector(): any;
                pointerValueInt(): any;
                pointerValueFloat(): any;
                pointerValueVector(): any;
                pointerValueIntInitialized(value: number): any;
                pointerValueFloatInitialized(value: number): any;
            };
            // GetHashKey accepts string|number since hashes can be pre-computed
            declare function GetHashKey(str: string | number): number;

            export const inv = Citizen.invokeNative;
            export const rai = Citizen.resultAsInteger;
            export const raf = Citizen.resultAsFloat;
            export const ras = Citizen.resultAsString;
            export const rav = Citizen.resultAsVector;
            export const pvi = Citizen.pointerValueInt;
            export const pvf = Citizen.pointerValueFloat;
            export const pvv = Citizen.pointerValueVector;
            export const pvii = Citizen.pointerValueIntInitialized;
            export const pvfi = Citizen.pointerValueFloatInitialized;
            export const _h = typeof GetHashKey !== 'undefined' ? GetHashKey : (s: string | number) => typeof s === 'number' ? s : 0;
            export const f = (n: number) => n + 0.00000001;

            // Non-class handle type aliases (these are just numbers at runtime)
            export type ScrHandle = number;
            export type Prompt = number;
            export type FireId = number;
            export type Blip = number;
            export type PopZone = number;
            export type PedGroup = number;
            """);
    }

    private void GenerateRawIndex(NativeDatabase db, string outputPath)
    {
        var cb = new CodeBuilder();

        cb.AppendLine("// Types");
        cb.AppendLine("export { IHandle } from './types/IHandle';");
        cb.AppendLine("export { Vector2 } from './types/Vector2';");
        cb.AppendLine("export { Vector3 } from './types/Vector3';");
        cb.AppendLine("export { Color } from './types/Color';");
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
        cb.AppendLine("export { Vector2 } from './types/Vector2';");
        cb.AppendLine("export { Vector3 } from './types/Vector3';");
        cb.AppendLine("export { Color } from './types/Color';");
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
