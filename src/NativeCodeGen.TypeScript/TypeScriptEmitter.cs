using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.TypeSystem;
using static NativeCodeGen.Core.Generation.SpecialNatives;

namespace NativeCodeGen.TypeScript;

/// <summary>
/// TypeScript-specific code emitter.
/// </summary>
public class TypeScriptEmitter : ILanguageEmitter
{
    private readonly TypeMapper _typeMapper = new();

    public ITypeMapper TypeMapper => _typeMapper;
    public LanguageConfig Config => LanguageConfig.TypeScript;
    public string FileExtension => ".ts";
    public string SelfReference => "this";

    public string MapDefaultValue(string value, TypeInfo type) =>
        Core.Utilities.DefaultValueMapper.MapDefaultValue(value, type);

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

    public void EmitHandleImports(CodeBuilder cb, IEnumerable<string> handleTypes)
    {
        var types = handleTypes.OrderBy(t => t).ToList();
        if (types.Count == 0) return;

        // Note: createFromHandle is imported by EmitClassStart for handle/namespace classes
        foreach (var handleType in types)
        {
            cb.AppendLine($"import type {{ {handleType} }} from '../classes/{handleType}';");
        }
    }

    public void EmitNonClassHandleImports(CodeBuilder cb, IEnumerable<string> handleTypes)
    {
        var types = handleTypes.OrderBy(t => t).ToList();
        if (types.Count == 0) return;

        // Import non-class handles as type aliases from NativeAliases
        var typeList = string.Join(", ", types);
        cb.AppendLine($"import type {{ {typeList} }} from '../types/NativeAliases';");
    }

    public void EmitTypeImports(CodeBuilder cb, IEnumerable<string> enumTypes, IEnumerable<string> structTypes)
    {
        foreach (var enumType in enumTypes.OrderBy(t => t))
        {
            cb.AppendLine($"import type {{ {enumType} }} from '../enums/{enumType}';");
        }
        foreach (var structType in structTypes.OrderBy(t => t))
        {
            cb.AppendLine($"import {{ {structType} }} from '../structs/{structType}';");
        }
    }

    public void EmitNativeAliases(CodeBuilder cb)
    {
        cb.AppendLine();
        cb.AppendLine("// Native invoke aliases");
        RawNativeBuilder.EmitTypeScriptAliases(cb);
    }

    // Common import constants
    private const string VectorColorImports = """
        import { Vector2 } from '../types/Vector2';
        import { Vector3 } from '../types/Vector3';
        import { Vector4 } from '../types/Vector4';
        import { Color } from '../types/Color';
        """;

    private const string NativeAliasImport = "import { inv, rai, raf, ras, rav, pvi, pvf, pvv, pvii, pvfi, _h, f, int, uint, float, Hash, u8, u16, u32, u64, i8, i16, i32, i64 } from '../types/NativeAliases';";

    private static void EmitVectorColorImports(CodeBuilder cb) =>
        cb.AppendLine(VectorColorImports);

    public void EmitClassStart(CodeBuilder cb, string className, string? baseClass, ClassKind kind)
    {
        EmitVectorColorImports(cb);

        switch (kind)
        {
            case ClassKind.Handle:
                if (baseClass == null)
                    cb.AppendLine("import { IHandle } from '../types/IHandle';");
                else
                    cb.AppendLine($"import {{ {baseClass} }} from './{baseClass}';");
                cb.AppendLine("import { registerHandle, createFromHandle } from '../types/HandleRegistry';");
                cb.AppendLine(NativeAliasImport);
                if (className == "Ped")
                {
                    cb.AppendLine("import { PedTask } from './PedTask';");
                    cb.AppendLine("import { Weapon } from './Weapon';");
                }
                else if (className == "Vehicle")
                {
                    cb.AppendLine("import { VehicleTask } from './VehicleTask';");
                }
                cb.AppendLine();
                cb.AppendLine(baseClass != null
                    ? $"export class {className} extends {baseClass} {{"
                    : $"export class {className} implements IHandle {{");
                cb.Indent();
                break;

            case ClassKind.Task:
                var entityType = NativeClassifier.GetTaskEntityType(className);
                if (baseClass != null)
                    cb.AppendLine($"import {{ {baseClass} }} from './{baseClass}';");
                cb.AppendLine($"import type {{ {entityType} }} from './{entityType}';");
                cb.AppendLine("import { createFromHandle } from '../types/HandleRegistry';");
                cb.AppendLine(NativeAliasImport);
                cb.AppendLine();
                cb.AppendLine(baseClass != null
                    ? $"export class {className} extends {baseClass} {{"
                    : $"export class {className} {{");
                cb.Indent();
                break;

            case ClassKind.Model:
                if (baseClass != null)
                    cb.AppendLine($"import {{ {baseClass} }} from './{baseClass}';");
                cb.AppendLine(NativeAliasImport);
                cb.AppendLine();
                cb.AppendLine(baseClass != null
                    ? $"export class {className} extends {baseClass} {{"
                    : $"export class {className} {{");
                cb.Indent();
                break;

            case ClassKind.Weapon:
                cb.AppendLine("import type { Ped } from './Ped';");
                cb.AppendLine("import { createFromHandle } from '../types/HandleRegistry';");
                cb.AppendLine(NativeAliasImport);
                cb.AppendLine();
                cb.AppendLine($"export class {className} {{");
                cb.Indent();
                break;

            case ClassKind.Namespace:
                cb.AppendLine("import { createFromHandle } from '../types/HandleRegistry';");
                cb.AppendLine(NativeAliasImport);
                cb.AppendLine();
                cb.AppendLine($"export class {className} {{");
                cb.Indent();
                break;
        }
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

        // Register handle classes with the factory
        if (kind == ClassKind.Handle)
        {
            cb.AppendLine();
            cb.AppendLine($"registerHandle('{className}', {className});");
        }
    }

