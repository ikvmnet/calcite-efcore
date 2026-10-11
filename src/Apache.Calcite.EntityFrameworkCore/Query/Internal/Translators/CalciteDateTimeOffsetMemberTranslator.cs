using System;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

/// <summary>
/// Translates <see cref="DateTimeOffset"/> members: its parts, <see cref="DateTimeOffset.Date"/>,
/// <see cref="DateTimeOffset.DateTime"/> and <see cref="DateTimeOffset.UtcDateTime"/>.
/// </summary>
/// <remarks>
/// Calcite keeps a <c>TIMESTAMP WITH TIME ZONE</c> normalized to UTC and answers its parts in UTC, and a
/// value read back from the store carries offset zero, so every part here is the part of the value as the
/// provider returns it. The clock members are left untranslated, as for <see cref="DateTime.UtcNow"/>.
/// </remarks>
public class CalciteDateTimeOffsetMemberTranslator : IMemberTranslator
{

    readonly CalciteSqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteDateTimeOffsetMemberTranslator(CalciteSqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null || member.DeclaringType != typeof(DateTimeOffset))
            return null;

        var dateTimeMapping = _typeMappingSource.FindMapping(typeof(DateTime));

        return member.Name switch
        {
            nameof(DateTimeOffset.DateTime) or nameof(DateTimeOffset.UtcDateTime)
                => _sqlExpressionFactory.Convert(instance, typeof(DateTime), dateTimeMapping),
            // through the UTC timestamp: a TIMESTAMP WITH TIME ZONE cast straight to DATE does not give the date
            nameof(DateTimeOffset.Date) => _sqlExpressionFactory.Convert(
                _sqlExpressionFactory.Convert(
                    _sqlExpressionFactory.Convert(instance, typeof(DateTime), dateTimeMapping),
                    typeof(DateOnly),
                    _typeMappingSource.FindMapping(typeof(DateOnly))),
                typeof(DateTime),
                dateTimeMapping),
            _ => _sqlExpressionFactory.DatePart(member.Name, instance),
        };
    }

}
