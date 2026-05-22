using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    public class CalciteQueryableMethodTranslatingExpressionVisitor : RelationalQueryableMethodTranslatingExpressionVisitor
    {

        readonly RelationalQueryCompilationContext _queryCompilationContext;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="relationalDependencies"></param>
        /// <param name="queryCompilationContext"></param>
        public CalciteQueryableMethodTranslatingExpressionVisitor(QueryableMethodTranslatingExpressionVisitorDependencies dependencies, RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies, RelationalQueryCompilationContext queryCompilationContext) :
            base(dependencies, relationalDependencies, queryCompilationContext)
        {
            _queryCompilationContext = queryCompilationContext;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="parentVisitor"></param>
        protected CalciteQueryableMethodTranslatingExpressionVisitor(CalciteQueryableMethodTranslatingExpressionVisitor parentVisitor) :
            base(parentVisitor)
        {
            _queryCompilationContext = parentVisitor._queryCompilationContext;
        }

    }

}
