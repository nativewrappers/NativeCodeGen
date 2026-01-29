using NativeCodeGen.Core.Export;
using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;
using NativeCodeGen.Core.Utilities;

namespace NativeCodeGen.Lua.Generation;

public class LuaGenerator : ICodeGenerator
{
    private readonly LuaEmitter _emitter = new();
    private readonly SharedClassGenerator _classGenerator;
    private readonly SharedStructGenerator _structGenerator;
    private readonly SharedEnumGenerator _enumGenerator;

    public IReadOnlyList<string> Warnings => _structGenerator.Warnings;

    public LuaGenerator()
    {
        _classGenerator = new SharedClassGenerator(_emitter);
        _structGenerator = new SharedStructGenerator(_emitter);
        _enumGenerator = new SharedEnumGenerator(_emitter);
    }

    public void Generate(NativeDatabase db, string outputPath, GeneratorOptions options)
    {
        Directory.CreateDirectory(outputPath);

        // Generate enums
        var enumsDir = Path.Combine(outputPath, "enums");
        Directory.CreateDirectory(enumsDir);
        foreach (var enumDef in db.Enums.Values)
        {
            _enumGenerator.GenerateFile(enumDef, enumsDir);
        }

        // Generate structs
        var structsDir = Path.Combine(outputPath, "structs");
        Directory.CreateDirectory(structsDir);
        _structGenerator.SetStructRegistry(db.Structs);
        foreach (var structDef in db.Structs.Values)
        {
            _structGenerator.GenerateFile(structDef, structsDir);
        }

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
            GenerateNamespaceOutput(db, outputPath);
        }

        GenerateFxManifest(db, outputPath, options);
    }

    private void GenerateNamespaceOutput(NativeDatabase db, string outputPath)
    {
        var nativesDir = Path.Combine(outputPath, "natives");
        Directory.CreateDirectory(nativesDir);

        var builder = new RawNativeBuilder(Language.Lua, _emitter.TypeMapper);

        foreach (var ns in db.Namespaces)
        {
            builder.Clear();
            builder.EmitImports();

            var moduleName = ToPascalCase(ns.Name.ToLowerInvariant());
            builder.EmitModuleHeader(moduleName);

            foreach (var native in ns.Natives)
            {
                builder.EmitFunction(native, BindingStyle.Module, moduleName);
            }

            builder.EmitModuleFooter(moduleName);

            File.WriteAllText(Path.Combine(nativesDir, $"{ns.Name}.lua"), builder.ToString());
        }
    }

    private void GenerateSingleFileOutput(NativeDatabase db, string outputPath)
    {
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

        var builder = new RawNativeBuilder(Language.Lua, _emitter.TypeMapper);
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

        File.WriteAllText(Path.Combine(outputPath, "natives.lua"), builder.ToString());
    }

    private void GenerateClassOutput(NativeDatabase db, string outputPath)
    {
        var classifier = new NativeClassifier();
        var classNatives = classifier.Classify(db);
        var handleClassNames = classNatives.HandleClasses.Keys.ToHashSet();

        var classesDir = Path.Combine(outputPath, "classes");
        Directory.CreateDirectory(classesDir);
        foreach (var (className, natives) in classNatives.HandleClasses)
        {
            var baseClass = NativeClassifier.HandleClassHierarchy.GetValueOrDefault(className);
            var content = _classGenerator.GenerateHandleClass(className, baseClass, natives);
            File.WriteAllText(Path.Combine(classesDir, $"{className}.lua"), content);
        }

        var namespacesDir = Path.Combine(outputPath, "namespaces");
        Directory.CreateDirectory(namespacesDir);
        foreach (var (namespaceName, natives) in classNatives.NamespaceClasses)
        {
            var className = NameConverter.NamespaceToClassName(namespaceName, handleClassNames);
            var content = _classGenerator.GenerateNamespaceClass(namespaceName, natives, handleClassNames);
            File.WriteAllText(Path.Combine(namespacesDir, $"{className}.lua"), content);
        }
    }

    private void GenerateFxManifest(NativeDatabase db, string outputPath, GeneratorOptions options)
    {
        var cb = new CodeBuilder();

        cb.AppendLine("-- Auto-generated fxmanifest for RDR3 native wrappers");
        cb.AppendLine("-- For struct support, include a DataView implementation:");
        cb.AppendLine("-- https://github.com/femga/rdr3_discoveries/blob/master/AI/EVENTS/dataview_by_Gottfriedleibniz.lua");
        cb.AppendLine();
        cb.AppendLine("fx_version 'cerulean'");
        cb.AppendLine("game 'rdr3'");
        cb.AppendLine();
        cb.AppendLine("name 'RDR3 Native Wrappers'");
        cb.AppendLine("description 'Auto-generated Lua wrappers for RDR3 natives'");
        cb.AppendLine("version '1.0.0'");
        cb.AppendLine();

        var files = new List<string>();

        foreach (var enumName in db.Enums.Keys.OrderBy(k => k))
            files.Add($"enums/{enumName}.lua");

        foreach (var structName in db.Structs.Keys.OrderBy(k => k))
            files.Add($"structs/{structName}.lua");

        if (options.UseClasses)
        {
            var classifier = new NativeClassifier();
            var classNatives = classifier.Classify(db);

            foreach (var className in classNatives.HandleClasses.Keys.OrderBy(k => k))
                files.Add($"classes/{className}.lua");

            foreach (var nsName in classNatives.NamespaceClasses.Keys.OrderBy(k => k))
            {
                var className = NameConverter.NamespaceToClassName(nsName);
                files.Add($"namespaces/{className}.lua");
            }
        }
        else if (options.SingleFile)
        {
            files.Add("natives.lua");
        }
        else
        {
            foreach (var ns in db.Namespaces.OrderBy(n => n.Name))
                files.Add($"natives/{ns.Name}.lua");
        }

        cb.AppendLine("files {");
        cb.Indent();
        for (int i = 0; i < files.Count; i++)
        {
            var comma = i < files.Count - 1 ? "," : "";
            cb.AppendLine($"'{files[i]}'{comma}");
        }
        cb.Dedent();
        cb.AppendLine("}");

        File.WriteAllText(Path.Combine(outputPath, "fxmanifest.lua"), cb.ToString());
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
