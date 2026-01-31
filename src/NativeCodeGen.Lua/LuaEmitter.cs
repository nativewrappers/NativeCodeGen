using NativeCodeGen.Core.Generation;
using NativeCodeGen.Core.Models;
using NativeCodeGen.Core.TypeSystem;

namespace NativeCodeGen.Lua;

/// <summary>
/// Lua-specific code emitter.
/// </summary>
public class LuaEmitter : ILanguageEmitter
{
    private readonly LuaTypeMapper _typeMapper = new();

    public ITypeMapper TypeMapper => _typeMapper;
    public LanguageConfig Config => LanguageConfig.Lua;
    public string FileExtension => ".lua";
    public string SelfReference => "self";

    public DocBuilder CreateDocBuilder() => new LuaDocBuilder();

    // === Enum Generation ===

    public void EmitEnumStart(CodeBuilder cb, string enumName)
    {
        cb.AppendLine($"---@enum {enumName}");
        cb.AppendLine($"{enumName} = {{");
        cb.Indent();
    }

    public void EmitEnumMember(CodeBuilder cb, string memberName, string? value, string? comment)
    {
        var val = value ?? "nil";
        cb.AppendLine($"{memberName} = {val},");
    }

    public void EmitEnumEnd(CodeBuilder cb, string enumName)
    {
        cb.Dedent();
        cb.AppendLine("}");
        cb.AppendLine();
        cb.AppendLine($"return {enumName}");
    }

    // === Class Generation ===

    public void EmitHandleImports(CodeBuilder cb, IEnumerable<string> handleTypes)
    {
        // Lua doesn't need imports - uses globals
    }

    public void EmitNonClassHandleImports(CodeBuilder cb, IEnumerable<string> handleTypes)
    {
        // Lua doesn't need imports - uses globals
    }

    public void EmitTypeImports(CodeBuilder cb, IEnumerable<string> enumTypes, IEnumerable<string> structTypes)
    {
        // Lua doesn't need imports - types are available globally
    }

    public void EmitClassStart(CodeBuilder cb, string className, string? baseClass, ClassKind kind)
    {
        // Emit native aliases at the top of the file
        RawNativeBuilder.EmitLuaAliases(cb);

        switch (kind)
        {
            case ClassKind.Handle:
                cb.AppendLine($"---@class {className}{(baseClass != null ? $" : {baseClass}" : "")}");
                cb.AppendLine($"---@field handle number");
                cb.AppendLine($"local {className} = {{}}");
                if (baseClass != null)
                {
                    cb.AppendLine($"setmetatable({className}, {{ __index = {baseClass} }})");
                }
                cb.AppendLine($"{className}.__index = {className}");
                cb.AppendLine();
                break;

            case ClassKind.Task:
                var entityType = NativeClassifier.GetTaskEntityType(className);
                cb.AppendLine($"---@class {className}");
                cb.AppendLine($"---@field entity {entityType}");
                cb.AppendLine($"local {className} = {{}}");
                cb.AppendLine($"{className}.__index = {className}");
                cb.AppendLine();
                break;

            case ClassKind.Model:
                cb.AppendLine($"---@class {className}");
                cb.AppendLine($"---@field hash number");
                cb.AppendLine($"local {className} = {{}}");
                cb.AppendLine($"{className}.__index = {className}");
                cb.AppendLine();
                break;

            case ClassKind.Weapon:
                cb.AppendLine($"---@class {className}");
                cb.AppendLine($"---@field ped Ped");
                cb.AppendLine($"local {className} = {{}}");
                cb.AppendLine($"{className}.__index = {className}");
                cb.AppendLine();
                break;

            case ClassKind.Namespace:
                cb.AppendLine($"---@class {className}");
                cb.AppendLine($"local {className} = {{}}");
                cb.AppendLine();
                break;
        }
    }

