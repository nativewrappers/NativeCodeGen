using NativeCodeGen.Core.Export;
using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;
using NativeCodeGen.Core.Utilities;

namespace NativeCodeGen.CSharp.Generation;

public class CSharpGenerator : ICodeGenerator
{
    private readonly CSharpEmitter _emitter = new();
    private readonly SharedClassGenerator _classGenerator;
    private readonly SharedStructGenerator _structGenerator;
    private readonly SharedEnumGenerator _enumGenerator;
    private readonly NativeClassifier _classifier = new();

    public IReadOnlyList<string> Warnings => _structGenerator.Warnings;

    public CSharpGenerator()
    {
        _classGenerator = new SharedClassGenerator(_emitter);
        _structGenerator = new SharedStructGenerator(_emitter);
        _enumGenerator = new SharedEnumGenerator(_emitter);
    }

    public void Generate(NativeDatabase db, string outputPath, GeneratorOptions options)
    {
        Directory.CreateDirectory(outputPath);

        // Generate enums
        var enumsDir = Path.Combine(outputPath, "Enums");
        Directory.CreateDirectory(enumsDir);
        foreach (var enumDef in db.Enums.Values)
        {
            GenerateEnumFile(enumDef, enumsDir);
        }

        // Generate structs (with warning comments about sandboxing)
        var structsDir = Path.Combine(outputPath, "Structs");
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
        else
        {
            GenerateStaticOutput(db, outputPath);
        }
    }

    private void GenerateEnumFile(EnumDefinition enumDef, string outputDir)
    {
        var cb = new CodeBuilder();

        cb.AppendLine("namespace Natives.Enums;");
        cb.AppendLine();

        _emitter.EmitEnumStart(cb, enumDef.Name);

        foreach (var member in enumDef.Members)
        {
            _emitter.EmitEnumMember(cb, member.Name, member.Value, member.Comment);
        }

        _emitter.EmitEnumEnd(cb, enumDef.Name);

        File.WriteAllText(Path.Combine(outputDir, $"{enumDef.Name}.cs"), cb.ToString());
    }

    private void GenerateClassOutput(NativeDatabase db, string outputPath)
    {
        var classNatives = _classifier.Classify(db);
        var handleClassNames = classNatives.HandleClasses.Keys.ToHashSet();

        // Generate handle classes
        var classesDir = Path.Combine(outputPath, "Classes");
        Directory.CreateDirectory(classesDir);
        foreach (var (className, natives) in classNatives.HandleClasses)
        {
            var baseClass = NativeClassifier.HandleClassHierarchy.GetValueOrDefault(className);
            var content = GenerateHandleClass(className, baseClass, natives);
            File.WriteAllText(Path.Combine(classesDir, $"{className}.cs"), content);
        }

        // Generate namespace classes
        var namespacesDir = Path.Combine(outputPath, "Namespaces");
        Directory.CreateDirectory(namespacesDir);
        foreach (var (namespaceName, natives) in classNatives.NamespaceClasses)
        {
            var className = NameConverter.NamespaceToClassName(namespaceName, handleClassNames);
            var content = GenerateNamespaceClass(namespaceName, natives, handleClassNames);
            File.WriteAllText(Path.Combine(namespacesDir, $"{className}.cs"), content);
        }
    }

    private string GenerateHandleClass(string className, string? baseClass, List<NativeDefinition> natives)
    {
        var cb = new CodeBuilder();

        // Add namespace wrapper
        cb.AppendLine("namespace Natives.Classes;");
        cb.AppendLine();

        // Generate the class content
        var classContent = _classGenerator.GenerateHandleClass(className, baseClass, natives);
        cb.Append(classContent);

        return cb.ToString();
    }

    private string GenerateNamespaceClass(string namespaceName, List<NativeDefinition> natives, HashSet<string> handleClassNames)
    {
        var cb = new CodeBuilder();
        var className = NameConverter.NamespaceToClassName(namespaceName, handleClassNames);

        // Add namespace wrapper
        cb.AppendLine("namespace Natives.Namespaces;");
        cb.AppendLine();

        // Generate the class content
        var classContent = _classGenerator.GenerateNamespaceClass(namespaceName, natives, handleClassNames);
        cb.Append(classContent);

        return cb.ToString();
    }

    private void GenerateStaticOutput(NativeDatabase db, string outputPath)
    {
        // Generate all natives as static methods in namespace classes
        var nativesDir = Path.Combine(outputPath, "Natives");
        Directory.CreateDirectory(nativesDir);

        foreach (var ns in db.Namespaces)
        {
            var cb = new CodeBuilder();
            cb.AppendLine("using CitizenFX.Core;");
            cb.AppendLine("using CitizenFX.Core.Native;");
            cb.AppendLine();
            cb.AppendLine($"namespace Natives;");
            cb.AppendLine();

            var className = ToPascalCase(ns.Name);
            cb.AppendLine($"public static class {className}");
            cb.AppendLine("{");
            cb.Indent();

            foreach (var native in ns.Natives)
            {
                GenerateStaticMethod(cb, native);
            }

            cb.Dedent();
            cb.AppendLine("}");

            File.WriteAllText(Path.Combine(nativesDir, $"{className}.cs"), cb.ToString());
        }
    }

