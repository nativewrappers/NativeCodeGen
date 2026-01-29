namespace NativeCodeGen.Core.Parsing;

public enum TokenType
{
    Hash,           // 0x followed by hex digits
    Identifier,     // Type names, parameter names
    LParen,         // (
    RParen,         // )
    Comma,          // ,
    Star,           // *
    Equals,         // =
    Semicolon,      // ;
    Attribute,      // @this, @notnull
    Number,         // Numeric literals
    String,         // String literals
    Minus,          // -
    Dot,            // .
    Ellipsis,       // ...
    True,           // true
    False,          // false
    Eof
}

public class Token
{
    public TokenType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public int Position { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
}

public class SignatureLexer
{
    private readonly string _input;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    public SignatureLexer(string input)
    {
        _input = input;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_position < _input.Length)
        {
            SkipWhitespace();
            if (_position >= _input.Length)
                break;

            var token = NextToken();
            if (token != null)
                tokens.Add(token);
        }

        tokens.Add(new Token { Type = TokenType.Eof, Position = _position, Line = _line, Column = _column });
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
        {
            if (_input[_position] == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _position++;
        }
    }

    private Token? NextToken()
    {
        var startPos = _position;
        var startLine = _line;
        var startCol = _column;
        var ch = _input[_position];

        // Hash literal: 0x...
        if (ch == '0' && _position + 1 < _input.Length && _input[_position + 1] == 'x')
        {
            _position += 2;
            _column += 2;
            while (_position < _input.Length && IsHexDigit(_input[_position]))
            {
                _position++;
                _column++;
            }
            return new Token
            {
                Type = TokenType.Hash,
                Value = _input[startPos.._position],
                Position = startPos,
                Line = startLine,
                Column = startCol
            };
        }

        // Number (including negative)
        if (char.IsDigit(ch))
        {
            return ReadNumber(startPos, startLine, startCol);
        }

        // Attribute: @identifier
        if (ch == '@')
        {
            _position++;
            _column++;
            while (_position < _input.Length && (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
            {
                _position++;
                _column++;
            }
            return new Token
            {
                Type = TokenType.Attribute,
                Value = _input[startPos.._position],
                Position = startPos,
                Line = startLine,
                Column = startCol
            };
        }

        // Identifier or keyword
        if (char.IsLetter(ch) || ch == '_')
        {
            while (_position < _input.Length && (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
            {
                _position++;
                _column++;
            }
            var value = _input[startPos.._position];
            var type = value switch
            {
                "true" => TokenType.True,
                "false" => TokenType.False,
                _ => TokenType.Identifier
            };
            return new Token
            {
                Type = type,
                Value = value,
                Position = startPos,
                Line = startLine,
                Column = startCol
            };
        }

        // String literal
        if (ch == '"')
        {
            _position++;
            _column++;
            while (_position < _input.Length && _input[_position] != '"')
            {
                if (_input[_position] == '\\' && _position + 1 < _input.Length)
                {
                    _position += 2;
                    _column += 2;
                }
                else
                {
                    _position++;
                    _column++;
                }
            }
            if (_position < _input.Length)
            {
                _position++; // Skip closing quote
                _column++;
            }
            return new Token
            {
                Type = TokenType.String,
                Value = _input[startPos.._position],
                Position = startPos,
                Line = startLine,
                Column = startCol
            };
        }

        // Ellipsis (...)
        if (ch == '.' && _position + 2 < _input.Length && _input[_position + 1] == '.' && _input[_position + 2] == '.')
        {
            _position += 3;
            _column += 3;
            return new Token { Type = TokenType.Ellipsis, Value = "...", Position = startPos, Line = startLine, Column = startCol };
        }

        // Single character tokens
        _position++;
        _column++;
        return ch switch
        {
            '(' => new Token { Type = TokenType.LParen, Value = "(", Position = startPos, Line = startLine, Column = startCol },
            ')' => new Token { Type = TokenType.RParen, Value = ")", Position = startPos, Line = startLine, Column = startCol },
            ',' => new Token { Type = TokenType.Comma, Value = ",", Position = startPos, Line = startLine, Column = startCol },
            '*' => new Token { Type = TokenType.Star, Value = "*", Position = startPos, Line = startLine, Column = startCol },
            '=' => new Token { Type = TokenType.Equals, Value = "=", Position = startPos, Line = startLine, Column = startCol },
            ';' => new Token { Type = TokenType.Semicolon, Value = ";", Position = startPos, Line = startLine, Column = startCol },
            '-' => new Token { Type = TokenType.Minus, Value = "-", Position = startPos, Line = startLine, Column = startCol },
            '.' => new Token { Type = TokenType.Dot, Value = ".", Position = startPos, Line = startLine, Column = startCol },
            _ => null
        };
    }

    private Token ReadNumber(int startPos, int startLine, int startCol)
    {
        while (_position < _input.Length && (char.IsDigit(_input[_position]) || _input[_position] == '.'))
        {
            _position++;
            _column++;
        }
        // Handle float suffix
        if (_position < _input.Length && (_input[_position] == 'f' || _input[_position] == 'F'))
        {
            _position++;
            _column++;
        }
        return new Token
        {
            Type = TokenType.Number,
            Value = _input[startPos.._position],
            Position = startPos,
            Line = startLine,
            Column = startCol
        };
    }

    private static bool IsHexDigit(char c) =>
        char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
