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

    public string GenerateNamespaceClass(string namespaceName, List<NativeDefinition> natives, HashSet<string>? handleClassNames = null)
    {
        var cb = new CodeBuilder();
        var className = handleClassNames != null
            ? NameConverter.NamespaceToClassName(namespaceName, handleClassNames)
            : NameConverter.NamespaceToClassName(namespaceName);

        // Collect handle types used in this namespace class
        var handleTypes = CollectHandleTypes(natives);
        _emitter.EmitHandleImports(cb, handleTypes);

        _emitter.EmitClassStart(cb, className, null, ClassKind.Namespace);

        foreach (var native in natives)
        {
            GenerateStaticMethod(cb, native, className, namespaceName);
        }

        _emitter.EmitClassEnd(cb, className);

        return cb.ToString();
    }

    private HashSet<string> CollectHandleTypes(List<NativeDefinition> natives)
    {
        var handleTypes = new HashSet<string>();

        foreach (var native in natives)
        {
            // Check return type
            if (native.ReturnType.Category == TypeCategory.Handle)
            {
                handleTypes.Add(NativeClassifier.NormalizeHandleType(native.ReturnType.Name));
            }

            // Check parameters
            foreach (var param in native.Parameters)
            {
                if (param.Type.Category == TypeCategory.Handle)
                {
                    handleTypes.Add(NativeClassifier.NormalizeHandleType(param.Type.Name));
                }
            }
        }

        return handleTypes;
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
            if (IsHandleMatch(firstParam.Type, className) || firstParam.IsThis)
            {
                parameters = parameters.Skip(1).ToList();
            }
        }

        var inputParams = parameters.Where(p => !p.IsPureOutput).ToList();
        var outputParams = parameters.Where(p => p.IsPureOutput).ToList();
        var outputParamTypes = outputParams.Select(p => p.Type).ToList();

        // Generate doc
        GenerateMethodDoc(cb, native, inputParams, outputParams);

        // Build method parameters
        var methodParams = inputParams.Select(p => new MethodParameter(
            p.Name,
            _emitter.TypeMapper.MapType(p.Type, p.IsNotNull),
            p.HasDefaultValue
        )).ToList();

        var returnType = _emitter.TypeMapper.BuildCombinedReturnType(native.ReturnType, outputParamTypes);
        _emitter.EmitMethodStart(cb, className, methodName, methodParams, returnType, MethodKind.Instance);

        // Build invoke args - pass ALL parameters (ArgumentBuilder handles output pointers)
        var args = new List<string>();
        if (isInstance)
        {
            args.Add($"{_emitter.SelfReference}.handle");
        }

        foreach (var param in parameters)
        {
            args.Add(ArgumentBuilder.GetArgumentExpression(param, _emitter.TypeMapper, _emitter.Config));
        }

        _emitter.EmitInvokeNative(cb, native.Hash, args, native.ReturnType, outputParamTypes);
        _emitter.EmitMethodEnd(cb);
    }

    private void GenerateMethodWithSkippedFirst(CodeBuilder cb, NativeDefinition native, string className, string namespaceForDedup, string firstArgExpr, string? entityType)
    {
        var methodName = NameDeduplicator.DeduplicateForNamespace(native.Name, namespaceForDedup, NamingConvention.CamelCase);

        var parameters = native.Parameters.Skip(1).ToList();
        var inputParams = parameters.Where(p => !p.IsPureOutput).ToList();
        var outputParams = parameters.Where(p => p.IsPureOutput).ToList();
        var outputParamTypes = outputParams.Select(p => p.Type).ToList();

        GenerateMethodDoc(cb, native, inputParams, outputParams);

        var methodParams = inputParams.Select(p => new MethodParameter(
            p.Name,
            _emitter.TypeMapper.MapType(p.Type, p.IsNotNull),
            p.HasDefaultValue
        )).ToList();

        var returnType = _emitter.TypeMapper.BuildCombinedReturnType(native.ReturnType, outputParamTypes);
        _emitter.EmitMethodStart(cb, className, methodName, methodParams, returnType, MethodKind.Instance);

        // Build invoke args - pass ALL parameters (ArgumentBuilder handles output pointers)
        var args = new List<string> { firstArgExpr };
        foreach (var param in parameters)
        {
            args.Add(ArgumentBuilder.GetArgumentExpression(param, _emitter.TypeMapper, _emitter.Config));
        }

        _emitter.EmitInvokeNative(cb, native.Hash, args, native.ReturnType, outputParamTypes);
        _emitter.EmitMethodEnd(cb);
    }

    private void GenerateStaticMethod(CodeBuilder cb, NativeDefinition native, string className, string namespaceName)
    {
        var methodName = NameDeduplicator.DeduplicateForNamespace(native.Name, namespaceName, NamingConvention.CamelCase);

        var inputParams = native.Parameters.Where(p => !p.IsPureOutput).ToList();
        var outputParams = native.Parameters.Where(p => p.IsPureOutput).ToList();
        var outputParamTypes = outputParams.Select(p => p.Type).ToList();

        GenerateMethodDoc(cb, native, inputParams, outputParams);

        var methodParams = inputParams.Select(p => new MethodParameter(
            p.Name,
            _emitter.TypeMapper.MapType(p.Type, p.IsNotNull),
            p.HasDefaultValue
        )).ToList();

        var returnType = _emitter.TypeMapper.BuildCombinedReturnType(native.ReturnType, outputParamTypes);
        _emitter.EmitMethodStart(cb, className, methodName, methodParams, returnType, MethodKind.Static);

        // Build invoke args - pass ALL parameters (ArgumentBuilder handles output pointers)
        var args = new List<string>();
        foreach (var param in native.Parameters)
        {
            args.Add(ArgumentBuilder.GetArgumentExpression(param, _emitter.TypeMapper, _emitter.Config));
        }

        _emitter.EmitInvokeNative(cb, native.Hash, args, native.ReturnType, outputParamTypes);
        _emitter.EmitMethodEnd(cb);
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

        if (typeName == "Entity" && EntitySubclasses.Contains(className))
            return true;

        return false;
    }
}
