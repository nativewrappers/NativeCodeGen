using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Tests.Parsing;

public class StructParserTests
{
    private readonly StructParser _parser = new();

    [Fact]
    public void Parse_SimpleStruct_ParsesCorrectly()
    {
        var content = """
            struct TestStruct {
                int value;
                float x;
                float y;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("TestStruct", result.Value!.Name);
        Assert.Equal(3, result.Value.Fields.Count);
        Assert.Equal("value", result.Value.Fields[0].Name);
        Assert.Equal("int", result.Value.Fields[0].Type.Name);
    }

    [Fact]
    public void Parse_StructWithArrays_ParsesCorrectly()
    {
        var content = """
            struct ArrayStruct {
                int data[4];
                char name[32];
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Fields[0].ArraySize);
        Assert.Equal(32, result.Value.Fields[1].ArraySize);
    }

    [Fact]
    public void Parse_StructWithPointers_ParsesCorrectly()
    {
        var content = """
            struct PointerStruct {
                int* ptr;
                char* str;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Fields[0].Type.IsPointer);
        Assert.True(result.Value.Fields[1].Type.IsPointer);
    }

    [Fact]
    public void Parse_StructWithInputOutput_ParsesCorrectly()
    {
        var content = """
            struct IOStruct {
                @in int input;
                @out int output;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Fields[0].IsInput);
        Assert.False(result.Value.Fields[0].IsOutput);
        Assert.False(result.Value.Fields[1].IsInput);
        Assert.True(result.Value.Fields[1].IsOutput);
    }

    [Fact]
    public void Parse_StructWithPadding_ParsesCorrectly()
    {
        var content = """
            struct PaddedStruct {
                int value;
                @padding char _padding[4];
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Fields[0].IsPadding);
        Assert.True(result.Value.Fields[1].IsPadding);
    }

    [Fact]
    public void Parse_StructWithAlignment_ParsesCorrectly()
    {
        var content = """
            @alignas(8)
            struct AlignedStruct {
                int value;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value!.DefaultAlignment);
    }

    [Fact]
    public void Parse_StructWithFieldAlignment_ParsesCorrectly()
    {
        var content = """
            struct FieldAlignStruct {
                @alignas(4) int value;
                @alignas(8) double bigValue;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Fields[0].Alignment);
        Assert.Equal(8, result.Value.Fields[1].Alignment);
    }

    [Fact]
    public void Parse_StructWithHashType_ParsesCorrectly()
    {
        var content = """
            struct HashStruct {
                Hash itemHash;
                Hash weaponHash;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("Hash", result.Value!.Fields[0].Type.Name);
        Assert.Equal("Hash", result.Value.Fields[1].Type.Name);
    }

    [Fact]
    public void Parse_StructWithVector3_ParsesCorrectly()
    {
        var content = """
            struct PositionStruct {
                Vector3 position;
                Vector3 rotation;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("Vector3", result.Value!.Fields[0].Type.Name);
    }

    [Fact]
    public void Parse_StructWithNestedStruct_ParsesCorrectly()
    {
        var content = """
            struct OuterStruct {
                struct InnerStruct inner;
                int value;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Fields[0].IsNestedStruct);
        Assert.Equal("InnerStruct", result.Value.Fields[0].NestedStructName);
    }

    [Fact]
    public void Parse_StructWithArrayExpression_ParsesCorrectly()
    {
        var content = """
            struct ExprStruct {
                int data[8 * 4];
                char buffer[16 + 8];
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal(32, result.Value!.Fields[0].ArraySize);
        Assert.Equal(24, result.Value.Fields[1].ArraySize);
    }

    [Fact]
    public void Parse_StructWithDocComment_ParsesComment()
    {
        var content = """
            struct CommentedStruct {
                /// This is a documented field
                int value;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.Fields[0].Comment);
        Assert.Contains("documented", result.Value.Fields[0].Comment);
    }

    // Error cases

    [Fact]
    public void Parse_EmptyContent_ReturnsError()
    {
        var result = _parser.Parse("", "test.c");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_NotAStruct_ReturnsError()
    {
        var content = """
            enum NotAStruct {
                VALUE = 0
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MissingStructName_ReturnsError()
    {
        var content = """
            struct {
                int value;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MissingOpenBrace_ReturnsError()
    {
        var content = """
            struct Test
                int value;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MissingCloseBrace_ReturnsError()
    {
        var content = """
            struct Test {
                int value;
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MissingSemicolonAfterField_ReturnsError()
    {
        var content = """
            struct Test {
                int value
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_InvalidArraySize_ReturnsError()
    {
        var content = """
            struct Test {
                int data[abc];
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ParseAll_MultipleStructs_ParsesAll()
    {
        var content = """
            struct First {
                int a;
            };

            struct Second {
                float b;
            };
            """;

        var results = _parser.ParseAll(content, "test.c");

        Assert.Equal(2, results.Count);
        Assert.Equal("First", results[0].Value!.Name);
        Assert.Equal("Second", results[1].Value!.Name);
    }
}
