using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Tests.Parsing;

public class MdxComponentParserTests
{
    private readonly MdxComponentParser _parser = new();

    [Fact]
    public void ParseEmbeddedEnums_Basic()
    {
        var content = "Refer to [enum: eWeaponHash]";
        var results = _parser.ParseEmbeddedEnums(content);

        Assert.Single(results);
        Assert.Equal("eWeaponHash", results[0].Name);
    }

    [Fact]
    public void ParseEmbeddedEnums_WithQuotes()
    {
        var content = "See [enum: \"eVehicleSeat\"] for values";
        var results = _parser.ParseEmbeddedEnums(content);

        Assert.Single(results);
        Assert.Equal("eVehicleSeat", results[0].Name);
    }

    [Fact]
    public void ParseEmbeddedEnums_WithSingleQuotes()
    {
        var content = "Use [enum: 'ePedType']";
        var results = _parser.ParseEmbeddedEnums(content);

        Assert.Single(results);
        Assert.Equal("ePedType", results[0].Name);
    }

    [Fact]
    public void ParseEmbeddedEnums_Multiple()
    {
        var content = "See [enum: eWeaponHash] or [enum: eAmmoType]";
        var results = _parser.ParseEmbeddedEnums(content);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "eWeaponHash");
        Assert.Contains(results, r => r.Name == "eAmmoType");
    }

    [Fact]
    public void ParseEmbeddedEnums_NoDuplicates()
    {
        var content = "Use [enum: eWeaponHash] here and [enum: eWeaponHash] there";
        var results = _parser.ParseEmbeddedEnums(content);

        Assert.Single(results);
        Assert.Equal("eWeaponHash", results[0].Name);
    }

    [Fact]
    public void ParseEmbeddedEnums_CaseInsensitive()
    {
        var content = "[ENUM: eWeaponHash]";
        var results = _parser.ParseEmbeddedEnums(content);

        Assert.Single(results);
        Assert.Equal("eWeaponHash", results[0].Name);
    }

    [Fact]
    public void ParseSharedExamples_Basic()
    {
        var content = "See [example: CreatePed]";
        var results = _parser.ParseSharedExamples(content);

        Assert.Single(results);
        Assert.Equal("CreatePed", results[0].Name);
    }

    [Fact]
    public void ParseStructRefs_Basic()
    {
        var content = "See [struct: scrItemInfo]";
        var results = _parser.ParseStructRefs(content);

        Assert.Single(results);
        Assert.Equal("scrItemInfo", results[0].Name);
    }

    [Fact]
    public void ParseNativeRefs_Basic()
    {
        var content = "See [native: GET_ENTITY_COORDS]";
        var results = _parser.ParseNativeRefs(content);

        Assert.Single(results);
        Assert.Equal("GET_ENTITY_COORDS", results[0].Name);
    }

    [Fact]
    public void ParseNativeRefs_Multiple()
    {
        var content = "Use [native: REQUEST_MODEL] then [native: CREATE_PED]";
        var results = _parser.ParseNativeRefs(content);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "REQUEST_MODEL");
        Assert.Contains(results, r => r.Name == "CREATE_PED");
    }
}
