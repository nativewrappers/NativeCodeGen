using NativeCodeGen.Core.Export;
using NativeCodeGen.Core.Models;
using ParsingDb = NativeCodeGen.Core.Parsing.NativeDatabase;
using ParsingNs = NativeCodeGen.Core.Parsing.NativeNamespace;
using ModelEnumMember = NativeCodeGen.Core.Models.EnumMember;
using ModelStructField = NativeCodeGen.Core.Models.StructField;
using ModelSharedExample = NativeCodeGen.Core.Models.SharedExample;

namespace NativeCodeGen.Tests.Export;

public class DatabaseConverterTests
{
    [Fact]
    public void Convert_EmptyDatabase_ReturnsEmptyExport()
    {
        var db = new ParsingDb();
        var options = new ExportOptions();

        var result = DatabaseConverter.Convert(db, options);

        Assert.Empty(result.Namespaces);
        Assert.Empty(result.Enums);
        Assert.Empty(result.Structs);
        Assert.Empty(result.SharedExamples);
    }

    [Fact]
    public void Convert_WithNatives_ConvertsCorrectly()
    {
        var db = new ParsingDb
        {
            Namespaces = new List<ParsingNs>
            {
                new ParsingNs
                {
                    Name = "ENTITY",
                    Natives = new List<NativeDefinition>
                    {
                        new NativeDefinition
                        {
                            Name = "GET_ENTITY_COORDS",
                            Hash = "0xA86D5F069399F44D",
                            Namespace = "ENTITY",
                            Description = "Gets coordinates",
                            ReturnType = TypeInfo.Parse("Vector3"),
                            ReturnDescription = "The coords",
                            ApiSet = "client",
                            Parameters = new List<NativeParameter>
                            {
                                new NativeParameter
                                {
                                    Name = "entity",
                                    Type = TypeInfo.Parse("Entity"),
                                    Description = "The entity"
                                }
                            },
                            Aliases = new List<string> { "0xA86D5F069399F44D" },
                            UsedEnums = new List<string> { "eEntityType" },
                            RelatedExamples = new List<string> { "CreatePed" }
                        }
                    }
                }
            }
        };
        var options = new ExportOptions();

        var result = DatabaseConverter.Convert(db, options);

        Assert.Single(result.Namespaces);
        var ns = result.Namespaces[0];
        Assert.Equal("ENTITY", ns.Name);
        Assert.Single(ns.Natives);

        var native = ns.Natives[0];
        Assert.Equal("GET_ENTITY_COORDS", native.Name);
        Assert.Equal("0xA86D5F069399F44D", native.Hash);
        Assert.Equal("ENTITY", native.Namespace);
        Assert.Equal("Gets coordinates", native.Description);
        Assert.Equal("Vector3", native.ReturnType);
        Assert.Equal("The coords", native.ReturnDescription);
        Assert.Equal("client", native.ApiSet);
        Assert.Single(native.Parameters);
        Assert.Single(native.Aliases!);
        Assert.Single(native.UsedEnums!);
        Assert.Single(native.RelatedExamples!);
    }

