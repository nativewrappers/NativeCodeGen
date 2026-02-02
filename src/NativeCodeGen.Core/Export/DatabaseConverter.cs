using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Core.Export;

/// <summary>
/// Converts internal models to export models for serialization.
/// </summary>
public static class DatabaseConverter
{
    public static ExportDatabase Convert(NativeDatabase db, ExportOptions options)
    {
        var export = new ExportDatabase();

        // Convert namespaces and natives
        var filteredNamespaces = options.Namespaces != null
            ? db.Namespaces.Where(ns => options.Namespaces.Contains(ns.Name))
            : db.Namespaces;

        foreach (var ns in filteredNamespaces)
        {
            var exportNs = new ExportNamespace { Name = ns.Name };

            foreach (var native in ns.Natives)
            {
                exportNs.Natives.Add(ConvertNative(native));
            }

            export.Namespaces.Add(exportNs);
        }

        // Convert enums
        if (options.IncludeEnums)
        {
            foreach (var (name, enumDef) in db.Enums)
            {
                export.Enums.Add(ConvertEnum(enumDef));
            }
        }

        // Convert structs
        if (options.IncludeStructs)
        {
            foreach (var (name, structDef) in db.Structs)
            {
                export.Structs.Add(ConvertStruct(structDef));
            }
        }

        // Convert shared examples - referenced examples first
        var referencedExamples = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ns in db.Namespaces)
        {
            foreach (var native in ns.Natives)
            {
                foreach (var exampleName in native.RelatedExamples)
                {
                    referencedExamples.Add(exampleName);
                }
            }
        }

        // Add referenced examples first, then the rest
        foreach (var name in referencedExamples)
        {
            if (db.SharedExamples.TryGetValue(name, out var example))
            {
                export.SharedExamples.Add(ConvertSharedExample(example));
            }
        }

        foreach (var (name, example) in db.SharedExamples)
        {
            if (!referencedExamples.Contains(name))
            {
                export.SharedExamples.Add(ConvertSharedExample(example));
            }
        }

        // Add type definitions
        foreach (var (name, typeInfo) in TypeRegistry.GetTypeDefinitions())
        {
            export.Types.Add(new ExportTypeEntry { Name = name, Type = typeInfo });
        }

        return export;
    }

    private static ExportNative ConvertNative(NativeDefinition native)
    {
        return new ExportNative
        {
            Name = native.Name,
            Hash = native.Hash,
            Namespace = native.Namespace,
            Description = native.Description,
            ReturnType = native.ReturnType.ToString(),
            ReturnDescription = native.ReturnDescription,
            Aliases = native.Aliases.Count > 0 ? native.Aliases : null,
            RelatedExamples = native.RelatedExamples.Count > 0 ? native.RelatedExamples : null,
            ApiSet = native.ApiSet,
            Parameters = native.Parameters.Select(ConvertParameter).ToList()
        };
    }

    private static ExportParameter ConvertParameter(NativeParameter param)
    {
        return new ExportParameter
        {
            Name = param.Name,
            Type = param.Type.ToString(),
            Description = param.Description,
            Flags = param.Flags,
            DefaultValue = param.DefaultValue
        };
    }

    private static ExportEnum ConvertEnum(EnumDefinition enumDef)
    {
        return new ExportEnum
        {
            Name = enumDef.Name,
            BaseType = enumDef.BaseType,
            Members = enumDef.Members.Select(m => new ExportEnumMember
            {
                Name = m.Name,
                Value = m.Value
            }).ToList(),
            UsedByNatives = enumDef.UsedByNatives.Count > 0
                ? enumDef.UsedByNatives
                : null
        };
    }

    private static ExportStruct ConvertStruct(StructDefinition structDef)
    {
        return new ExportStruct
        {
            Name = structDef.Name,
            DefaultAlignment = structDef.DefaultAlignment,
            UsedByNatives = structDef.UsedByNatives.Count > 0
                ? structDef.UsedByNatives
                : null,
            Fields = structDef.Fields.Select(ConvertStructField).ToList()
        };
    }

    private static ExportStructField ConvertStructField(StructField f)
    {
        return new ExportStructField
        {
            Name = f.Name,
            Type = f.Type.ToString(),
            Comment = f.Comment,
            Flags = f.Flags,
            ArraySize = f.ArraySize,
            NestedStructName = f.IsNestedStruct ? f.NestedStructName : null,
            Alignment = f.Alignment
        };
    }

    private static ExportSharedExample ConvertSharedExample(Models.SharedExample example)
    {
        return new ExportSharedExample
        {
            Name = example.Name,
            Title = example.Title,
            Examples = example.Examples.Select(e => new ExportSharedExampleCode
            {
                Content = e.Content,
                Language = e.Language
            }).ToList()
        };
    }
}