    private void GenerateStaticMethod(CodeBuilder cb, NativeDefinition native)
    {
        var methodName = ToPascalCase(NameConverter.NormalizeNativeName(native.Name));
        var inputParams = native.Parameters.Where(p => !p.IsPureOutput).ToList();
        var outputParams = native.Parameters.Where(p => p.IsPureOutput).ToList();

        // Emit XML documentation
        EmitXmlDoc(cb, native, inputParams, outputParams);

        // Build parameter list
        var paramList = BuildParamList(inputParams);
        var returnType = BuildReturnType(native.ReturnType, outputParams);

        cb.AppendLine($"public static {returnType} {methodName}({paramList})");
        cb.AppendLine("{");
        cb.Indent();

        // Emit native call
        EmitNativeCall(cb, native, inputParams, outputParams);

        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    private void EmitXmlDoc(CodeBuilder cb, NativeDefinition native, List<NativeParameter> inputParams, List<NativeParameter> outputParams)
    {
        cb.AppendLine("/// <summary>");
        if (!string.IsNullOrWhiteSpace(native.Description))
        {
            foreach (var line in native.Description.Split('\n').Take(5))
            {
                cb.AppendLine($"/// {EscapeXml(line.Trim())}");
            }
        }
        cb.AppendLine("/// </summary>");

        foreach (var param in inputParams)
        {
            var desc = !string.IsNullOrWhiteSpace(param.Description) ? EscapeXml(param.Description) : "";
            cb.AppendLine($"/// <param name=\"{param.Name}\">{desc}</param>");
        }

        if (native.ReturnType.Category != TypeCategory.Void || outputParams.Count > 0)
        {
            var returnDesc = !string.IsNullOrWhiteSpace(native.ReturnDescription)
                ? EscapeXml(native.ReturnDescription)
                : "";
            cb.AppendLine($"/// <returns>{returnDesc}</returns>");
        }
    }

    private string BuildParamList(List<NativeParameter> inputParams)
    {
        return string.Join(", ", inputParams.Select(p =>
        {
            var type = _emitter.TypeMapper.MapType(p.Type, p.IsNullable);
            if (p.HasDefaultValue)
            {
                var defaultVal = _emitter.MapDefaultValue(p.DefaultValue!, p.Type);
                return $"{type} {p.Name} = {defaultVal}";
            }
            return $"{type} {p.Name}";
        }));
    }

    private string BuildReturnType(TypeInfo returnType, List<NativeParameter> outputParams)
    {
        return _emitter.TypeMapper.BuildCombinedReturnType(returnType, outputParams.Select(p => p.Type));
    }

    private void EmitNativeCall(CodeBuilder cb, NativeDefinition native, List<NativeParameter> inputParams, List<NativeParameter> outputParams)
    {
        var hasOutputParams = outputParams.Count > 0;

        if (!hasOutputParams)
        {
            EmitSimpleCall(cb, native, inputParams);
            return;
        }

        // Create OutputArgument instances
        for (int i = 0; i < outputParams.Count; i++)
        {
            cb.AppendLine($"var outArg{i} = new OutputArgument();");
        }

        // Build argument list
        var args = new List<string>();
        int outIndex = 0;

        foreach (var param in native.Parameters)
        {
            if (param.IsPureOutput)
            {
                args.Add($"outArg{outIndex++}");
            }
            else
            {
                args.Add(GetArgumentExpression(param));
            }
        }

        var argsStr = string.Join(", ", args);
        var hasNativeReturn = native.ReturnType.Category != TypeCategory.Void;

        if (hasNativeReturn)
        {
            var nativeReturnType = ((CSharpTypeMapper)_emitter.TypeMapper).GetInvokeReturnType(native.ReturnType);
            cb.AppendLine($"var result = Function.Call<{nativeReturnType}>((Hash){native.Hash}, {argsStr});");
        }
        else
        {
            cb.AppendLine($"Function.Call((Hash){native.Hash}, {argsStr});");
        }

        // Build return
        var returnParts = new List<string>();

        if (hasNativeReturn)
        {
            if (native.ReturnType.Category == TypeCategory.Handle && TypeInfo.IsClassHandle(native.ReturnType.Name))
            {
                returnParts.Add("result == 0 ? null : result");
            }
            else
            {
                returnParts.Add("result");
            }
        }

        for (int i = 0; i < outputParams.Count; i++)
        {
            var outType = outputParams[i].Type;
            var getType = GetOutputResultType(outType);
            returnParts.Add($"outArg{i}.GetResult<{getType}>()");
        }

        if (returnParts.Count == 1)
        {
            cb.AppendLine($"return {returnParts[0]};");
        }
        else
        {
            cb.AppendLine($"return ({string.Join(", ", returnParts)});");
        }
    }

    private void EmitSimpleCall(CodeBuilder cb, NativeDefinition native, List<NativeParameter> inputParams)
    {
        var args = inputParams.Select(GetArgumentExpression).ToList();
        var argsStr = args.Count > 0 ? string.Join(", ", args) : "";
        var fullArgs = argsStr.Length > 0 ? $", {argsStr}" : "";

        if (native.ReturnType.Category == TypeCategory.Void)
        {
            cb.AppendLine($"Function.Call((Hash){native.Hash}{fullArgs});");
            return;
        }

        var returnType = ((CSharpTypeMapper)_emitter.TypeMapper).GetInvokeReturnType(native.ReturnType);
        cb.AppendLine($"return Function.Call<{returnType}>((Hash){native.Hash}{fullArgs});");
    }

    private string GetArgumentExpression(NativeParameter param)
    {
        // Handle types - get the handle value
        if (param.Type.Category == TypeCategory.Handle && TypeInfo.IsClassHandle(param.Type.Name))
        {
            return $"{param.Name}?.Handle ?? 0";
        }

        // Vector types - pass directly (CitizenFX handles this)
        if (param.Type.IsVector)
        {
            return param.Name;
        }

        // Default
        return param.Name;
    }

    private string GetOutputResultType(TypeInfo type)
    {
        if (type.IsVector3) return "Vector3";
        if (type.IsFloat) return "float";
        if (type.IsBool) return "bool";
        return "int";
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

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
