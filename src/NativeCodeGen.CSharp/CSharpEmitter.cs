using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.TypeSystem;
using static NativeCodeGen.Core.Generation.SpecialNatives;

namespace NativeCodeGen.CSharp;

/// <summary>
/// C#-specific code emitter for FiveM/RedM natives.
/// Uses Function.Call&lt;T&gt;() API with OutputArgument for out parameters.
/// </summary>
public class CSharpEmitter : ILanguageEmitter
{
    private readonly CSharpTypeMapper _typeMapper = new();

    // Track whether we're in a property getter/setter which needs two closing braces
    private bool _inPropertyAccessor;

    public ITypeMapper TypeMapper => _typeMapper;
    public LanguageConfig Config => LanguageConfig.CSharp;
    public string FileExtension => ".cs";
    public string SelfReference => "this";

    public string MapDefaultValue(string value, TypeInfo type) =>
        Core.Utilities.DefaultValueMapper.MapDefaultValueCSharp(value, type);

    public DocBuilder CreateDocBuilder() => new XmlDocBuilder();

    // === Enum Generation ===

    public void EmitEnumStart(CodeBuilder cb, string enumName)
    {
        cb.AppendLine($"public enum {enumName}");
        cb.AppendLine("{");
        cb.Indent();
    }

    public void EmitEnumMember(CodeBuilder cb, string memberName, string? value, string? comment)
    {
        if (!string.IsNullOrWhiteSpace(comment))
        {
            cb.AppendLine($"/// <summary>{comment}</summary>");
        }

        var line = memberName;
        if (value != null)
        {
            line += $" = {value}";
        }
        line += ",";
        cb.AppendLine(line);
    }

    public void EmitEnumEnd(CodeBuilder cb, string enumName)
    {
        cb.Dedent();
        cb.AppendLine("}");
    }

    // === Class Generation ===

    public void EmitHandleImports(CodeBuilder cb, IEnumerable<string> handleTypes)
    {
        // C# uses namespaces, imports handled at file level
    }

    public void EmitNonClassHandleImports(CodeBuilder cb, IEnumerable<string> handleTypes)
    {
        // C# uses namespaces
    }

    public void EmitTypeImports(CodeBuilder cb, IEnumerable<string> enumTypes, IEnumerable<string> structTypes)
    {
        // C# uses namespaces
    }

    public void EmitNativeAliases(CodeBuilder cb)
    {
        // C# doesn't use aliases - direct Function.Call<T>()
    }

    public void EmitClassStart(CodeBuilder cb, string className, string? baseClass, ClassKind kind)
    {
        // Emit using statements
        cb.AppendLine("using CitizenFX.Core;");
        cb.AppendLine("using CitizenFX.Core.Native;");
        cb.AppendLine();

        switch (kind)
        {
            case ClassKind.Handle:
                EmitHandleClassStart(cb, className, baseClass);
                break;

            case ClassKind.Task:
                var entityType = NativeClassifier.GetTaskEntityType(className);
                cb.AppendLine($"public class {className}{(baseClass != null ? $" : {baseClass}" : "")}");
                cb.AppendLine("{");
                cb.Indent();
                break;

            case ClassKind.Model:
                cb.AppendLine($"public class {className}{(baseClass != null ? $" : {baseClass}" : "")}");
                cb.AppendLine("{");
                cb.Indent();
                break;

            case ClassKind.Weapon:
                cb.AppendLine($"public class {className}");
                cb.AppendLine("{");
                cb.Indent();
                break;

            case ClassKind.Namespace:
                cb.AppendLine($"public static class {className}");
                cb.AppendLine("{");
                cb.Indent();
                break;
        }
    }

    private void EmitHandleClassStart(CodeBuilder cb, string className, string? baseClass)
    {
        if (baseClass == null)
        {
            cb.AppendLine("public interface IHandle");
            cb.AppendLine("{");
            cb.Indent();
            cb.AppendLine("int Handle { get; }");
            cb.Dedent();
            cb.AppendLine("}");
            cb.AppendLine();
        }

        cb.AppendLine($"public class {className}{(baseClass != null ? $" : {baseClass}" : " : IHandle")}");
        cb.AppendLine("{");
        cb.Indent();
    }

