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
}
