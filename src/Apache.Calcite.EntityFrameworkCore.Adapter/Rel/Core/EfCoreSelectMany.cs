using System;
using System.Linq;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;
using Apache.Calcite.EntityFrameworkCore.Core;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;

using static Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreTranslationContext;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Physical rel node that translates to <see cref="System.Linq.Queryable.SelectMany{TSource, TCollection, TResult}"/>.
    /// </summary>
    /// <remarks>
    /// SelectMany is used for:
    /// <list type="bullet">
    ///   <item>Flattening grouped results (e.g., after <see cref="EfCoreGroupJoin"/>)</item>
    ///   <item>Navigating collection properties</item>
    ///   <item>Correlated subqueries</item>
    /// </list>
    /// The collection selector is a <see cref="RexNode"/> that may contain a <see cref="RexSubQuery"/>
    /// referencing a correlated relational operation (e.g., DefaultIfEmpty for LEFT JOIN).
    /// </remarks>
    public class EfCoreSelectMany : SingleRel, EfCoreRel
    {

        /// <summary>
        /// Collection selector lambda: (source) => collection.
        /// The lambda parameter represents the source row, and the body is a collection expression
        /// (may be a <see cref="RexSubQuery"/> wrapping a relational operation).
        /// </summary>
        public RexLambda CollectionSelector { get; }

        /// <summary>
        /// Result selector lambda: (source, item) => result.
        /// The lambda parameters represent the source row and collection element, and the body combines them.
        /// </summary>
        public RexLambda ResultSelector { get; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="input">Input (source) relational expression.</param>
        /// <param name="collectionSelector">Lambda expression: (source) => collection.</param>
        /// <param name="resultSelector">Lambda expression: (source, item) => result.</param>
        public EfCoreSelectMany(
            RelOptCluster cluster,
            RelTraitSet traitSet,
            RelNode input,
            RexLambda collectionSelector,
            RexLambda resultSelector) :
            base(cluster, traitSet, input)
        {
            CollectionSelector = collectionSelector;
            ResultSelector = resultSelector;
            this.rowType = resultSelector.getType();
        }

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, List inputs)
        {
            return new EfCoreSelectMany(
                getCluster(),
                traitSet,
                (RelNode)inputs.get(0),
                CollectionSelector,
                ResultSelector);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            // SelectMany typically multiplies row count by estimated collection size
            var sourceRows = mq.getRowCount(getInput());

            // Estimate collection size - for now use a simple multiplier
            // In the future, this could analyze the CollectionSelector to get better estimates
            var avgCollectionSize = 10.0; // heuristic

            var resultRows = sourceRows.doubleValue() * avgCollectionSize;
            return planner.getCostFactory().makeCost(resultRows, sourceRows.doubleValue() + resultRows, 0)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext context)
        {
            var convention = (EfCoreConvention)getConvention();
            var translator = convention.TranslatorFactory.Create();

            // Implement the source input
            var sourceRel = (EfCoreRel)getInput();
            var sourceExpr = implementor.VisitChild(getInput(), context);

            // Determine source type from the source expression
            var sourceExprType = sourceExpr.Type;
            Type sourceType;
            if (sourceExprType.IsGenericType && sourceExprType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                sourceType = sourceExprType.GetGenericArguments()[0];
            }
            else if (sourceExprType.IsAssignableTo(typeof(System.Linq.IQueryable)))
            {
                var queryableInterface = sourceExprType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));

                if (queryableInterface != null)
                {
                    sourceType = queryableInterface.GetGenericArguments()[0];
                }
                else
                {
                    throw new InvalidOperationException($"EfCoreSelectMany source expression type {sourceExprType.Name} implements IQueryable but not IQueryable<T>");
                }
            }
            else
            {
                throw new InvalidOperationException($"EfCoreSelectMany source expression type {sourceExprType.Name} is not IQueryable<T>");
            }

            var resultType = CalciteTypeMapper.ToClrType(getRowType());

            // Extract the collection lambda parameter and body
            var collectionLambdaParams = CollectionSelector.getParameters();
            if (collectionLambdaParams.size() != 1)
            {
                throw new InvalidOperationException($"EfCoreSelectMany.CollectionSelector must have exactly 1 lambda parameter, found {collectionLambdaParams.size()}");
            }

            var collectionLambdaParam = (RexLambdaRef)collectionLambdaParams.get(0);
            var collectionBody = CollectionSelector.getExpression();

            // Build the CLR lambda parameter for the collection selector
            var sourceParam = Expression.Parameter(sourceType, collectionLambdaParam.getName());
            var sourceFields = sourceRel.getRowType().getFieldList();

            // Pass the implementor in the context so RexSubQuery nodes can be translated
            var sourceContext = context.WithLambdaParameter(collectionLambdaParam, sourceParam);
            var collectionExpr = translator.Translate(collectionBody, sourceContext);

            // Validate that the collection selector returns IEnumerable<T> or IQueryable<T>
            var collectionExprType = collectionExpr.Type;
            if (!IsEnumerableOrQueryable(collectionExprType))
            {
                throw new InvalidOperationException($"CollectionSelector body must translate to IEnumerable<T> or IQueryable<T>, but got {collectionExprType.Name}");
            }

            // Determine the collection element type
            var collectionType = collectionExprType.GetGenericArguments()[0]; // IEnumerable<T> or IQueryable<T> -> T

            var collectionSelector = Expression.Lambda(collectionExpr, sourceParam);

            // Extract the result lambda parameters and body
            var resultLambdaParams = ResultSelector.getParameters();
            if (resultLambdaParams.size() != 2)
            {
                throw new InvalidOperationException($"EfCoreSelectMany.ResultSelector must have exactly 2 lambda parameters, found {resultLambdaParams.size()}");
            }

            var resultLambdaSourceParam = (RexLambdaRef)resultLambdaParams.get(0);
            var resultLambdaItemParam = (RexLambdaRef)resultLambdaParams.get(1);
            var resultBody = ResultSelector.getExpression();

            // Build the CLR lambda parameters for the result selector
            var resultSourceParam = Expression.Parameter(sourceType, resultLambdaSourceParam.getName());
            var resultItemParam = Expression.Parameter(collectionType, resultLambdaItemParam.getName());

            // Determine the collection element's row type from the lambda body
            var collectionRowType = GetCollectionElementType(collectionBody, sourceRel);

            var resultSelector = BuildResultSelector(
                sourceRel,
                collectionRowType,
                resultSourceParam,
                resultItemParam,
                resultType,
                resultBody,
                resultLambdaSourceParam,
                resultLambdaItemParam,
                translator,
                context);

            // Build Expression.Call for Queryable.SelectMany<TSource, TCollection, TResult>
            var selectManyMethod = QueryableMethods.SelectMany.MakeGenericMethod(
                sourceType,
                collectionType,
                resultType);

            return Expression.Call(selectManyMethod, sourceExpr, collectionSelector, resultSelector);
        }

        /// <summary>
        /// Determines the row type of the collection elements from the collection lambda body.
        /// </summary>
        RelDataType GetCollectionElementType(RexNode collectionBody, EfCoreRel sourceRel)
        {
            // If the body is a RexSubQuery, extract the row type from the subquery's RelNode
            if (collectionBody is RexSubQuery subQuery)
            {
                return subQuery.rel.getRowType();
            }

            // If it's a field reference, the type should be derivable from the field's type
            // For now, assume it's the collection's element type from the body's type
            var bodyType = collectionBody.getType();

            // If it's a MULTISET type, unwrap to get the element type
            if (bodyType.getSqlTypeName() == org.apache.calcite.sql.type.SqlTypeName.MULTISET)
            {
                return bodyType.getComponentType();
            }

            // Otherwise return the body's type itself
            return bodyType;
        }

        /// <summary>
        /// Builds a result selector: (source, item) => new Result { Field1 = ..., Field2 = ..., ... }
        /// </summary>
        LambdaExpression BuildResultSelector(
            EfCoreRel sourceRel,
            RelDataType collectionRowType,
            ParameterExpression sourceParam,
            ParameterExpression itemParam,
            Type resultType,
            RexNode resultBody,
            RexLambdaRef resultLambdaSourceParam,
            RexLambdaRef resultLambdaItemParam,
            IRexToLinqTranslator translator,
            EfCoreTranslationContext context)
        {
            // The result body defines how to combine source and collection element
            // We need to translate it with both lambda parameters available
            var sourceFields = sourceRel.getRowType().getFieldList();
            var collectionFields = collectionRowType.getFieldList();
            var resultExpr = translator.Translate(resultBody, context.WithLambdaParameter(resultLambdaSourceParam, sourceParam).WithLambdaParameter(resultLambdaItemParam, itemParam));
            return Expression.Lambda(resultExpr, sourceParam, itemParam);
        }

        /// <summary>
        /// Checks if the given type is IEnumerable&lt;T&gt; or IQueryable&lt;T&gt;.
        /// </summary>
        static bool IsEnumerableOrQueryable(Type type)
        {
            if (!type.IsGenericType)
                return false;

            var genericTypeDef = type.GetGenericTypeDefinition();
            return genericTypeDef == typeof(IQueryable<>)
                || genericTypeDef == typeof(System.Collections.Generic.IEnumerable<>)
                || genericTypeDef == typeof(System.Linq.IOrderedQueryable<>)
                || genericTypeDef == typeof(System.Linq.IOrderedEnumerable<>);
        }

    }

}
