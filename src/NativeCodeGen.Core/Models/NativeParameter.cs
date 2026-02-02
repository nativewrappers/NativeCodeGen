namespace NativeCodeGen.Core.Models;

public class NativeParameter
{
    public string Name { get; set; } = string.Empty;
    public TypeInfo Type { get; set; } = new();

    /// <summary>
    /// Parameter attribute flags (@this, @nullable, @in, computed output).
    /// </summary>
    public ParamFlags Flags { get; set; } = ParamFlags.None;

    /// <summary>
    /// True if this is an output-only pointer parameter (excluded from method signature).
    /// Struct pointers are always inputs (we pass the buffer).
    /// Pointers with @in attribute are input+output (included in signature).
    /// </summary>
    public bool IsOutput => Flags.HasFlag(ParamFlags.Output);

    /// <summary>
    /// True if this is a pure output parameter (Output but not In).
    /// These are excluded from method signatures and returned as tuple values.
    /// </summary>
    public bool IsPureOutput => Flags.IsPureOutput();

    /// <summary>
    /// True if this is an input+output parameter (both Output and In flags).
    /// These appear in method signatures and use initialized pointer variants.
    /// </summary>
    public bool IsInOut => Flags.IsInOut();

    public string? DefaultValue { get; set; }
    public bool HasDefaultValue => DefaultValue != null;
    public string? Description { get; set; }

    // Convenience accessors for common flag checks
    public bool IsThis => Flags.HasFlag(ParamFlags.This);
    public bool IsNullable => Flags.HasFlag(ParamFlags.Nullable);
    public bool IsIn => Flags.HasFlag(ParamFlags.In);
}
