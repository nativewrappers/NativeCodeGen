using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Parsing;

public class StructParser
{
    public ParseResult<StructDefinition> Parse(string content, string filePath)
    {
        var result = new ParseResult<StructDefinition>();

        var lexer = new CLexer(content);
        var tokens = lexer.Tokenize();
        var parser = new StructTokenParser(tokens, filePath);

        try
        {
            var structDef = parser.Parse();
            result.Value = structDef;
        }
        catch (ParseException ex)
        {
            result.Errors.Add(new ParseError
            {
                FilePath = filePath,
                Line = ex.Line,
                Column = ex.Column,
                Message = ex.Message
            });
        }

        return result;
    }

    /// <summary>
    /// Parse all structs from a file (supports multiple structs per file)
    /// </summary>
    public List<ParseResult<StructDefinition>> ParseAll(string content, string filePath)
    {
        var results = new List<ParseResult<StructDefinition>>();

        var lexer = new CLexer(content);
        var tokens = lexer.Tokenize();
        var parser = new StructTokenParser(tokens, filePath);

        while (!parser.IsAtEnd())
        {
            var result = new ParseResult<StructDefinition>();
            try
            {
                var structDef = parser.Parse();
                if (structDef != null)
                {
                    result.Value = structDef;
                    results.Add(result);
                }
            }
            catch (ParseException ex)
            {
                result.Errors.Add(new ParseError
                {
                    FilePath = filePath,
                    Line = ex.Line,
                    Column = ex.Column,
                    Message = ex.Message
                });
                results.Add(result);
                break; // Stop on error
            }
        }

        return results;
    }

    public ParseResult<StructDefinition> ParseFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            return Parse(content, filePath);
        }
        catch (Exception ex)
        {
            var result = new ParseResult<StructDefinition>();
            result.Errors.Add(new ParseError
            {
                FilePath = filePath,
                Line = 1,
                Column = 1,
                Message = $"Failed to read file: {ex.Message}"
            });
            return result;
        }
    }

    public List<ParseResult<StructDefinition>> ParseFileAll(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            return ParseAll(content, filePath);
        }
        catch (Exception ex)
        {
            return new List<ParseResult<StructDefinition>>
            {
                new ParseResult<StructDefinition>
                {
                    Errors = { new ParseError
                    {
                        FilePath = filePath,
                        Line = 1,
                        Column = 1,
                        Message = $"Failed to read file: {ex.Message}"
                    }}
                }
            };
        }
    }
}

internal class StructTokenParser
{
    private readonly List<CToken> _tokens;
    private readonly string _filePath;
    private int _position;

    public StructTokenParser(List<CToken> tokens, string filePath)
    {
        _tokens = tokens;
        _filePath = filePath;
    }

