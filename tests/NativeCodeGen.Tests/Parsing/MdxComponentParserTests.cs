using NativeCodeGen.Core.Models;
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

    [Fact]
    public void ParseNativeRefs_WithGame()
    {
        var content = "Similar to [native: GET_ENTITY_COORDS | gta5]";
        var results = _parser.ParseNativeRefs(content);

        Assert.Single(results);
        Assert.Equal("GET_ENTITY_COORDS", results[0].Name);
        Assert.Equal("gta5", results[0].Game);
    }

    [Fact]
    public void ParseNativeRefs_WithoutGame()
    {
        var content = "See [native: GET_ENTITY_COORDS]";
        var results = _parser.ParseNativeRefs(content);

        Assert.Single(results);
        Assert.Equal("GET_ENTITY_COORDS", results[0].Name);
        Assert.Null(results[0].Game);
    }

    [Fact]
    public void ParseNativeRefs_MixedWithAndWithoutGame()
    {
        var content = "Use [native: REQUEST_MODEL] or [native: REQUEST_MODEL | gta5]";
        var results = _parser.ParseNativeRefs(content);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "REQUEST_MODEL" && r.Game == null);
        Assert.Contains(results, r => r.Name == "REQUEST_MODEL" && r.Game == "gta5");
    }

    [Fact]
    public void ParseCallouts_NoteWithoutTitle()
    {
        var content = "[note: This is a simple note]";
        var results = _parser.ParseCallouts(content);

        Assert.Single(results);
        Assert.Equal(CalloutType.Note, results[0].Type);
        Assert.Null(results[0].Title);
        Assert.Equal("This is a simple note", results[0].Description);
    }

    [Fact]
    public void ParseCallouts_NoteWithTitle()
    {
        var content = "[note: Important | This is the description]";
        var results = _parser.ParseCallouts(content);

        Assert.Single(results);
        Assert.Equal(CalloutType.Note, results[0].Type);
        Assert.Equal("Important", results[0].Title);
        Assert.Equal("This is the description", results[0].Description);
    }

    [Fact]
    public void ParseCallouts_Warning()
    {
        var content = "[warning: Deprecated | This function will be removed]";
        var results = _parser.ParseCallouts(content);

        Assert.Single(results);
        Assert.Equal(CalloutType.Warning, results[0].Type);
        Assert.Equal("Deprecated", results[0].Title);
        Assert.Equal("This function will be removed", results[0].Description);
    }

    [Fact]
    public void ParseCallouts_Info()
    {
        var content = "[info: Performance | This function is expensive]";
        var results = _parser.ParseCallouts(content);

        Assert.Single(results);
        Assert.Equal(CalloutType.Info, results[0].Type);
        Assert.Equal("Performance", results[0].Title);
        Assert.Equal("This function is expensive", results[0].Description);
    }

    [Fact]
    public void ParseCallouts_Danger()
    {
        var content = "[danger: Do not call in production]";
        var results = _parser.ParseCallouts(content);

        Assert.Single(results);
        Assert.Equal(CalloutType.Danger, results[0].Type);
        Assert.Null(results[0].Title);
        Assert.Equal("Do not call in production", results[0].Description);
    }

    [Fact]
    public void ParseCallouts_Multiple()
    {
        var content = "Some text [note: A note] more text [warning: A warning]";
        var results = _parser.ParseCallouts(content);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Type == CalloutType.Note && r.Description == "A note");
        Assert.Contains(results, r => r.Type == CalloutType.Warning && r.Description == "A warning");
    }

    [Fact]
    public void ParseCallouts_CaseInsensitive()
    {
        var content = "[NOTE: Important message] [WARNING: Be careful]";
        var results = _parser.ParseCallouts(content);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Type == CalloutType.Note);
        Assert.Contains(results, r => r.Type == CalloutType.Warning);
    }
}