    [Fact]
    public void Convert_WithParameterAttributes_ConvertsCorrectly()
    {
        var db = new ParsingDb
        {
            Namespaces = new List<ParsingNs>
            {
                new ParsingNs
                {
                    Name = "PED",
                    Natives = new List<NativeDefinition>
                    {
                        new NativeDefinition
                        {
                            Name = "SET_PED_NAME",
                            Hash = "0x1234",
                            Namespace = "PED",
                            ReturnType = TypeInfo.Parse("void"),
                            Parameters = new List<NativeParameter>
                            {
                                new NativeParameter
                                {
                                    Name = "ped",
                                    Type = TypeInfo.Parse("Ped"),
                                    Attributes = new ParameterAttributes { IsThis = true }
                                },
                                new NativeParameter
                                {
                                    Name = "name",
                                    Type = TypeInfo.Parse("char*"),
                                    Attributes = new ParameterAttributes { IsNotNull = true }
                                },
                                new NativeParameter
                                {
                                    Name = "custom",
                                    Type = TypeInfo.Parse("int"),
                                    Attributes = new ParameterAttributes
                                    {
                                        CustomAttributes = new List<string> { "@custom1", "@custom2" }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        var options = new ExportOptions();

        var result = DatabaseConverter.Convert(db, options);

        var native = result.Namespaces[0].Natives[0];
        Assert.Contains("@this", native.Parameters[0].Attributes!);
        Assert.Contains("@notnull", native.Parameters[1].Attributes!);
        Assert.Contains("@custom1", native.Parameters[2].Attributes!);
        Assert.Contains("@custom2", native.Parameters[2].Attributes!);
    }

    [Fact]
    public void Convert_WithOutputParameter_SetsIsOutput()
    {
        var db = new ParsingDb
        {
            Namespaces = new List<ParsingNs>
            {
                new ParsingNs
                {
                    Name = "MISC",
                    Natives = new List<NativeDefinition>
                    {
                        new NativeDefinition
                        {
                            Name = "GET_GROUND_Z",
                            Hash = "0x1234",
                            Namespace = "MISC",
                            ReturnType = TypeInfo.Parse("BOOL"),
                            Parameters = new List<NativeParameter>
                            {
                                // Non-pointer = not output
                                new NativeParameter { Name = "x", Type = TypeInfo.Parse("float") },
                                // Pointer (non-string) = output
                                new NativeParameter { Name = "z", Type = TypeInfo.Parse("float*") }
                            }
                        }
                    }
                }
            }
        };
        var options = new ExportOptions();

        var result = DatabaseConverter.Convert(db, options);

        var native = result.Namespaces[0].Natives[0];
        Assert.False(native.Parameters[0].IsOutput);
        Assert.True(native.Parameters[1].IsOutput);
    }

    [Fact]
    public void Convert_WithDefaultValue_ConvertsCorrectly()
    {
        var db = new ParsingDb
        {
            Namespaces = new List<ParsingNs>
            {
                new ParsingNs
                {
                    Name = "TEST",
                    Natives = new List<NativeDefinition>
                    {
                        new NativeDefinition
                        {
                            Name = "TEST_NATIVE",
                            Hash = "0x1234",
                            Namespace = "TEST",
                            ReturnType = TypeInfo.Parse("void"),
                            Parameters = new List<NativeParameter>
                            {
                                new NativeParameter
                                {
                                    Name = "value",
                                    Type = TypeInfo.Parse("int"),
                                    DefaultValue = "-1"
                                }
                            }
                        }
                    }
                }
            }
        };
        var options = new ExportOptions();

        var result = DatabaseConverter.Convert(db, options);

        Assert.Equal("-1", result.Namespaces[0].Natives[0].Parameters[0].DefaultValue);
    }

    [Fact]
    public void Convert_WithEnums_ConvertsWhenEnabled()
    {
        var db = new ParsingDb
        {
            Enums = new Dictionary<string, EnumDefinition>
            {
                ["eWeaponHash"] = new EnumDefinition
                {
                    Name = "eWeaponHash",
                    BaseType = "Hash",
                    Members = new List<ModelEnumMember>
                    {
                        new ModelEnumMember { Name = "WEAPON_PISTOL", Value = "0x1234" }
                    },
                    UsedByNatives = new List<string> { "GET_WEAPON" }
                }
            }
        };
        var options = new ExportOptions { IncludeEnums = true };

        var result = DatabaseConverter.Convert(db, options);

        Assert.Single(result.Enums);
        var enumDef = result.Enums[0];
        Assert.Equal("eWeaponHash", enumDef.Name);
        Assert.Equal("Hash", enumDef.BaseType);
        Assert.Single(enumDef.Members);
        Assert.Single(enumDef.UsedByNatives!);
    }

    [Fact]
    public void Convert_WithEnums_ExcludesWhenDisabled()
    {
        var db = new ParsingDb
        {
            Enums = new Dictionary<string, EnumDefinition>
            {
                ["eWeaponHash"] = new EnumDefinition { Name = "eWeaponHash" }
            }
        };
        var options = new ExportOptions { IncludeEnums = false };

        var result = DatabaseConverter.Convert(db, options);

        Assert.Empty(result.Enums);
    }

    [Fact]
    public void Convert_WithStructs_ConvertsWhenEnabled()
    {
        var db = new ParsingDb
        {
            Structs = new Dictionary<string, StructDefinition>
            {
                ["scrItemInfo"] = new StructDefinition
                {
                    Name = "scrItemInfo",
                    DefaultAlignment = 8,
                    Fields = new List<ModelStructField>
                    {
                        new ModelStructField
                        {
                            Name = "hash",
                            Type = TypeInfo.Parse("Hash"),
                            IsInput = true
                        },
                        new ModelStructField
                        {
                            Name = "data",
                            Type = TypeInfo.Parse("int"),
                            ArraySize = 4  // IsArray is computed from ArraySize > 0
                        }
                    },
                    UsedByNatives = new List<(string Name, string Hash)>
                    {
                        ("GET_ITEM", "0xABCD")
                    }
                }
            }
        };
        var options = new ExportOptions { IncludeStructs = true };

        var result = DatabaseConverter.Convert(db, options);

        Assert.Single(result.Structs);
        var structDef = result.Structs[0];
        Assert.Equal("scrItemInfo", structDef.Name);
        Assert.Equal(8, structDef.DefaultAlignment);
        Assert.Equal(2, structDef.Fields.Count);
        Assert.True(structDef.Fields[0].IsInput);
        Assert.True(structDef.Fields[1].IsArray);
        Assert.Equal(4, structDef.Fields[1].ArraySize);
        Assert.Single(structDef.UsedByNatives!);
    }

    [Fact]
    public void Convert_WithStructs_ExcludesWhenDisabled()
    {
        var db = new ParsingDb
        {
            Structs = new Dictionary<string, StructDefinition>
            {
                ["scrItemInfo"] = new StructDefinition { Name = "scrItemInfo" }
            }
        };
        var options = new ExportOptions { IncludeStructs = false };

        var result = DatabaseConverter.Convert(db, options);

        Assert.Empty(result.Structs);
    }

    [Fact]
    public void Convert_WithSharedExamples_ConvertsCorrectly()
    {
        var db = new ParsingDb
        {
            SharedExamples = new Dictionary<string, ModelSharedExample>
            {
                ["CreatePed"] = new ModelSharedExample
                {
                    Name = "CreatePed",
                    Content = "local ped = CreatePed(...)",
                    Language = "lua"
                }
            }
        };
        var options = new ExportOptions();

        var result = DatabaseConverter.Convert(db, options);

        Assert.Single(result.SharedExamples);
        var example = result.SharedExamples[0];
        Assert.Equal("CreatePed", example.Name);
        Assert.Equal("local ped = CreatePed(...)", example.Content);
        Assert.Equal("lua", example.Language);
    }

    [Fact]
    public void Convert_WithNamespaceFilter_FiltersCorrectly()
    {
        var db = new ParsingDb
        {
            Namespaces = new List<ParsingNs>
            {
                new ParsingNs { Name = "ENTITY", Natives = new List<NativeDefinition>() },
                new ParsingNs { Name = "PED", Natives = new List<NativeDefinition>() },
                new ParsingNs { Name = "VEHICLE", Natives = new List<NativeDefinition>() }
            }
        };
        var options = new ExportOptions
        {
            Namespaces = new HashSet<string> { "ENTITY", "PED" }
        };

        var result = DatabaseConverter.Convert(db, options);

        Assert.Equal(2, result.Namespaces.Count);
        Assert.Contains(result.Namespaces, n => n.Name == "ENTITY");
        Assert.Contains(result.Namespaces, n => n.Name == "PED");
        Assert.DoesNotContain(result.Namespaces, n => n.Name == "VEHICLE");
    }

    [Fact]
    public void Convert_EmptyLists_ConvertToNull()
    {
        var db = new ParsingDb
        {
            Namespaces = new List<ParsingNs>
            {
                new ParsingNs
                {
                    Name = "TEST",
                    Natives = new List<NativeDefinition>
                    {
                        new NativeDefinition
                        {
                            Name = "TEST_NATIVE",
                            Hash = "0x1234",
                            Namespace = "TEST",
                            ReturnType = TypeInfo.Parse("void"),
                            Aliases = new List<string>(),
                            UsedEnums = new List<string>(),
                            RelatedExamples = new List<string>()
                        }
                    }
                }
            }
        };
        var options = new ExportOptions();

        var result = DatabaseConverter.Convert(db, options);

        var native = result.Namespaces[0].Natives[0];
        Assert.Null(native.Aliases);
        Assert.Null(native.UsedEnums);
        Assert.Null(native.RelatedExamples);
    }
}
