using NativeCodeGen.Core.Parsing;

namespace NativeCodeGen.Tests.Parsing;

public class MdxParserTests
{
    private readonly MdxParser _parser = new();

    [Fact]
    public void Parse_ValidMdx_ReturnsNative()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity, BOOL alive);
```

Gets the current coordinates of an entity.

## Parameters
* **entity**: The entity handle
* **alive**: Whether to get coords for alive state

## Return value
The entity coordinates as a Vector3.
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.NotNull(result.Value);
        Assert.Empty(result.Errors);
        Assert.Equal("GET_ENTITY_COORDS", result.Value.Name);
        Assert.Equal("0xA86D5F069399F44D", result.Value.Hash);
        Assert.Equal("ENTITY", result.Value.Namespace);
        Assert.Equal("Vector3", result.Value.ReturnType.ToString());
        Assert.Equal(2, result.Value.Parameters.Count);
    }

    [Fact]
    public void Parse_WithAliases_ParsesCorrectly()
    {
        var mdx = @"---
ns: ENTITY
aliases: [""0xA86D5F069399F44D"", ""GET_COORDS""]
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity);
```

## Parameters
* **entity**: The entity
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(2, result.Value!.Aliases.Count);
        Assert.Contains("0xA86D5F069399F44D", result.Value.Aliases);
        Assert.Contains("GET_COORDS", result.Value.Aliases);
    }

    [Fact]
    public void Parse_WithApiSet_ParsesCorrectly()
    {
        var mdx = @"---
ns: ENTITY
apiset: server
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity);
```

## Parameters
* **entity**: The entity
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal("server", result.Value!.ApiSet);
    }

    [Fact]
    public void Parse_WithDefaultParameter_ParsesCorrectly()
    {
        var mdx = @"---
ns: VEHICLE
---
## SET_VEHICLE_DOOR

```c
// 0x1234567890ABCDEF
void SET_VEHICLE_DOOR(Vehicle vehicle, int doorIndex = -1);
```

## Parameters
* **vehicle**: The vehicle
* **doorIndex**: Door index
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(2, result.Value!.Parameters.Count);
        Assert.Equal("-1", result.Value.Parameters[1].DefaultValue);
    }

    [Fact]
    public void Parse_WithThisAttribute_ParsesCorrectly()
    {
        var mdx = @"---
ns: PED
---
## SET_PED_HEALTH

```c
// 0x1234567890ABCDEF
void SET_PED_HEALTH(@this Ped ped, int health);
```

## Parameters
* **ped**: The ped
* **health**: Health value
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.True(result.Value!.Parameters[0].Attributes.IsThis);
    }

    [Fact]
    public void Parse_WithNotNullAttribute_ParsesCorrectly()
    {
        var mdx = @"---
ns: MISC
---
## GET_HASH_KEY

```c
// 0x1234567890ABCDEF
Hash GET_HASH_KEY(@notnull char* str);
```

## Parameters
* **str**: The string
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.True(result.Value!.Parameters[0].Attributes.IsNotNull);
    }

    [Fact]
    public void Parse_WithPointerOutput_SetsIsOutput()
    {
        var mdx = @"---
ns: MISC
---
## GET_GROUND_Z

```c
// 0x1234567890ABCDEF
BOOL GET_GROUND_Z(float x, float y, float* z);
```

## Parameters
* **x**: X coordinate
* **y**: Y coordinate
* **z**: Output Z coordinate
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.False(result.Value!.Parameters[0].IsOutput);
        Assert.False(result.Value.Parameters[1].IsOutput);
        Assert.True(result.Value.Parameters[2].IsOutput);
    }

    [Fact]
    public void Parse_NoParameters_ReturnsEmptyList()
    {
        var mdx = @"---
ns: MISC
---
## GET_GAME_TIMER

```c
// 0x1234567890ABCDEF
int GET_GAME_TIMER();
```
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.Value!.Parameters);
    }

    [Fact]
    public void Parse_MissingFrontmatter_ReturnsError()
    {
        var mdx = @"## GET_ENTITY_COORDS

```c
// 0x1234567890ABCDEF
Vector3 GET_ENTITY_COORDS(Entity entity);
```
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MissingNamespace_ReturnsError()
    {
        var mdx = @"---
aliases: []
---
## GET_ENTITY_COORDS

```c
// 0x1234567890ABCDEF
Vector3 GET_ENTITY_COORDS(Entity entity);
```
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MissingCodeBlock_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

Some description without a code block.
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_InvalidHash_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// INVALID_HASH
Vector3 GET_ENTITY_COORDS(Entity entity);
```
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_VoidReturn_ParsesCorrectly()
    {
        var mdx = @"---
ns: ENTITY
---
## DELETE_ENTITY

```c
// 0x1234567890ABCDEF
void DELETE_ENTITY(Entity* entity);
```

## Parameters
* **entity**: The entity to delete
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal("void", result.Value!.ReturnType.ToString());
    }

    [Fact]
    public void Parse_MultipleDefaultValues_ParsesCorrectly()
    {
        var mdx = @"---
ns: TEST
---
## TEST_DEFAULTS

```c
// 0x1234567890ABCDEF
void TEST_DEFAULTS(int a = 0, float b = 1.0f, BOOL c = true);
```

## Parameters
* **a**: First param
* **b**: Second param
* **c**: Third param
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal("0", result.Value!.Parameters[0].DefaultValue);
        Assert.Equal("1.0f", result.Value.Parameters[1].DefaultValue);
        Assert.Equal("true", result.Value.Parameters[2].DefaultValue);
    }

    [Fact]
    public void Parse_ComplexSignature_ParsesCorrectly()
    {
        var mdx = @"---
ns: PED
---
## COMPLEX_NATIVE

```c
// 0xABCDEF1234567890
BOOL COMPLEX_NATIVE(@this Ped ped, @notnull char* name, int* outValue, float speed = 1.0f);
```

## Parameters
* **ped**: The ped
* **name**: The name
* **outValue**: Output value
* **speed**: Speed value
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(4, result.Value!.Parameters.Count);

        Assert.True(result.Value.Parameters[0].Attributes.IsThis);
        Assert.Equal("Ped", result.Value.Parameters[0].Type.Name);

        Assert.True(result.Value.Parameters[1].Attributes.IsNotNull);

        Assert.True(result.Value.Parameters[2].IsOutput);

        Assert.Equal("1.0f", result.Value.Parameters[3].DefaultValue);
    }

    [Fact]
    public void Parse_UnknownSection_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity);
