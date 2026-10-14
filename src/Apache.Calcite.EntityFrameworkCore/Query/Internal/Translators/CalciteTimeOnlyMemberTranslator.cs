using System;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

/// <summary>
/// Translates the <see cref="TimeOnly"/> parts, <see cref="TimeOnly.Hour"/> through
/// <see cref="TimeOnly.Nanosecond"/>.
/// </summary>
public class CalciteTimeOnlyMemberTranslator : IMemberTranslator
{

    readonly CalciteSqlExpressionFactory _sqlExpressionFactory;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    public CalciteTimeOnlyMemberTranslator(CalciteSqlExpressionFactory sqlExpressionFactory)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (instance is null || member.DeclaringType != typeof(TimeOnly))
            return null;

        return member.Name switch
        {
            nameof(TimeOnly.Hour) or nameof(TimeOnly.Minute) or nameof(TimeOnly.Second)
                or nameof(TimeOnly.Millisecond) or nameof(TimeOnly.Microsecond) or nameof(TimeOnly.Nanosecond)
                => _sqlExpressionFactory.DatePart(member.Name, instance),
            _ => null,
        };
    }

}