    public void EmitClassEnd(CodeBuilder cb, string className, ClassKind kind)
    {
        // Add special accessors before closing the class
        if (kind == ClassKind.Handle)
        {
            switch (className)
            {
                case "Ped":
                    foreach (var accessor in SpecialAccessors.PedAccessors)
                        EmitLazyAccessor(cb, className, accessor);
                    break;
                case "Vehicle":
                    foreach (var accessor in SpecialAccessors.VehicleAccessors)
                        EmitLazyAccessor(cb, className, accessor);
                    break;
                case "Player":
                    EmitNativeAccessor(cb, className, SpecialAccessors.PlayerServerId);
                    break;
                case "Entity":
                    EmitNativeAccessor(cb, className, SpecialAccessors.EntityNetworkId);
                    break;
            }
        }

        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitLazyAccessor(CodeBuilder cb, string className, LazyAccessor accessor)
    {
        var propertyName = accessor.FieldName.TrimStart('_');
        propertyName = char.ToUpper(propertyName[0]) + propertyName[1..];

        cb.AppendLine();
        cb.AppendLine($"private {accessor.ReturnType}? {accessor.FieldName};");
        cb.AppendLine();
        cb.AppendLine($"public {accessor.ReturnType} {propertyName}");
        cb.AppendLine("{");
        cb.Indent();
        cb.AppendLine("get");
        cb.AppendLine("{");
        cb.Indent();
        cb.AppendLine($"{accessor.FieldName} ??= new {accessor.InitExpression}(this);");
        cb.AppendLine($"return {accessor.FieldName};");
        cb.Dedent();
        cb.AppendLine("}");
        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitNativeAccessor(CodeBuilder cb, string className, NativeAccessor accessor)
    {
        cb.AppendLine();
        cb.AppendLine("/// <summary>");
        cb.AppendLine($"/// {accessor.Description}");
        cb.AppendLine("/// </summary>");
        cb.AppendLine($"public int {accessor.Name}");
        cb.AppendLine("{");
        cb.Indent();
        cb.AppendLine($"get => Function.Call<int>((Hash){accessor.Hash}, Handle);");
        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitHandleConstructor(CodeBuilder cb, string className, string? baseClass)
    {
        if (baseClass == null)
        {
            cb.AppendLine("public int Handle { get; }");
            cb.AppendLine();
            cb.AppendLine($"public {className}(int handle)");
            cb.AppendLine("{");
            cb.Indent();
            cb.AppendLine("Handle = handle;");
            cb.Dedent();
            cb.AppendLine("}");
        }
        else
        {
            cb.AppendLine($"public {className}(int handle) : base(handle) {{ }}");
        }
        cb.AppendLine();
    }

    public void EmitFromHandleMethod(CodeBuilder cb, string className)
    {
        cb.AppendLine($"public static {className}? FromHandle(int handle)");
        cb.AppendLine("{");
        cb.Indent();
        cb.AppendLine($"return handle == 0 ? null : new {className}(handle);");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitFromNetworkIdMethod(CodeBuilder cb, string className)
    {
        cb.AppendLine($"public static {className}? FromNetworkId(int netId)");
        cb.AppendLine("{");
        cb.Indent();
        cb.AppendLine($"if (!Function.Call<bool>((Hash){SpecialNatives.NetworkDoesEntityExistWithNetworkId}, netId)) return null;");
        cb.AppendLine($"return {className}.FromHandle(Function.Call<int>((Hash){SpecialNatives.NetworkGetEntityFromNetworkId}, netId));");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitTaskConstructor(CodeBuilder cb, string className, string entityType, string? baseClass)
    {
        if (baseClass == null)
        {
            cb.AppendLine($"protected {entityType} Entity {{ get; }}");
            cb.AppendLine();
        }
        cb.AppendLine($"public {className}({entityType} entity){(baseClass != null ? " : base(entity)" : "")}");
        cb.AppendLine("{");
        cb.Indent();
        if (baseClass == null)
        {
            cb.AppendLine("Entity = entity;");
        }
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitModelConstructor(CodeBuilder cb, string className, string? baseClass)
    {
        if (baseClass == null)
        {
            cb.AppendLine("protected uint Hash { get; }");
            cb.AppendLine();
        }
        cb.AppendLine($"public {className}(uint hash){(baseClass != null ? " : base(hash)" : "")}");
        cb.AppendLine("{");
        cb.Indent();
        if (baseClass == null)
        {
            cb.AppendLine("Hash = hash;");
        }
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitWeaponConstructor(CodeBuilder cb, string className)
    {
        cb.AppendLine("protected Ped Ped { get; }");
        cb.AppendLine();
        cb.AppendLine($"public {className}(Ped ped)");
        cb.AppendLine("{");
        cb.Indent();
        cb.AppendLine("Ped = ped;");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    // === Method Generation ===

    public void EmitMethodStart(CodeBuilder cb, string className, string methodName, List<MethodParameter> parameters, string returnType, MethodKind kind)
    {
        // Capitalize method name for C# convention
        var csharpMethodName = char.ToUpper(methodName[0]) + methodName[1..];

        if (kind == MethodKind.Getter)
        {
            _inPropertyAccessor = true;
            cb.AppendLine($"public {returnType} {csharpMethodName}");
            cb.AppendLine("{");
            cb.Indent();
            cb.AppendLine("get");
            cb.AppendLine("{");
            cb.Indent();
            return;
        }

        if (kind == MethodKind.Setter)
        {
            _inPropertyAccessor = true;
            var param = parameters.First();
            cb.AppendLine($"public {param.Type} {csharpMethodName}");
            cb.AppendLine("{");
            cb.Indent();
            cb.AppendLine($"set");
            cb.AppendLine("{");
            cb.Indent();
            return;
        }

        _inPropertyAccessor = false;

        if (kind == MethodKind.ChainableSetter)
        {
            var chainableParams = string.Join(", ", parameters.Select(p =>
                p.DefaultValue != null ? $"{p.Type} {p.Name} = {p.DefaultValue}" : $"{p.Type} {p.Name}"));
            cb.AppendLine($"public {className} {csharpMethodName}({chainableParams})");
            cb.AppendLine("{");
            cb.Indent();
            return;
        }

        var paramString = string.Join(", ", parameters.Select(p =>
        {
            if (p.DefaultValue != null)
            {
                return $"{p.Type} {p.Name} = {p.DefaultValue}";
            }
            return $"{p.Type} {p.Name}";
        }));

        var accessModifier = kind == MethodKind.Static ? "public static" : "public";
        cb.AppendLine($"{accessModifier} {returnType} {csharpMethodName}({paramString})");
        cb.AppendLine("{");
        cb.Indent();
    }

    public void EmitMethodEnd(CodeBuilder cb)
    {
        cb.Dedent();
        cb.AppendLine("}");

        // For property getters/setters, we need to close the property block as well
        if (_inPropertyAccessor)
        {
            cb.Dedent();
            cb.AppendLine("}");
            _inPropertyAccessor = false;
        }

        cb.AppendLine();
    }

    public void EmitGetterProxy(CodeBuilder cb, string propertyName, string methodName, string returnType)
    {
        var csharpPropertyName = char.ToUpper(propertyName[0]) + propertyName[1..];
        var csharpMethodName = char.ToUpper(methodName[0]) + methodName[1..];

        cb.AppendLine($"public {returnType} {csharpPropertyName} => {csharpMethodName}();");
        cb.AppendLine();
    }

    public void EmitSetterProxy(CodeBuilder cb, string propertyName, string methodName, string paramName, string paramType)
    {
        var csharpPropertyName = char.ToUpper(propertyName[0]) + propertyName[1..];
        var csharpMethodName = char.ToUpper(methodName[0]) + methodName[1..];

        cb.AppendLine($"public {paramType} {csharpPropertyName}");
        cb.AppendLine("{");
        cb.Indent();
        cb.AppendLine($"set => {csharpMethodName}(value);");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitInvokeNative(CodeBuilder cb, string hash, List<string> args, TypeInfo returnType, List<TypeInfo> outputParamTypes)
    {
        var hasOutputParams = outputParamTypes.Count > 0;

        if (!hasOutputParams)
        {
            EmitSimpleInvoke(cb, hash, args, returnType);
            return;
        }

        // For methods with output parameters, we need to:
        // 1. Create OutputArgument instances
        // 2. Call the native
        // 3. Extract results from OutputArgument instances

        var outArgNames = new List<string>();
        var outArgDecls = new List<string>();
        for (int i = 0; i < outputParamTypes.Count; i++)
        {
            var outArgName = $"outArg{i}";
            outArgNames.Add(outArgName);
            outArgDecls.Add($"var {outArgName} = new OutputArgument();");
        }

        // Emit OutputArgument declarations
        foreach (var decl in outArgDecls)
        {
            cb.AppendLine(decl);
        }

        // Replace placeholder arguments with actual OutputArgument names
        var modifiedArgs = new List<string>();
        int outIndex = 0;
        foreach (var arg in args)
        {
            if (arg == "new OutputArgument()")
            {
                modifiedArgs.Add(outArgNames[outIndex++]);
            }
            else
            {
                modifiedArgs.Add(arg);
            }
        }

        var invokeArgs = string.Join(", ", modifiedArgs);
        var hasNativeReturn = returnType.Category != TypeCategory.Void;

        if (hasNativeReturn)
        {
            var nativeReturnType = _typeMapper.GetInvokeReturnType(returnType);
            cb.AppendLine($"var result = Function.Call<{nativeReturnType}>((Hash){hash}, {invokeArgs});");
        }
        else
        {
            cb.AppendLine($"Function.Call((Hash){hash}, {invokeArgs});");
        }

        // Build return statement
        var returnParts = new List<string>();

        if (hasNativeReturn)
        {
            if (returnType.Category == TypeCategory.Handle && TypeInfo.IsClassHandle(returnType.Name))
            {
                var handleClass = NativeClassifier.NormalizeHandleType(returnType.Name);
                returnParts.Add($"{handleClass}.FromHandle(result)");
            }
            else
            {
                returnParts.Add("result");
            }
        }

        for (int i = 0; i < outputParamTypes.Count; i++)
        {
            var outType = outputParamTypes[i];
            var getResultType = GetOutputResultType(outType);
            var extraction = $"{outArgNames[i]}.GetResult<{getResultType}>()";

            if (outType.Category == TypeCategory.Handle && TypeInfo.IsClassHandle(outType.Name))
            {
                var handleClass = NativeClassifier.NormalizeHandleType(outType.Name);
                extraction = $"{handleClass}.FromHandle({extraction})";
            }

            returnParts.Add(extraction);
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

    private void EmitSimpleInvoke(CodeBuilder cb, string hash, List<string> args, TypeInfo returnType)
    {
        var invokeArgs = args.Count > 0 ? string.Join(", ", args) : "";
        var fullArgs = invokeArgs.Length > 0 ? $", {invokeArgs}" : "";

        if (returnType.Category == TypeCategory.Void)
        {
            cb.AppendLine($"Function.Call((Hash){hash}{fullArgs});");
            return;
        }

        var invokeReturnType = _typeMapper.GetInvokeReturnType(returnType);

        if (returnType.Category == TypeCategory.Handle && TypeInfo.IsClassHandle(returnType.Name))
        {
            var handleClass = NativeClassifier.NormalizeHandleType(returnType.Name);
            cb.AppendLine($"return {handleClass}.FromHandle(Function.Call<int>((Hash){hash}{fullArgs}));");
        }
        else
        {
            cb.AppendLine($"return Function.Call<{invokeReturnType}>((Hash){hash}{fullArgs});");
        }
    }

    private string GetOutputResultType(TypeInfo type)
    {
        if (type.IsVector3) return "Vector3";
        if (type.IsFloat) return "float";
        if (type.IsBool) return "bool";
        if (type.Category == TypeCategory.Handle) return "int";
        return "int";
    }

    // === Struct Generation ===
    // Note: Structs are generated but marked as non-functional due to sandboxing

    public void EmitStructStart(CodeBuilder cb, string structName, int size, List<string> nestedStructImports)
    {
        cb.AppendLine("using CitizenFX.Core;");
        cb.AppendLine("using CitizenFX.Core.Native;");
        cb.AppendLine("using System.Runtime.InteropServices;");
        cb.AppendLine();
        cb.AppendLine("namespace Natives.Structs;");
        cb.AppendLine();
        cb.AppendLine("// WARNING: This struct cannot be used with native functions due to FiveM/RedM sandboxing.");
        cb.AppendLine("// Unsafe memory operations are not permitted in the CLR runtime environment.");
        cb.AppendLine("// This definition is provided for reference only.");
        cb.AppendLine();
    }

    public void EmitStructDocumentation(CodeBuilder cb, StructDefinition structDef)
    {
        if (structDef.UsedByNatives.Count > 0)
        {
            cb.AppendLine("/// <summary>");
            cb.AppendLine("/// Used by natives:");
            foreach (var hash in structDef.UsedByNatives.Take(10))
            {
                cb.AppendLine($"/// - {hash}");
            }
            if (structDef.UsedByNatives.Count > 10)
            {
                cb.AppendLine($"/// - ... and {structDef.UsedByNatives.Count - 10} more");
            }
            cb.AppendLine("/// </summary>");
        }
    }

    public void EmitStructEnd(CodeBuilder cb, string structName)
    {
        cb.AppendLine("}");
    }

    public void EmitStructConstructor(CodeBuilder cb, string structName, int size, bool supportsNesting)
    {
        cb.AppendLine("[StructLayout(LayoutKind.Explicit)]");
        cb.AppendLine($"public struct {structName}");
        cb.AppendLine("{");
        cb.Indent();
        cb.AppendLine($"public const int SIZE = 0x{size:X};");
        cb.AppendLine();
    }

    public void EmitPrimitiveGetter(CodeBuilder cb, string structName, string fieldName, int offset, TypeInfo type, string? comment)
    {
        var (langType, _, _) = _typeMapper.GetDataViewAccessor(type);

        if (!string.IsNullOrWhiteSpace(comment))
        {
            cb.AppendLine($"/// <summary>{comment}</summary>");
        }

        cb.AppendLine($"[FieldOffset(0x{offset:X})]");
        cb.AppendLine($"public {langType} {fieldName};");
        cb.AppendLine();
    }

    public void EmitPrimitiveSetter(CodeBuilder cb, string structName, string fieldName, int offset, TypeInfo type)
    {
        // C# structs with FieldOffset don't need separate setters
    }

    public void EmitArrayGetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type, string? comment)
    {
        var (langType, _, _) = _typeMapper.GetDataViewAccessor(type);

        if (!string.IsNullOrWhiteSpace(comment))
        {
            cb.AppendLine($"/// <summary>{comment} (array of {arraySize})</summary>");
        }

        // For arrays, we'd need unsafe fixed buffers which aren't allowed
        cb.AppendLine($"// Array field at offset 0x{offset:X}, size {arraySize} - cannot be represented safely");
        cb.AppendLine($"// public fixed {langType} {fieldName}[{arraySize}];");
        cb.AppendLine();
    }

    public void EmitArraySetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type)
    {
        // Not needed for C# struct generation
    }

    public void EmitNestedStructAccessor(CodeBuilder cb, string structName, string fieldName, string nestedStructName, int offset, bool isArray, int arraySize, string? comment)
    {
        if (!string.IsNullOrWhiteSpace(comment))
        {
            cb.AppendLine($"/// <summary>{comment}</summary>");
        }

        if (isArray)
        {
            cb.AppendLine($"// Nested struct array at offset 0x{offset:X}, size {arraySize} - cannot be represented safely");
            cb.AppendLine($"// public fixed {nestedStructName} {fieldName}[{arraySize}];");
        }
        else
        {
            cb.AppendLine($"[FieldOffset(0x{offset:X})]");
            cb.AppendLine($"public {nestedStructName} {fieldName};");
        }
        cb.AppendLine();
    }
}