    private CToken Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];

    public bool IsAtEnd() => Current.Type == CTokenType.Eof;

    private CToken Advance()
    {
        var current = Current;
        _position++;
        return current;
    }

    private bool Check(CTokenType type) => Current.Type == type;

    private CToken Expect(CTokenType type, string message)
    {
        if (Check(type))
            return Advance();

        throw new ParseException(_filePath, Current.Line, Current.Column,
            $"{message}. Got '{Current.Value}' ({Current.Type})");
    }

    public StructDefinition Parse()
    {
        var structDef = new StructDefinition { SourceFile = _filePath };

        // Parse struct-level attributes (@alignas(N))
        while (Check(CTokenType.Attribute))
        {
            var attr = Advance();
            if (attr.Value == "@alignas")
            {
                structDef.DefaultAlignment = ParseAlignasValue();
            }
            // Other struct-level attributes can be added here
        }

        // Expect: struct
        var keyword = Expect(CTokenType.Identifier, "Expected 'struct' keyword");
        if (keyword.Value != "struct")
        {
            throw new ParseException(_filePath, keyword.Line, keyword.Column,
                $"Expected 'struct' keyword, got '{keyword.Value}'");
        }

        // Expect: StructName
        var nameToken = Expect(CTokenType.Identifier, "Expected struct name");
        structDef.Name = nameToken.Value;

        // Expect: {
        Expect(CTokenType.LBrace, "Expected '{' to start struct body");

        // Parse fields until }
        while (!Check(CTokenType.RBrace) && !Check(CTokenType.Eof))
        {
            var field = ParseField();
            if (field != null)
            {
                structDef.Fields.Add(field);
            }
        }

        // Expect: } or };
        Expect(CTokenType.RBrace, "Expected '}' to end struct body");

        // Optional semicolon
        if (Check(CTokenType.Semicolon))
        {
            Advance();
        }

        return structDef;
    }

    private StructField? ParseField()
    {
        var field = new StructField();

        // Parse doc comments (///) before the field
        var docComments = new List<string>();
        while (Check(CTokenType.DocComment))
        {
            docComments.Add(Advance().Value);
        }
        if (docComments.Count > 0)
        {
            field.Comment = string.Join("\n", docComments);
        }

        // Parse attributes (@in, @out, @padding, @alignas(N))
        var flags = FieldFlags.None;
        int? fieldAlignment = null;
        while (Check(CTokenType.Attribute))
        {
            var attr = Advance().Value;
            if (attr == "@in")
                flags |= FieldFlags.In;
            else if (attr == "@out")
                flags |= FieldFlags.Out;
            else if (attr == "@padding")
                flags |= FieldFlags.Padding;
            else if (attr == "@alignas")
                fieldAlignment = ParseAlignasValue();
        }
        field.Alignment = fieldAlignment;
        field.Flags = flags;

        if (!Check(CTokenType.Identifier))
            return null;

        // Check for 'struct' keyword (nested struct)
        var firstToken = Advance();
        string typeName;
        bool isNestedStruct = false;

        if (firstToken.Value == "struct")
        {
            // Nested struct: struct TypeName fieldName
            var structNameToken = Expect(CTokenType.Identifier, "Expected struct name");
            typeName = structNameToken.Value;
            isNestedStruct = true;
            field.IsNestedStruct = true;
            field.NestedStructName = typeName;
        }
        else
        {
            typeName = firstToken.Value;
        }

        var isPointer = false;

        // Check for pointer
        if (Check(CTokenType.Star))
        {
            Advance();
            isPointer = true;
        }

        // Field name
        var fieldNameToken = Expect(CTokenType.Identifier, "Expected field name");
        field.Name = fieldNameToken.Value;

        // Check for array syntax [N] or [N*M] etc.
        if (Check(CTokenType.LBracket))
        {
            Advance(); // consume [
            field.ArraySize = ParseArraySizeExpression();
            Expect(CTokenType.RBracket, "Expected ']'");
        }

        field.Type = new TypeInfo
        {
            Name = typeName,
            IsPointer = isPointer,
            Category = isNestedStruct ? TypeCategory.Struct : TypeInfo.Parse(isPointer ? $"{typeName}*" : typeName).Category
        };

        // Expect semicolon
        Expect(CTokenType.Semicolon, "Expected ';' after field declaration");

        return field;
    }

    /// <summary>
    /// Parses an array size expression like 40, 40*3, 8+16, etc.
    /// Supports +, -, *, / operators with standard precedence.
    /// </summary>
    private int ParseArraySizeExpression()
    {
        return ParseAddSubExpr();
    }

    private int ParseAddSubExpr()
    {
        var left = ParseMulDivExpr();

        while (Check(CTokenType.Plus) || Check(CTokenType.Minus))
        {
            var op = Advance();
            var right = ParseMulDivExpr();
            left = op.Type == CTokenType.Plus ? left + right : left - right;
        }

        return left;
    }

    private int ParseMulDivExpr()
    {
        var left = ParsePrimaryExpr();

        while (Check(CTokenType.Star) || Check(CTokenType.Slash))
        {
            var op = Advance();
            var right = ParsePrimaryExpr();
            left = op.Type == CTokenType.Star ? left * right : left / right;
        }

        return left;
    }

    private int ParsePrimaryExpr()
    {
        if (Check(CTokenType.LParen))
        {
            Advance(); // consume (
            var value = ParseArraySizeExpression();
            Expect(CTokenType.RParen, "Expected ')' in expression");
            return value;
        }

        var token = Expect(CTokenType.Number, "Expected number in array size expression");
        return ParseNumber(token.Value);
    }

    private static int ParseNumber(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(value, 16);
        }
        return int.Parse(value);
    }

    /// <summary>
    /// Parses the (N) part of @alignas(N)
    /// </summary>
    private int ParseAlignasValue()
    {
        Expect(CTokenType.LParen, "Expected '(' after @alignas");
        var valueToken = Expect(CTokenType.Number, "Expected alignment value");
        var value = ParseNumber(valueToken.Value);
        Expect(CTokenType.RParen, "Expected ')' after alignment value");
        return value;
    }
}