    public void EmitLazyAccessor(CodeBuilder cb, string className, LazyAccessor accessor)
    {
        var propertyName = accessor.FieldName.TrimStart('_');
        cb.AppendLine();
        cb.AppendLine($"private {accessor.FieldName}?: {accessor.ReturnType};");
        cb.AppendLine();
        cb.AppendLine($"get {propertyName}(): {accessor.ReturnType} {{");
        cb.Indent();
        cb.AppendLine($"if (!this.{accessor.FieldName}) {{");
        cb.Indent();
        cb.AppendLine($"this.{accessor.FieldName} = new {accessor.InitExpression}(this);");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine($"return this.{accessor.FieldName};");
        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitNativeAccessor(CodeBuilder cb, string className, NativeAccessor accessor)
    {
        cb.AppendLine();
        cb.AppendLine("/**");
        cb.AppendLine($" * {accessor.Description}");
        cb.AppendLine(" */");
        cb.AppendLine($"get {accessor.Name}(): {accessor.ReturnType} {{");
        cb.Indent();
        cb.AppendLine($"return inv<{accessor.ReturnType}>('{accessor.Hash}', this.handle, rai());");
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

    public void EmitFromNetworkIdMethod(CodeBuilder cb, string className)
    {
        cb.AppendLine($"static fromNetworkId(netId: number): {className} | null {{");
        cb.Indent();
        cb.AppendLine($"if (!inv<number>('{SpecialNatives.NetworkDoesEntityExistWithNetworkId}', netId, rai())) return null;");
        cb.AppendLine($"return {className}.fromHandle(inv<number>('{SpecialNatives.NetworkGetEntityFromNetworkId}', netId, rai()));");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitTaskConstructor(CodeBuilder cb, string className, string entityType, string? baseClass)
    {
        if (baseClass == null)
        {
            cb.AppendLine($"protected entity: {entityType};");
            cb.AppendLine();
        }
        cb.AppendLine($"constructor(entity: {entityType}) {{");
        cb.Indent();
        if (baseClass != null)
        {
            cb.AppendLine("super(entity);");
        }
        else
        {
            cb.AppendLine("this.entity = entity;");
        }
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitModelConstructor(CodeBuilder cb, string className, string? baseClass)
    {
        if (baseClass == null)
        {
            cb.AppendLine("protected hash: number;");
            cb.AppendLine();
        }
        cb.AppendLine("constructor(hash: number) {");
        cb.Indent();
        if (baseClass != null)
        {
            cb.AppendLine("super(hash);");
        }
        else
        {
            cb.AppendLine("this.hash = hash;");
        }
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitWeaponConstructor(CodeBuilder cb, string className)
    {
        cb.AppendLine("protected ped: Ped;");
        cb.AppendLine();
        cb.AppendLine("constructor(ped: Ped) {");
        cb.Indent();
        cb.AppendLine("this.ped = ped;");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    // === Method Generation ===

    public void EmitMethodStart(CodeBuilder cb, string className, string methodName, List<MethodParameter> parameters, string returnType, MethodKind kind)
    {
        if (kind == MethodKind.Getter)
        {
            // Emit as getter property
            cb.AppendLine($"get {methodName}(): {returnType} {{");
            cb.Indent();
            return;
        }

        if (kind == MethodKind.Setter)
        {
            // Emit as setter property - single parameter
            var param = parameters.First();
            cb.AppendLine($"set {methodName}({param.Name}: {param.Type}) {{");
            cb.Indent();
            return;
        }

        if (kind == MethodKind.ChainableSetter)
        {
            // Emit chainable method that returns this
            var chainableParams = string.Join(", ", parameters.Select(p =>
                p.DefaultValue != null ? $"{p.Name}: {p.Type} = {p.DefaultValue}" : $"{p.Name}: {p.Type}"));
            cb.AppendLine($"{methodName}({chainableParams}): this {{");
            cb.Indent();
            return;
        }

        var paramString = string.Join(", ", parameters.Select(p =>
        {
            // Rest parameters (starting with ...) need array type - use any[] since variadic can accept multiple types
            if (p.Name.StartsWith("..."))
            {
                return $"{p.Name}: any[]";
            }
            // Use default value syntax instead of optional marker
            if (p.DefaultValue != null)
            {
                return $"{p.Name}: {p.Type} = {p.DefaultValue}";
            }
            return $"{p.Name}: {p.Type}";
        }));

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

    public void EmitGetterProxy(CodeBuilder cb, string propertyName, string methodName, string returnType)
    {
        cb.AppendLine($"get {propertyName}(): {returnType} {{");
        cb.Indent();
        cb.AppendLine($"return this.{methodName}();");
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
    }

    public void EmitInvokeNative(CodeBuilder cb, string hash, List<string> args, TypeInfo returnType, List<TypeInfo> outputParamTypes)
    {
        var allArgs = new List<string> { $"'{hash}'" };
        allArgs.AddRange(args);

        if (_typeMapper.NeedsResultMarker(returnType))
        {
            allArgs.Add(_typeMapper.GetResultMarker(returnType));
        }

        var hasOutputParams = outputParamTypes.Count > 0;
        var invokeType = hasOutputParams
            ? _typeMapper.GetInvokeCombinedReturnType(returnType, outputParamTypes.Select(p => p))
            : _typeMapper.GetInvokeReturnType(returnType);

        var invokeExpr = $"inv<{invokeType}>({string.Join(", ", allArgs)})";

        // If no output params, use the simple return logic
        if (!hasOutputParams)
        {
            if (_typeMapper.IsVector3(returnType))
            {
                invokeExpr = $"Vector3.fromArray({invokeExpr})";
            }
            else if (_typeMapper.IsHandleType(returnType) && TypeInfo.IsClassHandle(returnType.Name))
            {
                var handleClass = NativeClassifier.NormalizeHandleType(returnType.Name);
                invokeExpr = $"createFromHandle<{handleClass}>('{handleClass}', {invokeExpr})";
            }
            else if (returnType.Category == TypeCategory.Hash)
            {
                invokeExpr = $"({invokeExpr}) & {HashMask}";
            }
            else if (returnType.IsBool)
            {
                // Coerce 0/1 to true/false
                invokeExpr = $"!!{invokeExpr}";
            }

            if (returnType.Category == TypeCategory.Void)
            {
                cb.AppendLine($"{invokeExpr};");
            }
            else
            {
                cb.AppendLine($"return {invokeExpr};");
            }
            return;
        }

        // With output params, we need to return a tuple
        // The invoke returns [retValue?, outParam1, outParam2, ...] when multiple values
        // Or just the single value directly when only 1 return value
        cb.AppendLine($"const result = {invokeExpr};");

        // Count total values to determine if result is array or single value
        var totalValues = (returnType.Category != TypeCategory.Void ? 1 : 0) + outputParamTypes.Count;
        var isSingleValue = totalValues == 1;

        // Helper to get the result access expression
        string GetResultAccess(int index) => isSingleValue ? "result" : $"result[{index}]";

        // Build the return tuple with proper type conversions
        var returnParts = new List<string>();
        int resultIndex = 0;

        // Add native return value if not void
        if (returnType.Category != TypeCategory.Void)
        {
            if (_typeMapper.IsVector3(returnType))
            {
                returnParts.Add($"Vector3.fromArray({GetResultAccess(resultIndex)})");
            }
            else if (_typeMapper.IsHandleType(returnType) && TypeInfo.IsClassHandle(returnType.Name))
            {
                var handleClass = NativeClassifier.NormalizeHandleType(returnType.Name);
                returnParts.Add($"createFromHandle<{handleClass}>('{handleClass}', {GetResultAccess(resultIndex)})");
            }
            else if (returnType.IsBool)
            {
                returnParts.Add($"!!{GetResultAccess(resultIndex)}");
            }
            else if (returnType.Category == TypeCategory.Hash)
            {
                returnParts.Add($"{GetResultAccess(resultIndex)} & {HashMask}");
            }
            else
            {
                returnParts.Add(GetResultAccess(resultIndex));
            }
            resultIndex++;
        }

        // Add each output param with conversion
        foreach (var outputType in outputParamTypes)
        {
            if (outputType.Category == TypeCategory.Vector3 || outputType.Name == "Vector3")
            {
                returnParts.Add($"Vector3.fromArray({GetResultAccess(resultIndex)})");
            }
            else if (outputType.Category == TypeCategory.Handle && TypeInfo.IsClassHandle(outputType.Name))
            {
                var handleClass = NativeClassifier.NormalizeHandleType(outputType.Name);
                returnParts.Add($"createFromHandle<{handleClass}>('{handleClass}', {GetResultAccess(resultIndex)})");
            }
            else if (outputType.IsBool)
            {
                returnParts.Add($"!!{GetResultAccess(resultIndex)}");
            }
            else
            {
                returnParts.Add(GetResultAccess(resultIndex));
            }
            resultIndex++;
        }

        // Single output with void return - return directly, not as tuple
        if (returnParts.Count == 1)
        {
            cb.AppendLine($"return {returnParts[0]};");
        }
        else
        {
            cb.AppendLine($"return [{string.Join(", ", returnParts)}];");
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
            foreach (var hash in structDef.UsedByNatives.Take(10))
            {
                usageLines.Add($"- [{hash}](https://natives.avarian.dev/?native={hash}&game=rdr3)");
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
        var info = _typeMapper.GetDataViewAccessorInfo(type);

        cb.AppendLine();
        if (!string.IsNullOrWhiteSpace(comment))
        {
            new JsDocBuilder().AddDescription(comment).Render(cb);
        }

        cb.AppendLine($"get {fieldName}(): {info.LanguageType} {{");
        cb.Indent();
        cb.AppendLine(info.IsBool
            ? $"return this.view.{info.GetMethod}({offset}) !== 0;"
            : $"return this.view.{info.GetMethod}({offset}{info.EndianArg});");
        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitPrimitiveSetter(CodeBuilder cb, string structName, string fieldName, int offset, TypeInfo type)
    {
        var info = _typeMapper.GetDataViewAccessorInfo(type);

        cb.AppendLine($"set {fieldName}(value: {info.LanguageType}) {{");
        cb.Indent();
        cb.AppendLine(info.IsBool
            ? $"this.view.{info.SetMethod}({offset}, value ? 1 : 0);"
            : $"this.view.{info.SetMethod}({offset}, value{info.EndianArg});");
        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitArrayGetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type, string? comment)
    {
        var info = _typeMapper.GetDataViewAccessorInfo(type);

        cb.AppendLine();
        new JsDocBuilder()
            .AddDescription(comment)
            .AddParam("index", $"Array index (0-{arraySize - 1})")
            .AddThrows("RangeError", "If index is out of bounds")
            .Render(cb);

        cb.AppendLine($"get{fieldName}(index: number): {info.LanguageType} {{");
        cb.Indent();
        cb.AppendLine($"if (index < 0 || index >= {arraySize}) throw new RangeError('Index out of bounds');");
        cb.AppendLine(info.IsBool
            ? $"return this.view.{info.GetMethod}({offset} + index * {elementSize}) !== 0;"
            : $"return this.view.{info.GetMethod}({offset} + index * {elementSize}{info.EndianArg});");
        cb.Dedent();
        cb.AppendLine("}");
    }

    public void EmitArraySetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type)
    {
        var info = _typeMapper.GetDataViewAccessorInfo(type);

        new JsDocBuilder()
            .AddParam("index", $"Array index (0-{arraySize - 1})")
            .AddParam("value", "Value to set")
            .AddThrows("RangeError", "If index is out of bounds")
            .Render(cb);

        cb.AppendLine($"set{fieldName}(index: number, value: {info.LanguageType}): void {{");
        cb.Indent();
        cb.AppendLine($"if (index < 0 || index >= {arraySize}) throw new RangeError('Index out of bounds');");
        cb.AppendLine(info.IsBool
            ? $"this.view.{info.SetMethod}({offset} + index * {elementSize}, value ? 1 : 0);"
            : $"this.view.{info.SetMethod}({offset} + index * {elementSize}, value{info.EndianArg});");
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
