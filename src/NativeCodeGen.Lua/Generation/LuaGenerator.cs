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

    public void Generate(NativeDatabase db, string outputPath, bool useClasses)
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

        if (useClasses)
        {
            GenerateClassOutput(db, outputPath);
        }
        else
        {
            GenerateNamespaceOutput(db, outputPath);
        }

        // Generate fxmanifest.lua
        GenerateFxManifest(db, outputPath, useClasses);
    }

    private void GenerateNamespaceOutput(NativeDatabase db, string outputPath)
    {
        var nativesDir = Path.Combine(outputPath, "natives");
        Directory.CreateDirectory(nativesDir);

        foreach (var ns in db.Namespaces)
        {
            var content = GenerateNamespace(ns);
            File.WriteAllText(Path.Combine(nativesDir, $"{ns.Name}.lua"), content);
        }
    }

    private void GenerateClassOutput(NativeDatabase db, string outputPath)
    {
        var classifier = new NativeClassifier();
        var classNatives = classifier.Classify(db);

        // Generate handle classes
        var classesDir = Path.Combine(outputPath, "classes");
        Directory.CreateDirectory(classesDir);
        foreach (var (className, natives) in classNatives.HandleClasses)
        {
            var baseClass = NativeClassifier.HandleClassHierarchy.GetValueOrDefault(className);
            var content = _classGenerator.GenerateHandleClass(className, baseClass, natives);
            File.WriteAllText(Path.Combine(classesDir, $"{className}.lua"), content);
        }

        // Generate namespace classes
        var namespacesDir = Path.Combine(outputPath, "namespaces");
        Directory.CreateDirectory(namespacesDir);
        foreach (var (namespaceName, natives) in classNatives.NamespaceClasses)
        {
            var content = _classGenerator.GenerateNamespaceClass(namespaceName, natives);
            var className = NameConverter.NamespaceToClassName(namespaceName);
            File.WriteAllText(Path.Combine(namespacesDir, $"{className}.lua"), content);
        }
    }

    private string GenerateNamespace(NativeNamespace ns)
    {
        var cb = new CodeBuilder();
        var moduleName = NameConverter.NamespaceToClassName(ns.Name);

        cb.AppendLine($"---@class {moduleName}");
        cb.AppendLine($"local {moduleName} = {{}}");
        cb.AppendLine();

        foreach (var native in ns.Natives)
        {
            GenerateFunction(cb, native, moduleName, ns.Name);
        }

        cb.AppendLine($"return {moduleName}");

        return cb.ToString();
    }

    private void GenerateFunction(CodeBuilder cb, NativeDefinition native, string moduleName, string namespaceName)
    {
        var functionName = NameDeduplicator.DeduplicateForNamespace(native.Name, namespaceName, NamingConvention.PascalCase);
        var inputParams = native.Parameters.Where(p => !p.IsOutput).ToList();

        // Build doc using shared DocBuilder
        var doc = new LuaDocBuilder()
            .AddDescription(native.Description);

        foreach (var param in inputParams)
        {
            var luaType = _emitter.TypeMapper.MapType(param.Type, param.Attributes.IsNotNull);
            doc.AddParam(param.Name, luaType, param.Description);
        }

        if (native.ReturnType.Category != TypeCategory.Void)
        {
            var returnType = _emitter.TypeMapper.GetInvokeReturnType(native.ReturnType);
            doc.AddReturn(returnType, native.ReturnDescription);
        }

        doc.Render(cb);

        // Function signature (only input params)
        var paramNames = string.Join(", ", inputParams.Select(p => p.Name));
        cb.AppendLine($"function {moduleName}.{functionName}({paramNames})");
        cb.Indent();

        // Function body - pass ALL parameters (ArgumentBuilder handles output pointers)
        var args = ArgumentBuilder.BuildInvokeArgs(native, native.Parameters, _emitter.TypeMapper, "{0}");

        if (native.ReturnType.Category == TypeCategory.Void)
        {
            cb.AppendLine($"Citizen.InvokeNative({string.Join(", ", args)})");
        }
        else
        {
            cb.AppendLine($"return Citizen.InvokeNative({string.Join(", ", args)})");
        }

        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    private void GenerateFxManifest(NativeDatabase db, string outputPath, bool useClasses)
    {
        var cb = new CodeBuilder();

        cb.AppendLine("-- Auto-generated fxmanifest for RDR3 native wrappers");
        cb.AppendLine("-- For struct support, you must include a DataView implementation:");
        cb.AppendLine("-- https://github.com/femga/rdr3_discoveries/blob/master/AI/EVENTS/dataview_by_Gottfriedleibniz.lua");
        cb.AppendLine();
        cb.AppendLine("fx_version 'cerulean'");
        cb.AppendLine("game 'rdr3'");
        cb.AppendLine();
        cb.AppendLine("name 'RDR3 Native Wrappers'");
        cb.AppendLine("description 'Auto-generated Lua wrappers for RDR3 natives'");
        cb.AppendLine("version '1.0.0'");
        cb.AppendLine();

        // Collect all files
        var files = new List<string>();

        foreach (var enumName in db.Enums.Keys.OrderBy(k => k))
        {
            files.Add($"enums/{enumName}.lua");
        }

        foreach (var structName in db.Structs.Keys.OrderBy(k => k))
        {
            files.Add($"structs/{structName}.lua");
        }

        if (useClasses)
        {
            var classifier = new NativeClassifier();
            var classNatives = classifier.Classify(db);

            foreach (var className in classNatives.HandleClasses.Keys.OrderBy(k => k))
            {
                files.Add($"classes/{className}.lua");
            }

            foreach (var nsName in classNatives.NamespaceClasses.Keys.OrderBy(k => k))
            {
                var className = NameConverter.NamespaceToClassName(nsName);
                files.Add($"namespaces/{className}.lua");
            }
        }
        else
        {
            foreach (var ns in db.Namespaces.OrderBy(n => n.Name))
            {
                files.Add($"natives/{ns.Name}.lua");
            }
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
}
