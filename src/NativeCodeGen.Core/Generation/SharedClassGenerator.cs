using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Utilities;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Shared class generator that works with any language via ILanguageEmitter.
/// Contains all the classification and method organization logic.
/// </summary>
public class SharedClassGenerator
{
    private readonly ILanguageEmitter _emitter;

    public SharedClassGenerator(ILanguageEmitter emitter)
    {
        _emitter = emitter;
    }

    public string GenerateHandleClass(string className, string? baseClass, List<NativeDefinition> natives)
    {
        var cb = new CodeBuilder();

        if (NativeClassifier.IsTaskClass(className))
        {
            GenerateTaskClass(cb, className, baseClass, natives);
        }
        else if (NativeClassifier.IsModelClass(className))
        {
            GenerateModelClass(cb, className, baseClass, natives);
        }
        else if (NativeClassifier.IsWeaponClass(className))
        {
            GenerateWeaponClass(cb, className, natives);
        }
        else
        {
            GenerateStandardHandleClass(cb, className, baseClass, natives);
        }

        return cb.ToString();
    }

    public string GenerateNamespaceClass(string namespaceName, List<NativeDefinition> natives, HashSet<string>? handleClassNames = null)
    {
        var cb = new CodeBuilder();
        var className = handleClassNames != null
            ? NameConverter.NamespaceToClassName(namespaceName, handleClassNames)
            : NameConverter.NamespaceToClassName(namespaceName);

        // Collect types used in this namespace class
        var handleTypes = CollectHandleTypes(natives);
        var nonClassHandleTypes = CollectNonClassHandleTypes(natives);
        var (enums, structs) = CollectTypeReferences(natives);
        _emitter.EmitTypeImports(cb, enums, structs);
        _emitter.EmitHandleImports(cb, handleTypes);
        _emitter.EmitNonClassHandleImports(cb, nonClassHandleTypes);

        _emitter.EmitClassStart(cb, className, null, ClassKind.Namespace);

        foreach (var native in natives)
        {
            GenerateStaticMethod(cb, native, className, namespaceName);
        }

        _emitter.EmitClassEnd(cb, className, ClassKind.Namespace);

        return cb.ToString();
    }

    private HashSet<string> CollectHandleTypes(List<NativeDefinition> natives) =>
        CollectHandles(natives, TypeInfo.IsClassHandle, NativeClassifier.NormalizeHandleType);

    private HashSet<string> CollectNonClassHandleTypes(List<NativeDefinition> natives) =>
        CollectHandles(natives, name => !TypeInfo.IsClassHandle(name), name => name);

    private HashSet<string> CollectHandles(
        List<NativeDefinition> natives,
        Func<string, bool> filter,
        Func<string, string> normalize)
    {
        var handles = new HashSet<string>();

        foreach (var native in natives)
        {
            if (native.ReturnType.Category == TypeCategory.Handle && filter(native.ReturnType.Name))
                handles.Add(normalize(native.ReturnType.Name));

            foreach (var param in native.Parameters)
            {
                if (param.Type.Category == TypeCategory.Handle && filter(param.Type.Name))
                    handles.Add(normalize(param.Type.Name));
            }
        }

        return handles;
    }

    private (HashSet<string> enums, HashSet<string> structs) CollectTypeReferences(List<NativeDefinition> natives)
    {
        var enums = new HashSet<string>();
        var structs = new HashSet<string>();

        foreach (var native in natives)
        {
            CollectTypeRef(native.ReturnType, enums, structs);
            foreach (var param in native.Parameters)
            {
                CollectTypeRef(param.Type, enums, structs);
            }
        }

        return (enums, structs);
    }

    private void CollectTypeRef(TypeInfo type, HashSet<string> enums, HashSet<string> structs)
    {
        if (type.Category == TypeCategory.Enum)
        {
            enums.Add(type.Name);
        }
        else if (type.Category == TypeCategory.Struct)
        {
            structs.Add(type.Name);
        }
    }

    private void GenerateStandardHandleClass(CodeBuilder cb, string className, string? baseClass, List<NativeDefinition> natives)
    {
        var (enums, structs) = CollectTypeReferences(natives);
        var handleTypes = CollectHandleTypes(natives);
        handleTypes.Remove(className); // Don't import self
        if (baseClass != null) handleTypes.Remove(baseClass); // Don't import base class - already imported
        _emitter.EmitTypeImports(cb, enums, structs);
        _emitter.EmitHandleImports(cb, handleTypes);
        _emitter.EmitClassStart(cb, className, baseClass, ClassKind.Handle);
        _emitter.EmitHandleConstructor(cb, className, baseClass);
        _emitter.EmitFromHandleMethod(cb, className);

        // Entity and its subclasses get fromNetworkId
        if (className == "Entity" || NativeClassifier.EntitySubclasses.Contains(className))
        {
            _emitter.EmitFromNetworkIdMethod(cb, className);
        }

        foreach (var native in natives)
        {
            GenerateInstanceMethod(cb, native, className);
        }

        _emitter.EmitClassEnd(cb, className, ClassKind.Handle);
    }

