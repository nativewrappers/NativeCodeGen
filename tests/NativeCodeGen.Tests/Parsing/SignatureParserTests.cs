using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Tests.Parsing;

public class SignatureParserTests
{
    private static (TypeInfo returnType, string name, List<NativeParameter> parameters) Parse(string signature)
    {
        var lexer = new SignatureLexer(signature);
        var tokens = lexer.Tokenize();
        var parser = new SignatureParser(tokens, "test.mdx");
        return parser.ParseSignature();
    }

    [Fact]
    public void Parse_InAttributeOnIntPointer_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@in int* value);");

        Assert.Single(parameters);
        Assert.True(parameters[0].Attributes.IsIn);
        Assert.True(parameters[0].Type.IsPointer);
    }

    [Fact]
    public void Parse_InAttributeOnFloatPointer_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@in float* value);");

        Assert.Single(parameters);
        Assert.True(parameters[0].Attributes.IsIn);
    }

    [Fact]
    public void Parse_InAttributeOnEntityPointer_Succeeds()
    {
        var (_, _, parameters) = Parse("void DELETE_ENTITY(@in Entity* entity);");

        Assert.Single(parameters);
        Assert.True(parameters[0].Attributes.IsIn);
        Assert.Equal(TypeCategory.Handle, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_InAttributeOnPedPointer_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@in Ped* ped);");

        Assert.Single(parameters);
        Assert.True(parameters[0].Attributes.IsIn);
        Assert.Equal(TypeCategory.Handle, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_InAttributeOnVector3Pointer_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@in Vector3* coords);");

        Assert.Single(parameters);
        Assert.True(parameters[0].Attributes.IsIn);
        Assert.Equal(TypeCategory.Vector3, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_InAttributeOnStructPointer_ThrowsException()
    {
        var ex = Assert.Throws<ParseException>(() => Parse("void TEST(@in SomeStruct* data);"));

        Assert.Contains("struct", ex.Message.ToLower());
        Assert.Contains("@in", ex.Message);
    }

    [Fact]
    public void Parse_InAttributeOnNonPointer_ThrowsException()
    {
        var ex = Assert.Throws<ParseException>(() => Parse("void TEST(@in int value);"));

        Assert.Contains("pointer", ex.Message.ToLower());
    }

    [Fact]
    public void Parse_MultipleThisAttributes_ThrowsException()
    {
        var ex = Assert.Throws<ParseException>(() => Parse("void TEST(@this Entity a, @this Ped b);"));

        Assert.Contains("@this", ex.Message);
        Assert.Contains("Multiple", ex.Message);
    }

    [Fact]
    public void Parse_SingleThisAttribute_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(Ped ped, @this Entity entity);");

        Assert.Equal(2, parameters.Count);
        Assert.False(parameters[0].Attributes.IsThis);
        Assert.True(parameters[1].Attributes.IsThis);
    }

    [Fact]
    public void Parse_ThisAttributeOnFirstParam_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@this Entity entity, int value);");

        Assert.Equal(2, parameters.Count);
        Assert.True(parameters[0].Attributes.IsThis);
        Assert.False(parameters[1].Attributes.IsThis);
    }

    [Fact]
    public void Parse_NotnullAttribute_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@notnull char* name);");

        Assert.Single(parameters);
        Assert.True(parameters[0].Attributes.IsNotNull);
    }

    [Fact]
    public void Parse_CombinedAttributes_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@this Entity entity, @notnull char* name, @in int* outValue);");

        Assert.Equal(3, parameters.Count);
        Assert.True(parameters[0].Attributes.IsThis);
        Assert.True(parameters[1].Attributes.IsNotNull);
        Assert.True(parameters[2].Attributes.IsIn);
    }
}
