namespace BlazorServerApp.ViewModel.Commons.Fields.Query;

/// <summary>
/// Représente un token produit par le lexer.
/// </summary>
/// <param name="Type">Le type du token.</param>
/// <param name="Lexeme">Le texte brut du token.</param>
/// <param name="Value">La valeur parsée (string, decimal, bool, null).</param>
/// <param name="Position">La position du premier caractère dans le texte source.</param>
public record Token(TokenType Type, string Lexeme, object? Value, int Position);
