using NativeCodeGen.Core.Export;
using ProtoBuf;

namespace NativeCodeGen.Tests.Export;

public class ProtobufSerializationTests
{
    [Fact]
    public void SerializeAndDeserialize_EmptyDatabase_RoundTrips()
    {
        var original = new ExportDatabase();

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        Assert.NotNull(deserialized);
        Assert.Empty(deserialized.Namespaces);
        Assert.Empty(deserialized.Enums);
        Assert.Empty(deserialized.Structs);
        Assert.Empty(deserialized.SharedExamples);
    }

    [Fact]
    public void SerializeAndDeserialize_WithNatives_RoundTrips()
    {
        var original = new ExportDatabase
        {
            Namespaces = new List<ExportNamespace>
            {
                new ExportNamespace
                {
                    Name = "ENTITY",
                    Natives = new List<ExportNative>
                    {
                        new ExportNative
                        {
                            Name = "GET_ENTITY_COORDS",
                            Hash = "0xA86D5F069399F44D",
                            Namespace = "ENTITY",
                            Description = "Gets entity coordinates",
                            ReturnType = "Vector3",
                            ReturnDescription = "The coordinates",
                            ApiSet = "client",
                            Parameters = new List<ExportParameter>
                            {
                                new ExportParameter
                                {
                                    Name = "entity",
                                    Type = "Entity",
                                    Description = "The entity handle"
                                },
                                new ExportParameter
                                {
                                    Name = "alive",
                                    Type = "BOOL",
                                    Description = "Whether entity is alive",
                                    DefaultValue = "true"
                                }
                            },
                            Aliases = new List<string> { "0xA86D5F069399F44D" }
                        }
                    }
                }
            }
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        Assert.Single(deserialized.Namespaces);
        var ns = deserialized.Namespaces[0];
        Assert.Equal("ENTITY", ns.Name);
        Assert.Single(ns.Natives);

        var native = ns.Natives[0];
        Assert.Equal("GET_ENTITY_COORDS", native.Name);
        Assert.Equal("0xA86D5F069399F44D", native.Hash);
        Assert.Equal("Vector3", native.ReturnType);
        Assert.Equal(2, native.Parameters.Count);
        Assert.Equal("entity", native.Parameters[0].Name);
        Assert.Equal("true", native.Parameters[1].DefaultValue);
        Assert.Single(native.Aliases!);
    }

    [Fact]
    public void SerializeAndDeserialize_WithEnums_RoundTrips()
    {
        var original = new ExportDatabase
        {
            Enums = new List<Core.Export.ExportEnum>
            {
                new Core.Export.ExportEnum
                {
                    Name = "eWeaponHash",
                    BaseType = "Hash",
                    Members = new List<ExportEnumMember>
                    {
                        new ExportEnumMember { Name = "WEAPON_PISTOL", Value = "0x1234" },
                        new ExportEnumMember { Name = "WEAPON_RIFLE", Value = "0x5678" }
                    }
                }
            }
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        Assert.Single(deserialized.Enums);
        var enumDef = deserialized.Enums[0];
        Assert.Equal("eWeaponHash", enumDef.Name);
        Assert.Equal("Hash", enumDef.BaseType);
        Assert.Equal(2, enumDef.Members.Count);
        Assert.Equal("WEAPON_PISTOL", enumDef.Members[0].Name);
        Assert.Equal("0x1234", enumDef.Members[0].Value);
    }

    [Fact]
    public void SerializeAndDeserialize_WithStructs_RoundTrips()
    {
        var original = new ExportDatabase
        {
            Structs = new List<ExportStruct>
            {
                new ExportStruct
                {
                    Name = "scrItemInfo",
                    DefaultAlignment = 8,
                    Fields = new List<ExportStructField>
                    {
                        new ExportStructField
                        {
                            Name = "hash",
                            Type = "Hash",
                            Flags = FieldFlags.In
                        },
                        new ExportStructField
                        {
                            Name = "data",
                            Type = "int",
                            ArraySize = 4
                        },
                        new ExportStructField
                        {
                            Name = "_padding",
                            Type = "char",
                            Flags = FieldFlags.Padding,
                            ArraySize = 4
                        }
                    },
                    UsedByNatives = new List<ExportNativeReference>
                    {
                        new ExportNativeReference { Name = "GET_ITEM_INFO", Hash = "0xABCD" }
                    }
                }
            }
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        Assert.Single(deserialized.Structs);
        var structDef = deserialized.Structs[0];
        Assert.Equal("scrItemInfo", structDef.Name);
        Assert.Equal(8, structDef.DefaultAlignment);
        Assert.Equal(3, structDef.Fields.Count);
        Assert.True(structDef.Fields[0].Flags.HasFlag(FieldFlags.In));
        Assert.Equal(4, structDef.Fields[1].ArraySize);
        Assert.True(structDef.Fields[2].Flags.HasFlag(FieldFlags.Padding));
        Assert.Single(structDef.UsedByNatives!);
    }

    [Fact]
    public void SerializeAndDeserialize_WithSharedExamples_RoundTrips()
    {
        var original = new ExportDatabase
        {
            SharedExamples = new List<ExportSharedExample>
            {
                new ExportSharedExample
                {
                    Name = "CreatePed",
                    Content = "local ped = CreatePed(...)",
                    Language = "lua"
                }
            }
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        Assert.Single(deserialized.SharedExamples);
        var example = deserialized.SharedExamples[0];
        Assert.Equal("CreatePed", example.Name);
        Assert.Equal("local ped = CreatePed(...)", example.Content);
        Assert.Equal("lua", example.Language);
    }

    [Fact]
    public void SerializeAndDeserialize_WithOutputParameters_RoundTrips()
    {
        var original = new ExportDatabase
        {
            Namespaces = new List<ExportNamespace>
            {
                new ExportNamespace
                {
                    Name = "MISC",
                    Natives = new List<ExportNative>
                    {
                        new ExportNative
                        {
                            Name = "GET_GROUND_Z",
                            Hash = "0x1234",
                            Namespace = "MISC",
                            ReturnType = "BOOL",
                            Parameters = new List<ExportParameter>
                            {
                                new ExportParameter { Name = "x", Type = "float", Flags = ParamFlags.None },
                                new ExportParameter { Name = "y", Type = "float", Flags = ParamFlags.None },
                                new ExportParameter { Name = "z", Type = "float*", Flags = ParamFlags.Output }
                            }
                        }
                    }
                }
            }
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        var native = deserialized.Namespaces[0].Natives[0];
        Assert.False(native.Parameters[0].Flags.HasFlag(ParamFlags.Output));
        Assert.False(native.Parameters[1].Flags.HasFlag(ParamFlags.Output));
        Assert.True(native.Parameters[2].Flags.HasFlag(ParamFlags.Output));
    }

    [Fact]
    public void SerializeAndDeserialize_WithFlags_RoundTrips()
    {
        var original = new ExportDatabase
        {
            Namespaces = new List<ExportNamespace>
            {
                new ExportNamespace
                {
                    Name = "PED",
                    Natives = new List<ExportNative>
                    {
                        new ExportNative
                        {
                            Name = "SET_PED_NAME",
                            Hash = "0x5678",
                            Namespace = "PED",
                            ReturnType = "void",
                            Parameters = new List<ExportParameter>
                            {
                                new ExportParameter
                                {
                                    Name = "ped",
                                    Type = "Ped",
                                    Flags = ParamFlags.This
                                },
                                new ExportParameter
                                {
                                    Name = "name",
                                    Type = "char*",
                                    Flags = ParamFlags.NotNull
                                }
                            }
                        }
                    }
                }
            }
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        var native = deserialized.Namespaces[0].Natives[0];
        Assert.True(native.Parameters[0].Flags.HasFlag(ParamFlags.This));
        Assert.True(native.Parameters[1].Flags.HasFlag(ParamFlags.NotNull));
    }

    [Fact]
    public void SerializeAndDeserialize_NestedStruct_RoundTrips()
    {
        var original = new ExportDatabase
        {
            Structs = new List<ExportStruct>
            {
                new ExportStruct
                {
                    Name = "OuterStruct",
                    Fields = new List<ExportStructField>
                    {
                        new ExportStructField
                        {
                            Name = "inner",
                            Type = "InnerStruct",
                            NestedStructName = "InnerStruct"
                        }
                    }
                }
            }
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        var field = deserialized.Structs[0].Fields[0];
        Assert.Equal("InnerStruct", field.NestedStructName);
    }

    [Fact]
    public void SerializeAndDeserialize_LargeDatabase_RoundTrips()
    {
        var original = new ExportDatabase();

        // Create multiple namespaces with multiple natives
        for (int i = 0; i < 10; i++)
        {
            var ns = new ExportNamespace { Name = $"NAMESPACE_{i}" };
            for (int j = 0; j < 100; j++)
            {
                ns.Natives.Add(new ExportNative
                {
                    Name = $"NATIVE_{i}_{j}",
                    Hash = $"0x{i:X4}{j:X4}",
                    Namespace = ns.Name,
                    ReturnType = "void"
                });
            }
            original.Namespaces.Add(ns);
        }

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        Assert.Equal(10, deserialized.Namespaces.Count);
        Assert.Equal(100, deserialized.Namespaces[0].Natives.Count);
        Assert.Equal("NATIVE_5_50", deserialized.Namespaces[5].Natives[50].Name);
    }
}
