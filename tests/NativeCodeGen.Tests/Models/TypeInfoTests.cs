using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Tests.Models;

public class TypeInfoTests
{
    #region CategorizeType Tests

    [Theory]
    [InlineData("int", TypeCategory.Primitive)]
    [InlineData("uint", TypeCategory.Primitive)]
    [InlineData("float", TypeCategory.Primitive)]
    [InlineData("double", TypeCategory.Primitive)]
    [InlineData("BOOL", TypeCategory.Primitive)]
    [InlineData("bool", TypeCategory.Primitive)]
    [InlineData("long", TypeCategory.Primitive)]
    [InlineData("u8", TypeCategory.Primitive)]
    [InlineData("u16", TypeCategory.Primitive)]
    [InlineData("u32", TypeCategory.Primitive)]
    [InlineData("u64", TypeCategory.Primitive)]
    [InlineData("i8", TypeCategory.Primitive)]
    [InlineData("i16", TypeCategory.Primitive)]
    [InlineData("i32", TypeCategory.Primitive)]
    [InlineData("i64", TypeCategory.Primitive)]
    [InlineData("f32", TypeCategory.Primitive)]
    [InlineData("f64", TypeCategory.Primitive)]
    [InlineData("variadic", TypeCategory.Primitive)]
    public void CategorizeType_Primitives_ReturnsPrimitive(string name, TypeCategory expected)
    {
        var result = TypeInfo.CategorizeType(name, isPointer: false);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Entity")]
    [InlineData("Ped")]
    [InlineData("Vehicle")]
    [InlineData("Object")]
    [InlineData("Pickup")]
    [InlineData("Player")]
    [InlineData("Cam")]
    [InlineData("Blip")]
    [InlineData("Interior")]
    [InlineData("FireId")]
    [InlineData("AnimScene")]
    [InlineData("ItemSet")]
    [InlineData("PersChar")]
    [InlineData("PopZone")]
    [InlineData("PropSet")]
    [InlineData("Volume")]
    [InlineData("ScrHandle")]
    [InlineData("PedGroup")]
    [InlineData("Prompt")]
    public void CategorizeType_Handles_ReturnsHandle(string name)
    {
        var result = TypeInfo.CategorizeType(name, isPointer: false);
        Assert.Equal(TypeCategory.Handle, result);
    }

    [Theory]
    [InlineData("Entity")]
    [InlineData("Ped")]
    [InlineData("Vehicle")]
    [InlineData("Object")]
    [InlineData("Pickup")]
    [InlineData("Player")]
    [InlineData("Cam")]
    [InlineData("Interior")]
    [InlineData("AnimScene")]
    [InlineData("ItemSet")]
    [InlineData("PersChar")]
    [InlineData("PropSet")]
    [InlineData("Volume")]
    public void IsClassHandle_ClassHandles_ReturnsTrue(string name)
    {
        Assert.True(TypeInfo.IsClassHandle(name));
    }

    [Theory]
    [InlineData("Blip")]
    [InlineData("FireId")]
    [InlineData("PopZone")]
    [InlineData("ScrHandle")]
    [InlineData("PedGroup")]
    [InlineData("Prompt")]
    public void IsClassHandle_NonClassHandles_ReturnsFalse(string name)
    {
        Assert.False(TypeInfo.IsClassHandle(name));
    }

    [Fact]
    public void CategorizeType_Void_ReturnsVoid()
    {
        var result = TypeInfo.CategorizeType("void", isPointer: false);
        Assert.Equal(TypeCategory.Void, result);
    }

    [Fact]
    public void CategorizeType_VoidPointer_ReturnsVoid()
    {
        // void* is still TypeCategory.Void - the pointer aspect is tracked separately
        var result = TypeInfo.CategorizeType("void", isPointer: true);
        Assert.Equal(TypeCategory.Void, result);
    }

    [Theory]
    [InlineData("char", true, TypeCategory.String)]
    [InlineData("string", true, TypeCategory.String)]
    [InlineData("string", false, TypeCategory.String)]
    public void CategorizeType_StringTypes_ReturnsString(string name, bool isPointer, TypeCategory expected)
    {
        var result = TypeInfo.CategorizeType(name, isPointer);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CategorizeType_Hash_ReturnsHash()
    {
        var result = TypeInfo.CategorizeType("Hash", isPointer: false);
        Assert.Equal(TypeCategory.Hash, result);
    }

    [Fact]
    public void CategorizeType_Vector2_ReturnsVector2()
    {
        var result = TypeInfo.CategorizeType("Vector2", isPointer: false);
        Assert.Equal(TypeCategory.Vector2, result);
    }

    [Fact]
    public void CategorizeType_Vector3_ReturnsVector3()
    {
        var result = TypeInfo.CategorizeType("Vector3", isPointer: false);
        Assert.Equal(TypeCategory.Vector3, result);
    }

    [Fact]
    public void CategorizeType_Vector4_ReturnsVector4()
    {
        var result = TypeInfo.CategorizeType("Vector4", isPointer: false);
        Assert.Equal(TypeCategory.Vector4, result);
    }

    [Fact]
    public void CategorizeType_Color_ReturnsColor()
    {
        var result = TypeInfo.CategorizeType("Color", isPointer: false);
        Assert.Equal(TypeCategory.Color, result);
    }

    [Fact]
    public void CategorizeType_Any_ReturnsAny()
    {
        var result = TypeInfo.CategorizeType("Any", isPointer: false);
        Assert.Equal(TypeCategory.Any, result);
    }

    [Theory]
    [InlineData("UnknownType")]
    [InlineData("CustomStruct")]
    [InlineData("MyEnum")]
    [InlineData("SomeData")]
    public void CategorizeType_UnknownTypes_ReturnsStruct(string name)
    {
        var result = TypeInfo.CategorizeType(name, isPointer: false);
        Assert.Equal(TypeCategory.Struct, result);
    }

    #endregion

    #region Case Sensitivity Tests

    [Fact]
    public void CategorizeType_CaseSensitive_LowercaseIntIsPrimitive()
    {
        Assert.Equal(TypeCategory.Primitive, TypeInfo.CategorizeType("int", false));
    }

    [Fact]
    public void CategorizeType_CaseSensitive_UppercaseINTIsStruct()
    {
        // Type names are case-sensitive
        Assert.Equal(TypeCategory.Struct, TypeInfo.CategorizeType("INT", false));
    }

    [Fact]
    public void CategorizeType_CaseSensitive_LowercaseEntityIsStruct()
    {
        // 'entity' != 'Entity'
        Assert.Equal(TypeCategory.Struct, TypeInfo.CategorizeType("entity", false));
    }

    #endregion

    #region ResolveEnumType Tests

    [Fact]
    public void ResolveEnumType_StructWithEnumLookup_BecomesEnum()
    {
        var type = new TypeInfo { Name = "MyEnum", Category = TypeCategory.Struct };

        type.ResolveEnumType(name => name == "MyEnum" ? "int" : null);

        Assert.Equal(TypeCategory.Enum, type.Category);
        Assert.Equal("int", type.EnumBaseType);
    }

    [Fact]
    public void ResolveEnumType_StructWithNoMatch_RemainsStruct()
    {
        var type = new TypeInfo { Name = "MyStruct", Category = TypeCategory.Struct };

        type.ResolveEnumType(name => null);

        Assert.Equal(TypeCategory.Struct, type.Category);
        Assert.Null(type.EnumBaseType);
    }

    [Fact]
    public void ResolveEnumType_NonStruct_NotModified()
    {
        var type = new TypeInfo { Name = "int", Category = TypeCategory.Primitive };

        type.ResolveEnumType(name => "int"); // Lookup returns value but shouldn't affect primitive

        Assert.Equal(TypeCategory.Primitive, type.Category);
        Assert.Null(type.EnumBaseType);
    }

    [Fact]
    public void ResolveEnumType_HashBaseType_Preserved()
    {
        var type = new TypeInfo { Name = "eWeaponHash", Category = TypeCategory.Struct };

        type.ResolveEnumType(name => name == "eWeaponHash" ? "Hash" : null);

        Assert.Equal(TypeCategory.Enum, type.Category);
        Assert.Equal("Hash", type.EnumBaseType);
    }

    #endregion

    #region ValidAttributes Tests

    [Theory]
    [InlineData("@this")]
    [InlineData("@nullable")]
    [InlineData("@in")]
    public void ValidAttributes_ContainsKnownAttributes(string attr)
    {
        Assert.True(TypeInfo.ValidAttributes.Contains(attr));
    }

    [Theory]
    [InlineData("@out")]
    [InlineData("@optional")]
    [InlineData("@notnull")]
    [InlineData("this")]
    [InlineData("nullable")]
    public void ValidAttributes_DoesNotContainUnknown(string attr)
    {
        Assert.False(TypeInfo.ValidAttributes.Contains(attr));
    }

    #endregion

    #region Type Helper Properties Tests

    [Theory]
    [InlineData("bool", true)]
    [InlineData("BOOL", true)]
    [InlineData("int", false)]
    [InlineData("float", false)]
    public void IsBool_ReturnsCorrectValue(string name, bool expected)
    {
        var type = new TypeInfo { Name = name, Category = TypeCategory.Primitive };
        Assert.Equal(expected, type.IsBool);
    }

    [Theory]
    [InlineData("float", true)]
    [InlineData("double", true)]
    [InlineData("f32", true)]
    [InlineData("f64", true)]
    [InlineData("int", false)]
    [InlineData("bool", false)]
    public void IsFloat_ReturnsCorrectValue(string name, bool expected)
    {
        var type = new TypeInfo { Name = name, Category = TypeCategory.Primitive };
        Assert.Equal(expected, type.IsFloat);
    }

    [Theory]
    [InlineData(TypeCategory.Vector2, true)]
    [InlineData(TypeCategory.Vector3, true)]
    [InlineData(TypeCategory.Vector4, true)]
    [InlineData(TypeCategory.Primitive, false)]
    [InlineData(TypeCategory.Handle, false)]
    public void IsVector_ReturnsCorrectValue(TypeCategory category, bool expected)
    {
        var type = new TypeInfo { Name = "test", Category = category };
        Assert.Equal(expected, type.IsVector);
    }

    [Theory]
    [InlineData(TypeCategory.Vector2, 2)]
    [InlineData(TypeCategory.Vector3, 3)]
    [InlineData(TypeCategory.Vector4, 4)]
    [InlineData(TypeCategory.Primitive, 0)]
    [InlineData(TypeCategory.Handle, 0)]
    public void VectorComponentCount_ReturnsCorrectValue(TypeCategory category, int expected)
    {
        var type = new TypeInfo { Name = "test", Category = category };
        Assert.Equal(expected, type.VectorComponentCount);
    }

    [Fact]
    public void VectorComponents_ContainsCorrectComponents()
    {
        Assert.Equal(4, TypeInfo.VectorComponents.Length);
        Assert.Equal("x", TypeInfo.VectorComponents[0]);
        Assert.Equal("y", TypeInfo.VectorComponents[1]);
        Assert.Equal("z", TypeInfo.VectorComponents[2]);
        Assert.Equal("w", TypeInfo.VectorComponents[3]);
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void Parse_SimpleType_CorrectNameAndCategory()
    {
        var result = TypeInfo.Parse("int");

        Assert.Equal("int", result.Name);
        Assert.False(result.IsPointer);
        Assert.Equal(TypeCategory.Primitive, result.Category);
    }

    [Fact]
    public void Parse_PointerType_CorrectNameAndPointer()
    {
        var result = TypeInfo.Parse("int*");

        Assert.Equal("int", result.Name);
        Assert.True(result.IsPointer);
    }

    [Fact]
    public void Parse_PointerWithSpaces_TrimmedCorrectly()
    {
        var result = TypeInfo.Parse("  int *  ");

        Assert.Equal("int", result.Name);
        Assert.True(result.IsPointer);
    }

    #endregion
}
