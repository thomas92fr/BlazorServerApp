namespace BlazorServerApp.ViewModel.Commons.Fields.Query;

/// <summary>
/// Exception levée lors d'une erreur de parsing de requête.
/// </summary>
public class QueryParseException : Exception
{
    public int Position { get; }

    public QueryParseException(string message, int position)
        : base(message)
    {
        Position = position;
    }

    public QueryParseException(string message, int position, Exception innerException)
        : base(message, innerException)
    {
        Position = position;
    }
}
