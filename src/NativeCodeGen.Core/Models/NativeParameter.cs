namespace NativeCodeGen.Core.Models;

public class NativeParameter
{
    public string Name { get; set; } = string.Empty;
    public TypeInfo Type { get; set; } = new();
    /// <summary>
    /// True if this is an output-only pointer parameter (excluded from method signature).
    /// Struct pointers are always inputs (we pass the buffer).
    /// Pointers with @in attribute are input+output (included in signature).
    /// </summary>
    public bool IsOutput => Type.IsPointer &&
                            Type.Category != TypeCategory.String &&
                            Type.Category != TypeCategory.Struct &&
                            !Attributes.IsIn;
    public string? DefaultValue { get; set; }
    public bool HasDefaultValue => DefaultValue != null;
    public ParameterAttributes Attributes { get; set; } = new();
    public string? Description { get; set; }
}

public class ParameterAttributes
{
    public bool IsThis { get; set; }
    public bool IsNotNull { get; set; }
    /// <summary>
    /// Indicates this is an input+output pointer parameter.
    /// For int*/float*/Vector3* with @in, we use the initialized pointer variant.
    /// </summary>
    public bool IsIn { get; set; }
    public List<string> CustomAttributes { get; set; } = new();
}
