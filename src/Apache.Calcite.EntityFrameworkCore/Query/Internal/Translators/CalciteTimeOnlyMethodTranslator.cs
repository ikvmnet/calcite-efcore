using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

/// <summary>
/// Translates the <see cref="TimeOnly"/> methods: <see cref="TimeOnly.AddHours(double)"/>,
/// <see cref="TimeOnly.AddMinutes(double)"/>, <see cref="TimeOnly.IsBetween"/> and
/// <see cref="TimeOnly.FromDateTime"/>.
/// </summary>
/// <remarks>
/// <c>TIMESTAMPADD</c> wraps a <c>TIME</c> past midnight, as the .NET methods do. The members that take or
/// give a <see cref="TimeSpan"/> wait on a store mapping for it.
/// </remarks>
public class CalciteTimeOnlyMethodTranslator : IMethodCallTranslator
{

    static readonly MethodInfo FromDateTimeMethod = typeof(TimeOnly).GetRuntimeMethod(nameof(TimeOnly.FromDateTime), [typeof(DateTime)])!;
    static readonly MethodInfo AddHoursMethod = typeof(TimeOnly).GetRuntimeMethod(nameof(TimeOnly.AddHours), [typeof(double)])!;
    static readonly MethodInfo AddMinutesMethod = typeof(TimeOnly).GetRuntimeMethod(nameof(TimeOnly.AddMinutes), [typeof(double)])!;
    static readonly MethodInfo IsBetweenMethod = typeof(TimeOnly).GetRuntimeMethod(nameof(TimeOnly.IsBetween), [typeof(TimeOnly), typeof(TimeOnly)])!;

    readonly CalciteSqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteTimeOnlyMethodTranslator(CalciteSqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(TimeOnly))
            return null;

        if (method.Equals(FromDateTimeMethod))
            return _sqlExpressionFactory.Convert(arguments[0], typeof(TimeOnly), _typeMappingSource.FindMapping(typeof(TimeOnly)));

        if (instance is null)
            return null;

        if (method.Equals(AddHoursMethod))
            return _sqlExpressionFactory.TimestampAddMilliseconds(arguments[0], 3_600_000, instance);

        if (method.Equals(AddMinutesMethod))
            return _sqlExpressionFactory.TimestampAddMilliseconds(arguments[0], 60_000, instance);

        if (method.Equals(IsBetweenMethod))
            return IsBetween(instance, arguments[0], arguments[1]);

        return null;
    }

    /// <summary>
    /// <see cref="TimeOnly.IsBetween"/> includes its start and excludes its end, and a range whose start is
    /// after its end wraps past midnight.
    /// </summary>
    SqlExpression IsBetween(SqlExpression time, SqlExpression start, SqlExpression end)
    {
        var f = _sqlExpressionFactory;
        var mapping = time.TypeMapping;
        start = f.ApplyTypeMapping(start, mapping);
        end = f.ApplyTypeMapping(end, mapping);

        return f.OrElse(
            f.AndAlso(f.LessThanOrEqual(start, end), f.AndAlso(f.GreaterThanOrEqual(time, start), f.LessThan(time, end))),
            f.AndAlso(f.GreaterThan(start, end), f.OrElse(f.GreaterThanOrEqual(time, start), f.LessThan(time, end))));
    }

}
