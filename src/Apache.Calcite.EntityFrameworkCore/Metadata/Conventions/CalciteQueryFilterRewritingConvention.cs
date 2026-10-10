using System;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Apache.Calcite.EntityFrameworkCore.Metadata.Conventions
{

    /// <inheritdoc />
    public class CalciteQueryFilterRewritingConvention : RelationalQueryFilterRewritingConvention
    {

        /// <summary>
        /// Initialies a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="relationalDependencies"></param>
        public CalciteQueryFilterRewritingConvention(ProviderConventionSetBuilderDependencies dependencies, RelationalConventionSetBuilderDependencies relationalDependencies) :
            base(dependencies, relationalDependencies)
        {
            DbSetAccessRewriter = new CalciteDbSetAccessRewritingExpressionVisitor(Dependencies.ContextType);
        }

        sealed class CalciteDbSetAccessRewritingExpressionVisitor : RelationalDbSetAccessRewritingExpressionVisitor
        {

            public CalciteDbSetAccessRewritingExpressionVisitor(Type contextType) :
                base(contextType)
            {

            }

            protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
            {
                return base.VisitMethodCall(methodCallExpression);
            }

        }

    }

}
