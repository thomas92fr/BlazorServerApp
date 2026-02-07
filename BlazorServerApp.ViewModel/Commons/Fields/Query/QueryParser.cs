namespace BlazorServerApp.ViewModel.Commons.Fields.Query;

/// <summary>
/// Parser recursive descent qui convertit une liste de tokens en AST.
/// Précédence (du plus bas au plus haut) : OR → AND → NOT → comparaison.
/// </summary>
public class QueryParser
{
    private readonly List<Token> _tokens;
    private int _current;

    public QueryParser(List<Token> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public QueryNode Parse()
    {
        _current = 0;
        var node = ParseOr();

        if (!IsAtEnd())
        {
            var token = Current();
            throw new QueryParseException(
                $"Unexpected token '{token.Lexeme}' at position {token.Position}.",
                token.Position);
        }

        return node;
    }

    private QueryNode ParseOr()
    {
        var left = ParseAnd();

        while (Match(TokenType.Or))
        {
            var right = ParseAnd();
            left = new BinaryNode(left, BinaryOp.Or, right);
        }

        return left;
    }

    private QueryNode ParseAnd()
    {
        var left = ParseNot();

        while (Match(TokenType.And))
        {
            var right = ParseNot();
            left = new BinaryNode(left, BinaryOp.And, right);
        }

        return left;
    }

    private QueryNode ParseNot()
    {
        if (Match(TokenType.Not))
        {
            var operand = ParseNot();
            return new NotNode(operand);
        }

        return ParsePrimary();
    }

    private QueryNode ParsePrimary()
    {
        // Grouped expression: (expr)
        if (Match(TokenType.LeftParen))
        {
            var node = ParseOr();
            Expect(TokenType.RightParen, "Expected ')' to close grouped expression");
            return node;
        }

        // Comparison: field operator value
        return ParseComparison();
    }

    private QueryNode ParseComparison()
    {
        var fieldToken = Expect(TokenType.Identifier, "Expected field name");
        var fieldName = (string)fieldToken.Value!;

        // Support dotted paths: Mentor.Age, Mentor.Mentor.Name
        while (Current().Type == TokenType.Dot)
        {
            Advance(); // consume the dot
            var nextToken = Expect(TokenType.Identifier, "Expected property name after '.'");
            fieldName += "." + (string)nextToken.Value!;
        }

        var opToken = Current();

        // Handle "is null" / "is not null"
        if (opToken.Type == TokenType.IsNull)
        {
            Advance();
            return new ComparisonNode(fieldName, ComparisonOp.IsNull, null);
        }

        if (opToken.Type == TokenType.IsNotNull)
        {
            Advance();
            return new ComparisonNode(fieldName, ComparisonOp.IsNotNull, null);
        }

        // Handle standard comparison operators
        var comparisonOp = opToken.Type switch
        {
            TokenType.Equal => ComparisonOp.Equal,
            TokenType.NotEqual => ComparisonOp.NotEqual,
            TokenType.LessThan => ComparisonOp.LessThan,
            TokenType.LessThanOrEqual => ComparisonOp.LessThanOrEqual,
            TokenType.GreaterThan => ComparisonOp.GreaterThan,
            TokenType.GreaterThanOrEqual => ComparisonOp.GreaterThanOrEqual,
            TokenType.Contains => ComparisonOp.Contains,
            TokenType.StartsWith => ComparisonOp.StartsWith,
            TokenType.EndsWith => ComparisonOp.EndsWith,
            TokenType.In => ComparisonOp.In,
            _ => throw new QueryParseException(
                $"Expected operator but got '{opToken.Lexeme}' at position {opToken.Position}.",
                opToken.Position)
        };
        Advance();

        // Handle IN operator: field in (value1, value2, ...)
        if (comparisonOp == ComparisonOp.In)
        {
            var values = ParseValueList();
            return new ComparisonNode(fieldName, ComparisonOp.In, values);
        }

        // Parse single value
        var value = ParseValue();
        return new ComparisonNode(fieldName, comparisonOp, value);
    }

    private object? ParseValue()
    {
        var token = Current();

        switch (token.Type)
        {
            case TokenType.String:
                Advance();
                return (string)token.Value!;

            case TokenType.Number:
                Advance();
                return (decimal)token.Value!;

            case TokenType.Boolean:
                Advance();
                return (bool)token.Value!;

            case TokenType.Null:
                Advance();
                return null;

            default:
                throw new QueryParseException(
                    $"Expected value but got '{token.Lexeme}' at position {token.Position}.",
                    token.Position);
        }
    }

    private List<object> ParseValueList()
    {
        Expect(TokenType.LeftParen, "Expected '(' after 'in'");

        var values = new List<object>();
        values.Add(ParseValue()!);

        while (Match(TokenType.Comma))
        {
            values.Add(ParseValue()!);
        }

        Expect(TokenType.RightParen, "Expected ')' to close value list");
        return values;
    }

    #region Helpers

    private Token Current()
    {
        if (_current >= _tokens.Count)
        {
            return _tokens[^1]; // Return EOF
        }
        return _tokens[_current];
    }

    private bool IsAtEnd() => Current().Type == TokenType.Eof;

    private Token Advance()
    {
        var token = Current();
        if (!IsAtEnd()) _current++;
        return token;
    }

    private bool Match(TokenType type)
    {
        if (Current().Type == type)
        {
            Advance();
            return true;
        }
        return false;
    }

    private Token Expect(TokenType type, string errorMessage)
    {
        var token = Current();
        if (token.Type != type)
        {
            throw new QueryParseException(
                $"{errorMessage} but got '{token.Lexeme}' at position {token.Position}.",
                token.Position);
        }
        return Advance();
    }

    #endregion
}
