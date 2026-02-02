using System.Reflection;
using NativeCodeGen.Core.Export;
using NativeCodeGen.Core.Models;
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
                    UsedByNatives = new List<string> { "0xABCD" }
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
                    Title = "Creating a Ped",
                    Examples = new List<ExportSharedExampleCode>
                    {
                        new() { Content = "local ped = CreatePed(...)", Language = "lua" },
                        new() { Content = "const ped = CreatePed(...)", Language = "js" }
                    }
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
        Assert.Equal("Creating a Ped", example.Title);
        Assert.Equal(2, example.Examples.Count);
        Assert.Equal("local ped = CreatePed(...)", example.Examples[0].Content);
        Assert.Equal("lua", example.Examples[0].Language);
        Assert.Equal("const ped = CreatePed(...)", example.Examples[1].Content);
        Assert.Equal("js", example.Examples[1].Language);
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
                                    Flags = ParamFlags.Nullable
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
        Assert.True(native.Parameters[1].Flags.HasFlag(ParamFlags.Nullable));
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

    [Fact]
    public void SerializeAndDeserialize_WithTypes_RoundTrips()
    {
        var original = new ExportDatabase
        {
            Types = new List<ExportTypeEntry>
            {
                new ExportTypeEntry
                {
                    Name = "int",
                    Type = new ExportTypeInfo
                    {
                        Category = ExportTypeCategory.Primitive,
                        NativeType = "int",
                        Description = "32-bit signed integer"
                    }
                },
                new ExportTypeEntry
                {
                    Name = "Entity",
                    Type = new ExportTypeInfo
                    {
                        Category = ExportTypeCategory.Handle,
                        NativeType = "int",
                        Description = "Base type for all world entities"
                    }
                },
                new ExportTypeEntry
                {
                    Name = "Vector3",
                    Type = new ExportTypeInfo
                    {
                        Category = ExportTypeCategory.Vector3,
                        Description = "3D vector (x, y, z floats)"
                    }
                }
            }
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        Assert.Equal(3, deserialized.Types.Count);

        var intType = deserialized.Types[0];
        Assert.Equal("int", intType.Name);
        Assert.Equal(ExportTypeCategory.Primitive, intType.Type.Category);
        Assert.Equal("int", intType.Type.NativeType);
        Assert.Equal("32-bit signed integer", intType.Type.Description);

        var entityType = deserialized.Types[1];
        Assert.Equal("Entity", entityType.Name);
        Assert.Equal(ExportTypeCategory.Handle, entityType.Type.Category);

        var vectorType = deserialized.Types[2];
        Assert.Equal("Vector3", vectorType.Name);
        Assert.Equal(ExportTypeCategory.Vector3, vectorType.Type.Category);
        Assert.Null(vectorType.Type.NativeType);
    }

    [Fact]
    public void SerializeAndDeserialize_AllTypeCategories_RoundTrips()
    {
        var categories = Enum.GetValues<ExportTypeCategory>();
        var original = new ExportDatabase
        {
            Types = categories.Select((cat, i) => new ExportTypeEntry
            {
                Name = $"Type_{cat}",
                Type = new ExportTypeInfo { Category = cat }
            }).ToList()
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        Assert.Equal(categories.Length, deserialized.Types.Count);
        for (int i = 0; i < categories.Length; i++)
        {
            Assert.Equal(categories[i], deserialized.Types[i].Type.Category);
        }
    }

    [Fact]
    public void SerializeAndDeserialize_CombinedParamFlags_RoundTrips()
    {
        var original = new ExportDatabase
        {
            Namespaces = new List<ExportNamespace>
            {
                new ExportNamespace
                {
                    Name = "TEST",
                    Natives = new List<ExportNative>
                    {
                        new ExportNative
                        {
                            Name = "TEST_NATIVE",
                            Hash = "0x1234",
                            Namespace = "TEST",
                            ReturnType = "void",
                            Parameters = new List<ExportParameter>
                            {
                                // Test combined flags: Output | In (value 9)
                                new ExportParameter
                                {
                                    Name = "inOutParam",
                                    Type = "int*",
                                    Flags = ParamFlags.Output | ParamFlags.In
                                },
                                // Test combined flags: This | Nullable (value 6)
                                new ExportParameter
                                {
                                    Name = "thisNullable",
                                    Type = "Entity",
                                    Flags = ParamFlags.This | ParamFlags.Nullable
                                },
                                // Test all flags combined (value 15)
                                new ExportParameter
                                {
                                    Name = "allFlags",
                                    Type = "int*",
                                    Flags = ParamFlags.Output | ParamFlags.This | ParamFlags.Nullable | ParamFlags.In
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

        // Verify combined flags are preserved
        Assert.Equal(ParamFlags.Output | ParamFlags.In, native.Parameters[0].Flags);
        Assert.True(native.Parameters[0].Flags.HasFlag(ParamFlags.Output));
        Assert.True(native.Parameters[0].Flags.HasFlag(ParamFlags.In));
        Assert.False(native.Parameters[0].Flags.HasFlag(ParamFlags.This));

        Assert.Equal(ParamFlags.This | ParamFlags.Nullable, native.Parameters[1].Flags);

        Assert.Equal(ParamFlags.Output | ParamFlags.This | ParamFlags.Nullable | ParamFlags.In, native.Parameters[2].Flags);
        Assert.Equal((ParamFlags)15, native.Parameters[2].Flags);
    }

    [Fact]
    public void SerializeAndDeserialize_CombinedFieldFlags_RoundTrips()
    {
        var original = new ExportDatabase
        {
            Structs = new List<ExportStruct>
            {
                new ExportStruct
                {
                    Name = "TestStruct",
                    Fields = new List<ExportStructField>
                    {
                        // Test combined flags: In | Out (value 3) - bidirectional
                        new ExportStructField
                        {
                            Name = "inOutField",
                            Type = "int",
                            Flags = FieldFlags.In | FieldFlags.Out
                        },
                        // Test combined flags: In | Padding (value 5) - unusual but valid
                        new ExportStructField
                        {
                            Name = "inPadding",
                            Type = "char",
                            Flags = FieldFlags.In | FieldFlags.Padding
                        },
                        // Test all flags combined (value 7)
                        new ExportStructField
                        {
                            Name = "allFlags",
                            Type = "int",
                            Flags = FieldFlags.In | FieldFlags.Out | FieldFlags.Padding
                        }
                    }
                }
            }
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var deserialized = Serializer.Deserialize<ExportDatabase>(stream);

        var structDef = deserialized.Structs[0];

        // Verify combined flags are preserved
        Assert.Equal(FieldFlags.In | FieldFlags.Out, structDef.Fields[0].Flags);
        Assert.True(structDef.Fields[0].Flags.HasFlag(FieldFlags.In));
        Assert.True(structDef.Fields[0].Flags.HasFlag(FieldFlags.Out));
        Assert.False(structDef.Fields[0].Flags.HasFlag(FieldFlags.Padding));

        Assert.Equal(FieldFlags.In | FieldFlags.Padding, structDef.Fields[1].Flags);

        Assert.Equal(FieldFlags.In | FieldFlags.Out | FieldFlags.Padding, structDef.Fields[2].Flags);
        Assert.Equal((FieldFlags)7, structDef.Fields[2].Flags);
    }

    /// <summary>
    /// Verifies that all public properties on ProtoContract classes have ProtoMember attributes.
    /// This test will fail if someone adds a new property but forgets the protobuf attribute.
    /// </summary>
    [Fact]
    public void AllProtoContractClasses_HaveProtoMemberOnAllProperties()
    {
        var assembly = typeof(ExportDatabase).Assembly;
        var protoContractTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ProtoContractAttribute>() != null)
            .ToList();

        var errors = new List<string>();

        foreach (var type in protoContractTypes)
        {
            var publicProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite) // Only read-write properties need ProtoMember
                .ToList();

            foreach (var prop in publicProperties)
            {
                var hasProtoMember = prop.GetCustomAttribute<ProtoMemberAttribute>() != null;
                if (!hasProtoMember)
                {
                    errors.Add($"{type.Name}.{prop.Name} is missing [ProtoMember] attribute");
                }
            }
        }

        if (errors.Count > 0)
        {
            Assert.Fail($"Found properties without [ProtoMember] attribute:\n{string.Join("\n", errors)}");
        }
    }

    /// <summary>
    /// Verifies that ProtoMember IDs are unique within each class.
    /// </summary>
    [Fact]
    public void AllProtoContractClasses_HaveUniqueProtoMemberIds()
    {
        var assembly = typeof(ExportDatabase).Assembly;
        var protoContractTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ProtoContractAttribute>() != null)
            .ToList();

        var errors = new List<string>();

        foreach (var type in protoContractTypes)
        {
            var protoMembers = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new { Property = p, Attr = p.GetCustomAttribute<ProtoMemberAttribute>() })
                .Where(x => x.Attr != null)
                .ToList();

            var duplicateIds = protoMembers
                .GroupBy(x => x.Attr!.Tag)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var dup in duplicateIds)
            {
                var propNames = string.Join(", ", dup.Select(x => x.Property.Name));
                errors.Add($"{type.Name} has duplicate ProtoMember ID {dup.Key}: {propNames}");
            }
        }

        if (errors.Count > 0)
        {
            Assert.Fail($"Found duplicate ProtoMember IDs:\n{string.Join("\n", errors)}");
        }
    }

    /// <summary>
    /// Verifies that all enums used as property types in ProtoContract classes also have ProtoContract.
    /// This ensures flag enums and other enum types are properly decorated for protobuf serialization.
    /// </summary>
    [Fact]
    public void AllEnumsUsedInProtoContracts_HaveProtoContractAttribute()
    {
        var assembly = typeof(ExportDatabase).Assembly;
        var protoContractTypes = assembly.GetTypes()
            .Where(t => t.IsClass && t.GetCustomAttribute<ProtoContractAttribute>() != null)
            .ToList();

        var errors = new List<string>();
        var checkedEnums = new HashSet<Type>();

        foreach (var type in protoContractTypes)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ProtoMemberAttribute>() != null)
                .ToList();

            foreach (var prop in properties)
            {
                var propType = prop.PropertyType;

                // Handle nullable types
                var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;

                if (underlyingType.IsEnum && !checkedEnums.Contains(underlyingType))
                {
                    checkedEnums.Add(underlyingType);

                    if (underlyingType.GetCustomAttribute<ProtoContractAttribute>() == null)
                    {
                        errors.Add($"Enum {underlyingType.Name} (used in {type.Name}.{prop.Name}) is missing [ProtoContract] attribute");
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            Assert.Fail($"Found enums without [ProtoContract] attribute:\n{string.Join("\n", errors)}");
        }
    }

    /// <summary>
    /// Lists all ProtoContract types for documentation/verification purposes.
    /// </summary>
    [Fact]
    public void ListAllProtoContractTypes()
    {
        var assembly = typeof(ExportDatabase).Assembly;
        var protoContractTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ProtoContractAttribute>() != null)
            .OrderBy(t => t.IsEnum ? 0 : 1)
            .ThenBy(t => t.Name)
            .ToList();

        // This test just verifies we have the expected types
        var expectedClasses = new[]
        {
            "ExportDatabase", "ExportNamespace", "ExportNative", "ExportParameter",
            "ExportEnum", "ExportEnumMember", "ExportStruct", "ExportStructField",
            "ExportSharedExample", "ExportSharedExampleCode",
            "ExportTypeInfo", "ExportTypeEntry", "ExportCallout"
        };

        var expectedEnums = new[]
        {
            "ParamFlags", "FieldFlags", "ExportTypeCategory", "CalloutType"
        };

        foreach (var expected in expectedClasses)
        {
            Assert.Contains(protoContractTypes, t => t.Name == expected && t.IsClass);
        }

        foreach (var expected in expectedEnums)
        {
            Assert.Contains(protoContractTypes, t => t.Name == expected && t.IsEnum);
        }

        // Total count check
        Assert.Equal(expectedClasses.Length + expectedEnums.Length, protoContractTypes.Count);
    }
}
