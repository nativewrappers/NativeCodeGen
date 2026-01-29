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

        // Convert shared examples
        foreach (var (name, example) in db.SharedExamples)
        {
            export.SharedExamples.Add(ConvertSharedExample(example));
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
            UsedEnums = native.UsedEnums.Count > 0 ? native.UsedEnums : null,
            RelatedExamples = native.RelatedExamples.Count > 0 ? native.RelatedExamples : null,
            ApiSet = native.ApiSet,
            Parameters = native.Parameters.Select(ConvertParameter).ToList()
        };
    }

    private static ExportParameter ConvertParameter(NativeParameter param)
    {
        var attrs = new List<string>();
        if (param.Attributes.IsThis) attrs.Add("@this");
        if (param.Attributes.IsNotNull) attrs.Add("@notnull");
        attrs.AddRange(param.Attributes.CustomAttributes);

        return new ExportParameter
        {
            Name = param.Name,
            Type = param.Type.ToString(),
            Description = param.Description,
            Attributes = attrs.Count > 0 ? attrs : null,
            DefaultValue = param.DefaultValue,
            IsOutput = param.IsOutput
        };
    }

    private static ExportEnum ConvertEnum(EnumDefinition enumDef)
    {
        return new ExportEnum
        {
            Name = enumDef.Name,
            BaseType = enumDef.BaseType,
            UsedByNatives = enumDef.UsedByNatives.Count > 0 ? enumDef.UsedByNatives : null,
            Members = enumDef.Members.Select(m => new ExportEnumMember
            {
                Name = m.Name,
                Value = m.Value
            }).ToList()
        };
    }

    private static ExportStruct ConvertStruct(StructDefinition structDef)
    {
        return new ExportStruct
        {
            Name = structDef.Name,
            DefaultAlignment = structDef.DefaultAlignment,
            UsedByNatives = structDef.UsedByNatives.Count > 0
                ? structDef.UsedByNatives.Select(u => new ExportNativeReference
                {
                    Name = u.Name,
                    Hash = u.Hash
                }).ToList()
                : null,
            Fields = structDef.Fields.Select(f => new ExportStructField
            {
                Name = f.Name,
                Type = f.Type.ToString(),
                Comment = f.Comment,
                IsInput = f.IsInput,
                IsOutput = f.IsOutput,
                ArraySize = f.ArraySize,
                IsArray = f.IsArray,
                IsNestedStruct = f.IsNestedStruct,
                NestedStructName = f.NestedStructName,
                IsPadding = f.IsPadding,
                Alignment = f.Alignment
            }).ToList()
        };
    }

    private static ExportSharedExample ConvertSharedExample(Models.SharedExample example)
    {
        return new ExportSharedExample
        {
            Name = example.Name,
            Content = example.Content,
            Language = example.Language
        };
    }
}
