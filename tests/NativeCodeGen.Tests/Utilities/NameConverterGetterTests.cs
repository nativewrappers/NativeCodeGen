using NativeCodeGen.Core.Utilities;

namespace NativeCodeGen.Tests.Utilities;

public class NameConverterGetterTests
{
    [Theory]
    [InlineData("getHealth", true, "Health")]
    [InlineData("getName", true, "Name")]
    [InlineData("getTeam", true, "Team")]
    [InlineData("isMale", true, "IsMale")]
    [InlineData("isAttached", true, "IsAttached")]
    [InlineData("isAttachedToAnyObject", true, "IsAttachedToAnyObject")]
    [InlineData("setHealth", false, "setHealth")]
    [InlineData("get", false, "get")]
    [InlineData("getA", true, "A")]
    [InlineData("is", false, "is")]
    [InlineData("isA", true, "IsA")]
    public void IsGetterName_DetectsGettersCorrectly(string methodName, bool expectedIsGetter, string expectedPropertyName)
    {
        Assert.Equal(expectedIsGetter, NameConverter.IsGetterName(methodName));
        Assert.Equal(expectedPropertyName, NameConverter.GetterToPropertyName(methodName));
    }

    [Theory]
    [InlineData("setHealth", true, "Health")]
    [InlineData("setName", true, "Name")]
    [InlineData("getHealth", false, "getHealth")]
    [InlineData("set", false, "set")]
    [InlineData("setA", true, "A")]
    public void IsSetterName_DetectsSettersCorrectly(string methodName, bool expectedIsSetter, string expectedPropertyName)
    {
        Assert.Equal(expectedIsSetter, NameConverter.IsSetterName(methodName));
        Assert.Equal(expectedPropertyName, NameConverter.SetterToPropertyName(methodName));
    }
}
