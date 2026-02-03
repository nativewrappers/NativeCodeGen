using NativeCodeGen.Core.Utilities;

namespace NativeCodeGen.Core.Parsing;

public enum CTokenType
{
    Identifier,
    Number,         // Decimal or hex (0x...)
    Attribute,      // @in, @out, etc.
    LBrace,         // {
    RBrace,         // }
    LBracket,       // [
    RBracket,       // ]
    LParen,         // (
    RParen,         // )
    Comma,          // ,
    Semicolon,      // ;
    Colon,          // :
    Equals,         // =
    Star,           // *
    Minus,          // -
    Plus,           // +
    Slash,          // /
    LShift,         // <<
    RShift,         // >>
    Pipe,           // |
    Ampersand,      // &
    Tilde,          // ~
    LineComment,    // // ...
    DocComment,     // /// ...
    Eof
}

public class CToken
{
    public CTokenType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}

public class CLexer
{
    private readonly string _input;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    public CLexer(string input)
    {
        _input = input;
    }

    public List<CToken> Tokenize()
    {
        var tokens = new List<CToken>();

        while (_position < _input.Length)
        {
            SkipWhitespace();
            if (_position >= _input.Length)
                break;

            var token = NextToken();
            if (token != null)
            {
                // Skip regular line comments, but keep doc comments (///)
                if (token.Type != CTokenType.LineComment)
                    tokens.Add(token);
            }
        }

        tokens.Add(new CToken { Type = CTokenType.Eof, Line = _line, Column = _column });
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_position < _input.Length)
        {
            var ch = _input[_position];
            if (ch == '\n')
            {
                _line++;
                _column = 1;
                _position++;
            }
            else if (char.IsWhiteSpace(ch))
            {
                _column++;
                _position++;
            }
            else
            {
                break;
            }
        }
    }

    private CToken? NextToken()
    {
        var startLine = _line;
        var startCol = _column;
        var ch = _input[_position];

        // Line comment or doc comment
        if (ch == '/' && _position + 1 < _input.Length && _input[_position + 1] == '/')
        {
            _position += 2;
            _column += 2;

            // Check for doc comment (///)
            bool isDocComment = _position < _input.Length && _input[_position] == '/';
            if (isDocComment)
            {
                _position++;
                _column++;
            }

            var start = _position;
            while (_position < _input.Length && _input[_position] != '\n')
            {
                _position++;
                _column++;
            }
            return new CToken
            {
                Type = isDocComment ? CTokenType.DocComment : CTokenType.LineComment,
                Value = _input[start.._position].Trim(),
                Line = startLine,
                Column = startCol
            };
        }

        // Number (decimal or hex)
        if (char.IsDigit(ch) || (ch == '-' && _position + 1 < _input.Length && char.IsDigit(_input[_position + 1])))
        {
            return ReadNumber(startLine, startCol);
        }

        // Attribute (@in, @out)
        if (ch == '@')
        {
            _position++;
            _column++;
            var start = _position;
            while (_position < _input.Length && (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
            {
                _position++;
                _column++;
            }
            return new CToken
            {
                Type = CTokenType.Attribute,
                Value = "@" + _input[start.._position],
                Line = startLine,
                Column = startCol
            };
        }

        // Identifier or keyword
        if (char.IsLetter(ch) || ch == '_')
        {
            return ReadIdentifier(startLine, startCol);
        }

        // Multi-character operators
        if (ch == '<' && _position + 1 < _input.Length && _input[_position + 1] == '<')
        {
            _position += 2;
            _column += 2;
            return new CToken { Type = CTokenType.LShift, Value = "<<", Line = startLine, Column = startCol };
        }
        if (ch == '>' && _position + 1 < _input.Length && _input[_position + 1] == '>')
        {
            _position += 2;
            _column += 2;
            return new CToken { Type = CTokenType.RShift, Value = ">>", Line = startLine, Column = startCol };
        }

        // Single character tokens
        _position++;
        _column++;
        return ch switch
        {
            '{' => new CToken { Type = CTokenType.LBrace, Value = "{", Line = startLine, Column = startCol },
            '}' => new CToken { Type = CTokenType.RBrace, Value = "}", Line = startLine, Column = startCol },
            '[' => new CToken { Type = CTokenType.LBracket, Value = "[", Line = startLine, Column = startCol },
            ']' => new CToken { Type = CTokenType.RBracket, Value = "]", Line = startLine, Column = startCol },
            '(' => new CToken { Type = CTokenType.LParen, Value = "(", Line = startLine, Column = startCol },
            ')' => new CToken { Type = CTokenType.RParen, Value = ")", Line = startLine, Column = startCol },
            ',' => new CToken { Type = CTokenType.Comma, Value = ",", Line = startLine, Column = startCol },
            ';' => new CToken { Type = CTokenType.Semicolon, Value = ";", Line = startLine, Column = startCol },
            ':' => new CToken { Type = CTokenType.Colon, Value = ":", Line = startLine, Column = startCol },
            '=' => new CToken { Type = CTokenType.Equals, Value = "=", Line = startLine, Column = startCol },
            '*' => new CToken { Type = CTokenType.Star, Value = "*", Line = startLine, Column = startCol },
            '-' => new CToken { Type = CTokenType.Minus, Value = "-", Line = startLine, Column = startCol },
            '+' => new CToken { Type = CTokenType.Plus, Value = "+", Line = startLine, Column = startCol },
            '/' => new CToken { Type = CTokenType.Slash, Value = "/", Line = startLine, Column = startCol },
            '|' => new CToken { Type = CTokenType.Pipe, Value = "|", Line = startLine, Column = startCol },
            '&' => new CToken { Type = CTokenType.Ampersand, Value = "&", Line = startLine, Column = startCol },
            '~' => new CToken { Type = CTokenType.Tilde, Value = "~", Line = startLine, Column = startCol },
            '<' => new CToken { Type = CTokenType.Identifier, Value = "<", Line = startLine, Column = startCol }, // Fallback
            '>' => new CToken { Type = CTokenType.Identifier, Value = ">", Line = startLine, Column = startCol }, // Fallback
            _ => null // Skip unknown characters
        };
    }

    private CToken ReadNumber(int startLine, int startCol)
    {
        var start = _position;

        // Handle negative sign
        if (_input[_position] == '-')
        {
            _position++;
            _column++;
        }

        // Check for hex prefix
        if (_position + 1 < _input.Length && _input[_position] == '0' &&
            (_input[_position + 1] == 'x' || _input[_position + 1] == 'X'))
        {
            _position += 2;
            _column += 2;
            // Read hex digits
            while (_position < _input.Length && LexerUtilities.IsHexDigit(_input[_position]))
            {
                _position++;
                _column++;
            }
        }
        else
        {
            // Read decimal digits
            while (_position < _input.Length && char.IsDigit(_input[_position]))
            {
                _position++;
                _column++;
            }
        }

        return new CToken
        {
            Type = CTokenType.Number,
            Value = _input[start.._position],
            Line = startLine,
            Column = startCol
        };
    }

    private CToken ReadIdentifier(int startLine, int startCol)
    {
        var start = _position;
        while (_position < _input.Length && (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
        {
            _position++;
            _column++;
        }

        return new CToken
        {
            Type = CTokenType.Identifier,
            Value = _input[start.._position],
            Line = startLine,
            Column = startCol
        };
    }

}
