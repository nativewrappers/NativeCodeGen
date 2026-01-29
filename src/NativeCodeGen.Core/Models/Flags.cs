using ProtoBuf;

namespace NativeCodeGen.Core.Models;

/// <summary>
/// Bitflags for native parameter attributes.
/// </summary>
[Flags]
[ProtoContract]
public enum ParamFlags
{
    None = 0,
    /// <summary>
    /// Pointer output parameter (value returned via pointer).
    /// Computed from type being a pointer (excluding strings and structs).
    /// </summary>
    Output = 1,
    /// <summary>
    /// @this - use as instance method receiver in generated wrappers.
    /// </summary>
    This = 2,
    /// <summary>
    /// @notnull - string parameter cannot be null.
    /// </summary>
    NotNull = 4,
    /// <summary>
    /// @in - input+output pointer (uses initialized value, not pure output).
    /// When set with Output, the parameter appears in method signature.
    /// </summary>
    In = 8
}

/// <summary>
/// Bitflags for struct field attributes.
/// </summary>
[Flags]
[ProtoContract]
public enum FieldFlags
{
    None = 0,
    /// <summary>
    /// @in - setter only (input to native). No getter generated.
    /// </summary>
    In = 1,
    /// <summary>
    /// @out - getter only (output from native). No setter generated.
    /// </summary>
    Out = 2,
    /// <summary>
    /// @padding - no accessors generated, reserves space in buffer.
    /// </summary>
    Padding = 4
}

/// <summary>
/// Extension methods for flag enums.
/// </summary>
public static class FlagExtensions
{
    /// <summary>
    /// Check if this is a pure output parameter (Output set, In not set).
    /// </summary>
    public static bool IsPureOutput(this ParamFlags flags) =>
        flags.HasFlag(ParamFlags.Output) && !flags.HasFlag(ParamFlags.In);

    /// <summary>
    /// Check if this is an input+output parameter (both Output and In set).
    /// </summary>
    public static bool IsInOut(this ParamFlags flags) =>
        flags.HasFlag(ParamFlags.Output) && flags.HasFlag(ParamFlags.In);

    /// <summary>
    /// Check if field has both getter and setter (neither In-only nor Out-only nor Padding).
    /// </summary>
    public static bool HasFullAccess(this FieldFlags flags) =>
        !flags.HasFlag(FieldFlags.Padding) &&
        !(flags.HasFlag(FieldFlags.In) && !flags.HasFlag(FieldFlags.Out)) &&
        !(flags.HasFlag(FieldFlags.Out) && !flags.HasFlag(FieldFlags.In));

    /// <summary>
    /// Check if field should have a getter (Out-only or full access, not padding).
    /// </summary>
    public static bool HasGetter(this FieldFlags flags) =>
        !flags.HasFlag(FieldFlags.Padding) &&
        (!flags.HasFlag(FieldFlags.In) || flags.HasFlag(FieldFlags.Out));

    /// <summary>
    /// Check if field should have a setter (In-only or full access, not padding).
    /// </summary>
    public static bool HasSetter(this FieldFlags flags) =>
        !flags.HasFlag(FieldFlags.Padding) &&
        (!flags.HasFlag(FieldFlags.Out) || flags.HasFlag(FieldFlags.In));
}
