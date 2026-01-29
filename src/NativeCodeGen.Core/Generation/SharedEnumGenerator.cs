using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Generation;

/// <summary>
/// Shared enum generator that works with any language via ILanguageEmitter.
/// </summary>
public class SharedEnumGenerator
{
    private readonly ILanguageEmitter _emitter;

    public SharedEnumGenerator(ILanguageEmitter emitter)
    {
        _emitter = emitter;
    }

    public string Generate(EnumDefinition enumDef)
    {
        var cb = new CodeBuilder();

        _emitter.EmitEnumStart(cb, enumDef.Name);

        foreach (var member in enumDef.Members)
        {
            _emitter.EmitEnumMember(cb, member.Name, member.Value, member.Comment);
        }

        _emitter.EmitEnumEnd(cb, enumDef.Name);

        return cb.ToString();
    }

    public void GenerateFile(EnumDefinition enumDef, string outputDir)
    {
        var content = Generate(enumDef);
        var filePath = Path.Combine(outputDir, $"{enumDef.Name}{_emitter.FileExtension}");

        Directory.CreateDirectory(outputDir);
        File.WriteAllText(filePath, content);
    }
}
