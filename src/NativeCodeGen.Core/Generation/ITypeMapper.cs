using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Maps C types to target language types.
/// </summary>
public interface ITypeMapper
{
    /// <summary>
    /// Maps a C type to the target language type.
    /// </summary>
    string MapType(TypeInfo type, bool isNotNull = false);

    /// <summary>
    /// Gets the expression for marking a return type in native invocation.
    /// </summary>
    string GetResultMarker(TypeInfo type);

    /// <summary>
    /// Whether the type needs a result marker in native invocation.
    /// </summary>
    bool NeedsResultMarker(TypeInfo type);

    /// <summary>
    /// Whether the type is a handle type (Entity, Ped, etc.)
    /// </summary>
    bool IsHandleType(TypeInfo type);

    /// <summary>
    /// Whether the type is a Vector3.
    /// </summary>
    bool IsVector3(TypeInfo type);

    /// <summary>
    /// Gets the raw invoke return type (e.g., "number" for handles, "number[]" for Vector3).
    /// </summary>
    string GetInvokeReturnType(TypeInfo type);

    /// <summary>
    /// Gets the pointer placeholder for an output-only pointer parameter.
    /// E.g., "Citizen.pointerValueInt()" for int*.
    /// </summary>
    string GetPointerPlaceholder(TypeInfo type);

    /// <summary>
    /// Gets the initialized pointer expression for an input+output pointer parameter.
    /// E.g., "Citizen.pointerValueIntInitialized({0})" for int* with @in.
    /// The {0} placeholder will be replaced with the parameter name.
    /// </summary>
    string GetInitializedPointerFormat(TypeInfo type);

    /// <summary>
    /// Gets the DataView accessor info for a type (used in struct generation).
    /// Returns (languageType, getMethod, setMethod).
    /// </summary>
    (string LanguageType, string GetMethod, string SetMethod) GetDataViewAccessor(TypeInfo type);
}
