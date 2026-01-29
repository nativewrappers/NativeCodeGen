using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.TypeSystem;

namespace NativeCodeGen.Tests.TypeSystem;

public class TypeMapperTests
{
    private readonly TypeMapper _mapper = new();

    [Theory]
    [InlineData("int", "number")]
    [InlineData("float", "number")]
    [InlineData("double", "number")]
    [InlineData("u32", "number")]
    [InlineData("i32", "number")]
    [InlineData("f32", "number")]
    [InlineData("BOOL", "boolean")]
    [InlineData("bool", "boolean")]
    public void MapToTypeScript_Primitives_ReturnsNumber(string typeName, string expected)
    {
        var typeInfo = TypeInfo.Parse(typeName);
        var result = _mapper.MapToTypeScript(typeInfo);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapToTypeScript_Void_ReturnsVoid()
    {
        var typeInfo = TypeInfo.Parse("void");
        var result = _mapper.MapToTypeScript(typeInfo);
        Assert.Equal("void", result);
    }

    [Fact]
    public void MapToTypeScript_Hash_ReturnsNumber()
    {
        var typeInfo = TypeInfo.Parse("Hash");
        var result = _mapper.MapToTypeScript(typeInfo);
        Assert.Equal("number", result);
    }

    [Fact]
    public void MapToTypeScript_Vector3_ReturnsVector3()
    {
        var typeInfo = TypeInfo.Parse("Vector3");
        var result = _mapper.MapToTypeScript(typeInfo);
        Assert.Equal("Vector3", result);
    }

    [Fact]
    public void MapToTypeScript_Any_ReturnsAny()
    {
        var typeInfo = TypeInfo.Parse("Any");
        var result = _mapper.MapToTypeScript(typeInfo);
        Assert.Equal("any", result);
    }

    [Fact]
    public void MapToTypeScript_NullableString_ReturnsUnion()
    {
        var typeInfo = TypeInfo.Parse("char*");
        var result = _mapper.MapToTypeScript(typeInfo, isNotNull: false);
        Assert.Equal("string | null", result);
    }

    [Fact]
    public void MapToTypeScript_NotNullString_ReturnsString()
    {
        var typeInfo = TypeInfo.Parse("char*");
        var result = _mapper.MapToTypeScript(typeInfo, isNotNull: true);
        Assert.Equal("string", result);
    }

    [Theory]
    [InlineData("Entity")]
    [InlineData("Ped")]
    [InlineData("Vehicle")]
    [InlineData("Blip")]
    [InlineData("Cam")]
    [InlineData("Player")]
    public void MapToTypeScript_HandleTypes_ReturnsSameName(string typeName)
    {
        var typeInfo = TypeInfo.Parse(typeName);
        var result = _mapper.MapToTypeScript(typeInfo);
        Assert.Equal(typeName, result);
    }

    [Fact]
    public void MapToTypeScript_Object_ReturnsProp()
    {
        var typeInfo = TypeInfo.Parse("Object");
        var result = _mapper.MapToTypeScript(typeInfo);
        Assert.Equal("Prop", result);
    }

    [Fact]
    public void MapToTypeScript_PointerType_ReturnsBaseType()
    {
        var typeInfo = TypeInfo.Parse("int*");
        var result = _mapper.MapToTypeScript(typeInfo);
        Assert.Equal("number", result);
    }

    [Fact]
    public void IsHandleType_ForHandles_ReturnsTrue()
    {
        var typeInfo = TypeInfo.Parse("Entity");
        Assert.True(_mapper.IsHandleType(typeInfo));
    }

    [Fact]
    public void IsHandleType_ForPrimitives_ReturnsFalse()
    {
        var typeInfo = TypeInfo.Parse("int");
        Assert.False(_mapper.IsHandleType(typeInfo));
    }

    [Fact]
    public void IsVector3_ForVector3_ReturnsTrue()
    {
        var typeInfo = TypeInfo.Parse("Vector3");
        Assert.True(_mapper.IsVector3(typeInfo));
    }

    [Fact]
    public void IsVector3_ForOtherTypes_ReturnsFalse()
    {
        var typeInfo = TypeInfo.Parse("int");
        Assert.False(_mapper.IsVector3(typeInfo));
    }

    [Fact]
    public void GetCitizenResultType_Vector3_ReturnsResultAsVector()
    {
        var typeInfo = TypeInfo.Parse("Vector3");
        var result = _mapper.GetCitizenResultType(typeInfo);
        Assert.Equal("Citizen.resultAsVector()", result);
    }

    [Fact]
    public void GetCitizenResultType_String_ReturnsResultAsString()
    {
        var typeInfo = TypeInfo.Parse("char*");
        var result = _mapper.GetCitizenResultType(typeInfo);
        Assert.Equal("Citizen.resultAsString()", result);
    }

    [Fact]
    public void GetCitizenResultType_Float_ReturnsResultAsFloat()
    {
        var typeInfo = TypeInfo.Parse("float");
        var result = _mapper.GetCitizenResultType(typeInfo);
        Assert.Equal("Citizen.resultAsFloat()", result);
    }

    [Fact]
    public void GetCitizenResultType_Handle_ReturnsResultAsInteger()
    {
        var typeInfo = TypeInfo.Parse("Entity");
        var result = _mapper.GetCitizenResultType(typeInfo);
        Assert.Equal("Citizen.resultAsInteger()", result);
    }

    [Fact]
    public void NeedsResultMarker_Void_ReturnsFalse()
    {
        var typeInfo = TypeInfo.Parse("void");
        Assert.False(_mapper.NeedsResultMarker(typeInfo));
    }

    [Fact]
    public void NeedsResultMarker_NonVoid_ReturnsTrue()
    {
        var typeInfo = TypeInfo.Parse("int");
        Assert.True(_mapper.NeedsResultMarker(typeInfo));
    }

    [Fact]
    public void NeedsResultMarker_Any_ReturnsFalse()
    {
        var typeInfo = TypeInfo.Parse("Any");
        Assert.False(_mapper.NeedsResultMarker(typeInfo));
    }
}
