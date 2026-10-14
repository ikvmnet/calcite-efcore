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
/// Translates the <see cref="DateOnly"/> methods: the <c>Add</c> methods, <see cref="DateOnly.FromDateTime"/>
/// and <see cref="DateOnly.ToDateTime(TimeOnly)"/>.
/// </summary>
public class CalciteDateOnlyMethodTranslator : IMethodCallTranslator
{

    static readonly MethodInfo FromDateTimeMethod = typeof(DateOnly).GetRuntimeMethod(nameof(DateOnly.FromDateTime), [typeof(DateTime)])!;
    static readonly MethodInfo ToDateTimeMethod = typeof(DateOnly).GetRuntimeMethod(nameof(DateOnly.ToDateTime), [typeof(TimeOnly)])!;

    readonly CalciteSqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteDateOnlyMethodTranslator(CalciteSqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(DateOnly))
            return null;

        if (method.Equals(FromDateTimeMethod))
            return _sqlExpressionFactory.Convert(arguments[0], typeof(DateOnly), _typeMappingSource.FindMapping(typeof(DateOnly)));

        if (instance is null || arguments is not [var argument])
            return null;

        if (method.Equals(ToDateTimeMethod))
            return ToDateTime(instance, argument);

        return method.Name switch
        {
            nameof(DateOnly.AddYears) => _sqlExpressionFactory.TimestampAdd("YEAR", argument, instance),
            nameof(DateOnly.AddMonths) => _sqlExpressionFactory.TimestampAdd("MONTH", argument, instance),
            nameof(DateOnly.AddDays) => _sqlExpressionFactory.TimestampAdd("DAY", argument, instance),
            _ => null,
        };
    }

    /// <summary>
    /// Adds the milliseconds since midnight of <paramref name="time"/> to <paramref name="date"/> taken as a
    /// timestamp: Calcite has no constructor from a date and a time, and cannot subtract one <c>TIME</c> from
    /// another to make an interval.
    /// </summary>
    SqlExpression ToDateTime(SqlExpression date, SqlExpression time)
    {
        var longMapping = _typeMappingSource.FindMapping(typeof(long));

        SqlExpression Part(string function)
            => _sqlExpressionFactory.Function(function, [time], nullable: true, argumentsPropagateNullability: [true], typeof(long), longMapping);

        SqlExpression Long(long value)
            => _sqlExpressionFactory.Constant(value, longMapping);

        // EXTRACT(MILLISECOND …) already carries the seconds, as seconds * 1000 + milliseconds
        var milliseconds = _sqlExpressionFactory.Add(
            _sqlExpressionFactory.Multiply(
                _sqlExpressionFactory.Add(
                    _sqlExpressionFactory.Multiply(Part("HOUR"), Long(3600)),
                    _sqlExpressionFactory.Multiply(Part("MINUTE"), Long(60))),
                Long(1000)),
            _sqlExpressionFactory.Extract("MILLISECOND", time));

        var midnight = _sqlExpressionFactory.Convert(date, typeof(DateTime), _typeMappingSource.FindMapping(typeof(DateTime)));
        return _sqlExpressionFactory.TimestampAdd("MILLISECOND", milliseconds, midnight);
    }

}
