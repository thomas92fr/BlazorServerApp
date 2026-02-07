namespace BlazorServerApp.ViewModel.Commons.Fields.Query;

/// <summary>
/// Noeud de base de l'arbre syntaxique abstrait (AST).
/// </summary>
public abstract record QueryNode;

/// <summary>
/// Opération binaire logique (AND, OR).
/// </summary>
public record BinaryNode(QueryNode Left, BinaryOp Operator, QueryNode Right) : QueryNode;

/// <summary>
/// Opérateurs binaires logiques.
/// </summary>
public enum BinaryOp
{
    And,
    Or
}

/// <summary>
/// Négation logique (NOT).
/// </summary>
public record NotNode(QueryNode Operand) : QueryNode;

/// <summary>
/// Comparaison entre un champ et une valeur.
/// </summary>
public record ComparisonNode(string FieldName, ComparisonOp Operator, object? Value) : QueryNode;

/// <summary>
/// Opérateurs de comparaison supportés.
/// </summary>
public enum ComparisonOp
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    Contains,
    StartsWith,
    EndsWith,
    In,
    IsNull,
    IsNotNull
}
