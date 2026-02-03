using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Registry;
using NativeCodeGen.Core.Validation;

namespace NativeCodeGen.Tests.Validation;

public class TypeValidatorTests
{
    private readonly EnumRegistry _emptyEnumRegistry;
    private readonly StructRegistry _emptyStructRegistry;
    private readonly TypeValidator _validator;

    public TypeValidatorTests()
    {
        _emptyEnumRegistry = new EnumRegistry();
        _emptyStructRegistry = new StructRegistry();
        _validator = new TypeValidator(_emptyEnumRegistry, _emptyStructRegistry);
    }

    #region Unknown Struct Types

    [Fact]
    public void ValidateNative_UnknownStructInParameter_ReturnsError()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "void", Category = TypeCategory.Void },
            Parameters = new List<NativeParameter>
            {
                new()
                {
                    Name = "data",
                    Type = new TypeInfo { Name = "UnknownStruct", Category = TypeCategory.Struct }
                }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Single(errors);
        Assert.Contains("Unknown struct type", errors[0].Message);
        Assert.Contains("UnknownStruct", errors[0].Message);
        Assert.Contains("parameter 'data'", errors[0].Message);
    }

    [Fact]
    public void ValidateNative_UnknownStructInReturnType_ReturnsError()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "UnknownStruct", Category = TypeCategory.Struct },
            Parameters = new List<NativeParameter>()
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Single(errors);
        Assert.Contains("Unknown struct type", errors[0].Message);
        Assert.Contains("UnknownStruct", errors[0].Message);
        Assert.Contains("return type", errors[0].Message);
    }

    [Fact]
    public void ValidateNative_MultipleUnknownStructs_ReturnsMultipleErrors()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "UnknownReturn", Category = TypeCategory.Struct },
            Parameters = new List<NativeParameter>
            {
                new() { Name = "a", Type = new TypeInfo { Name = "UnknownA", Category = TypeCategory.Struct } },
                new() { Name = "b", Type = new TypeInfo { Name = "UnknownB", Category = TypeCategory.Struct } }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Equal(3, errors.Count);
    }

    #endregion

    #region Unknown Enum Types

    [Fact]
    public void ValidateNative_UnknownEnumInParameter_ReturnsError()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "void", Category = TypeCategory.Void },
            Parameters = new List<NativeParameter>
            {
                new()
                {
                    Name = "flags",
                    Type = new TypeInfo { Name = "UnknownEnum", Category = TypeCategory.Enum }
                }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Single(errors);
        Assert.Contains("Unknown enum type", errors[0].Message);
        Assert.Contains("UnknownEnum", errors[0].Message);
    }

    [Fact]
    public void ValidateNative_UnknownEnumInReturnType_ReturnsError()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "UnknownEnum", Category = TypeCategory.Enum },
            Parameters = new List<NativeParameter>()
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Single(errors);
        Assert.Contains("Unknown enum type", errors[0].Message);
        Assert.Contains("return type", errors[0].Message);
    }

    #endregion

    #region Known Types (No Errors)

    [Fact]
    public void ValidateNative_VoidReturnWithPrimitives_NoErrors()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "void", Category = TypeCategory.Void },
            Parameters = new List<NativeParameter>
            {
                new() { Name = "a", Type = new TypeInfo { Name = "int", Category = TypeCategory.Primitive } },
                new() { Name = "b", Type = new TypeInfo { Name = "float", Category = TypeCategory.Primitive } },
                new() { Name = "c", Type = new TypeInfo { Name = "BOOL", Category = TypeCategory.Primitive } }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateNative_HandleTypes_NoErrors()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "Entity", Category = TypeCategory.Handle },
            Parameters = new List<NativeParameter>
            {
                new() { Name = "ped", Type = new TypeInfo { Name = "Ped", Category = TypeCategory.Handle } },
                new() { Name = "vehicle", Type = new TypeInfo { Name = "Vehicle", Category = TypeCategory.Handle } }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateNative_Vector3Type_NoErrors()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "Vector3", Category = TypeCategory.Vector3 },
            Parameters = new List<NativeParameter>
            {
                new() { Name = "pos", Type = new TypeInfo { Name = "Vector3", Category = TypeCategory.Vector3 } }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Empty(errors);
    }

    #endregion

    #region Suggestions for Typos

    [Fact]
    public void ValidateNative_TypeoInTypeName_ProvidesSuggestions()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "void", Category = TypeCategory.Void },
            Parameters = new List<NativeParameter>
            {
                new() { Name = "pos", Type = new TypeInfo { Name = "Vestor3", Category = TypeCategory.Struct } }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Single(errors);
        Assert.Contains("Did you mean", errors[0].Message);
        Assert.Contains("Vector3", errors[0].Message);
    }

    [Fact]
    public void ValidateNative_EntityTypo_ProvidesSuggestions()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "void", Category = TypeCategory.Void },
            Parameters = new List<NativeParameter>
            {
                new() { Name = "e", Type = new TypeInfo { Name = "Entitty", Category = TypeCategory.Struct } }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Single(errors);
        Assert.Contains("Entity", errors[0].Message);
    }

    [Fact]
    public void ValidateNative_BooleanTypo_ProvidesSuggestions()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "void", Category = TypeCategory.Void },
            Parameters = new List<NativeParameter>
            {
                new() { Name = "flag", Type = new TypeInfo { Name = "Boolean", Category = TypeCategory.Struct } }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Single(errors);
        Assert.Contains("BOOL", errors[0].Message);
    }

    #endregion

    #region Default Value Validation

    [Theory]
    [InlineData("int", "0", null)]
    [InlineData("int", "123", null)]
    [InlineData("int", "-1", null)]
    [InlineData("int", "0x1F", null)]
    [InlineData("u32", "0", null)]
    [InlineData("float", "0", null)]
    [InlineData("float", "1.5", null)]
    [InlineData("float", "-3.14f", null)]
    [InlineData("float", "1e-5", null)]
    [InlineData("BOOL", "true", null)]
    [InlineData("BOOL", "false", null)]
    [InlineData("BOOL", "0", null)]
    [InlineData("BOOL", "1", null)]
    public void ValidateDefaultForType_ValidPrimitiveDefaults_ReturnsNull(string typeName, string defaultValue, string? expected)
    {
        var type = new TypeInfo { Name = typeName, Category = TypeCategory.Primitive };
        var result = TypeValidator.ValidateDefaultForType(type, defaultValue, "parameter 'test'");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("int", "abc")]
    [InlineData("int", "1.5")]
    [InlineData("float", "abc")]
    [InlineData("BOOL", "yes")]
    [InlineData("BOOL", "2")]
    public void ValidateDefaultForType_InvalidPrimitiveDefaults_ReturnsError(string typeName, string defaultValue)
    {
        var type = new TypeInfo { Name = typeName, Category = TypeCategory.Primitive };
        var result = TypeValidator.ValidateDefaultForType(type, defaultValue, "parameter 'test'");
        Assert.NotNull(result);
        Assert.Contains("invalid default", result);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("nullptr")]
    [InlineData("NULL")]
    public void ValidateDefaultForType_HandleTypeWithNullValues_ReturnsNull(string defaultValue)
    {
        var type = new TypeInfo { Name = "Entity", Category = TypeCategory.Handle };
        var result = TypeValidator.ValidateDefaultForType(type, defaultValue, "parameter 'entity'");
        Assert.Null(result);
    }

    [Fact]
    public void ValidateDefaultForType_HandleTypeWithNonZero_ReturnsError()
    {
        var type = new TypeInfo { Name = "Entity", Category = TypeCategory.Handle };
        var result = TypeValidator.ValidateDefaultForType(type, "1", "parameter 'entity'");
        Assert.NotNull(result);
        Assert.Contains("invalid default", result);
        Assert.Contains("'0', 'nullptr', or 'NULL'", result);
    }

    [Fact]
    public void ValidateDefaultForType_ClassHandleWithNonZero_ProvidesSuggestion()
    {
        var type = new TypeInfo { Name = "Ped", Category = TypeCategory.Handle };
        var result = TypeValidator.ValidateDefaultForType(type, "123", "parameter 'ped'");
        Assert.NotNull(result);
        Assert.Contains("will be converted to null", result);
    }

    [Theory]
    [InlineData("\"model\"", null)]
    [InlineData("'model'", null)]
    [InlineData("0", null)]
    [InlineData("0x12345678", null)]
    public void ValidateDefaultForType_ValidHashDefaults_ReturnsNull(string defaultValue, string? expected)
    {
        var type = new TypeInfo { Name = "Hash", Category = TypeCategory.Hash };
        var result = TypeValidator.ValidateDefaultForType(type, defaultValue, "parameter 'hash'");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidateDefaultForType_InvalidHashDefault_ReturnsError()
    {
        var type = new TypeInfo { Name = "Hash", Category = TypeCategory.Hash };
        var result = TypeValidator.ValidateDefaultForType(type, "invalid", "parameter 'hash'");
        Assert.NotNull(result);
        Assert.Contains("invalid default", result);
    }

    [Theory]
    [InlineData("\"hello\"", null)]
    [InlineData("nullptr", null)]
    [InlineData("NULL", null)]
    [InlineData("0", null)]
    public void ValidateDefaultForType_ValidStringDefaults_ReturnsNull(string defaultValue, string? expected)
    {
        var type = new TypeInfo { Name = "char", IsPointer = true, Category = TypeCategory.String };
        var result = TypeValidator.ValidateDefaultForType(type, defaultValue, "parameter 'str'");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidateDefaultForType_InvalidStringDefault_ReturnsError()
    {
        var type = new TypeInfo { Name = "char", IsPointer = true, Category = TypeCategory.String };
        var result = TypeValidator.ValidateDefaultForType(type, "unquoted", "parameter 'str'");
        Assert.NotNull(result);
        Assert.Contains("invalid default", result);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("SomeEnumMember")]
    public void ValidateDefaultForType_ValidEnumDefaults_ReturnsNull(string defaultValue)
    {
        var type = new TypeInfo { Name = "MyEnum", Category = TypeCategory.Enum };
        var result = TypeValidator.ValidateDefaultForType(type, defaultValue, "parameter 'flag'");
        Assert.Null(result);
    }

    [Fact]
    public void ValidateDefaultForType_VectorType_ReturnsError()
    {
        var type = new TypeInfo { Name = "Vector3", Category = TypeCategory.Vector3 };
        var result = TypeValidator.ValidateDefaultForType(type, "0", "parameter 'pos'");
        Assert.NotNull(result);
        Assert.Contains("should not have a default value", result);
    }

    [Fact]
    public void ValidateNative_ParameterWithInvalidDefault_ReturnsError()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "void", Category = TypeCategory.Void },
            Parameters = new List<NativeParameter>
            {
                new()
                {
                    Name = "entity",
                    Type = new TypeInfo { Name = "Entity", Category = TypeCategory.Handle },
                    DefaultValue = "123" // Invalid - should be 0
                }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Single(errors);
        Assert.Contains("invalid default", errors[0].Message);
    }

    [Fact]
    public void ValidateNative_ParameterWithValidDefault_NoError()
    {
        var native = new NativeDefinition
        {
            Name = "TEST_NATIVE",
            Hash = "0x123",
            ReturnType = new TypeInfo { Name = "void", Category = TypeCategory.Void },
            Parameters = new List<NativeParameter>
            {
                new()
                {
                    Name = "entity",
                    Type = new TypeInfo { Name = "Entity", Category = TypeCategory.Handle },
                    DefaultValue = "0" // Valid null handle
                }
            }
        };

        var errors = _validator.ValidateNative(native, "test.mdx");

        Assert.Empty(errors);
    }

    #endregion
}
