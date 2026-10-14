using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

/// <summary>
/// Translates the <see cref="DateTime"/> <c>Add</c> methods into <c>TIMESTAMPADD</c>.
/// </summary>
/// <remarks>
/// <see cref="DateTime.AddYears"/> and <see cref="DateTime.AddMonths"/> step in their own unit, so the end
/// of the month clamps as it does in .NET. The methods that take a <see cref="double"/> add whole
/// milliseconds, the precision the store keeps.
/// </remarks>
public class CalciteDateTimeMethodTranslator : IMethodCallTranslator
{

    readonly CalciteSqlExpressionFactory _sqlExpressionFactory;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    public CalciteDateTimeMethodTranslator(CalciteSqlExpressionFactory sqlExpressionFactory)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null || method.DeclaringType != typeof(DateTime) || arguments is not [var amount])
            return null;

        return method.Name switch
        {
            nameof(DateTime.AddYears) => _sqlExpressionFactory.TimestampAdd("YEAR", amount, instance),
            nameof(DateTime.AddMonths) => _sqlExpressionFactory.TimestampAdd("MONTH", amount, instance),
            nameof(DateTime.AddDays) => _sqlExpressionFactory.TimestampAddMilliseconds(amount, 86_400_000, instance),
            nameof(DateTime.AddHours) => _sqlExpressionFactory.TimestampAddMilliseconds(amount, 3_600_000, instance),
            nameof(DateTime.AddMinutes) => _sqlExpressionFactory.TimestampAddMilliseconds(amount, 60_000, instance),
            nameof(DateTime.AddSeconds) => _sqlExpressionFactory.TimestampAddMilliseconds(amount, 1_000, instance),
            nameof(DateTime.AddMilliseconds) => _sqlExpressionFactory.TimestampAddMilliseconds(amount, 1, instance),
            _ => null,
        };
    }

}
