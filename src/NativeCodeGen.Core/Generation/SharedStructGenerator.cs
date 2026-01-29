using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Shared struct generator that works with any language via ILanguageEmitter.
/// Contains all the layout calculation and field organization logic.
/// </summary>
public class SharedStructGenerator
{
    private readonly ILanguageEmitter _emitter;
    private readonly StructLayoutCalculator _layoutCalculator = new();

    public IReadOnlyList<string> Warnings => _layoutCalculator.Warnings;

    public SharedStructGenerator(ILanguageEmitter emitter)
    {
        _emitter = emitter;
    }

    public void ClearWarnings() => _layoutCalculator.ClearWarnings();

    public void SetStructRegistry(Dictionary<string, StructDefinition> registry)
    {
        _layoutCalculator.SetStructRegistry(registry);
    }

    public string Generate(StructDefinition structDef)
    {
        var cb = new CodeBuilder();
        var layout = _layoutCalculator.CalculateLayout(structDef);

        // Collect nested struct imports
        var nestedStructs = structDef.Fields
            .Where(f => f.IsNestedStruct && f.NestedStructName != null)
            .Select(f => f.NestedStructName!)
            .Distinct()
            .ToList();

        _emitter.EmitStructStart(cb, structDef.Name, layout.TotalSize, nestedStructs);
        _emitter.EmitStructDocumentation(cb, structDef);
        _emitter.EmitStructConstructor(cb, structDef.Name, layout.TotalSize, supportsNesting: true);

        // Generate field accessors
        foreach (var fieldLayout in layout.Fields)
        {
            var field = fieldLayout.Field;
            if (field.IsPadding) continue;

            var fieldName = StructLayoutCalculator.ConvertFieldName(field.Name);

            if (field.IsNestedStruct)
            {
                _emitter.EmitNestedStructAccessor(
                    cb, structDef.Name, fieldName, field.NestedStructName!,
                    fieldLayout.Offset, field.IsArray, field.ArraySize, field.Comment);
            }
            else if (field.IsArray)
            {
                if (field.IsOutput)
                {
                    _emitter.EmitArrayGetter(
                        cb, structDef.Name, fieldName, fieldLayout.Offset,
                        fieldLayout.Alignment, field.ArraySize, field.Type, field.Comment);
                }
                if (field.IsInput)
                {
                    _emitter.EmitArraySetter(
                        cb, structDef.Name, fieldName, fieldLayout.Offset,
                        fieldLayout.Alignment, field.ArraySize, field.Type);
                }
            }
            else
            {
                if (field.IsOutput)
                {
                    _emitter.EmitPrimitiveGetter(
                        cb, structDef.Name, fieldName, fieldLayout.Offset, field.Type, field.Comment);
                }
                if (field.IsInput)
                {
                    _emitter.EmitPrimitiveSetter(
                        cb, structDef.Name, fieldName, fieldLayout.Offset, field.Type);
                }
            }
        }

        _emitter.EmitStructEnd(cb, structDef.Name);

        return cb.ToString();
    }

    public void GenerateFile(StructDefinition structDef, string outputDir)
    {
        var content = Generate(structDef);
        var filePath = Path.Combine(outputDir, $"{structDef.Name}{_emitter.FileExtension}");

        Directory.CreateDirectory(outputDir);
        File.WriteAllText(filePath, content);
    }
}