    public void EmitClassEnd(CodeBuilder cb, string className, ClassKind kind)
    {
        // Add special accessors before returning the class
        if (kind == ClassKind.Handle && className == "Ped")
        {
            cb.AppendLine();
            cb.AppendLine("---@return PedTask");
            cb.AppendLine("function Ped:getTask()");
            cb.Indent();
            cb.AppendLine("if not self._task then");
            cb.Indent();
            cb.AppendLine("self._task = PedTask.new(self)");
            cb.Dedent();
            cb.AppendLine("end");
            cb.AppendLine("return self._task");
            cb.Dedent();
            cb.AppendLine("end");
            cb.AppendLine();
            cb.AppendLine("---@return Weapon");
            cb.AppendLine("function Ped:getWeapon()");
            cb.Indent();
            cb.AppendLine("if not self._weapon then");
            cb.Indent();
            cb.AppendLine("self._weapon = Weapon.new(self)");
            cb.Dedent();
            cb.AppendLine("end");
            cb.AppendLine("return self._weapon");
            cb.Dedent();
            cb.AppendLine("end");
        }
        else if (kind == ClassKind.Handle && className == "Vehicle")
        {
            cb.AppendLine();
            cb.AppendLine("---@return VehicleTask");
            cb.AppendLine("function Vehicle:getTask()");
            cb.Indent();
            cb.AppendLine("if not self._task then");
            cb.Indent();
            cb.AppendLine("self._task = VehicleTask.new(self)");
            cb.Dedent();
            cb.AppendLine("end");
            cb.AppendLine("return self._task");
            cb.Dedent();
            cb.AppendLine("end");
        }
        else if (kind == ClassKind.Handle && className == "Player")
        {
            cb.AppendLine();
            cb.AppendLine("---Gets the player's server ID. In multiplayer, this is the player's unique server-side identifier.");
            cb.AppendLine("---@return number");
            cb.AppendLine("function Player:getServerId()");
            cb.Indent();
            // GET_PLAYER_SERVER_ID = 0x4D97BCC7 (CFX native)
            cb.AppendLine("return inv(0x4D97BCC7, self.handle, rai())");
            cb.Dedent();
            cb.AppendLine("end");
        }
        else if (kind == ClassKind.Handle && className == "Entity")
        {
            cb.AppendLine();
            cb.AppendLine("---Gets the network ID of this entity for network synchronization.");
            cb.AppendLine("---@return number");
            cb.AppendLine("function Entity:getNetworkId()");
            cb.Indent();
            // NETWORK_GET_NETWORK_ID_FROM_ENTITY = 0xF260AF6F43953316
            cb.AppendLine("return inv(0xF260AF6F43953316, self.handle, rai())");
            cb.Dedent();
            cb.AppendLine("end");
        }

        cb.AppendLine($"return {className}");
    }

