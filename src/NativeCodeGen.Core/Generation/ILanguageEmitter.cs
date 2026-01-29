using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Abstraction for language-specific code emission.
/// Implementations handle the syntax differences between languages.
/// </summary>
public interface ILanguageEmitter
{
    /// <summary>
    /// Gets the type mapper for this language.
    /// </summary>
    ITypeMapper TypeMapper { get; }

    /// <summary>
    /// Creates a new doc builder for this language.
    /// </summary>
    DocBuilder CreateDocBuilder();

    /// <summary>
    /// Gets the file extension for this language (e.g., ".ts", ".lua").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Gets the self reference for this language (e.g., "this" for TS, "self" for Lua).
    /// </summary>
    string SelfReference { get; }

    // === Enum Generation ===

    /// <summary>
    /// Emits the start of an enum definition.
    /// </summary>
    void EmitEnumStart(CodeBuilder cb, string enumName);

    /// <summary>
    /// Emits an enum member.
    /// </summary>
    void EmitEnumMember(CodeBuilder cb, string memberName, string? value, string? comment);

    /// <summary>
    /// Emits the end of an enum definition.
    /// </summary>
    void EmitEnumEnd(CodeBuilder cb, string enumName);

    // === Class Generation ===

    /// <summary>
    /// Emits the start of a class definition.
    /// </summary>
    void EmitClassStart(CodeBuilder cb, string className, string? baseClass, ClassKind kind);

    /// <summary>
    /// Emits the end of a class definition.
    /// </summary>
    void EmitClassEnd(CodeBuilder cb, string className);

    /// <summary>
    /// Emits a constructor for a handle-based class.
    /// </summary>
    void EmitHandleConstructor(CodeBuilder cb, string className, string? baseClass);

    /// <summary>
    /// Emits a static fromHandle factory method.
    /// </summary>
    void EmitFromHandleMethod(CodeBuilder cb, string className);

    /// <summary>
    /// Emits a constructor for a task class (takes entity).
    /// </summary>
    void EmitTaskConstructor(CodeBuilder cb, string className, string entityType);

    /// <summary>
    /// Emits a constructor for a model class (takes hash).
    /// </summary>
    void EmitModelConstructor(CodeBuilder cb, string className);

    // === Method Generation ===

    /// <summary>
    /// Emits the start of a method.
    /// </summary>
    void EmitMethodStart(CodeBuilder cb, string className, string methodName, List<MethodParameter> parameters, string returnType, MethodKind kind);

    /// <summary>
    /// Emits the end of a method.
    /// </summary>
    void EmitMethodEnd(CodeBuilder cb);

    /// <summary>
    /// Emits a native invoke call.
    /// </summary>
    void EmitInvokeNative(CodeBuilder cb, string hash, List<string> args, TypeInfo returnType);

    // === Struct Generation ===

    /// <summary>
    /// Emits struct-level documentation (e.g., usage info).
    /// Called before EmitStructStart if structDef has relevant info.
    /// </summary>
    void EmitStructDocumentation(CodeBuilder cb, StructDefinition structDef);

    /// <summary>
    /// Emits the start of a struct/buffered class.
    /// </summary>
    void EmitStructStart(CodeBuilder cb, string structName, int size, List<string> nestedStructImports);

    /// <summary>
    /// Emits the end of a struct.
    /// </summary>
    void EmitStructEnd(CodeBuilder cb, string structName);

    /// <summary>
    /// Emits a struct constructor.
    /// </summary>
    void EmitStructConstructor(CodeBuilder cb, string structName, int size, bool supportsNesting);

    /// <summary>
    /// Emits a primitive field getter.
    /// </summary>
    void EmitPrimitiveGetter(CodeBuilder cb, string structName, string fieldName, int offset, TypeInfo type, string? comment);

    /// <summary>
    /// Emits a primitive field setter.
    /// </summary>
    void EmitPrimitiveSetter(CodeBuilder cb, string structName, string fieldName, int offset, TypeInfo type);

    /// <summary>
    /// Emits an array field getter.
    /// </summary>
    void EmitArrayGetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type, string? comment);

    /// <summary>
    /// Emits an array field setter.
    /// </summary>
    void EmitArraySetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type);

    /// <summary>
    /// Emits a nested struct field accessor.
    /// </summary>
    void EmitNestedStructAccessor(CodeBuilder cb, string structName, string fieldName, string nestedStructName, int offset, bool isArray, int arraySize, string? comment);
}

/// <summary>
/// Kind of class being generated.
/// </summary>
public enum ClassKind
{
    /// <summary>Handle-based class (Entity, Ped, etc.)</summary>
    Handle,
    /// <summary>Task class that wraps an entity</summary>
    Task,
    /// <summary>Model class that wraps a hash</summary>
    Model,
    /// <summary>Namespace utility class with static methods</summary>
    Namespace
}

/// <summary>
/// Kind of method being generated.
/// </summary>
public enum MethodKind
{
    /// <summary>Instance method (uses self/this)</summary>
    Instance,
    /// <summary>Static method</summary>
    Static
}

/// <summary>
/// Represents a method parameter for emission.
/// </summary>
public record MethodParameter(string Name, string Type, bool IsOptional = false);