```

## Unknown Section
This section is not allowed.
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("Unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsError()
    {
        var result = _parser.Parse("", "test.mdx");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MalformedYaml_ReturnsError()
    {
        var mdx = @"---
ns: [invalid yaml
---
## TEST

```c
// 0x1234
void TEST();
```
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MissingHash_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
Vector3 GET_ENTITY_COORDS(Entity entity);
```
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_MalformedSignature_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## TEST

```c
// 0x1234567890ABCDEF
this is not a valid signature
```
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_ParameterCountMismatch_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity, BOOL alive);
```

## Parameters
* **entity**: The entity handle
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("Parameter count mismatch"));
        Assert.Contains(result.Errors, e => e.Message.Contains("2") && e.Message.Contains("1"));
    }

    [Fact]
    public void Parse_ParameterNameMismatch_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity, BOOL alive);
```

## Parameters
* **entity**: The entity handle
* **dead**: Whether to get coords for dead state
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("Parameter name mismatch"));
        Assert.Contains(result.Errors, e => e.Message.Contains("alive") && e.Message.Contains("dead"));
    }

    [Fact]
    public void Parse_ParameterOrderMismatch_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity, BOOL alive);
```

## Parameters
* **alive**: Whether to get coords for alive state
* **entity**: The entity handle
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("order mismatch"));
    }

    [Fact]
    public void Parse_TooManyDocumentedParameters_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity);
```

## Parameters
* **entity**: The entity handle
* **alive**: Extra undeclared parameter
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("Parameter count mismatch"));
        Assert.Contains(result.Errors, e => e.Message.Contains("1") && e.Message.Contains("2"));
    }

    [Fact]
    public void Parse_CorrectParameterNamesAndOrder_NoErrors()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity, BOOL alive);
```

## Parameters
* **entity**: The entity handle
* **alive**: Whether to get coords for alive state
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_MultipleParameterIssues_ReportsAllErrors()
    {
        var mdx = @"---
ns: TEST
---
## TEST_NATIVE

```c
// 0x1234567890ABCDEF
void TEST_NATIVE(int a, int b, int c);
```

## Parameters
* **a**: First param
* **c**: Second param
* **b**: Third param
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.True(result.Errors.Count >= 2, $"Expected at least 2 errors, got {result.Errors.Count}");
    }

    [Fact]
    public void Parse_MissingNativeNameHeading_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity);
```
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("Missing native name heading"));
    }

    [Fact]
    public void Parse_MissingParametersSection_ReturnsError()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity, BOOL alive);
