using Microsoft.EntityFrameworkCore.Query;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    public class CalciteParameterBasedSqlProcessor : RelationalParameterBasedSqlProcessor
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="parameters"></param>
        public CalciteParameterBasedSqlProcessor(RelationalParameterBasedSqlProcessorDependencies dependencies, RelationalParameterBasedSqlProcessorParameters parameters) :
            base(dependencies, parameters)
        {

        }

        /// <inheritdoc/>
        protected override System.Linq.Expressions.Expression ProcessSqlNullability(System.Linq.Expressions.Expression queryExpression, ParametersCacheDecorator parametersDecorator)
        {
            return new CalciteSqlNullabilityProcessor(Dependencies, Parameters).Process(queryExpression, parametersDecorator);
        }

    }

}
