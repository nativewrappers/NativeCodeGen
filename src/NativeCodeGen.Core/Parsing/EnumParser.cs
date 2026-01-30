using NativeCodeGen.Core.Models;

namespace NativeCodeGen.Core.Parsing;

public class EnumParser
{
    public ParseResult<EnumDefinition> Parse(string content, string filePath)
    {
        var result = new ParseResult<EnumDefinition>();
        var enumDef = new EnumDefinition { SourceFile = filePath };

        // Skip frontmatter if present (for .mdx files)
        var contentToParse = SkipFrontmatter(content);

        var lexer = new CLexer(contentToParse);
        var tokens = lexer.Tokenize();
        var parser = new EnumTokenParser(tokens, filePath);

        try
        {
            enumDef = parser.Parse();
            result.Value = enumDef;
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

    private static string SkipFrontmatter(string content)
    {
        var lines = content.Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
            return content;

        int endLine = 1;
        while (endLine < lines.Length && lines[endLine].Trim() != "---")
        {
            endLine++;
        }

        if (endLine < lines.Length)
        {
            return string.Join('\n', lines.Skip(endLine + 1));
        }

        return content;
    }

    public ParseResult<EnumDefinition> ParseFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            return Parse(content, filePath);
        }
        catch (Exception ex)
        {
            var result = new ParseResult<EnumDefinition>();
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
}

internal class EnumTokenParser
{
    private readonly List<CToken> _tokens;
    private readonly string _filePath;
    private int _position;

    public EnumTokenParser(List<CToken> tokens, string filePath)
    {
        _tokens = tokens;
        _filePath = filePath;
    }

    private CToken Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];

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

    public EnumDefinition Parse()
    {
        var enumDef = new EnumDefinition { SourceFile = _filePath };

        // Expect: enum keyword
        var keyword = Expect(CTokenType.Identifier, "Expected 'enum' keyword");
        if (keyword.Value != "enum")
        {
            throw new ParseException(_filePath, keyword.Line, keyword.Column,
                $"Expected 'enum' keyword, got '{keyword.Value}'");
        }

        // Expect: EnumName
        var nameToken = Expect(CTokenType.Identifier, "Expected enum name");
        enumDef.Name = nameToken.Value;

        // Optional: : BaseType
        if (Check(CTokenType.Colon))
        {
            Advance(); // consume ':'
            var baseTypeToken = Expect(CTokenType.Identifier, "Expected base type after ':'");
            enumDef.BaseType = baseTypeToken.Value;
        }

        // Expect: {
        Expect(CTokenType.LBrace, "Expected '{' to start enum body");

        // Parse members until }
        while (!Check(CTokenType.RBrace) && !Check(CTokenType.Eof))
        {
            var member = ParseMember();
            if (member != null)
            {
                enumDef.Members.Add(member);
            }

            // Optional comma between members
            if (Check(CTokenType.Comma))
            {
                Advance();
            }
        }

        // Expect: } or };
        Expect(CTokenType.RBrace, "Expected '}' to end enum body");

        // Optional semicolon
        if (Check(CTokenType.Semicolon))
        {
            Advance();
        }

        // Fill in missing values (C-style enum: unassigned = previous + 1)
        FillMissingValues(enumDef.Members);

        return enumDef;
    }

    private static void FillMissingValues(List<EnumMember> members)
    {
        long nextValue = 0;

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];

            if (member.Value != null)
            {
                // Try to parse the explicit value
                if (TryParseEnumValue(member.Value, out var parsed))
                {
                    nextValue = parsed + 1;
                }
                else
                {
                    // Complex expression - can't determine next value, reset to unknown
                    // Keep the expression as-is and hope next member has explicit value
                    nextValue = 0;
                }
            }
            else
            {
                // No explicit value - assign sequential value
                member.Value = nextValue.ToString();
                nextValue++;
            }
        }
    }

    private static bool TryParseEnumValue(string value, out long result)
    {
        result = 0;
        var trimmed = value.Trim();

        // Handle hex values
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, null, out result);
        }

        // Handle negative and positive integers
        return long.TryParse(trimmed, out result);
    }

    private EnumMember? ParseMember()
    {
        if (!Check(CTokenType.Identifier))
        {
            // Skip unexpected token to avoid infinite loop
            if (!Check(CTokenType.RBrace) && !Check(CTokenType.Eof))
                Advance();
            return null;
        }

        var member = new EnumMember();
        member.Name = Advance().Value;

        // Optional: = value (can be number, identifier, or expression like "(1 << 5)")
        if (Check(CTokenType.Equals))
        {
            Advance(); // consume '='
            member.Value = ParseValue();
        }

        return member;
    }

    private string ParseValue()
    {
        var parts = new List<string>();

        // Consume tokens until we hit comma, rbrace, or eof
        while (!Check(CTokenType.Comma) && !Check(CTokenType.RBrace) && !Check(CTokenType.Eof))
        {
            var token = Advance();
            parts.Add(token.Value);
        }

        return string.Concat(parts);
    }
}