    private void GenerateTaskClass(CodeBuilder cb, string className, string? baseClass, List<NativeDefinition> natives)
    {
        var entityType = NativeClassifier.GetTaskEntityType(className);
        var (enums, structs) = CollectTypeReferences(natives);
        var handleTypes = CollectHandleTypes(natives);
        handleTypes.Remove(entityType); // Don't import entity type - already imported via type import
        _emitter.EmitTypeImports(cb, enums, structs);
        _emitter.EmitHandleImports(cb, handleTypes);
        _emitter.EmitClassStart(cb, className, baseClass, ClassKind.Task);
        _emitter.EmitTaskConstructor(cb, className, entityType, baseClass);

        foreach (var native in natives)
        {
            GenerateMethodWithSkippedFirst(cb, native, className, "TASK", $"{_emitter.SelfReference}.entity.handle", entityType);
        }

        _emitter.EmitClassEnd(cb, className, ClassKind.Task);
    }

    private void GenerateModelClass(CodeBuilder cb, string className, string? baseClass, List<NativeDefinition> natives)
    {
        var (enums, structs) = CollectTypeReferences(natives);
        var handleTypes = CollectHandleTypes(natives);
        _emitter.EmitTypeImports(cb, enums, structs);
        _emitter.EmitHandleImports(cb, handleTypes);
        _emitter.EmitClassStart(cb, className, baseClass, ClassKind.Model);
        _emitter.EmitModelConstructor(cb, className, baseClass);

        foreach (var native in natives)
        {
            GenerateMethodWithSkippedFirst(cb, native, className, "STREAMING", $"{_emitter.SelfReference}.hash", null);
        }

        _emitter.EmitClassEnd(cb, className, ClassKind.Model);
    }

    private void GenerateWeaponClass(CodeBuilder cb, string className, List<NativeDefinition> natives)
    {
        var (enums, structs) = CollectTypeReferences(natives);
        var handleTypes = CollectHandleTypes(natives);
        handleTypes.Remove("Ped"); // Don't import Ped - already imported via type import
        _emitter.EmitTypeImports(cb, enums, structs);
        _emitter.EmitHandleImports(cb, handleTypes);
        _emitter.EmitClassStart(cb, className, null, ClassKind.Weapon);
        _emitter.EmitWeaponConstructor(cb, className);

        foreach (var native in natives)
        {
            GenerateMethodWithSkippedFirst(cb, native, className, "WEAPON", $"{_emitter.SelfReference}.ped.handle", "Ped");
        }

        _emitter.EmitClassEnd(cb, className, ClassKind.Weapon);
    }

    private void GenerateInstanceMethod(CodeBuilder cb, NativeDefinition native, string className)
    {
        var methodName = native.MethodNameOverride ?? NameDeduplicator.DeduplicateForClass(native.Name, className, NamingConvention.CamelCase);
        var isInstance = IsInstanceMethod(native, className);

        var parameters = native.Parameters.ToList();
        if (isInstance && parameters.Count > 0)
        {
            var firstParam = parameters[0];
            if (IsHandleMatch(firstParam.Type, className) || firstParam.IsThis)
                parameters = parameters.Skip(1).ToList();
        }

        var firstArg = isInstance ? $"{_emitter.SelfReference}.handle" : null;
        EmitMethod(cb, native, className, methodName, parameters, firstArg, MethodKind.Instance);
    }

    private void GenerateMethodWithSkippedFirst(CodeBuilder cb, NativeDefinition native, string className, string namespaceForDedup, string firstArgExpr, string? entityType)
    {
        var methodName = native.MethodNameOverride ?? NameDeduplicator.DeduplicateForNamespace(native.Name, namespaceForDedup, NamingConvention.CamelCase);
        var parameters = native.Parameters.Skip(1).ToList();
        EmitMethod(cb, native, className, methodName, parameters, firstArgExpr, MethodKind.Instance);
    }

    private void GenerateStaticMethod(CodeBuilder cb, NativeDefinition native, string className, string namespaceName)
    {
        var methodName = native.MethodNameOverride ?? NameDeduplicator.DeduplicateForNamespace(native.Name, namespaceName, NamingConvention.CamelCase);
        EmitMethod(cb, native, className, methodName, native.Parameters.ToList(), null, MethodKind.Static);
    }

