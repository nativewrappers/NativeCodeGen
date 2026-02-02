using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Parsing;

public class SignatureParser
{
    private readonly List<Token> _tokens;
    private int _position;
    private readonly string _filePath;
    private readonly int _baseLineNumber;

    public SignatureParser(List<Token> tokens, string filePath, int baseLineNumber = 1)
    {
        _tokens = tokens;
        _filePath = filePath;
        _baseLineNumber = baseLineNumber;
    }

    private Token Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];
    private Token Peek(int offset = 1) =>
        _position + offset < _tokens.Count ? _tokens[_position + offset] : _tokens[^1];

    private Token Advance()
    {
        var current = Current;
        _position++;
        return current;
    }

    private bool Check(TokenType type) => Current.Type == type;
    private bool CheckAny(params TokenType[] types) => types.Contains(Current.Type);

    private Token Expect(TokenType type, string message)
    {
        if (Check(type))
            return Advance();

        throw new ParseException(
            _filePath,
            _baseLineNumber + Current.Line - 1,
            Current.Column,
            $"{message}. Got '{Current.Value}' ({Current.Type})");
    }

    public (TypeInfo returnType, string name, List<NativeParameter> parameters) ParseSignature()
    {
        // Parse return type
        var returnType = ParseType();

        // Parse function name
        var nameToken = Expect(TokenType.Identifier, "Expected function name");
        var name = nameToken.Value;

        // Parse parameters
        Expect(TokenType.LParen, "Expected '('");
        var parameters = ParseParameters();
        Expect(TokenType.RParen, "Expected ')'");

        // Validate: only one @this attribute allowed
        var thisParams = parameters.Where(p => p.IsThis).ToList();
        if (thisParams.Count > 1)
        {
            throw new ParseException(
                _filePath,
                _baseLineNumber,
                0,
                $"Multiple @this attributes found. Only one parameter can have @this.");
        }

        // Validate: @in can only be applied to pointer types (not structs)
        foreach (var param in parameters.Where(p => p.IsIn))
        {
            if (!param.Type.IsPointer)
            {
                throw new ParseException(
                    _filePath,
                    _baseLineNumber,
                    0,
                    $"@in attribute on '{param.Name}' requires a pointer type.");
            }

            if (param.Type.Category == TypeCategory.Struct)
            {
                throw new ParseException(
                    _filePath,
                    _baseLineNumber,
                    0,
                    $"@in attribute cannot be applied to struct pointer '{param.Name}'. Use struct field attributes instead.");
            }
        }

        // Optional semicolon
        if (Check(TokenType.Semicolon))
            Advance();

        return (returnType, name, parameters);
    }

    private TypeInfo ParseType()
    {
        // Consume any attributes before return type (they're ignored for return types)
        ParseAttributeFlags();

        var typeToken = Expect(TokenType.Identifier, "Expected type name");
        var typeName = typeToken.Value;
        var isPointer = false;
        int? arraySize = null;

        if (Check(TokenType.Star))
        {
            Advance();
            isPointer = true;
        }

        // Check for fixed-size array syntax: type[N]
        if (Check(TokenType.LBracket))
        {
            Advance();
            var sizeToken = Expect(TokenType.Number, "Expected array size");
            arraySize = int.Parse(sizeToken.Value);
            Expect(TokenType.RBracket, "Expected ']'");
        }

        return new TypeInfo
        {
            Name = typeName,
            IsPointer = isPointer,
            Category = TypeInfo.CategorizeType(typeName, isPointer),
            ArraySize = arraySize
        };
    }

    private List<NativeParameter> ParseParameters()
    {
        var parameters = new List<NativeParameter>();

        if (Check(TokenType.RParen))
            return parameters;

        do
        {
            if (Check(TokenType.Comma))
                Advance();

            var param = ParseParameter();
            parameters.Add(param);
        } while (Check(TokenType.Comma));

        return parameters;
    }

    private NativeParameter ParseParameter()
    {
        var flags = ParseAttributeFlags();
        var type = ParseTypeInfo();

        // Handle variadic parameters: ...args or just ...
        var isVariadic = false;
        if (Check(TokenType.Ellipsis))
        {
            Advance();
            isVariadic = true;
        }

        string name;
        if (Check(TokenType.Identifier))
        {
            name = Advance().Value;
        }
        else if (isVariadic)
        {
            name = "args"; // Default name for variadic
        }
        else
        {
            var token = Current;
            throw new ParseException(_filePath, _baseLineNumber + token.Line - 1, token.Column,
                $"Expected parameter name. Got '{token.Value}' ({token.Type})");
        }

        string? defaultValue = null;
        if (Check(TokenType.Equals))
        {
            Advance();
            defaultValue = ParseDefaultValue();
        }

        // Compute Output flag based on type
        // Output is true for pointer types that aren't strings or structs, unless @in is set
        var isOutputPointer = type.IsPointer &&
                              type.Category != TypeCategory.String &&
                              type.Category != TypeCategory.Struct;

        if (isOutputPointer)
        {
            flags |= ParamFlags.Output;
        }

        return new NativeParameter
        {
            Name = isVariadic ? "..." + name : name,
            Type = type,
            DefaultValue = defaultValue,
            Flags = flags
        };
    }

    private ParamFlags ParseAttributeFlags()
    {
        var flags = ParamFlags.None;

        while (Check(TokenType.Attribute))
        {
            var attr = Advance().Value;
            switch (attr)
            {
                case "@this":
                    flags |= ParamFlags.This;
                    break;
                case "@nullable":
                    flags |= ParamFlags.Nullable;
                    break;
                case "@in":
                    flags |= ParamFlags.In;
                    break;
                default:
                    // Error on unknown attributes
                    throw new ParseException(
                        _filePath,
                        _baseLineNumber,
                        0,
                        $"Unknown attribute '{attr}'. Valid attributes: {TypeInfo.ValidAttributesList}");
            }
        }

        return flags;
    }

    private TypeInfo ParseTypeInfo()
    {
        var typeToken = Expect(TokenType.Identifier, "Expected type name");
        var typeName = typeToken.Value;
        var isPointer = false;
        int? arraySize = null;

        if (Check(TokenType.Star))
        {
            Advance();
            isPointer = true;
        }

        // Check for fixed-size array syntax: type[N]
        if (Check(TokenType.LBracket))
        {
            Advance();
            var sizeToken = Expect(TokenType.Number, "Expected array size");
            arraySize = int.Parse(sizeToken.Value);
            Expect(TokenType.RBracket, "Expected ']'");
        }

        return new TypeInfo
        {
            Name = typeName,
            IsPointer = isPointer,
            Category = TypeInfo.CategorizeType(typeName, isPointer),
            ArraySize = arraySize
        };
    }

    private string ParseDefaultValue()
    {
        var value = new System.Text.StringBuilder();

        // Handle negative numbers
        if (Check(TokenType.Minus))
        {
            value.Append(Advance().Value);
        }

        if (Check(TokenType.Number))
        {
            value.Append(Advance().Value);
        }
        else if (Check(TokenType.True))
        {
            value.Append(Advance().Value);
        }
        else if (Check(TokenType.False))
        {
            value.Append(Advance().Value);
        }
        else if (Check(TokenType.String))
        {
            value.Append(Advance().Value);
        }
        else if (Check(TokenType.Hash))
        {
            value.Append(Advance().Value);
        }
        else if (Check(TokenType.Identifier))
        {
            // Could be an enum value or constant
            value.Append(Advance().Value);
        }
        else
        {
            throw new ParseException(
                _filePath,
                _baseLineNumber + Current.Line - 1,
                Current.Column,
                $"Expected default value, got '{Current.Value}'");
        }

        // Handle float suffix that might be separate
        if (value.Length > 0 && Check(TokenType.Identifier) && Current.Value == "f")
        {
            value.Append(Advance().Value);
        }

        return value.ToString();
    }
}

public class ParseException : Exception
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }

    public ParseException(string filePath, int line, int column, string message)
        : base(message)
    {
        FilePath = filePath;
        Line = line;
        Column = column;
    }
}
