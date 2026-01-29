using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.TypeSystem;

namespace NativeCodeGen.TypeScript;

/// <summary>
/// TypeScript-specific code emitter.
/// </summary>
public class TypeScriptEmitter : ILanguageEmitter
{
    private readonly TypeMapper _typeMapper = new();

    public ITypeMapper TypeMapper => _typeMapper;
    public string FileExtension => ".ts";
    public string SelfReference => "this";

    public DocBuilder CreateDocBuilder() => new JsDocBuilder();

    // === Enum Generation ===

    public void EmitEnumStart(CodeBuilder cb, string enumName)
    {
        cb.AppendLine($"export enum {enumName} {{");
        cb.Indent();
    }

    public void EmitEnumMember(CodeBuilder cb, string memberName, string? value, string? comment)
    {
        var line = memberName;
        if (value != null)
        {
            line += $" = {value}";
        }
        line += ",";

        if (!string.IsNullOrWhiteSpace(comment))
        {
            line += $" // {comment}";
        }

        cb.AppendLine(line);
    }

    public void EmitEnumEnd(CodeBuilder cb, string enumName)
    {
        cb.Dedent();
        cb.AppendLine("}");
    }

    // === Class Generation ===

    public void EmitClassStart(CodeBuilder cb, string className, string? baseClass, ClassKind kind)
    {
        switch (kind)
        {
            case ClassKind.Handle:
                if (baseClass != null)
                {
                    cb.AppendLine("import { Vector3 } from '@nativewrappers/common';");
                    cb.AppendLine($"import {{ {baseClass} }} from './{baseClass}';");
                    cb.AppendLine();
                    cb.AppendLine($"export class {className} extends {baseClass} {{");
                }
                else
                {
                    cb.AppendLine("import { IHandle, Vector3 } from '@nativewrappers/common';");
                    cb.AppendLine();
                    cb.AppendLine($"export class {className} implements IHandle {{");
                }
                cb.Indent();
                break;

            case ClassKind.Task:
                var entityType = NativeClassifier.GetTaskEntityType(className);
                cb.AppendLine("import { Vector3 } from '@nativewrappers/common';");
                if (baseClass != null)
                {
                    cb.AppendLine($"import {{ {baseClass} }} from './{baseClass}';");
                }
                cb.AppendLine($"import {{ {entityType} }} from './{entityType}';");
                cb.AppendLine();
                if (baseClass != null)
                {
                    cb.AppendLine($"export class {className} extends {baseClass} {{");
                }
                else
                {
                    cb.AppendLine($"export class {className} {{");
                }
                cb.Indent();
                break;

            case ClassKind.Model:
                cb.AppendLine("import { Vector3 } from '@nativewrappers/common';");
                if (baseClass != null)
                {
                    cb.AppendLine($"import {{ {baseClass} }} from './{baseClass}';");
                }
                cb.AppendLine();
                if (baseClass != null)
                {
                    cb.AppendLine($"export class {className} extends {baseClass} {{");
                }
                else
                {
                    cb.AppendLine($"export class {className} {{");
                }
                cb.Indent();
                break;

            case ClassKind.Namespace:
                cb.AppendLine("import { Vector3 } from '@nativewrappers/common';");
                cb.AppendLine();
                cb.AppendLine($"export class {className} {{");
                cb.Indent();
                break;
        }
    }

