namespace NativeCodeGen.Core.Models;

public class StructDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<StructField> Fields { get; set; } = new();
    public string? SourceFile { get; set; }

    /// <summary>
    /// Default alignment for fields in this struct (in bytes).
    /// Set via @alignas(N) before struct keyword.
    /// If null, uses the generator's default (typically 8 for native structs).
    /// </summary>
    public int? DefaultAlignment { get; set; }

    /// <summary>
    /// Natives that use this struct (name, hash pairs)
    /// </summary>
    public List<(string Name, string Hash)> UsedByNatives { get; set; } = new();
}

public class StructField
{
    public string Name { get; set; } = string.Empty;
    public TypeInfo Type { get; set; } = new();
    public string? Comment { get; set; }

    /// <summary>
    /// Field is an input (setter only) - marked with @in
    /// </summary>
    public bool IsInput { get; set; } = true;

    /// <summary>
    /// Field is an output (getter only) - marked with @out
    /// </summary>
    public bool IsOutput { get; set; } = true;

    /// <summary>
    /// Array size if this is an array field (0 = not an array)
    /// </summary>
    public int ArraySize { get; set; } = 0;

    /// <summary>
    /// True if this field is an array
    /// </summary>
    public bool IsArray => ArraySize > 0;

    /// <summary>
    /// True if this is a nested struct type
    /// </summary>
    public bool IsNestedStruct { get; set; } = false;

    /// <summary>
    /// The struct type name if this is a nested struct
    /// </summary>
    public string? NestedStructName { get; set; }

    /// <summary>
    /// True if this is a padding field - marked with @padding.
    /// Padding fields use actual byte sizes and don't generate accessors.
    /// </summary>
    public bool IsPadding { get; set; } = false;

    /// <summary>
    /// Custom alignment for this field (in bytes).
    /// Set via @alignas(N) before the field type.
    /// If null, uses the struct's DefaultAlignment.
    /// </summary>
    public int? Alignment { get; set; }
}
