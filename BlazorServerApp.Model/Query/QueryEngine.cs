using System.Linq.Expressions;

namespace BlazorServerApp.Model.Query;

/// <summary>
/// Façade principale du moteur de requêtes.
/// Parse un texte de requête et génère une Expression&lt;Func&lt;TEntity, bool&gt;&gt; pour EF Core.
/// </summary>
public class QueryEngine
{
    /// <summary>
    /// Parse et construit un filtre en une seule étape.
    /// Lève une QueryParseException si la requête est invalide.
    /// </summary>
    public Expression<Func<TEntity, bool>> BuildFilter<TEntity>(
        string queryText,
        Dictionary<string, string>? fieldAliases = null)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            // Retourne un filtre qui accepte tout
            return _ => true;
        }

        var lexer = new QueryLexer(queryText);
        var tokens = lexer.Tokenize();

        var parser = new QueryParser(tokens);
        var ast = parser.Parse();

        return ExpressionBuilder.Build<TEntity>(ast, fieldAliases);
    }

    /// <summary>
    /// Version Try qui retourne false avec un message d'erreur si la requête est invalide.
    /// </summary>
    public bool TryBuildFilter<TEntity>(
        string queryText,
        out Expression<Func<TEntity, bool>>? expression,
        out string? error,
        Dictionary<string, string>? fieldAliases = null)
    {
        try
        {
            expression = BuildFilter<TEntity>(queryText, fieldAliases);
            error = null;
            return true;
        }
        catch (QueryParseException ex)
        {
            expression = null;
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            expression = null;
            error = $"Unexpected error: {ex.Message}";
            return false;
        }
    }
}