    public void EmitClassEnd(CodeBuilder cb, string className)
    {
        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitHandleConstructor(CodeBuilder cb, string className, string? baseClass)
    {
        if (baseClass == null)
        {
            cb.AppendLine("constructor(public handle: number) {}");
            cb.AppendLine();
        }
    }

    public void EmitFromHandleMethod(CodeBuilder cb, string className)
    {
        cb.AppendLine($"static fromHandle(handle: number): {className} | null {{");
        cb.Indent();
        cb.AppendLine($"return handle === 0 ? null : new {className}(handle);");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitTaskConstructor(CodeBuilder cb, string className, string entityType)
    {
        cb.AppendLine($"protected entity: {entityType};");
        cb.AppendLine();
        cb.AppendLine($"constructor(entity: {entityType}) {{");
        cb.Indent();
        cb.AppendLine("this.entity = entity;");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitModelConstructor(CodeBuilder cb, string className)
    {
        cb.AppendLine("protected hash: number;");
        cb.AppendLine();
        cb.AppendLine("constructor(hash: number) {");
        cb.Indent();
        cb.AppendLine("this.hash = hash;");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    // === Method Generation ===

    public void EmitMethodStart(CodeBuilder cb, string className, string methodName, List<MethodParameter> parameters, string returnType, MethodKind kind)
    {
        var paramString = string.Join(", ", parameters.Select(p =>
            $"{p.Name}{(p.IsOptional ? "?" : "")}: {p.Type}"));

        var staticKeyword = kind == MethodKind.Static ? "static " : "";
        cb.AppendLine($"{staticKeyword}{methodName}({paramString}): {returnType} {{");
        cb.Indent();
    }

    public void EmitMethodEnd(CodeBuilder cb)
    {
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitInvokeNative(CodeBuilder cb, string hash, List<string> args, TypeInfo returnType)
    {
        var allArgs = new List<string> { $"'{hash}'" };
        allArgs.AddRange(args);

        if (_typeMapper.NeedsResultMarker(returnType))
        {
            allArgs.Add(_typeMapper.GetResultMarker(returnType));
        }

        var invokeType = _typeMapper.GetInvokeReturnType(returnType);
        var invokeExpr = $"Citizen.invokeNative<{invokeType}>({string.Join(", ", allArgs)})";

        if (_typeMapper.IsVector3(returnType))
        {
            invokeExpr = $"Vector3.fromArray({invokeExpr})";
        }
        else if (_typeMapper.IsHandleType(returnType))
        {
            var handleClass = NativeClassifier.NormalizeHandleType(returnType.Name);
            invokeExpr = $"{handleClass}.fromHandle({invokeExpr})";
        }

        if (returnType.Category == TypeCategory.Void)
        {
            cb.AppendLine($"{invokeExpr};");
        }
        else
        {
            cb.AppendLine($"return {invokeExpr};");
        }
    }

    // === Struct Generation ===

    public void EmitStructStart(CodeBuilder cb, string structName, int size, List<string> nestedStructImports)
    {
        cb.AppendLine($"import {{ BufferedClass }} from './BufferedClass';");
        foreach (var nested in nestedStructImports)
        {
            cb.AppendLine($"import {{ {nested} }} from './{nested}';");
        }
        cb.AppendLine();
    }

    public void EmitStructDocumentation(CodeBuilder cb, StructDefinition structDef)
    {
        if (structDef.UsedByNatives.Count > 0)
        {
            var usageLines = new List<string> { "Used by:" };
            foreach (var (name, hash) in structDef.UsedByNatives.Take(10))
            {
                usageLines.Add($"- [{name}](https://natives.avarian.dev/?native={hash}&game=rdr3)");
            }
            if (structDef.UsedByNatives.Count > 10)
            {
                usageLines.Add($"- ... and {structDef.UsedByNatives.Count - 10} more");
            }
            new JsDocBuilder()
                .AddDescription(string.Join("\n", usageLines))
                .Render(cb);
        }
    }

    public void EmitStructEnd(CodeBuilder cb, string structName)
    {
        cb.AppendLine("}");
    }

    public void EmitStructConstructor(CodeBuilder cb, string structName, int size, bool supportsNesting)
    {
        cb.AppendLine($"export class {structName} extends BufferedClass {{");
        cb.Indent();
        cb.AppendLine($"static readonly SIZE = 0x{size:X};");
        cb.AppendLine();
        cb.AppendLine($"constructor(existingBuffer?: ArrayBuffer, offset?: number) {{");
        cb.Indent();
        cb.AppendLine($"super(0x{size:X}, existingBuffer, offset);");
        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitPrimitiveGetter(CodeBuilder cb, string structName, string fieldName, int offset, TypeInfo type, string? comment)
    {
        var (tsType, getMethod, _) = _typeMapper.GetDataViewAccessor(type);
        var needsEndian = StructLayoutCalculator.NeedsEndianArgument(type);
        var endianArg = needsEndian ? ", true" : "";

        cb.AppendLine();
        if (!string.IsNullOrWhiteSpace(comment))
        {
            new JsDocBuilder().AddDescription(comment).Render(cb);
        }

        cb.AppendLine($"get {fieldName}(): {tsType} {{");
        cb.Indent();

        if (type.Name == "bool" || type.Name == "BOOL")
        {
            cb.AppendLine($"return this.view.{getMethod}({offset}) !== 0;");
        }
        else
        {
            cb.AppendLine($"return this.view.{getMethod}({offset}{endianArg});");
        }

        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitPrimitiveSetter(CodeBuilder cb, string structName, string fieldName, int offset, TypeInfo type)
    {
        var (tsType, _, setMethod) = _typeMapper.GetDataViewAccessor(type);
        var needsEndian = StructLayoutCalculator.NeedsEndianArgument(type);
        var endianArg = needsEndian ? ", true" : "";

        cb.AppendLine($"set {fieldName}(value: {tsType}) {{");
        cb.Indent();

        if (type.Name == "bool" || type.Name == "BOOL")
        {
            cb.AppendLine($"this.view.{setMethod}({offset}, value ? 1 : 0);");
        }
        else
        {
            cb.AppendLine($"this.view.{setMethod}({offset}, value{endianArg});");
        }

        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitArrayGetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type, string? comment)
    {
        var (tsType, getMethod, _) = _typeMapper.GetDataViewAccessor(type);
        var needsEndian = StructLayoutCalculator.NeedsEndianArgument(type);
        var endianArg = needsEndian ? ", true" : "";

        cb.AppendLine();
        new JsDocBuilder()
            .AddDescription(comment)
            .AddParam("index", $"Array index (0-{arraySize - 1})")
            .AddThrows("RangeError", "If index is out of bounds")
            .Render(cb);

        cb.AppendLine($"get{fieldName}(index: number): {tsType} {{");
        cb.Indent();
        cb.AppendLine($"if (index < 0 || index >= {arraySize}) throw new RangeError('Index out of bounds');");

        if (type.Name == "bool" || type.Name == "BOOL")
        {
            cb.AppendLine($"return this.view.{getMethod}({offset} + index * {elementSize}) !== 0;");
        }
        else
        {
            cb.AppendLine($"return this.view.{getMethod}({offset} + index * {elementSize}{endianArg});");
        }

        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitArraySetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type)
    {
        var (tsType, _, setMethod) = _typeMapper.GetDataViewAccessor(type);
        var needsEndian = StructLayoutCalculator.NeedsEndianArgument(type);
        var endianArg = needsEndian ? ", true" : "";

        new JsDocBuilder()
            .AddParam("index", $"Array index (0-{arraySize - 1})")
            .AddParam("value", "Value to set")
            .AddThrows("RangeError", "If index is out of bounds")
            .Render(cb);

        cb.AppendLine($"set{fieldName}(index: number, value: {tsType}): void {{");
        cb.Indent();
        cb.AppendLine($"if (index < 0 || index >= {arraySize}) throw new RangeError('Index out of bounds');");

        if (type.Name == "bool" || type.Name == "BOOL")
        {
            cb.AppendLine($"this.view.{setMethod}({offset} + index * {elementSize}, value ? 1 : 0);");
        }
        else
        {
            cb.AppendLine($"this.view.{setMethod}({offset} + index * {elementSize}, value{endianArg});");
        }

        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitNestedStructAccessor(CodeBuilder cb, string structName, string fieldName, string nestedStructName, int offset, bool isArray, int arraySize, string? comment)
    {
        cb.AppendLine();

        if (isArray)
        {
            new JsDocBuilder()
                .AddDescription(comment)
                .AddParam("index", $"Array index (0-{arraySize - 1})")
                .AddThrows("RangeError", "If index is out of bounds")
                .Render(cb);

            cb.AppendLine($"{fieldName}(index: number): {nestedStructName} {{");
            cb.Indent();
            cb.AppendLine($"if (index < 0 || index >= {arraySize}) throw new RangeError('Index out of bounds');");
            cb.AppendLine($"return new {nestedStructName}(this.buffer, {offset} + index * {nestedStructName}.SIZE);");
            cb.Dedent();
            cb.AppendLine("}");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(comment))
            {
                new JsDocBuilder().AddDescription(comment).Render(cb);
            }

            cb.AppendLine($"get {fieldName}(): {nestedStructName} {{");
            cb.Indent();
            cb.AppendLine($"return new {nestedStructName}(this.buffer, {offset});");
            cb.Dedent();
            cb.AppendLine("}");
        }
    }
}