```

Description without parameters section.
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("Parameters section"));
    }

    [Fact]
    public void Parse_MissingReturnValueSection_ReturnsWarning()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 GET_ENTITY_COORDS(Entity entity);
```

## Parameters
* **entity**: The entity
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Message.Contains("Return value"));
    }

    [Fact]
    public void Parse_VoidReturnWithoutReturnSection_NoWarning()
    {
        var mdx = @"---
ns: ENTITY
---
## DELETE_ENTITY

```c
// 0xA86D5F069399F44D
void DELETE_ENTITY(Entity entity);
```

## Parameters
* **entity**: The entity
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.DoesNotContain(result.Warnings, w => w.Message.Contains("Return value"));
    }

    [Fact]
    public void Parse_FunctionNameMismatch_ReturnsWarning()
    {
        var mdx = @"---
ns: ENTITY
---
## GET_ENTITY_COORDS

```c
// 0xA86D5F069399F44D
Vector3 DIFFERENT_NAME(Entity entity);
```

## Parameters
* **entity**: The entity

## Return value
The coordinates.
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Message.Contains("doesn't match heading"));
    }

    [Fact]
    public void Parse_RequiredParamAfterOptional_ReturnsError()
    {
        var mdx = @"---
ns: TEST
---
## TEST_NATIVE

```c
// 0x1234567890ABCDEF
void TEST_NATIVE(int optional = 0, int required);
```

## Parameters
* **optional**: Optional param
* **required**: Required param
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("Required parameter") && e.Message.Contains("follows optional"));
    }

    [Fact]
    public void Parse_AllOptionalParams_Succeeds()
    {
        var mdx = @"---
ns: TEST
---
## TEST_NATIVE

```c
// 0x1234567890ABCDEF
void TEST_NATIVE(int a = 0, int b = 1, int c = 2);
```

## Parameters
* **a**: First
* **b**: Second
* **c**: Third
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void Parse_RequiredThenOptionalParams_Succeeds()
    {
        var mdx = @"---
ns: TEST
---
## TEST_NATIVE

```c
// 0x1234567890ABCDEF
void TEST_NATIVE(int required, int optional = 0);
```

## Parameters
* **required**: Required param
* **optional**: Optional param
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void Parse_MultipleThisAttributes_ReturnsError()
    {
        var mdx = @"---
ns: TEST
---
## TEST_NATIVE

```c
// 0x1234567890ABCDEF
void TEST_NATIVE(@this Entity a, @this Ped b);
```

## Parameters
* **a**: First
* **b**: Second
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("@this") && e.Message.Contains("Multiple"));
    }

    [Fact]
    public void Parse_InAttributeOnNonPointer_ReturnsError()
    {
        var mdx = @"---
ns: TEST
---
## TEST_NATIVE

```c
// 0x1234567890ABCDEF
void TEST_NATIVE(@in int value);
```

## Parameters
* **value**: The value
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("@in") && e.Message.Contains("pointer"));
    }

    [Fact]
    public void Parse_InAttributeOnStructPointer_ReturnsError()
    {
        var mdx = @"---
ns: TEST
---
## TEST_NATIVE

```c
// 0x1234567890ABCDEF
void TEST_NATIVE(@in SomeStruct* data);
```

## Parameters
* **data**: The data
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("@in") && e.Message.Contains("struct"));
    }

    [Fact]
    public void Parse_InAttributeOnEntityPointer_Succeeds()
    {
        var mdx = @"---
ns: ENTITY
---
## DELETE_ENTITY

```c
// 0x1234567890ABCDEF
void DELETE_ENTITY(@in Entity* entity);
```

## Parameters
* **entity**: The entity to delete

## Return value
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.True(result.Value!.Parameters[0].Attributes.IsIn);
    }

    [Fact]
    public void Parse_InAttributeOnVector3Pointer_Succeeds()
    {
        var mdx = @"---
ns: TEST
---
## TEST_NATIVE

```c
// 0x1234567890ABCDEF
void TEST_NATIVE(@in Vector3* coords);
```

## Parameters
* **coords**: The coordinates

## Return value
";

        var result = _parser.Parse(mdx, "test.mdx");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.True(result.Value!.Parameters[0].Attributes.IsIn);
    }
}
