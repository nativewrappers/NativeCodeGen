using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.Utilities;
using NativeCodeGen.TypeScript;

namespace NativeCodeGen.Tests.Generation;

public class GetterProxyTests
{
    [Fact]
    public void GetterProxy_GeneratedWhenAllParamsOptional()
    {
        var emitter = new TypeScriptEmitter();
        var generator = new SharedClassGenerator(emitter);

        // Create a native with optional parameters
        var native = new NativeDefinition
        {
            Name = "GET_COORDS",
            Hash = "0x12345678",
            Parameters =
            [
                new NativeParameter
                {
                    Name = "alive",
                    Type = new TypeInfo { Name = "BOOL", Category = TypeCategory.Primitive },
                    DefaultValue = "false"  // Has default value
                },
                new NativeParameter
                {
                    Name = "realCoords",
                    Type = new TypeInfo { Name = "BOOL", Category = TypeCategory.Primitive },
                    DefaultValue = "false"  // Has default value
                }
            ],
            ReturnType = new TypeInfo { Name = "Vector3", Category = TypeCategory.Struct }
        };

        var result = generator.GenerateHandleClass("Entity", null, [native]);

        // Should have both the function and the getter proxy
        Assert.Contains("getCoords(alive: boolean = false, realCoords: boolean = false): Vector3", result);
        Assert.Contains("get Coords(): Vector3 {", result);
        Assert.Contains("return this.getCoords();", result);
    }

    [Fact]
    public void GetterProxy_NotGeneratedWhenParamsRequired()
    {
        var emitter = new TypeScriptEmitter();
        var generator = new SharedClassGenerator(emitter);

        // Create a native with required parameters (no defaults)
        var native = new NativeDefinition
        {
            Name = "GET_COORDS",
            Hash = "0x12345678",
            Parameters =
            [
                new NativeParameter
                {
                    Name = "alive",
                    Type = new TypeInfo { Name = "BOOL", Category = TypeCategory.Primitive }
                    // No DefaultValue - required param
                }
            ],
            ReturnType = new TypeInfo { Name = "Vector3", Category = TypeCategory.Struct }
        };

        var result = generator.GenerateHandleClass("Entity", null, [native]);

        // Should have the function but NOT the getter proxy
        Assert.Contains("getCoords(alive: boolean): Vector3", result);
        Assert.DoesNotContain("get Coords()", result);
    }

    [Fact]
    public void GetterProxy_NotGeneratedWhenMixedParams()
    {
        var emitter = new TypeScriptEmitter();
        var generator = new SharedClassGenerator(emitter);

        // Create a native with mixed params (one required, one optional)
        var native = new NativeDefinition
        {
            Name = "GET_DATA",
            Hash = "0x12345678",
            Parameters =
            [
                new NativeParameter
                {
                    Name = "id",
                    Type = new TypeInfo { Name = "int", Category = TypeCategory.Primitive }
                    // Required
                },
                new NativeParameter
                {
                    Name = "includeExtra",
                    Type = new TypeInfo { Name = "BOOL", Category = TypeCategory.Primitive },
                    DefaultValue = "true"  // Optional
                }
            ],
            ReturnType = new TypeInfo { Name = "int", Category = TypeCategory.Primitive }
        };

        var result = generator.GenerateHandleClass("Test", null, [native]);

        // Should have the function but NOT the getter proxy (not all params optional)
        Assert.Contains("getData(", result);
        Assert.DoesNotContain("get Data()", result);
    }
}
