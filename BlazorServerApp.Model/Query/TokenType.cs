namespace BlazorServerApp.Model.Query;

/// <summary>
/// Types de tokens pour le langage de requête.
/// </summary>
public enum TokenType
{
    // Valeurs
    Identifier,
    String,
    Number,
    Boolean,
    Null,

    // Opérateurs de comparaison
    Equal,              // =
    NotEqual,           // !=
    LessThan,           // <
    LessThanOrEqual,    // <=
    GreaterThan,        // >
    GreaterThanOrEqual, // >=

    // Opérateurs string
    Contains,           // contains
    StartsWith,         // startsWith
    EndsWith,           // endsWith

    // Opérateurs spéciaux
    In,                 // in
    IsNull,             // is null
    IsNotNull,          // is not null

    // Opérateurs logiques
    And,                // AND
    Or,                 // OR
    Not,                // NOT

    // Ponctuation
    LeftParen,          // (
    RightParen,         // )
    Comma,              // ,
    Dot,                // .

    // Fin de flux
    Eof
}
