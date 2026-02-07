using System.Globalization;
using System.Text;

namespace BlazorServerApp.ViewModel.Commons.Fields.Query;

/// <summary>
/// Tokenizer pour le langage de requête.
/// Scanne le texte caractère par caractère et produit une liste de tokens.
/// </summary>
public class QueryLexer
{
    private readonly string _source;
    private int _position;
    private readonly List<Token> _tokens = new();

    public QueryLexer(string source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public List<Token> Tokenize()
    {
        _tokens.Clear();
        _position = 0;

        while (_position < _source.Length)
        {
            SkipWhitespace();
            if (_position >= _source.Length) break;

            var c = _source[_position];

            switch (c)
            {
                case '(':
                    _tokens.Add(new Token(TokenType.LeftParen, "(", null, _position));
                    _position++;
                    break;

                case ')':
                    _tokens.Add(new Token(TokenType.RightParen, ")", null, _position));
                    _position++;
                    break;

                case ',':
                    _tokens.Add(new Token(TokenType.Comma, ",", null, _position));
                    _position++;
                    break;

                case '.':
                    _tokens.Add(new Token(TokenType.Dot, ".", null, _position));
                    _position++;
                    break;

                case '=':
                    _tokens.Add(new Token(TokenType.Equal, "=", null, _position));
                    _position++;
                    break;

                case '!':
                    if (Peek(1) == '=')
                    {
                        _tokens.Add(new Token(TokenType.NotEqual, "!=", null, _position));
                        _position += 2;
                    }
                    else
                    {
                        throw new QueryParseException($"Unexpected character '!' at position {_position}. Did you mean '!='?", _position);
                    }
                    break;

                case '<':
                    if (Peek(1) == '=')
                    {
                        _tokens.Add(new Token(TokenType.LessThanOrEqual, "<=", null, _position));
                        _position += 2;
                    }
                    else
                    {
                        _tokens.Add(new Token(TokenType.LessThan, "<", null, _position));
                        _position++;
                    }
                    break;

                case '>':
                    if (Peek(1) == '=')
                    {
                        _tokens.Add(new Token(TokenType.GreaterThanOrEqual, ">=", null, _position));
                        _position += 2;
                    }
                    else
                    {
                        _tokens.Add(new Token(TokenType.GreaterThan, ">", null, _position));
                        _position++;
                    }
                    break;

                case '"':
                    ReadString();
                    break;

                default:
                    if (char.IsDigit(c) || (c == '-' && _position + 1 < _source.Length && char.IsDigit(_source[_position + 1])))
                    {
                        ReadNumber();
                    }
                    else if (char.IsLetter(c) || c == '_')
                    {
                        ReadIdentifierOrKeyword();
                    }
                    else
                    {
                        throw new QueryParseException($"Unexpected character '{c}' at position {_position}.", _position);
                    }
                    break;
            }
        }

        _tokens.Add(new Token(TokenType.Eof, "", null, _position));
        return _tokens;
    }

    private void SkipWhitespace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            _position++;
        }
    }

    private char Peek(int offset)
    {
        var index = _position + offset;
        return index < _source.Length ? _source[index] : '\0';
    }

    private void ReadString()
    {
        var start = _position;
        _position++; // Skip opening quote

        var sb = new StringBuilder();
        while (_position < _source.Length && _source[_position] != '"')
        {
            if (_source[_position] == '\\' && _position + 1 < _source.Length)
            {
                _position++; // Skip backslash
                sb.Append(_source[_position]);
            }
            else
            {
                sb.Append(_source[_position]);
            }
            _position++;
        }

        if (_position >= _source.Length)
        {
            throw new QueryParseException($"Unterminated string starting at position {start}.", start);
        }

        _position++; // Skip closing quote
        var value = sb.ToString();
        _tokens.Add(new Token(TokenType.String, _source[start.._position], value, start));
    }

    private void ReadNumber()
    {
        var start = _position;
        var hasDecimal = false;

        if (_source[_position] == '-')
        {
            _position++;
        }

        while (_position < _source.Length && (char.IsDigit(_source[_position]) || _source[_position] == '.'))
        {
            if (_source[_position] == '.')
            {
                if (hasDecimal) break;
                hasDecimal = true;
            }
            _position++;
        }

        var lexeme = _source[start.._position];
        var value = decimal.Parse(lexeme, CultureInfo.InvariantCulture);
        _tokens.Add(new Token(TokenType.Number, lexeme, value, start));
    }

    private void ReadIdentifierOrKeyword()
    {
        var start = _position;
        while (_position < _source.Length && (char.IsLetterOrDigit(_source[_position]) || _source[_position] == '_'))
        {
            _position++;
        }

        var lexeme = _source[start.._position];
        var upper = lexeme.ToUpperInvariant();

        switch (upper)
        {
            case "AND":
                _tokens.Add(new Token(TokenType.And, lexeme, null, start));
                break;
            case "OR":
                _tokens.Add(new Token(TokenType.Or, lexeme, null, start));
                break;
            case "NOT":
                _tokens.Add(new Token(TokenType.Not, lexeme, null, start));
                break;
            case "TRUE":
                _tokens.Add(new Token(TokenType.Boolean, lexeme, true, start));
                break;
            case "FALSE":
                _tokens.Add(new Token(TokenType.Boolean, lexeme, false, start));
                break;
            case "NULL":
                _tokens.Add(new Token(TokenType.Null, lexeme, null, start));
                break;
            case "CONTAINS":
                _tokens.Add(new Token(TokenType.Contains, lexeme, null, start));
                break;
            case "STARTSWITH":
                _tokens.Add(new Token(TokenType.StartsWith, lexeme, null, start));
                break;
            case "ENDSWITH":
                _tokens.Add(new Token(TokenType.EndsWith, lexeme, null, start));
                break;
            case "IN":
                _tokens.Add(new Token(TokenType.In, lexeme, null, start));
                break;
            case "IS":
                ResolveIsKeyword(start, lexeme);
                break;
            default:
                _tokens.Add(new Token(TokenType.Identifier, lexeme, lexeme, start));
                break;
        }
    }

    /// <summary>
    /// Résout "is null" et "is not null" en tokens composites.
    /// </summary>
    private void ResolveIsKeyword(int start, string isLexeme)
    {
        SkipWhitespace();

        if (_position >= _source.Length)
        {
            throw new QueryParseException($"Expected 'null' or 'not null' after 'is' at position {start}.", start);
        }

        var nextStart = _position;
        while (_position < _source.Length && char.IsLetter(_source[_position]))
        {
            _position++;
        }

        var nextWord = _source[nextStart.._position].ToUpperInvariant();

        if (nextWord == "NULL")
        {
            _tokens.Add(new Token(TokenType.IsNull, _source[start.._position], null, start));
        }
        else if (nextWord == "NOT")
        {
            SkipWhitespace();
            var nullStart = _position;
            while (_position < _source.Length && char.IsLetter(_source[_position]))
            {
                _position++;
            }

            var nullWord = _source[nullStart.._position].ToUpperInvariant();
            if (nullWord == "NULL")
            {
                _tokens.Add(new Token(TokenType.IsNotNull, _source[start.._position], null, start));
            }
            else
            {
                throw new QueryParseException($"Expected 'null' after 'is not' at position {nullStart}, got '{_source[nullStart.._position]}'.", nullStart);
            }
        }
        else
        {
            throw new QueryParseException($"Expected 'null' or 'not null' after 'is' at position {nextStart}, got '{_source[nextStart.._position]}'.", nextStart);
        }
    }
}
