using System;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators
{

    /// <summary>
    /// Translates the <see cref="CalciteDbFunctionsExtensions"/> stubs into the Calcite functions they name.
    /// </summary>
    /// <remarks>
    /// Each stub maps to one function of the same name, so the translation is the mapping from
    /// <see cref="MethodInfo"/> to function name and back: there is no rewriting to do, which is the point of
    /// naming the methods after Calcite's functions rather than after another store's.
    /// </remarks>
    public class CalciteDbFunctionsTranslator : IMethodCallTranslator
    {

        /// <summary>
        /// The function each stub translates to, and whether its arguments propagate null.
        /// </summary>
        static readonly Dictionary<MethodInfo, string> _functions = Build();

        /// <summary>
        /// Returns the stub-to-function map, built from the extension class so a method that is renamed or
        /// removed fails here rather than silently stopping translating.
        /// </summary>
        /// <returns></returns>
        static Dictionary<MethodInfo, string> Build()
        {
            var map = new Dictionary<MethodInfo, string>();

            foreach (var method in typeof(CalciteDbFunctionsExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                map[method] = method.Name switch
                {
                    nameof(CalciteDbFunctionsExtensions.RegexpLike) => "REGEXP_LIKE",
                    nameof(CalciteDbFunctionsExtensions.RegexpContains) => "REGEXP_CONTAINS",
                    nameof(CalciteDbFunctionsExtensions.RegexpExtract) => "REGEXP_EXTRACT",
                    nameof(CalciteDbFunctionsExtensions.RegexpInstr) => "REGEXP_INSTR",
                    nameof(CalciteDbFunctionsExtensions.RegexpReplace) => "REGEXP_REPLACE",
                    nameof(CalciteDbFunctionsExtensions.ContainsSubstr) => "CONTAINS_SUBSTR",
                    nameof(CalciteDbFunctionsExtensions.Soundex) => "SOUNDEX",
                    nameof(CalciteDbFunctionsExtensions.Difference) => "DIFFERENCE",
                    _ => throw new InvalidOperationException($"No Calcite function is mapped for '{method.Name}'."),
                };
            }

            return map;
        }

        readonly ISqlExpressionFactory _sqlExpressionFactory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="sqlExpressionFactory"></param>
        public CalciteDbFunctionsTranslator(ISqlExpressionFactory sqlExpressionFactory)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        /// <inheritdoc/>
        public virtual SqlExpression? Translate(
            SqlExpression? instance,
            MethodInfo method,
            IReadOnlyList<SqlExpression> arguments,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (_functions.TryGetValue(method, out var name) == false)
                return null;

            // the first argument is the DbFunctions instance the extension method hangs off, which is not
            // part of the call
            var operands = new SqlExpression[arguments.Count - 1];
            var propagatesNull = new bool[operands.Length];

            for (var i = 0; i < operands.Length; i++)
            {
                operands[i] = arguments[i + 1];
                propagatesNull[i] = true;
            }

            return _sqlExpressionFactory.Function(
                name,
                operands,
                nullable: true,
                argumentsPropagateNullability: propagatesNull,
                Nullable.GetUnderlyingType(method.ReturnType) ?? method.ReturnType);
        }

    }

}
