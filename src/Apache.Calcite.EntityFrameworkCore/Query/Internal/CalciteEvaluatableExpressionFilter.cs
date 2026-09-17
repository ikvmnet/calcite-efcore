using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    public class CalciteEvaluatableExpressionFilter : RelationalEvaluatableExpressionFilter
    {

        /// <summary>
        /// Initializes a new instane.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="relationalDependencies"></param>
        public CalciteEvaluatableExpressionFilter(EvaluatableExpressionFilterDependencies dependencies, RelationalEvaluatableExpressionFilterDependencies relationalDependencies) :
            base(dependencies, relationalDependencies)
        {

        }

        /// <inheritdoc/>
        /// <remarks>
        /// A call to one of this provider's <see cref="DbFunctions"/> stubs is never evaluated on the client.
        /// Every one of them throws if it is, because it exists to be translated — and a call whose arguments
        /// are all constants, such as reading a geography from well-known text, is otherwise folded away
        /// before translation is reached and throws from inside the fold, where the message says only that a
        /// query parameter could not be evaluated.
        /// </remarks>
        /// <param name="expression"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public override bool IsEvaluatableExpression(Expression expression, IModel model)
        {
            if (expression is MethodCallExpression call && IsCalciteDbFunction(call.Method.DeclaringType))
                return false;

            return base.IsEvaluatableExpression(expression, model);
        }

        /// <summary>
        /// Returns whether the type declaring a method is one of this provider's <see cref="DbFunctions"/>
        /// extension classes.
        /// </summary>
        /// <remarks>
        /// By name rather than by reference, so the geometry package's own surface is covered without this
        /// assembly having to know about it.
        /// </remarks>
        /// <param name="type"></param>
        /// <returns></returns>
        static bool IsCalciteDbFunction(System.Type? type)
        {
            return type is not null
                && type.IsSealed
                && type.IsAbstract
                && type.Name.StartsWith("Calcite", System.StringComparison.Ordinal)
                && type.Name.EndsWith("DbFunctionsExtensions", System.StringComparison.Ordinal);
        }

    }

}