    public void EmitHandleConstructor(CodeBuilder cb, string className, string? baseClass)
    {
        cb.AppendLine($"---@param handle number");
        cb.AppendLine($"---@return {className}");
        cb.AppendLine($"function {className}.new(handle)");
        cb.Indent();
        if (baseClass != null)
        {
            cb.AppendLine($"local self = {baseClass}.new(handle)");
            cb.AppendLine($"setmetatable(self, {className})");
        }
        else
        {
            cb.AppendLine($"local self = setmetatable({{}}, {className})");
            cb.AppendLine("self.handle = handle");
        }
        cb.AppendLine("return self");
        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitFromHandleMethod(CodeBuilder cb, string className)
    {
        cb.AppendLine($"---@param handle number");
        cb.AppendLine($"---@return {className}|nil");
        cb.AppendLine($"function {className}.fromHandle(handle)");
        cb.Indent();
        cb.AppendLine("if handle == 0 then return nil end");
        cb.AppendLine($"return {className}.new(handle)");
        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitFromNetworkIdMethod(CodeBuilder cb, string className)
    {
        cb.AppendLine($"---@param netId number");
        cb.AppendLine($"---@return {className}|nil");
        cb.AppendLine($"function {className}.fromNetworkId(netId)");
        cb.Indent();
        // NETWORK_DOES_ENTITY_EXIST_WITH_NETWORK_ID = 0x38CE16C96BD11F2C
        // NETWORK_GET_ENTITY_FROM_NETWORK_ID = 0x5B912C3F653822E6
        cb.AppendLine("if not inv(0x38CE16C96BD11F2C, netId, rai()) then return nil end");
        cb.AppendLine($"return {className}.fromHandle(inv(0x5B912C3F653822E6, netId, rai()))");
        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitTaskConstructor(CodeBuilder cb, string className, string entityType, string? baseClass)
    {
        cb.AppendLine($"---@param entity {entityType}");
        cb.AppendLine($"---@return {className}");
        cb.AppendLine($"function {className}.new(entity)");
        cb.Indent();
        cb.AppendLine($"local self = setmetatable({{}}, {className})");
        cb.AppendLine("self.entity = entity");
        cb.AppendLine("return self");
        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitModelConstructor(CodeBuilder cb, string className, string? baseClass)
    {
        cb.AppendLine($"---@param hash number");
        cb.AppendLine($"---@return {className}");
        cb.AppendLine($"function {className}.new(hash)");
        cb.Indent();
        cb.AppendLine($"local self = setmetatable({{}}, {className})");
        cb.AppendLine("self.hash = hash");
        cb.AppendLine("return self");
        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitWeaponConstructor(CodeBuilder cb, string className)
    {
        cb.AppendLine($"---@param ped Ped");
        cb.AppendLine($"---@return {className}");
        cb.AppendLine($"function {className}.new(ped)");
        cb.Indent();
        cb.AppendLine($"local self = setmetatable({{}}, {className})");
        cb.AppendLine("self.ped = ped");
        cb.AppendLine("return self");
        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    // === Method Generation ===

    public void EmitMethodStart(CodeBuilder cb, string className, string methodName, List<MethodParameter> parameters, string returnType, MethodKind kind)
    {
        var paramNames = string.Join(", ", parameters.Select(p => p.Name));
        // Lua doesn't have getters/setters, so treat them as Instance methods
        var separator = (kind == MethodKind.Instance || kind == MethodKind.Getter || kind == MethodKind.Setter) ? ":" : ".";
        cb.AppendLine($"function {className}{separator}{methodName}({paramNames})");
        cb.Indent();
    }

    public void EmitMethodEnd(CodeBuilder cb)
    {
        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitGetterProxy(CodeBuilder cb, string propertyName, string methodName, string returnType)
    {
        // Lua doesn't support getters, so this is a no-op
    }

    public void EmitInvokeNative(CodeBuilder cb, string hash, List<string> args, TypeInfo returnType, List<TypeInfo> outputParamTypes)
    {
        var allArgs = new List<string> { hash };
        allArgs.AddRange(args);

        if (_typeMapper.NeedsResultMarker(returnType))
        {
            allArgs.Add(_typeMapper.GetResultMarker(returnType));
        }

        var invokeExpr = $"inv({string.Join(", ", allArgs)})";
        var hasOutputParams = outputParamTypes.Count > 0;

        // If no output params, use the simple return logic
        if (!hasOutputParams)
        {
            if (returnType.Category == TypeCategory.Void)
            {
                cb.AppendLine(invokeExpr);
            }
            else if (_typeMapper.IsHandleType(returnType))
            {
                var handleClass = NativeClassifier.NormalizeHandleType(returnType.Name);
                cb.AppendLine($"return {handleClass}.fromHandle({invokeExpr})");
            }
            else
            {
                cb.AppendLine($"return {invokeExpr}");
            }
            return;
        }

        // With output params, Lua returns multiple values
        // Generate variable names for each return value
        var varNames = new List<string>();
        int varIndex = 0;

        if (returnType.Category != TypeCategory.Void)
        {
            varNames.Add($"retVal");
            varIndex++;
        }

        for (int i = 0; i < outputParamTypes.Count; i++)
        {
            varNames.Add($"out{i + 1}");
        }

        cb.AppendLine($"local {string.Join(", ", varNames)} = {invokeExpr}");

        // Build the return with proper conversions
        var returnParts = new List<string>();
        int returnIndex = 0;

        if (returnType.Category != TypeCategory.Void)
        {
            if (_typeMapper.IsHandleType(returnType))
            {
                var handleClass = NativeClassifier.NormalizeHandleType(returnType.Name);
                returnParts.Add($"{handleClass}.fromHandle(retVal)");
            }
            else
            {
                returnParts.Add("retVal");
            }
            returnIndex++;
        }

        for (int i = 0; i < outputParamTypes.Count; i++)
        {
            var outputType = outputParamTypes[i];
            var varName = $"out{i + 1}";

            if (outputType.Category == TypeCategory.Handle)
            {
                var handleClass = NativeClassifier.NormalizeHandleType(outputType.Name);
                returnParts.Add($"{handleClass}.fromHandle({varName})");
            }
            else
            {
                returnParts.Add(varName);
            }
        }

        cb.AppendLine($"return {string.Join(", ", returnParts)}");
    }

    // === Struct Generation ===

    public void EmitStructStart(CodeBuilder cb, string structName, int size, List<string> nestedStructImports)
    {
        // Lua doesn't need imports at the top - assumes globals
        cb.AppendLine($"---@class {structName}");
        cb.AppendLine($"local {structName} = {{}}");
        cb.AppendLine($"{structName}.__index = {structName}");
        cb.AppendLine();
        cb.AppendLine($"{structName}.SIZE = 0x{size:X}");
        cb.AppendLine();
    }

    public void EmitStructDocumentation(CodeBuilder cb, StructDefinition structDef)
    {
        // Lua doesn't need struct-level usage documentation
        // The @class annotation is sufficient
    }

    public void EmitStructEnd(CodeBuilder cb, string structName)
    {
        cb.AppendLine($"return {structName}");
    }

    public void EmitStructConstructor(CodeBuilder cb, string structName, int size, bool supportsNesting)
    {
        cb.AppendLine($"function {structName}.new(existingView, offset)");
        cb.Indent();
        cb.AppendLine($"local self = setmetatable({{}}, {structName})");
        cb.AppendLine("if existingView and offset then");
        cb.Indent();
        cb.AppendLine("self._view = existingView");
        cb.AppendLine("self._offset = offset");
        cb.AppendLine("self.buffer = existingView.buffer");
        cb.Dedent();
        cb.AppendLine("else");
        cb.Indent();
        cb.AppendLine($"self._view = DataView.ArrayBuffer(0x{size:X})");
        cb.AppendLine("self._offset = 0");
        cb.AppendLine("self.buffer = self._view.buffer");
        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine("return self");
        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitPrimitiveGetter(CodeBuilder cb, string structName, string fieldName, int offset, TypeInfo type, string? comment)
    {
        var (luaType, getter, _) = _typeMapper.GetDataViewAccessor(type);
        var needsEndian = StructLayoutCalculator.NeedsEndianArgument(type);
        var endianArg = needsEndian ? ", true" : "";

        new LuaDocBuilder()
            .AddReturn(luaType)
            .Render(cb);

        cb.AppendLine($"function {structName}:get{fieldName}()");
        cb.Indent();

        if (type.IsBool)
        {
            cb.AppendLine($"return self._view:{getter}(self._offset + {offset}{endianArg}) ~= 0");
        }
        else
        {
            cb.AppendLine($"return self._view:{getter}(self._offset + {offset}{endianArg})");
        }

        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitPrimitiveSetter(CodeBuilder cb, string structName, string fieldName, int offset, TypeInfo type)
    {
        var (luaType, _, setter) = _typeMapper.GetDataViewAccessor(type);
        var needsEndian = StructLayoutCalculator.NeedsEndianArgument(type);
        var endianArg = needsEndian ? ", true" : "";

        new LuaDocBuilder()
            .AddParam("value", luaType)
            .Render(cb);

        cb.AppendLine($"function {structName}:set{fieldName}(value)");
        cb.Indent();

        if (type.IsBool)
        {
            cb.AppendLine($"self._view:{setter}(self._offset + {offset}, value and 1 or 0{endianArg})");
        }
        else
        {
            cb.AppendLine($"self._view:{setter}(self._offset + {offset}, value{endianArg})");
        }

        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitArrayGetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type, string? comment)
    {
        var (luaType, getter, _) = _typeMapper.GetDataViewAccessor(type);
        var needsEndian = StructLayoutCalculator.NeedsEndianArgument(type);
        var endianArg = needsEndian ? ", true" : "";

        new LuaDocBuilder()
            .AddParam("index", "number", $"Array index (0-{arraySize - 1})")
            .AddReturn(luaType)
            .Render(cb);

        cb.AppendLine($"function {structName}:get{fieldName}(index)");
        cb.Indent();
        cb.AppendLine($"if index < 0 or index >= {arraySize} then error('Index out of bounds') end");

        if (type.IsBool)
        {
            cb.AppendLine($"return self._view:{getter}(self._offset + {offset} + index * {elementSize}{endianArg}) ~= 0");
        }
        else
        {
            cb.AppendLine($"return self._view:{getter}(self._offset + {offset} + index * {elementSize}{endianArg})");
        }

        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitArraySetter(CodeBuilder cb, string structName, string fieldName, int offset, int elementSize, int arraySize, TypeInfo type)
    {
        var (luaType, _, setter) = _typeMapper.GetDataViewAccessor(type);
        var needsEndian = StructLayoutCalculator.NeedsEndianArgument(type);
        var endianArg = needsEndian ? ", true" : "";

        new LuaDocBuilder()
            .AddParam("index", "number", $"Array index (0-{arraySize - 1})")
            .AddParam("value", luaType)
            .Render(cb);

        cb.AppendLine($"function {structName}:set{fieldName}(index, value)");
        cb.Indent();
        cb.AppendLine($"if index < 0 or index >= {arraySize} then error('Index out of bounds') end");

        if (type.IsBool)
        {
            cb.AppendLine($"self._view:{setter}(self._offset + {offset} + index * {elementSize}, value and 1 or 0{endianArg})");
        }
        else
        {
            cb.AppendLine($"self._view:{setter}(self._offset + {offset} + index * {elementSize}, value{endianArg})");
        }

        cb.Dedent();
        cb.AppendLine("end");
        cb.AppendLine();
    }

    public void EmitNestedStructAccessor(CodeBuilder cb, string structName, string fieldName, string nestedStructName, int offset, bool isArray, int arraySize, string? comment)
    {
        if (isArray)
        {
            new LuaDocBuilder()
                .AddParam("index", "number", $"Array index (0-{arraySize - 1})")
                .AddReturn(nestedStructName)
                .Render(cb);

            cb.AppendLine($"function {structName}:get{fieldName}(index)");
            cb.Indent();
            cb.AppendLine($"if index < 0 or index >= {arraySize} then error('Index out of bounds') end");
            cb.AppendLine($"return {nestedStructName}.new(self._view, self._offset + {offset} + index * {nestedStructName}.SIZE)");
            cb.Dedent();
            cb.AppendLine("end");
            cb.AppendLine();
        }
        else
        {
            new LuaDocBuilder()
                .AddReturn(nestedStructName)
                .Render(cb);

            cb.AppendLine($"function {structName}:get{fieldName}()");
            cb.Indent();
            cb.AppendLine($"return {nestedStructName}.new(self._view, self._offset + {offset})");
            cb.Dedent();
            cb.AppendLine("end");
            cb.AppendLine();
        }
    }
}
