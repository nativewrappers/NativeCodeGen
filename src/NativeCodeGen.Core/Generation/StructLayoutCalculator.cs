using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Shared struct layout calculation and field utilities for all language generators.
/// </summary>
public class StructLayoutCalculator
{
    public const int DefaultFieldSize = 8; // Default alignment for native struct fields (pointer width)

    private Dictionary<string, StructDefinition> _structRegistry = new();
    private readonly List<string> _warnings = new();

    public IReadOnlyList<string> Warnings => _warnings;

    public void ClearWarnings() => _warnings.Clear();

    public void SetStructRegistry(Dictionary<string, StructDefinition> registry)
    {
        _structRegistry = registry;
    }

    /// <summary>
    /// Calculates the memory layout of a struct, including field offsets and total size.
    /// </summary>
    public StructLayout CalculateLayout(StructDefinition structDef)
    {
        var fields = new List<FieldLayout>();
        int currentOffset = 0;

        // Struct's default alignment (use DefaultFieldSize if not specified)
        var structAlignment = structDef.DefaultAlignment ?? DefaultFieldSize;

        foreach (var field in structDef.Fields)
        {
            // Field's alignment: field override > struct default > global default
            var fieldAlignment = field.Alignment ?? structAlignment;

            int fieldSize;
            if (field.IsNestedStruct && field.NestedStructName != null)
            {
                // Get nested struct size
                var nestedSize = GetStructSize(field.NestedStructName);
                fieldSize = field.IsArray ? nestedSize * field.ArraySize : nestedSize;
            }
            else if (field.IsPadding)
            {
                // Padding fields use actual byte size, optionally rounded to alignment
                var typeSize = GetTypeByteSize(field.Type);
                var rawSize = field.IsArray ? typeSize * field.ArraySize : typeSize;

                // If field has explicit alignment, round to that; otherwise use raw size
                if (field.Alignment.HasValue)
                {
                    fieldSize = RoundUpTo(rawSize, field.Alignment.Value);
                }
                else
                {
                    fieldSize = rawSize;
                }

                if (fieldSize != rawSize)
                {
                    _warnings.Add($"Struct '{structDef.Name}': Padding field '{field.Name}' size {rawSize} rounded up to {fieldSize} bytes");
                }
            }
            else if (field.IsArray)
            {
                fieldSize = fieldAlignment * field.ArraySize;
            }
            else
            {
                fieldSize = fieldAlignment;
            }

            fields.Add(new FieldLayout(field, currentOffset, fieldSize, fieldAlignment));
            currentOffset += fieldSize;
        }

        return new StructLayout(fields, currentOffset);
    }

    /// <summary>
    /// Gets the effective alignment for a field (for array element sizing in generated code).
    /// </summary>
    public int GetFieldAlignment(StructDefinition structDef, StructField field)
    {
        return field.Alignment ?? structDef.DefaultAlignment ?? DefaultFieldSize;
    }

    /// <summary>
    /// Gets the actual byte size of a primitive type.
    /// </summary>
    public static int GetTypeByteSize(TypeInfo type)
    {
        return type.Name switch
        {
            "u8" or "i8" or "bool" or "BOOL" or "char" => 1,
            "u16" or "i16" or "short" => 2,
            "u32" or "i32" or "int" or "uint" or "float" or "f32" or "Hash" => 4,
            "u64" or "i64" or "long" or "double" or "f64" => 8,
            _ => 8 // Default to pointer size
        };
    }

    /// <summary>
    /// Rounds a size up to the nearest multiple of the given alignment.
    /// </summary>
    public static int RoundUpTo(int size, int alignment)
    {
        if (alignment <= 1) return size;
        return (size + alignment - 1) / alignment * alignment;
    }

    /// <summary>
    /// Gets the size of a nested struct by name.
    /// </summary>
    public int GetStructSize(string structName)
    {
        if (_structRegistry.TryGetValue(structName, out var nestedDef))
        {
            return CalculateLayout(nestedDef).TotalSize;
        }
        // Default fallback
        return DefaultFieldSize;
    }

    /// <summary>
    /// Converts a C field name to a language-appropriate property name.
    /// Handles m_ prefix and converts to PascalCase.
    /// Examples:
    ///   m_inNumPelts -> InNumPelts
    ///   m_albedoHash -> AlbedoHash
    ///   someField -> SomeField
    /// </summary>
    public static string ConvertFieldName(string name)
    {
        // Remove m_ prefix if present
        if (name.StartsWith("m_"))
        {
            name = name[2..];
        }

        // Ensure first character is uppercase (PascalCase)
        if (name.Length > 0 && char.IsLower(name[0]))
        {
            name = char.ToUpperInvariant(name[0]) + name[1..];
        }

        return name;
    }

    /// <summary>
    /// Returns true if 8-bit operations don't need endianness argument.
    /// </summary>
    public static bool NeedsEndianArgument(TypeInfo type)
    {
        return type.Name switch
        {
            "u8" or "i8" or "bool" or "BOOL" => false,
            _ => true
        };
    }
}

/// <summary>
/// Represents the calculated layout of a struct.
/// </summary>
public record StructLayout(List<FieldLayout> Fields, int TotalSize);

/// <summary>
/// Represents the layout of a single field within a struct.
/// </summary>
public record FieldLayout(StructField Field, int Offset, int Size, int Alignment);
