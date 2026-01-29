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
    private static readonly HashSet<string> EntitySubclasses = new() { "Ped", "Vehicle", "Object", "Prop" };

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
        else
        {
            GenerateStandardHandleClass(cb, className, baseClass, natives);
        }

        return cb.ToString();
    }

    public string GenerateNamespaceClass(string namespaceName, List<NativeDefinition> natives)
    {
        var cb = new CodeBuilder();
        var className = NameConverter.NamespaceToClassName(namespaceName);

        _emitter.EmitClassStart(cb, className, null, ClassKind.Namespace);

        foreach (var native in natives)
        {
            GenerateStaticMethod(cb, native, className, namespaceName);
        }

        _emitter.EmitClassEnd(cb, className);

        return cb.ToString();
    }

    private void GenerateStandardHandleClass(CodeBuilder cb, string className, string? baseClass, List<NativeDefinition> natives)
    {
        _emitter.EmitClassStart(cb, className, baseClass, ClassKind.Handle);
        _emitter.EmitHandleConstructor(cb, className, baseClass);
        _emitter.EmitFromHandleMethod(cb, className);

        foreach (var native in natives)
        {
            GenerateInstanceMethod(cb, native, className);
        }

        _emitter.EmitClassEnd(cb, className);
    }

    private void GenerateTaskClass(CodeBuilder cb, string className, string? baseClass, List<NativeDefinition> natives)
    {
        var entityType = NativeClassifier.GetTaskEntityType(className);

        _emitter.EmitClassStart(cb, className, baseClass, ClassKind.Task);
        _emitter.EmitTaskConstructor(cb, className, entityType);

        foreach (var native in natives)
        {
            GenerateMethodWithSkippedFirst(cb, native, className, "TASK", $"{_emitter.SelfReference}.entity.handle", entityType);
        }

        _emitter.EmitClassEnd(cb, className);
    }

    private void GenerateModelClass(CodeBuilder cb, string className, string? baseClass, List<NativeDefinition> natives)
    {
        _emitter.EmitClassStart(cb, className, baseClass, ClassKind.Model);
        _emitter.EmitModelConstructor(cb, className);

        foreach (var native in natives)
        {
            GenerateMethodWithSkippedFirst(cb, native, className, "STREAMING", $"{_emitter.SelfReference}.hash", null);
        }

        _emitter.EmitClassEnd(cb, className);
    }

    private void GenerateInstanceMethod(CodeBuilder cb, NativeDefinition native, string className)
    {
        var methodName = NameDeduplicator.DeduplicateForClass(native.Name, className, NamingConvention.CamelCase);
        var isInstance = IsInstanceMethod(native, className);

        var parameters = native.Parameters.ToList();
        if (isInstance && parameters.Count > 0)
        {
            var firstParam = parameters[0];
            if (IsHandleMatch(firstParam.Type, className) || firstParam.Attributes.IsThis)
            {
                parameters = parameters.Skip(1).ToList();
            }
        }

        var inputParams = parameters.Where(p => !p.IsOutput).ToList();

        // Generate doc
        GenerateMethodDoc(cb, native, inputParams);

        // Build method parameters
        var methodParams = inputParams.Select(p => new MethodParameter(
            p.Name,
            _emitter.TypeMapper.MapType(p.Type, p.Attributes.IsNotNull),
            p.HasDefaultValue
        )).ToList();

        var returnType = _emitter.TypeMapper.MapType(native.ReturnType);
        _emitter.EmitMethodStart(cb, className, methodName, methodParams, returnType, MethodKind.Instance);

        // Build invoke args - pass ALL parameters (ArgumentBuilder handles output pointers)
        var args = new List<string>();
        if (isInstance)
        {
            args.Add($"{_emitter.SelfReference}.handle");
        }

        foreach (var param in parameters)
        {
            args.Add(ArgumentBuilder.GetArgumentExpression(param, _emitter.TypeMapper));
        }

        _emitter.EmitInvokeNative(cb, native.Hash, args, native.ReturnType);
        _emitter.EmitMethodEnd(cb);
    }

    private void GenerateMethodWithSkippedFirst(CodeBuilder cb, NativeDefinition native, string className, string namespaceForDedup, string firstArgExpr, string? entityType)
    {
        var methodName = NameDeduplicator.DeduplicateForNamespace(native.Name, namespaceForDedup, NamingConvention.CamelCase);

        var parameters = native.Parameters.Skip(1).ToList();
        var inputParams = parameters.Where(p => !p.IsOutput).ToList();

        GenerateMethodDoc(cb, native, inputParams);

        var methodParams = inputParams.Select(p => new MethodParameter(
            p.Name,
            _emitter.TypeMapper.MapType(p.Type, p.Attributes.IsNotNull),
            p.HasDefaultValue
        )).ToList();

        var returnType = _emitter.TypeMapper.MapType(native.ReturnType);
        _emitter.EmitMethodStart(cb, className, methodName, methodParams, returnType, MethodKind.Instance);

        // Build invoke args - pass ALL parameters (ArgumentBuilder handles output pointers)
        var args = new List<string> { firstArgExpr };
        foreach (var param in parameters)
        {
            args.Add(ArgumentBuilder.GetArgumentExpression(param, _emitter.TypeMapper));
        }

        _emitter.EmitInvokeNative(cb, native.Hash, args, native.ReturnType);
        _emitter.EmitMethodEnd(cb);
    }

    private void GenerateStaticMethod(CodeBuilder cb, NativeDefinition native, string className, string namespaceName)
    {
        var methodName = NameDeduplicator.DeduplicateForNamespace(native.Name, namespaceName, NamingConvention.CamelCase);

        var inputParams = native.Parameters.Where(p => !p.IsOutput).ToList();

        GenerateMethodDoc(cb, native, inputParams);

        var methodParams = inputParams.Select(p => new MethodParameter(
            p.Name,
            _emitter.TypeMapper.MapType(p.Type, p.Attributes.IsNotNull),
            p.HasDefaultValue
        )).ToList();

        var returnType = _emitter.TypeMapper.MapType(native.ReturnType);
        _emitter.EmitMethodStart(cb, className, methodName, methodParams, returnType, MethodKind.Static);

        // Build invoke args - pass ALL parameters (ArgumentBuilder handles output pointers)
        var args = new List<string>();
        foreach (var param in native.Parameters)
        {
            args.Add(ArgumentBuilder.GetArgumentExpression(param, _emitter.TypeMapper));
        }

        _emitter.EmitInvokeNative(cb, native.Hash, args, native.ReturnType);
        _emitter.EmitMethodEnd(cb);
    }

    private void GenerateMethodDoc(CodeBuilder cb, NativeDefinition native, List<NativeParameter> inputParams)
    {
        var doc = _emitter.CreateDocBuilder()
            .AddDescription(native.Description);

        foreach (var param in inputParams)
        {
            var type = _emitter.TypeMapper.MapType(param.Type, param.Attributes.IsNotNull);
            doc.AddParam(param.Name, type, param.Description);
        }

        if (native.ReturnType.Category != TypeCategory.Void)
        {
            var returnType = _emitter.TypeMapper.GetInvokeReturnType(native.ReturnType);
            doc.AddReturn(returnType, native.ReturnDescription);
        }

        doc.Render(cb);
    }

    private bool IsInstanceMethod(NativeDefinition native, string className)
    {
        if (native.Parameters.Count == 0)
            return false;

        var firstParam = native.Parameters[0];

        if (firstParam.Attributes.IsThis)
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

        if (typeName == "Entity" && EntitySubclasses.Contains(className))
            return true;

        return false;
    }
}
