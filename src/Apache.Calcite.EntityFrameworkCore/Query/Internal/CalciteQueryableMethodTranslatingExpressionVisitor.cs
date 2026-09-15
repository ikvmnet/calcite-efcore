using System.Linq;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal;
using Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;
using Apache.Calcite.EntityFrameworkCore.Utilities;

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    public class CalciteQueryableMethodTranslatingExpressionVisitor : RelationalQueryableMethodTranslatingExpressionVisitor
    {

        readonly RelationalQueryCompilationContext _queryCompilationContext;
        readonly IRelationalTypeMappingSource _typeMappingSource;
        readonly SqlAliasManager _sqlAliasManager;

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
            _typeMappingSource = relationalDependencies.TypeMappingSource;
            _sqlAliasManager = queryCompilationContext.SqlAliasManager;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="parentVisitor"></param>
        protected CalciteQueryableMethodTranslatingExpressionVisitor(CalciteQueryableMethodTranslatingExpressionVisitor parentVisitor) :
            base(parentVisitor)
        {
            _queryCompilationContext = parentVisitor._queryCompilationContext;
            _typeMappingSource = parentVisitor._typeMappingSource;
            _sqlAliasManager = parentVisitor._sqlAliasManager;
        }

        /// <inheritdoc/>
        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        {
            return new CalciteQueryableMethodTranslatingExpressionVisitor(this);
        }

        /// <summary>
        /// Translates a collection of primitive values into a table of one row per element, so that
        /// the standard LINQ operators compose over it.
        /// </summary>
        /// <remarks>
        /// An <c>ARRAY</c> column is expanded with <c>UNNEST</c>, which gives the elements and their
        /// positions as an ordinary rowset; everything else follows from that, because the operators
        /// EF asks for are the ones it already knows how to build over a table. <c>Contains</c>, for
        /// one, arrives here only indirectly: EF rewrites it to <c>Any</c> over this table and emits
        /// the correlated <c>EXISTS</c> itself.
        ///
        /// <para>A parameter collection is declined. Calcite types a bare dynamic parameter as
        /// <c>UNKNOWN</c> and refuses to unnest it, so the base translation — which expands the
        /// parameter into a <c>VALUES</c> list or a set of scalars — is the better answer and is
        /// what returning <see langword="null"/> falls back to.</para>
        /// </remarks>
        /// <param name="sqlExpression"></param>
        /// <param name="property"></param>
        /// <param name="tableAlias"></param>
        /// <returns></returns>
        protected override ShapedQueryExpression? TranslatePrimitiveCollection(SqlExpression sqlExpression, IProperty? property, string tableAlias)
        {
            if (sqlExpression is SqlParameterExpression)
                return null;

            // only a column the store really holds as an ARRAY can be unnested; a collection that
            // landed in JSON text is not an array to Calcite and has no translation here
            if (sqlExpression.TypeMapping is not CalciteArrayTypeMapping arrayMapping)
                return null;

            // a collection reached through a JSON document is one of those: the property carries the
            // array mapping, but what the expression produces is the document's text, and Calcite
            // refuses to unnest a VARCHAR
            if (sqlExpression is JsonScalarExpression)
                return null;

            var elementClrType = sqlExpression.Type.GetSequenceType();
            var elementTypeMapping = arrayMapping.ElementMapping;
            var ordinalityTypeMapping = _typeMappingSource.FindMapping(typeof(int))!;

            var unnestExpression = new CalciteUnnestExpression(tableAlias, sqlExpression);

            var isElementNullable = property?.GetElementType()?.IsNullable ?? elementClrType.IsNullableType();

            var valueColumn = new ColumnExpression(
                CalciteUnnestExpression.ValueColumnName,
                tableAlias,
                elementClrType.UnwrapNullableType(),
                elementTypeMapping,
                isElementNullable);

            var ordinalityColumn = new ColumnExpression(
                CalciteUnnestExpression.OrdinalityColumnName,
                tableAlias,
                typeof(int),
                ordinalityTypeMapping,
                nullable: false);

            // the ordinality is the identifier: it is what makes two equal elements distinct rows,
            // which is what EF needs to keep a collection's duplicates apart
            var selectExpression = new SelectExpression(
                [unnestExpression],
                valueColumn,
                [(ordinalityColumn, ordinalityTypeMapping.Comparer)],
                _sqlAliasManager);

            selectExpression.AppendOrdering(new OrderingExpression(ordinalityColumn, ascending: true));

            Expression shaperExpression = new ProjectionBindingExpression(selectExpression, new ProjectionMember(), elementClrType.MakeNullable());
            if (elementClrType != shaperExpression.Type)
                shaperExpression = Expression.Convert(shaperExpression, elementClrType);

            return new ShapedQueryExpression(selectExpression, shaperExpression);
        }

        /// <summary>
        /// Returns whether a select is already in the order its rows naturally come in, which is
        /// what tells EF that dropping its ordering is not a surprise.
        /// </summary>
        /// <remarks>
        /// An unnested array is the case that matters: the ordering on the ordinality column is put
        /// there by <see cref="TranslatePrimitiveCollection"/> to express the order the array
        /// already has, not by anything the user wrote. Without this, composing <c>Distinct</c> over
        /// a primitive collection warns that an ordering is about to be erased — and a warning EF
        /// raises as an error by default, so the query fails outright.
        /// </remarks>
        /// <param name="selectExpression"></param>
        /// <returns></returns>
        protected override bool IsNaturallyOrdered(SelectExpression selectExpression)
        {
            return selectExpression is { Tables: [var firstTable, ..], Orderings: [{ IsAscending: true, Expression: ColumnExpression column }] }
                && column.Name == CalciteUnnestExpression.OrdinalityColumnName
                && column.TableAlias == firstTable.Alias
                && IsOrdinalityColumn(selectExpression, column);

            // the ordering column has to belong to an UNNEST, either directly or through a subquery
            // that projects it, and not merely share the name
            static bool IsOrdinalityColumn(SelectExpression selectExpression, ColumnExpression column)
            {
                var table = selectExpression.Tables.FirstOrDefault(t => t.Alias == column.TableAlias);

                return (table is JoinExpressionBase join ? join.Table : table) switch
                {
                    CalciteUnnestExpression => true,
                    SelectExpression subquery => subquery.Projection.FirstOrDefault(p => p.Alias == CalciteUnnestExpression.OrdinalityColumnName)?.Expression is ColumnExpression projected
                        && IsOrdinalityColumn(subquery, projected),
                    _ => false,
                };
            }
        }

    }

}
