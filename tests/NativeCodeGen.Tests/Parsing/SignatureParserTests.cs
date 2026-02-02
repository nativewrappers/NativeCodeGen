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
        Assert.True(parameters[0].IsIn);
        Assert.True(parameters[0].Type.IsPointer);
    }

    [Fact]
    public void Parse_InAttributeOnFloatPointer_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@in float* value);");

        Assert.Single(parameters);
        Assert.True(parameters[0].IsIn);
    }

    [Fact]
    public void Parse_InAttributeOnEntityPointer_Succeeds()
    {
        var (_, _, parameters) = Parse("void DELETE_ENTITY(@in Entity* entity);");

        Assert.Single(parameters);
        Assert.True(parameters[0].IsIn);
        Assert.Equal(TypeCategory.Handle, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_InAttributeOnPedPointer_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@in Ped* ped);");

        Assert.Single(parameters);
        Assert.True(parameters[0].IsIn);
        Assert.Equal(TypeCategory.Handle, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_InAttributeOnVector3Pointer_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@in Vector3* coords);");

        Assert.Single(parameters);
        Assert.True(parameters[0].IsIn);
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
        Assert.False(parameters[0].IsThis);
        Assert.True(parameters[1].IsThis);
    }

    [Fact]
    public void Parse_ThisAttributeOnFirstParam_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@this Entity entity, int value);");

        Assert.Equal(2, parameters.Count);
        Assert.True(parameters[0].IsThis);
        Assert.False(parameters[1].IsThis);
    }

    [Fact]
    public void Parse_NullableAttribute_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@nullable char* name);");

        Assert.Single(parameters);
        Assert.True(parameters[0].IsNullable);
    }

    [Fact]
    public void Parse_CombinedAttributes_Succeeds()
    {
        var (_, _, parameters) = Parse("void TEST(@this Entity entity, @nullable char* name, @in int* outValue);");

        Assert.Equal(3, parameters.Count);
        Assert.True(parameters[0].IsThis);
        Assert.True(parameters[1].IsNullable);
        Assert.True(parameters[2].IsIn);
    }

    #region Unknown Attribute Tests

    [Fact]
    public void Parse_UnknownAttribute_ThrowsException()
    {
        var ex = Assert.Throws<ParseException>(() => Parse("void TEST(@unknown int value);"));

        Assert.Contains("Unknown attribute", ex.Message);
        Assert.Contains("@unknown", ex.Message);
        Assert.Contains("@this", ex.Message); // Should list valid attributes
    }

    [Fact]
    public void Parse_TypoInAttribute_ThrowsException()
    {
        var ex = Assert.Throws<ParseException>(() => Parse("void TEST(@thsi Entity entity);"));

        Assert.Contains("Unknown attribute", ex.Message);
        Assert.Contains("@thsi", ex.Message);
    }

    [Fact]
    public void Parse_MissingAtSign_NotAttribute()
    {
        // 'nullable' without @ is treated as a type name, not attribute
        var ex = Assert.Throws<ParseException>(() => Parse("void TEST(nullable char* name);"));

        // Should fail because it expects parameter name after type
        Assert.Contains("Expected", ex.Message);
    }

    #endregion

    #region Type Categorization Tests

    [Fact]
    public void Parse_PrimitiveTypes_CorrectCategory()
    {
        var (_, _, parameters) = Parse("void TEST(int a, float b, BOOL c, double d, long e);");

        Assert.All(parameters, p => Assert.Equal(TypeCategory.Primitive, p.Type.Category));
    }

    [Fact]
    public void Parse_HandleTypes_CorrectCategory()
    {
        var (_, _, parameters) = Parse("void TEST(Entity e, Ped p, Vehicle v, Object o);");

        Assert.All(parameters, p => Assert.Equal(TypeCategory.Handle, p.Type.Category));
    }

    [Fact]
    public void Parse_Vector3Type_CorrectCategory()
    {
        var (returnType, _, parameters) = Parse("Vector3 TEST(Vector3 pos);");

        Assert.Equal(TypeCategory.Vector3, returnType.Category);
        Assert.Equal(TypeCategory.Vector3, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_Vector2Type_CorrectCategory()
    {
        var (returnType, _, _) = Parse("Vector2 TEST();");

        Assert.Equal(TypeCategory.Vector2, returnType.Category);
    }

    [Fact]
    public void Parse_Vector4Type_CorrectCategory()
    {
        var (returnType, _, _) = Parse("Vector4 TEST();");

        Assert.Equal(TypeCategory.Vector4, returnType.Category);
    }

    [Fact]
    public void Parse_ColorType_CorrectCategory()
    {
        var (returnType, _, _) = Parse("Color TEST();");

        Assert.Equal(TypeCategory.Color, returnType.Category);
    }

    [Fact]
    public void Parse_HashType_CorrectCategory()
    {
        var (_, _, parameters) = Parse("void TEST(Hash hash);");

        Assert.Equal(TypeCategory.Hash, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_StringTypes_CorrectCategory()
    {
        var (_, _, parameters) = Parse("void TEST(char* s1, string s2);");

        Assert.All(parameters, p => Assert.Equal(TypeCategory.String, p.Type.Category));
    }

    [Fact]
    public void Parse_UnknownType_CategorizedAsStruct()
    {
        var (_, _, parameters) = Parse("void TEST(SomeCustomType data);");

        Assert.Equal(TypeCategory.Struct, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_AllNonClassHandles_CorrectCategory()
    {
        var (_, _, parameters) = Parse("void TEST(ScrHandle a, Prompt b, FireId c, Blip d, PopZone e, PedGroup f);");

        Assert.All(parameters, p => Assert.Equal(TypeCategory.Handle, p.Type.Category));
    }

    #endregion

    #region Pointer Type Tests

    [Fact]
    public void Parse_IntPointer_MarkedAsOutput()
    {
        var (_, _, parameters) = Parse("void TEST(int* outValue);");

        Assert.True(parameters[0].IsPureOutput);
        Assert.True(parameters[0].Type.IsPointer);
    }

    [Fact]
    public void Parse_FloatPointer_MarkedAsOutput()
    {
        var (_, _, parameters) = Parse("void TEST(float* outValue);");

        Assert.True(parameters[0].IsPureOutput);
    }

    [Fact]
    public void Parse_StringPointer_NotMarkedAsOutput()
    {
        var (_, _, parameters) = Parse("void TEST(char* name);");

        Assert.False(parameters[0].IsPureOutput);
        Assert.Equal(TypeCategory.String, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_StructPointer_NotMarkedAsOutput()
    {
        var (_, _, parameters) = Parse("void TEST(SomeStruct* data);");

        Assert.False(parameters[0].IsPureOutput);
        Assert.Equal(TypeCategory.Struct, parameters[0].Type.Category);
    }

    [Fact]
    public void Parse_Vector3Pointer_MarkedAsOutput()
    {
        var (_, _, parameters) = Parse("void TEST(Vector3* outPos);");

        Assert.True(parameters[0].IsPureOutput);
    }

    [Fact]
    public void Parse_HandlePointer_MarkedAsOutput()
    {
        var (_, _, parameters) = Parse("void TEST(Entity* outEntity);");

        Assert.True(parameters[0].IsPureOutput);
    }

    #endregion

    #region Variadic Parameter Tests

    [Fact]
    public void Parse_VariadicParameter_HasEllipsis()
    {
        var (_, _, parameters) = Parse("void TEST(char* format, variadic ...args);");

        Assert.Equal(2, parameters.Count);
        Assert.Equal("...args", parameters[1].Name);
    }

    [Fact]
    public void Parse_VariadicNoName_DefaultsToArgs()
    {
        var (_, _, parameters) = Parse("void TEST(char* format, variadic ...);");

        Assert.Equal("...args", parameters[1].Name);
    }

    #endregion

    #region Default Value Tests

    [Fact]
    public void Parse_DefaultIntValue_Parses()
    {
        var (_, _, parameters) = Parse("void TEST(int value = 0);");

        Assert.Equal("0", parameters[0].DefaultValue);
        Assert.True(parameters[0].HasDefaultValue);
    }

    [Fact]
    public void Parse_DefaultNegativeValue_Parses()
    {
        var (_, _, parameters) = Parse("void TEST(int value = -1);");

        Assert.Equal("-1", parameters[0].DefaultValue);
    }

    [Fact]
    public void Parse_DefaultFloatValue_Parses()
    {
        var (_, _, parameters) = Parse("void TEST(float value = 1.5f);");

        Assert.Equal("1.5f", parameters[0].DefaultValue);
    }

    [Fact]
    public void Parse_DefaultBoolValue_Parses()
    {
        var (_, _, parameters) = Parse("void TEST(BOOL flag = true);");

        Assert.Equal("true", parameters[0].DefaultValue);
    }

    [Fact]
    public void Parse_DefaultHashValue_Parses()
    {
        var (_, _, parameters) = Parse("void TEST(Hash model = `some_model`);");

        // Hash values have backticks stripped during lexing
        Assert.Equal("some_model", parameters[0].DefaultValue);
    }

    #endregion
}
