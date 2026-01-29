using System.Text.Json;
using System.Text.Json.Serialization;
using NativeCodeGen.Core.Export;

namespace NativeCodeGen.Tests.Export;

public class JsonSerializationTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Serialize_Native_ProducesExpectedJson()
    {
        var native = new ExportNative
        {
            Name = "GET_ENTITY_COORDS",
            Hash = "0xA86D5F069399F44D",
            Namespace = "ENTITY",
            Description = "Gets entity coordinates",
            ReturnType = "Vector3",
            ApiSet = "client",
            Parameters = new List<ExportParameter>
            {
                new ExportParameter { Name = "entity", Type = "Entity" }
            }
        };

        var json = JsonSerializer.Serialize(native, _jsonOptions);

        Assert.Contains("\"name\":", json);
        Assert.Contains("\"hash\":", json);
        Assert.Contains("\"ns\":", json); // Namespace uses JsonPropertyName("ns")
        Assert.Contains("\"returnType\":", json);
        Assert.Contains("\"apiset\":", json);
    }

    [Fact]
    public void Serialize_Enum_OmitsTopLevelNameInJson()
    {
        var enumDef = new Core.Export.ExportEnum
        {
            Name = "eWeaponHash",
            BaseType = "Hash",
            Members = new List<ExportEnumMember>
            {
                new ExportEnumMember { Name = "WEAPON_PISTOL", Value = "0x1234" }
            }
        };

        var json = JsonSerializer.Serialize(enumDef, _jsonOptions);

        // The top-level Enum.Name should be omitted (it's used as dictionary key)
        // But EnumMember.Name should still be present
        Assert.DoesNotContain("\"eWeaponHash\"", json); // The actual name value shouldn't appear as a value
        Assert.Contains("\"baseType\":", json);
        Assert.Contains("\"members\":", json);
        Assert.Contains("\"WEAPON_PISTOL\"", json); // Member name IS included
    }

    [Fact]
    public void Serialize_Struct_OmitsTopLevelNameInJson()
    {
        var structDef = new ExportStruct
        {
            Name = "scrItemInfo",
            DefaultAlignment = 8,
            Fields = new List<ExportStructField>
            {
                new ExportStructField { Name = "hash", Type = "Hash" }
            }
        };

        var json = JsonSerializer.Serialize(structDef, _jsonOptions);

        // The top-level Struct.Name should be omitted (it's used as dictionary key)
        // But StructField.Name should still be present
        Assert.DoesNotContain("\"scrItemInfo\"", json); // The actual struct name shouldn't appear as a value
        Assert.Contains("\"defaultAlignment\":", json);
        Assert.Contains("\"fields\":", json);
        Assert.Contains("\"hash\"", json); // Field name IS included
    }

    [Fact]
    public void Serialize_SharedExample_OmitsTopLevelNameInJson()
    {
        var example = new ExportSharedExample
        {
            Name = "CreatePed",
            Content = "local ped = CreatePed(...)",
            Language = "lua"
        };

        var json = JsonSerializer.Serialize(example, _jsonOptions);

        // The top-level SharedExample.Name should be omitted (it's used as dictionary key)
        Assert.DoesNotContain("\"CreatePed\"", json); // The actual name value shouldn't appear
        Assert.Contains("\"content\":", json);
        Assert.Contains("\"language\":", json);
    }

    [Fact]
    public void Serialize_NullOptionalFields_AreOmitted()
    {
        var native = new ExportNative
        {
            Name = "TEST",
            Hash = "0x1234",
            Namespace = "TEST",
            ReturnType = "void",
            // Description, Aliases, UsedEnums, RelatedExamples are null
        };

        var json = JsonSerializer.Serialize(native, _jsonOptions);

        Assert.DoesNotContain("\"description\":", json);
        Assert.DoesNotContain("\"aliases\":", json);
        Assert.DoesNotContain("\"relatedExamples\":", json);
    }

    [Fact]
    public void Serialize_EmptyListFields_AreOmitted()
    {
        var native = new ExportNative
        {
            Name = "TEST",
            Hash = "0x1234",
            Namespace = "TEST",
            ReturnType = "void",
            Aliases = null // Should be omitted
        };

        var json = JsonSerializer.Serialize(native, _jsonOptions);

        Assert.DoesNotContain("\"aliases\":", json);
    }

    [Fact]
    public void Serialize_Parameter_WithDefaultValue()
    {
        var param = new ExportParameter
        {
            Name = "alive",
            Type = "BOOL",
            DefaultValue = "true"
        };

        var json = JsonSerializer.Serialize(param, _jsonOptions);

        Assert.Contains("\"defaultValue\":", json);
        Assert.Contains("\"true\"", json);
    }

    [Fact]
    public void Serialize_Parameter_WithFlags()
    {
        var param = new ExportParameter
        {
            Name = "ped",
            Type = "Ped",
            Flags = ParamFlags.This | ParamFlags.NotNull
        };

        var json = JsonSerializer.Serialize(param, _jsonOptions);

        Assert.Contains("\"flags\":", json);
        // Flags are serialized as integer (6 = This | NotNull = 2 | 4)
        Assert.Contains("6", json);
    }

    [Fact]
    public void Serialize_StructField_WithArrayInfo()
    {
        var field = new ExportStructField
        {
            Name = "data",
            Type = "int",
            ArraySize = 4
        };

        var json = JsonSerializer.Serialize(field, _jsonOptions);

        // Check arraySize is serialized
        Assert.Contains("\"arraySize\":", json);
        Assert.Contains("4", json);
        // flags should be omitted when 0 (None)
        Assert.DoesNotContain("\"flags\":", json);
    }

    [Fact]
    public void DeserializeAndSerialize_Native_RoundTrips()
    {
        var original = new ExportNative
        {
            Name = "GET_ENTITY_COORDS",
            Hash = "0xA86D5F069399F44D",
            Namespace = "ENTITY",
            Description = "Gets entity coordinates",
            ReturnType = "Vector3",
            ApiSet = "client",
            Parameters = new List<ExportParameter>
            {
                new ExportParameter { Name = "entity", Type = "Entity" }
            },
            Aliases = new List<string> { "0xA86D5F069399F44D" }
        };

        var json = JsonSerializer.Serialize(original, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<ExportNative>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Hash, deserialized.Hash);
        Assert.Equal(original.Namespace, deserialized.Namespace);
        Assert.Equal(original.Description, deserialized.Description);
        Assert.Equal(original.ReturnType, deserialized.ReturnType);
        Assert.Single(deserialized.Parameters);
        Assert.Single(deserialized.Aliases!);
    }

    [Fact]
    public void JsonOutput_Structure_IsCorrect()
    {
        var output = new JsonOutput
        {
            Natives = new List<ExportNative>
            {
                new ExportNative { Name = "TEST", Hash = "0x1234", Namespace = "TEST", ReturnType = "void" }
            },
            Enums = new Dictionary<string, Core.Export.ExportEnum>
            {
                ["eTest"] = new Core.Export.ExportEnum { Name = "eTest", Members = new List<ExportEnumMember>() }
            },
            Structs = new Dictionary<string, ExportStruct>
            {
                ["TestStruct"] = new ExportStruct { Name = "TestStruct", Fields = new List<ExportStructField>() }
            },
            SharedExamples = new Dictionary<string, ExportSharedExample>
            {
                ["Example1"] = new ExportSharedExample { Name = "Example1", Content = "code" }
            }
        };

        var json = JsonSerializer.Serialize(output, _jsonOptions);

        Assert.Contains("\"natives\":", json);
        Assert.Contains("\"enums\":", json);
        Assert.Contains("\"structs\":", json);
        Assert.Contains("\"sharedExamples\":", json);

        // Verify structure: enums/structs/examples should be objects with keys
        Assert.Contains("\"eTest\":", json);
        Assert.Contains("\"TestStruct\":", json);
        Assert.Contains("\"Example1\":", json);
    }
}
