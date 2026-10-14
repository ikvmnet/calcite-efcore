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
/// Translates <see cref="DateTimeOffset.ToUnixTimeSeconds"/> and
/// <see cref="DateTimeOffset.ToUnixTimeMilliseconds"/>.
/// </summary>
/// <remarks>
/// The seconds are counted as whole days since the epoch plus the seconds of the day, rather than with
/// <c>TIMESTAMPDIFF(SECOND, …)</c>, whose <c>INTEGER</c> result overflows in 2038.
/// </remarks>
public class CalciteDateTimeOffsetMethodTranslator : IMethodCallTranslator
{

    static readonly DateTime Epoch = new(1970, 1, 1);

    readonly CalciteSqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteDateTimeOffsetMethodTranslator(CalciteSqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null || method.DeclaringType != typeof(DateTimeOffset) || arguments.Count != 0)
            return null;

        return method.Name switch
        {
            nameof(DateTimeOffset.ToUnixTimeSeconds) => UnixSeconds(instance),
            nameof(DateTimeOffset.ToUnixTimeMilliseconds) => _sqlExpressionFactory.Add(
                _sqlExpressionFactory.Multiply(UnixSeconds(instance), Long(1000)),
                _sqlExpressionFactory.Function(
                    "MOD",
                    [_sqlExpressionFactory.Extract("MILLISECOND", instance), Long(1000)],
                    nullable: true,
                    argumentsPropagateNullability: [true, false],
                    typeof(long),
                    LongMapping)),
            _ => null,
        };
    }

    RelationalTypeMapping? LongMapping => _typeMappingSource.FindMapping(typeof(long));

    SqlExpression Long(long value) => _sqlExpressionFactory.Constant(value, LongMapping);

    SqlExpression UnixSeconds(SqlExpression instance)
    {
        // counted on the UTC timestamp: a TIMESTAMP WITH TIME ZONE cast straight to DATE does not give
        // the date, and TIMESTAMPDIFF over it counts nonsense
        var dateTimeMapping = _typeMappingSource.FindMapping(typeof(DateTime));
        var utc = _sqlExpressionFactory.Convert(instance, typeof(DateTime), dateTimeMapping);

        SqlExpression Part(string function)
            => _sqlExpressionFactory.Function(function, [utc], nullable: true, argumentsPropagateNullability: [true], typeof(long), LongMapping);

        var days = _sqlExpressionFactory.Convert(
            _sqlExpressionFactory.Function(
                "TIMESTAMPDIFF",
                [_sqlExpressionFactory.Fragment("DAY"), _sqlExpressionFactory.Constant(Epoch, dateTimeMapping), utc],
                nullable: true,
                argumentsPropagateNullability: [false, false, true],
                typeof(int),
                _typeMappingSource.FindMapping(typeof(int))),
            typeof(long),
            LongMapping);

        return _sqlExpressionFactory.Add(
            _sqlExpressionFactory.Multiply(days, Long(86_400)),
            _sqlExpressionFactory.Add(
                _sqlExpressionFactory.Add(
                    _sqlExpressionFactory.Multiply(Part("HOUR"), Long(3_600)),
                    _sqlExpressionFactory.Multiply(Part("MINUTE"), Long(60))),
                Part("SECOND")));
    }

}
