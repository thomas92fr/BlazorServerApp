using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace BlazorServerApp.Model.Query;

/// <summary>
/// Convertit un AST (QueryNode) en Expression&lt;Func&lt;TEntity, bool&gt;&gt; pour EF Core.
/// </summary>
public static class ExpressionBuilder
{
    public static Expression<Func<TEntity, bool>> Build<TEntity>(
        QueryNode ast,
        Dictionary<string, string>? fieldAliases = null)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var body = BuildExpression<TEntity>(ast, parameter, fieldAliases);
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static Expression BuildExpression<TEntity>(
        QueryNode node,
        ParameterExpression parameter,
        Dictionary<string, string>? fieldAliases)
    {
        return node switch
        {
            BinaryNode binary => BuildBinary<TEntity>(binary, parameter, fieldAliases),
            NotNode not => Expression.Not(BuildExpression<TEntity>(not.Operand, parameter, fieldAliases)),
            ComparisonNode comparison => BuildComparison<TEntity>(comparison, parameter, fieldAliases),
            _ => throw new QueryParseException($"Unknown AST node type: {node.GetType().Name}.", 0)
        };
    }

    private static Expression BuildBinary<TEntity>(
        BinaryNode node,
        ParameterExpression parameter,
        Dictionary<string, string>? fieldAliases)
    {
        var left = BuildExpression<TEntity>(node.Left, parameter, fieldAliases);
        var right = BuildExpression<TEntity>(node.Right, parameter, fieldAliases);

        return node.Operator switch
        {
            BinaryOp.And => Expression.AndAlso(left, right),
            BinaryOp.Or => Expression.OrElse(left, right),
            _ => throw new QueryParseException($"Unknown binary operator: {node.Operator}.", 0)
        };
    }

    private static Expression BuildComparison<TEntity>(
        ComparisonNode node,
        ParameterExpression parameter,
        Dictionary<string, string>? fieldAliases)
    {
        var resolvedPath = ResolveFieldName<TEntity>(node.FieldName, fieldAliases);
        var (memberAccess, propertyType) = ResolvePropertyPath<TEntity>(resolvedPath, parameter, node.FieldName);
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        // Handle IsNull / IsNotNull
        if (node.Operator == ComparisonOp.IsNull)
        {
            return BuildNullCheck(memberAccess, propertyType, isNull: true);
        }

        if (node.Operator == ComparisonOp.IsNotNull)
        {
            return BuildNullCheck(memberAccess, propertyType, isNull: false);
        }

        // Handle string operators
        if (underlyingType == typeof(string))
        {
            return BuildStringComparison(node, memberAccess);
        }

        // Handle IN operator
        if (node.Operator == ComparisonOp.In)
        {
            return BuildInExpression(node, memberAccess, propertyType, underlyingType);
        }

        // Handle standard comparison operators
        ValidateOperatorForType(node.Operator, underlyingType, node.FieldName);
        var convertedValue = ConvertValue(node.Value, propertyType, underlyingType, node.FieldName);
        var constant = Expression.Constant(convertedValue, propertyType);

        return node.Operator switch
        {
            ComparisonOp.Equal => Expression.Equal(memberAccess, constant),
            ComparisonOp.NotEqual => Expression.NotEqual(memberAccess, constant),
            ComparisonOp.LessThan => Expression.LessThan(memberAccess, constant),
            ComparisonOp.LessThanOrEqual => Expression.LessThanOrEqual(memberAccess, constant),
            ComparisonOp.GreaterThan => Expression.GreaterThan(memberAccess, constant),
            ComparisonOp.GreaterThanOrEqual => Expression.GreaterThanOrEqual(memberAccess, constant),
            _ => throw new QueryParseException(
                $"Operator '{node.Operator}' not supported for property '{node.FieldName}' ({underlyingType.Name}).",
                0)
        };
    }

