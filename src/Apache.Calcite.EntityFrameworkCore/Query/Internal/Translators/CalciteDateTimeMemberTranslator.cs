using System;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

/// <summary>
/// Translates <see cref="DateTime"/> members: its parts, <see cref="DateTime.Date"/>, and the
/// <see cref="DateTime.Now"/> and <see cref="DateTime.Today"/> clock.
/// </summary>
/// <remarks>
/// <see cref="DateTime.UtcNow"/> is left untranslated: Calcite's clock functions answer in the session
/// time zone, and the standard library has nothing that converts to UTC. <see cref="DateTime.TimeOfDay"/>
/// waits on a store mapping for <see cref="TimeSpan"/>.
/// </remarks>
public class CalciteDateTimeMemberTranslator : IMemberTranslator
{

    readonly CalciteSqlExpressionFactory _sqlExpressionFactory;
    readonly IRelationalTypeMappingSource _typeMappingSource;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteDateTimeMemberTranslator(CalciteSqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        _sqlExpressionFactory = sqlExpressionFactory;
        _typeMappingSource = typeMappingSource;
    }

    /// <inheritdoc />
    public virtual SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (member.DeclaringType != typeof(DateTime))
            return null;

        var dateTimeMapping = _typeMappingSource.FindMapping(typeof(DateTime));

        if (instance is null)
        {
            return member.Name switch
            {
                nameof(DateTime.Now) => _sqlExpressionFactory.Function(
                    "LOCALTIMESTAMP",
                    [_sqlExpressionFactory.Constant(3)],
                    nullable: false,
                    argumentsPropagateNullability: [false],
                    typeof(DateTime),
                    dateTimeMapping),
                nameof(DateTime.Today) => _sqlExpressionFactory.Convert(
                    _sqlExpressionFactory.NiladicFunction("CURRENT_DATE", nullable: false, typeof(DateOnly), _typeMappingSource.FindMapping(typeof(DateOnly))),
                    typeof(DateTime),
                    dateTimeMapping),
                _ => null,
            };
        }

        return member.Name switch
        {
            nameof(DateTime.Date) => _sqlExpressionFactory.Convert(
                _sqlExpressionFactory.Convert(instance, typeof(DateOnly), _typeMappingSource.FindMapping(typeof(DateOnly))),
                typeof(DateTime),
                instance.TypeMapping),
            _ => _sqlExpressionFactory.DatePart(member.Name, instance),
        };
    }

}
