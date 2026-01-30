using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Tests.Parsing;

public class EnumParserTests
{
    private readonly EnumParser _parser = new();

    [Fact]
    public void Parse_SimpleEnum_ParsesCorrectly()
    {
        var content = """
            enum eTestEnum {
                VALUE_ONE = 0,
                VALUE_TWO = 1,
                VALUE_THREE = 2
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("eTestEnum", result.Value!.Name);
        Assert.Null(result.Value.BaseType);
        Assert.Equal(3, result.Value.Members.Count);
        Assert.Equal("VALUE_ONE", result.Value.Members[0].Name);
        Assert.Equal("0", result.Value.Members[0].Value);
    }

    [Fact]
    public void Parse_EnumWithBaseType_ParsesCorrectly()
    {
        var content = """
            enum eWeaponHash : Hash {
                WEAPON_PISTOL = 0x1234,
                WEAPON_RIFLE = 0x5678
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("eWeaponHash", result.Value!.Name);
        Assert.Equal("Hash", result.Value.BaseType);
        Assert.Equal(2, result.Value.Members.Count);
    }

    [Fact]
    public void Parse_EnumWithHexValues_ParsesCorrectly()
    {
        var content = """
            enum eFlags {
                FLAG_A = 0x01,
                FLAG_B = 0x02,
                FLAG_C = 0x04,
                FLAG_ALL = 0xFF
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("0x01", result.Value!.Members[0].Value);
        Assert.Equal("0x02", result.Value.Members[1].Value);
        Assert.Equal("0x04", result.Value.Members[2].Value);
        Assert.Equal("0xFF", result.Value.Members[3].Value);
    }

    [Fact]
    public void Parse_EnumWithNegativeValues_ParsesCorrectly()
    {
        var content = """
            enum eStatus {
                STATUS_INVALID = -1,
                STATUS_OK = 0,
                STATUS_ERROR = 1
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("-1", result.Value!.Members[0].Value);
        Assert.Equal("0", result.Value.Members[1].Value);
        Assert.Equal("1", result.Value.Members[2].Value);
    }

    [Fact]
    public void Parse_EnumWithNegativeStartAndAutoIncrement_GeneratesCorrectValues()
    {
        var content = """
            enum eVehicleSeat {
                VS_ANY_PASSENGER = -2,
                VS_DRIVER,
                VS_FRONT_RIGHT,
                VS_BACK_LEFT
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("-2", result.Value!.Members[0].Value);  // Explicit: -2
        Assert.Equal("-1", result.Value.Members[1].Value);   // Auto: -2 + 1
        Assert.Equal("0", result.Value.Members[2].Value);    // Auto: -1 + 1
        Assert.Equal("1", result.Value.Members[3].Value);    // Auto: 0 + 1
    }

    [Fact]
    public void Parse_EnumWithoutValues_AutoGeneratesSequentialValues()
    {
        var content = """
            enum eSimple {
                FIRST,
                SECOND,
                THIRD
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Members.Count);
        Assert.Equal("0", result.Value.Members[0].Value);
        Assert.Equal("1", result.Value.Members[1].Value);
        Assert.Equal("2", result.Value.Members[2].Value);
    }

    [Fact]
    public void Parse_EnumWithMixedValues_AutoGeneratesCorrectValues()
    {
        var content = """
            enum eMixed {
                AUTO_ONE,
                EXPLICIT = 5,
                AUTO_TWO,
                EXPLICIT_HEX = 0x10
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("0", result.Value!.Members[0].Value);  // Auto: starts at 0
        Assert.Equal("5", result.Value.Members[1].Value);   // Explicit: 5
        Assert.Equal("6", result.Value.Members[2].Value);   // Auto: 5 + 1
        Assert.Equal("0x10", result.Value.Members[3].Value); // Explicit: 0x10
    }

    [Fact]
    public void Parse_EnumWithTrailingComma_ParsesCorrectly()
    {
        var content = """
            enum eTrailing {
                VALUE_ONE = 0,
                VALUE_TWO = 1,
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Members.Count);
    }

    [Fact]
    public void Parse_LargeHexValues_ParsesCorrectly()
    {
        var content = """
            enum eLargeHash : Hash {
                HASH_VALUE = 0xDEADBEEFCAFEBABE
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("0xDEADBEEFCAFEBABE", result.Value!.Members[0].Value);
    }

    [Fact]
    public void Parse_EnumWithUnderscoredName_ParsesCorrectly()
    {
        var content = """
            enum e_weapon_type {
                WEAPON_TYPE_MELEE = 0,
                WEAPON_TYPE_RANGED = 1
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("e_weapon_type", result.Value!.Name);
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
    public void Parse_NotAnEnum_ReturnsError()
    {
        var content = """
            struct NotAnEnum {
                int value;
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("enum"));
    }

    [Fact]
    public void Parse_MissingEnumName_ReturnsError()
    {
        var content = """
            enum {
                VALUE = 0
            };
            """;

        var result = _parser.Parse(content, "test.c");

        // Should fail because enum name is required
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MissingOpenBrace_ReturnsError()
    {
        var content = """
            enum eTest
                VALUE = 0
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
            enum eTest {
                VALUE = 0
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_WithFrontmatter_SkipsFrontmatter()
    {
        var content = """
            ---
            ns: TEST
            ---
            enum eTest {
                VALUE = 0
            };
            """;

        var result = _parser.Parse(content, "test.c");

        Assert.True(result.IsSuccess);
        Assert.Equal("eTest", result.Value!.Name);
    }
}