    /// <summary>
    /// Résout un chemin pointé (ex: "Mentor.Age") en expression chaînée (e.Mentor.Age).
    /// Supporte les chemins simples ("Age") et multi-niveaux ("Mentor.Mentor.Name").
    /// </summary>
    private static (Expression MemberAccess, Type PropertyType) ResolvePropertyPath<TEntity>(
        string path,
        ParameterExpression parameter,
        string originalFieldName)
    {
        var segments = path.Split('.');
        Expression currentExpression = parameter;
        var currentType = typeof(TEntity);

        foreach (var segment in segments)
        {
            var property = currentType.GetProperty(segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null)
            {
                var available = string.Join(", ", currentType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead)
                    .Select(p => p.Name));
                var context = segments.Length > 1
                    ? $" (in path '{originalFieldName}')"
                    : "";
                throw new QueryParseException(
                    $"Field '{segment}' not found on entity '{currentType.Name}'{context}. Available: {available}.",
                    0);
            }

            currentExpression = Expression.Property(currentExpression, property);
            currentType = property.PropertyType;
        }

        return (currentExpression, currentType);
    }

    private static Expression BuildNullCheck(Expression memberAccess, Type propertyType, bool isNull)
    {
        if (propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) == null)
        {
            // Non-nullable value type — "is null" is always false, "is not null" is always true
            return Expression.Constant(!isNull);
        }

        var nullConstant = Expression.Constant(null, propertyType);
        return isNull
            ? Expression.Equal(memberAccess, nullConstant)
            : Expression.NotEqual(memberAccess, nullConstant);
    }

    private static Expression BuildStringComparison(ComparisonNode node, Expression memberAccess)
    {
        if (node.Value is not string strValue)
        {
            throw new QueryParseException(
                $"Expected string value for field '{node.FieldName}', got {node.Value?.GetType().Name ?? "null"}.",
                0);
        }

        var constant = Expression.Constant(strValue, typeof(string));

        switch (node.Operator)
        {
            case ComparisonOp.Equal:
                return Expression.Equal(memberAccess, constant);

            case ComparisonOp.NotEqual:
                return Expression.NotEqual(memberAccess, constant);

            case ComparisonOp.Contains:
                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
                return Expression.Call(memberAccess, containsMethod, constant);

            case ComparisonOp.StartsWith:
                var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!;
                return Expression.Call(memberAccess, startsWithMethod, constant);

            case ComparisonOp.EndsWith:
                var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) })!;
                return Expression.Call(memberAccess, endsWithMethod, constant);

            case ComparisonOp.IsNull:
                return Expression.Equal(memberAccess, Expression.Constant(null, typeof(string)));

            case ComparisonOp.IsNotNull:
                return Expression.NotEqual(memberAccess, Expression.Constant(null, typeof(string)));

            default:
                throw new QueryParseException(
                    $"Operator '{node.Operator}' not supported for string property '{node.FieldName}'.",
                    0);
        }
    }

    private static Expression BuildInExpression(
        ComparisonNode node,
        Expression memberAccess,
        Type propertyType,
        Type underlyingType)
    {
        if (node.Value is not List<object> values)
        {
            throw new QueryParseException($"Expected list of values for 'in' operator on field '{node.FieldName}'.", 0);
        }

        // Convert all values to the property type
        var convertedValues = values.Select(v => ConvertValue(v, underlyingType, underlyingType, node.FieldName)).ToList();

        // Build: new[] { v1, v2, v3 }.Contains(property)
        var arrayType = underlyingType;
        var array = Array.CreateInstance(arrayType, convertedValues.Count);
        for (int i = 0; i < convertedValues.Count; i++)
        {
            array.SetValue(convertedValues[i], i);
        }

        var arrayConstant = Expression.Constant(array);

        // For nullable types, we need to access .Value or cast
        Expression propertyExpression = memberAccess;
        if (Nullable.GetUnderlyingType(propertyType) != null)
        {
            propertyExpression = Expression.Property(memberAccess, "Value");
        }

        var containsMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(arrayType);

        return Expression.Call(null, containsMethod, arrayConstant, propertyExpression);
    }

    private static void ValidateOperatorForType(ComparisonOp op, Type type, string fieldName)
    {
        var isNumeric = type == typeof(int) || type == typeof(decimal) || type == typeof(long)
                     || type == typeof(double) || type == typeof(float) || type == typeof(short);
        var isDateTime = type == typeof(DateTime);
        var isTimeSpan = type == typeof(TimeSpan);
        var isBool = type == typeof(bool);
        var isString = type == typeof(string);

        var comparisonOps = new[]
        {
            ComparisonOp.LessThan, ComparisonOp.LessThanOrEqual,
            ComparisonOp.GreaterThan, ComparisonOp.GreaterThanOrEqual
        };
        var stringOps = new[] { ComparisonOp.Contains, ComparisonOp.StartsWith, ComparisonOp.EndsWith };

        if (stringOps.Contains(op) && !isString)
        {
            throw new QueryParseException(
                $"Operator '{op}' not supported for property '{fieldName}' ({type.Name}). String operators are only valid for string properties.",
                0);
        }

        if (comparisonOps.Contains(op) && !isNumeric && !isDateTime && !isTimeSpan)
        {
            throw new QueryParseException(
                $"Operator '{op}' not supported for property '{fieldName}' ({type.Name}).",
                0);
        }

        if (isBool && op != ComparisonOp.Equal && op != ComparisonOp.NotEqual)
        {
            throw new QueryParseException(
                $"Operator '{op}' not supported for boolean property '{fieldName}'. Use '=' or '!='.",
                0);
        }
    }

    private static string ResolveFieldName<TEntity>(string fieldName, Dictionary<string, string>? fieldAliases)
    {
        if (fieldAliases != null)
        {
            // Try case-insensitive alias lookup
            var alias = fieldAliases.FirstOrDefault(
                kvp => string.Equals(kvp.Key, fieldName, StringComparison.OrdinalIgnoreCase));
            if (alias.Key != null)
            {
                return alias.Value;
            }
        }

        // Direct property name match (case-insensitive is handled by GetProperty with IgnoreCase)
        return fieldName;
    }

    private static object? ConvertValue(object? value, Type targetType, Type underlyingType, string fieldName)
    {
        if (value == null)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
            {
                throw new QueryParseException(
                    $"Cannot assign null to non-nullable property '{fieldName}' ({underlyingType.Name}).",
                    0);
            }
            return null;
        }

        try
        {
            if (underlyingType == typeof(int))
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(decimal))
            {
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(double))
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(float))
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(long))
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(short))
            {
                return Convert.ToInt16(value, CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(bool))
            {
                if (value is bool b) return b;
                throw new QueryParseException(
                    $"Expected boolean value for field '{fieldName}', got '{value}'.",
                    0);
            }

            if (underlyingType == typeof(DateTime))
            {
                if (value is string dateStr)
                {
                    if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                    {
                        return dt;
                    }
                    throw new QueryParseException(
                        $"Invalid date format '{dateStr}' for field '{fieldName}'. Use ISO 8601 format (e.g., \"2024-01-01\").",
                        0);
                }
                throw new QueryParseException(
                    $"Expected string date value for field '{fieldName}', got {value.GetType().Name}.",
                    0);
            }

            if (underlyingType == typeof(TimeSpan))
            {
                if (value is string tsStr)
                {
                    if (TimeSpan.TryParse(tsStr, CultureInfo.InvariantCulture, out var ts))
                    {
                        return ts;
                    }
                    throw new QueryParseException(
                        $"Invalid timespan format '{tsStr}' for field '{fieldName}'. Use format \"hh:mm:ss\".",
                        0);
                }
                throw new QueryParseException(
                    $"Expected string timespan value for field '{fieldName}', got {value.GetType().Name}.",
                    0);
            }

            if (underlyingType == typeof(string))
            {
                return value.ToString();
            }

            return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        }
        catch (QueryParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new QueryParseException(
                $"Cannot convert value '{value}' to type {underlyingType.Name} for field '{fieldName}': {ex.Message}.",
                0, ex);
        }
    }
}
