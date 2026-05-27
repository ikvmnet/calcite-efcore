using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
{

    /// <summary>
    /// Implementation of <see cref="Sort"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// Translates Calcite collation and optional offset/fetch into LINQ <c>OrderBy</c>/<c>ThenBy</c>/<c>Skip</c>/<c>Take</c> operators.
    /// </summary>
    public class EfCoreOrderBy : Sort, EfCoreRel
    {

        // Queryable.OrderBy<TSource, TKey>(IQueryable<TSource>, Expression<Func<TSource, TKey>>)
        static readonly MethodInfo QueryableOrderByMethod =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.OrderBy) && m.GetParameters().Length == 2);

        // Queryable.OrderByDescending<TSource, TKey>
        static readonly MethodInfo QueryableOrderByDescendingMethod =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.OrderByDescending) && m.GetParameters().Length == 2);

        // Queryable.ThenBy<TSource, TKey>
        static readonly MethodInfo QueryableThenByMethod =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.ThenBy) && m.GetParameters().Length == 2);

        // Queryable.ThenByDescending<TSource, TKey>
        static readonly MethodInfo QueryableThenByDescendingMethod =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.ThenByDescending) && m.GetParameters().Length == 2);

        // Queryable.Skip<TSource>
        static readonly MethodInfo QueryableSkipMethod =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Skip) && m.GetParameters().Length == 2);

        // Queryable.Take<TSource>
        static readonly MethodInfo QueryableTakeMethod =
            typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Take) && m.GetParameters().Length == 2);

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="input">The input relational expression.</param>
        /// <param name="collation">The sort collation.</param>
        /// <param name="offset">Optional offset (OFFSET clause).</param>
        /// <param name="fetch">Optional fetch (LIMIT/FETCH FIRST clause).</param>
        public EfCoreOrderBy(RelOptCluster cluster, RelTraitSet traitSet, RelNode input, RelCollation collation, RexNode? offset, RexNode? fetch) :
            base(cluster, traitSet, input, collation, offset, fetch)
        {

        }

        /// <inheritdoc />
        public Type ClrElementType => ((EfCoreRel)getInput()).ClrElementType;

        /// <inheritdoc />
        public override Sort copy(RelTraitSet traitSet, RelNode newInput, RelCollation newCollation, RexNode? offset, RexNode? fetch)
        {
            return new EfCoreOrderBy(getCluster(), traitSet, newInput, newCollation, offset, fetch);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public IQueryable implement()
        {
            var efRel = (EfCoreRel)getInput();
            var source = efRel.implement();
            var elementType = efRel.ClrElementType;
            var inputFields = efRel.getRowType().getFieldList();
            var param = Expression.Parameter(elementType, "e");
            var context = RexTranslationContext.ForSingleInput(inputFields, param);
            var fieldKeys = collation.getFieldCollations();
            var n = fieldKeys.size();

            IQueryable result = source;

            for (int i = 0; i < n; i++)
            {
                var fieldCollation = (RelFieldCollation)fieldKeys.get(i);
                var fieldIndex = fieldCollation.getFieldIndex();
                var fieldName = ((org.apache.calcite.rel.type.RelDataTypeField)inputFields.get(fieldIndex)).getName();
                var prop = elementType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                    ?? throw new InvalidOperationException($"EfCoreSort: property '{fieldName}' not found on '{elementType.Name}'.");
                var keySelector = Expression.Lambda(
                    typeof(Func<,>).MakeGenericType(elementType, prop.PropertyType),
                    Expression.Property(param, prop),
                    param);

                var isDescending = fieldCollation.getDirection() == RelFieldCollation.Direction.DESCENDING
                    || fieldCollation.getDirection() == RelFieldCollation.Direction.STRICTLY_DESCENDING;

                MethodInfo method;
                if (i == 0)
                    method = isDescending ? QueryableOrderByDescendingMethod : QueryableOrderByMethod;
                else
                    method = isDescending ? QueryableThenByDescendingMethod : QueryableThenByMethod;

                result = (IQueryable)method.MakeGenericMethod(elementType, prop.PropertyType).Invoke(null, [result, keySelector])!;
            }

            if (offset != null)
            {
                var offsetExpr = RexToLinqTranslator.Default.Translate(offset, context);
                if (offsetExpr.Type != typeof(int))
                    offsetExpr = Expression.Convert(offsetExpr, typeof(int));

                result = (IQueryable)QueryableSkipMethod.MakeGenericMethod(elementType).Invoke(null, [result, offsetExpr])!;
            }

            if (fetch != null)
            {
                var fetchExpr = RexToLinqTranslator.Default.Translate(fetch, context);
                if (fetchExpr.Type != typeof(int))
                    fetchExpr = Expression.Convert(fetchExpr, typeof(int));

                result = (IQueryable)QueryableTakeMethod.MakeGenericMethod(elementType).Invoke(null, [result, fetchExpr])!;
            }

            return result;
        }

    }

}