    /// <summary>
    /// Core method generation logic shared by instance, static, and skipped-first methods.
    /// </summary>
    private void EmitMethod(
        CodeBuilder cb,
        NativeDefinition native,
        string className,
        string methodName,
        List<NativeParameter> parameters,
        string? firstArg,
        MethodKind kind)
    {
        var inputParams = parameters.Where(p => !p.IsPureOutput).ToList();
        var outputParams = parameters.Where(p => p.IsPureOutput).ToList();
        var outputParamTypes = outputParams.Select(p => p.Type).ToList();

        var returnType = _emitter.TypeMapper.BuildCombinedReturnType(native.ReturnType, outputParamTypes);

        // Determine if this should be a getter or setter (TypeScript only)
        var actualKind = kind;
        var actualMethodName = methodName;
        var hasOutputParams = outputParams.Count > 0;

        // Check if this is a getter candidate (for generating getter proxy when params are optional)
        // Note: Methods with output params return tuples, which are still valid getter return types
        var hasReturnValue = native.ReturnType.Category != TypeCategory.Void || hasOutputParams;
        var isGetterCandidate = kind == MethodKind.Instance &&
                                _emitter.Config.SupportsGetters &&
                                hasReturnValue &&
                                NameConverter.IsGetterName(methodName);

        var allParamsOptional = inputParams.Count > 0 && inputParams.All(p => p.HasDefaultValue);
        var shouldEmitGetterProxy = isGetterCandidate && allParamsOptional;

        if (kind == MethodKind.Instance && _emitter.Config.SupportsGetters)
        {
            // Getter: parameterless, returns a value (including tuples), name starts with "get" or "is"
            if (inputParams.Count == 0 &&
                hasReturnValue &&
                NameConverter.IsGetterName(methodName))
            {
                actualKind = MethodKind.Getter;
                actualMethodName = NameConverter.GetterToPropertyName(methodName);
            }
            // Setter: single parameter, void return, name starts with "set"
            else if (inputParams.Count == 1 &&
                     native.ReturnType.Category == TypeCategory.Void &&
                     !hasOutputParams &&
                     NameConverter.IsSetterName(methodName))
            {
                actualKind = MethodKind.Setter;
                actualMethodName = NameConverter.SetterToPropertyName(methodName);
            }
        }

        GenerateMethodDoc(cb, native, inputParams, outputParams);

        var methodParams = inputParams.Select(p => new MethodParameter(
            p.Name,
            _emitter.TypeMapper.MapType(p.Type, p.IsNotNull),
            p.HasDefaultValue
        )).ToList();

        _emitter.EmitMethodStart(cb, className, actualMethodName, methodParams, returnType, actualKind);

        // Build invoke args
        var args = new List<string>();
        if (firstArg != null)
            args.Add(firstArg);

        foreach (var param in parameters)
            args.Add(ArgumentBuilder.GetArgumentExpression(param, _emitter.TypeMapper, _emitter.Config));

        _emitter.EmitInvokeNative(cb, native.Hash, args, native.ReturnType, outputParamTypes);
        _emitter.EmitMethodEnd(cb);

        // If all params are optional and it's a getter candidate, also emit a getter proxy
        if (shouldEmitGetterProxy)
        {
            var propertyName = NameConverter.GetterToPropertyName(methodName);
            _emitter.EmitGetterProxy(cb, propertyName, methodName, returnType);
        }
    }

    private void GenerateMethodDoc(CodeBuilder cb, NativeDefinition native, List<NativeParameter> inputParams, List<NativeParameter>? outputParams = null)
    {
        var doc = _emitter.CreateDocBuilder()
            .AddDescription(native.Description);

        foreach (var param in inputParams)
        {
            var type = _emitter.TypeMapper.MapType(param.Type, param.IsNotNull);
            doc.AddParam(param.Name, type, param.Description);
        }

        var outputParamTypes = outputParams?.Select(p => p.Type).ToList() ?? new List<TypeInfo>();
        var hasOutputParams = outputParamTypes.Count > 0;
        var hasReturn = native.ReturnType.Category != TypeCategory.Void || hasOutputParams;

        if (hasReturn)
        {
            var returnType = _emitter.TypeMapper.BuildCombinedReturnType(native.ReturnType, outputParamTypes);
            var description = native.ReturnDescription ?? "";

            // Add output param descriptions to return doc
            if (hasOutputParams && outputParams != null)
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(native.ReturnDescription))
                {
                    parts.Add(native.ReturnDescription);
                }
                foreach (var outParam in outputParams)
                {
                    var paramDesc = !string.IsNullOrWhiteSpace(outParam.Description)
                        ? $"{outParam.Name}: {outParam.Description}"
                        : outParam.Name;
                    parts.Add(paramDesc);
                }
                description = string.Join("; ", parts);
            }

            doc.AddReturn(returnType, description);
        }

        doc.Render(cb);
    }

    private bool IsInstanceMethod(NativeDefinition native, string className)
    {
        if (native.Parameters.Count == 0)
            return false;

        var firstParam = native.Parameters[0];

        if (firstParam.IsThis)
            return true;

        return IsHandleMatch(firstParam.Type, className);
    }

    private bool IsHandleMatch(TypeInfo type, string className)
    {
        if (type.Category != TypeCategory.Handle)
            return false;

        var typeName = type.Name;

        if (typeName.Equals(className, StringComparison.OrdinalIgnoreCase))
            return true;

        if (typeName == "Object" && className == "Prop")
            return true;

        if (typeName == "Entity" && NativeClassifier.EntitySubclasses.Contains(className))
            return true;

        return false;
    }
}
