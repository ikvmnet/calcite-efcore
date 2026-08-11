using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Core;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{
    /// <summary>
    /// Physical implementation of DefaultIfEmpty operation for the EF Core adapter.
    /// Wraps an input queryable with .DefaultIfEmpty() to ensure at least one result (with nulls).
    /// Used primarily in LEFT JOIN decomposition.
    /// </summary>
    public sealed class EfCoreDefaultIfEmpty : SingleRel, EfCoreRel
    {
        /// <summary>
        /// Creates a new <see cref="EfCoreDefaultIfEmpty"/>.
        /// </summary>
        public EfCoreDefaultIfEmpty(
            RelOptCluster cluster,
            RelTraitSet traitSet,
            RelNode input)
            : base(cluster, traitSet, input)
        {
        }

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, List inputs)
        {
            return new EfCoreDefaultIfEmpty(
                getCluster(),
                traitSet,
                (RelNode)inputs.get(0));
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            // DefaultIfEmpty has minimal cost - just adds a default row if empty
            var inputRows = mq.getRowCount(getInput());

            // Ensure at least 1 row
            var resultRows = Math.Max(1.0, inputRows.doubleValue());
            return planner.getCostFactory().makeCost(resultRows, resultRows, 0)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext rexContext)
        {
            // Get the input expression, passing the rexContext through
            var inputExpr = implementor.VisitChild(getInput(), rexContext);

            // Determine the element type and whether to use Queryable or Enumerable
            var inputType = inputExpr.Type;
            Type elementType;
            bool isQueryable;

            // Check if it's IQueryable<T> (IQueryable<T> is also IEnumerable<T>, so check it first)
            if (inputType.IsGenericType && inputType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                elementType = inputType.GetGenericArguments()[0];
                isQueryable = true;
            }
            else if (inputType.IsAssignableTo(typeof(System.Linq.IQueryable)))
            {
                // Find the IQueryable<T> interface
                var queryableInterface = inputType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));

                if (queryableInterface != null)
                {
                    elementType = queryableInterface.GetGenericArguments()[0];
                    isQueryable = true;
                }
                else
                {
                    throw new InvalidOperationException($"EfCoreDefaultIfEmpty input expression type {inputType.Name} implements IQueryable but not IQueryable<T>");
                }
            }
            else if (inputType.IsGenericType && inputType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = inputType.GetGenericArguments()[0];
                isQueryable = false;
            }
            else if (inputType.IsAssignableTo(typeof(System.Collections.IEnumerable)))
            {
                // Find the IEnumerable<T> interface
                var enumerableInterface = inputType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

                if (enumerableInterface != null)
                {
                    elementType = enumerableInterface.GetGenericArguments()[0];
                    isQueryable = false;
                }
                else
                {
                    throw new InvalidOperationException($"EfCoreDefaultIfEmpty input expression type {inputType.Name} implements IEnumerable but not IEnumerable<T>");
                }
            }
            else
            {
                throw new InvalidOperationException($"EfCoreDefaultIfEmpty input expression type {inputType.Name} is not IQueryable<T> or IEnumerable<T>");
            }

            // Use Queryable.DefaultIfEmpty for IQueryable, Enumerable.DefaultIfEmpty for IEnumerable
            var defaultIfEmptyMethod = isQueryable
                ? QueryableMethods.DefaultIfEmpty.MakeGenericMethod(elementType)
                : EnumerableMethods.DefaultIfEmpty.MakeGenericMethod(elementType);
            return Expression.Call(defaultIfEmptyMethod, inputExpr);
        }
    }
}
