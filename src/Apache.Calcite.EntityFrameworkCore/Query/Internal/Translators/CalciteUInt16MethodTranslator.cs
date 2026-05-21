using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators
{

    /// <summary>
    /// Translates <see cref="ushort"/> instance methods into Calcite SQL expressions.
    /// </summary>
    public class CalciteUInt16MethodTranslator : IMethodCallTranslator
    {

        static readonly MethodInfo ToStringMethodInfo
            = typeof(ushort).GetRuntimeMethod(nameof(ushort.ToString), [])!;

        readonly CalciteSqlExpressionFactory _sqlExpressionFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="sqlExpressionFactory"></param>
        public CalciteUInt16MethodTranslator(CalciteSqlExpressionFactory sqlExpressionFactory)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        /// <inheritdoc/>
        public virtual SqlExpression? Translate(SqlExpression? instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (instance is null)
                return null;

            if (Equals(method, ToStringMethodInfo))
                return TranslateToString(instance);

            return null;
        }

        /// <summary>
        /// Translates <see cref="ushort.ToString()"/> into <c>CAST(instance AS VARCHAR)</c>.
        /// </summary>
        SqlExpression TranslateToString(SqlExpression instance)
            => _sqlExpressionFactory.Convert(instance, typeof(string));

    }

}
