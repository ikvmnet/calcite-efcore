using System;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators
{

    /// <summary>
    /// Translates <see cref="string"/> member accesses into Calcite SQL expressions.
    /// Calcite uses <c>CHAR_LENGTH</c> rather than <c>LENGTH</c> for character types.
    /// </summary>
    public class CalciteStringMemberTranslator : IMemberTranslator
    {

        static readonly MemberInfo LengthMemberInfo = typeof(string).GetRuntimeProperty(nameof(string.Length))!;

        readonly CalciteSqlExpressionFactory _sqlExpressionFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="sqlExpressionFactory"></param>
        public CalciteStringMemberTranslator(CalciteSqlExpressionFactory sqlExpressionFactory)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        /// <inheritdoc/>
        public SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (instance is null || !Equals(member, LengthMemberInfo))
                return null;

            return _sqlExpressionFactory.Function(
                "CHAR_LENGTH",
                [instance],
                nullable: true,
                argumentsPropagateNullability: [true],
                returnType);
        }

    }

}
