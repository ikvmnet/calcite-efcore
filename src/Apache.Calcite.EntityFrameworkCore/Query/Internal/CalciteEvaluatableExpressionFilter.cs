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

            if (expression is UnaryExpression { NodeType: ExpressionType.Convert, Method: { } conversion }
                && IsCalciteMarkerConversion(conversion))
                return false;

            return base.IsEvaluatableExpression(expression, model);
        }

        /// <summary>
        /// Returns whether a method is a conversion into one of this provider's marker types.
        /// </summary>
        /// <remarks>
        /// A marker carries nothing, so evaluating the conversion does not merely move work to the client:
        /// it <em>discards the value</em>. A full text keyword converted on the client is an empty marker
        /// standing where the string was, and no error says so. The conversion means nothing in SQL either —
        /// see <c>CalciteSqlTranslatingExpressionVisitor.VisitUnary</c>, which unwraps it — so refusing it
        /// here is what keeps the string in the tree until something can read it.
        /// </remarks>
        /// <param name="conversion"></param>
        /// <returns></returns>
        static bool IsCalciteMarkerConversion(System.Reflection.MethodInfo conversion)
        {
            return conversion.Name is "op_Implicit" or "op_Explicit" && IsCalciteMarker(conversion.DeclaringType);
        }

        /// <summary>
        /// Returns whether a type is one of this provider's marker types.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        static bool IsCalciteMarker(System.Type? type)
        {
            return type is not null && type.Name.StartsWith("Calcite", System.StringComparison.Ordinal);
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
