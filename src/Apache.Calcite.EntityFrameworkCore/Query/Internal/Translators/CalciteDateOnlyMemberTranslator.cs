using System;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

/// <summary>
/// Translates <see cref="DateOnly"/> members: its parts and <see cref="DateOnly.DayNumber"/>.
/// </summary>
public class CalciteDateOnlyMemberTranslator : IMemberTranslator
{

    static readonly DateOnly DayNumberOrigin = new(1, 1, 1);

    readonly CalciteSqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteDateOnlyMemberTranslator(CalciteSqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null || member.DeclaringType != typeof(DateOnly))
            return null;

        return member.Name switch
        {
            // the days since 0001-01-01, which TIMESTAMPDIFF answers as an INTEGER
            nameof(DateOnly.DayNumber) => _sqlExpressionFactory.Function(
                "TIMESTAMPDIFF",
                [_sqlExpressionFactory.Fragment("DAY"), _sqlExpressionFactory.Constant(DayNumberOrigin, instance.TypeMapping), instance],
                nullable: true,
                argumentsPropagateNullability: [false, false, true],
                typeof(int),
                _typeMappingSource.FindMapping(typeof(int))),
            nameof(DateOnly.Year) or nameof(DateOnly.Month) or nameof(DateOnly.Day) or nameof(DateOnly.DayOfYear) or nameof(DateOnly.DayOfWeek)
                => _sqlExpressionFactory.DatePart(member.Name, instance),
            _ => null,
        };
    }

}
