using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Core;

using com.google.common.collect;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.sql;
using org.apache.calcite.util;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Implementation of <see cref="Aggregate"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// Translates Calcite GROUP BY + aggregate calls into LINQ <c>GroupBy(...).Select(...)</c>.
    /// </summary>
    /// <remarks>
    /// Supported aggregate functions: <c>COUNT(*)</c>, <c>COUNT(col)</c>, <c>SUM</c>, <c>MIN</c>, <c>MAX</c>, <c>AVG</c>.
    /// All other aggregate functions throw <see cref="NotImplementedException"/>.
    /// </remarks>
    public class EfCoreGroupBy : Aggregate, EfCoreRel
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query-planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="input">The input relational expression.</param>
        /// <param name="groupSet">The set of group-by keys.</param>
        /// <param name="groupSets">The full list of grouping sets (may be <see langword="null"/>).</param>
        /// <param name="aggCalls">The aggregate function calls.</param>
        public EfCoreGroupBy(RelOptCluster cluster, RelTraitSet traitSet, RelNode input, ImmutableBitSet groupSet, List? groupSets, List aggCalls) :
            base(cluster, traitSet, ImmutableList.of(), input, groupSet, groupSets, aggCalls)
        {
        }

        /// <inheritdoc />
        public Type ClrElementType => CalciteTypeMapper.ToClrType(getRowType());

        /// <inheritdoc />
        public override Aggregate copy(RelTraitSet traitSet, RelNode input, ImmutableBitSet groupSet, List? groupSets, List aggCalls)
        {
            return new EfCoreGroupBy(getCluster(), traitSet, input, groupSet, groupSets, aggCalls);
        }

        /// <inheritdoc />
        public override RelOptCost? computeSelfCost(RelOptPlanner planner, RelMetadataQuery mq)
        {
            return base.computeSelfCost(planner, mq)?.multiplyBy(EfCoreConvention.CostMultiplier);
        }

        /// <inheritdoc />
        public Expression Implement(EfCoreRelImplementor implementor, EfCoreTranslationContext rexContext)
        {
            var efRel = (EfCoreRel)getInput();
            var sourceExpr = implementor.VisitChild(getInput(), rexContext);

            // Determine element type from the source expression
            var sourceType = sourceExpr.Type;
            Type elementType;
            if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                elementType = sourceType.GetGenericArguments()[0];
            }
            else
            {
                throw new InvalidOperationException($"EfCoreGroupBy source expression type {sourceType.Name} is not IQueryable<T>");
            }

            var inputFields = efRel.getRowType().getFieldList();
            var outputFields = getRowType().getFieldList();

            // Collect the ordered list of group key field indices from the ImmutableBitSet.
            var groupKeyIndices = new List<int>(groupSet.cardinality());
            for (int idx = groupSet.nextSetBit(0); idx >= 0; idx = groupSet.nextSetBit(idx + 1))
                groupKeyIndices.Add(idx);

            var elementParam = Expression.Parameter(elementType, "e");

            // ---- Build key type and key selector ----------------------------------------
            // For zero-key aggregates (SELECT COUNT(*) FROM …) we use a constant 0 as the key
            // so that all rows land in a single group.
            Type keyType;
            Expression keySelectorBody;

            if (groupKeyIndices.Count == 0)
            {
                keyType = typeof(int);
                keySelectorBody = Expression.Constant(0);
            }
            else if (groupKeyIndices.Count == 1)
            {
                var fieldName = ((RelDataTypeField)inputFields.get(groupKeyIndices[0])).getName();
                var prop = elementType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
                keyType = prop.PropertyType;
                keySelectorBody = Expression.Property(elementParam, prop);
            }
            else
            {
                // Multiple keys: build a DynamicRowType to carry them.
                var keyFields = new List<RelDataTypeField>(groupKeyIndices.Count);
                for (int i = 0; i < groupKeyIndices.Count; i++)
                    keyFields.Add((RelDataTypeField)inputFields.get(groupKeyIndices[i]));
                keyType = CalciteTypeMapper.ToClrType((IReadOnlyList<RelDataTypeField>)keyFields);

                var keyBindings = new MemberBinding[groupKeyIndices.Count];
                for (int i = 0; i < groupKeyIndices.Count; i++)
                {
                    var fieldName = ((RelDataTypeField)inputFields.get(groupKeyIndices[i])).getName();
                    var srcProp = elementType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
                    var dstProp = keyType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
                    keyBindings[i] = Expression.Bind(dstProp, Expression.Property(elementParam, srcProp));
                }

                keySelectorBody = Expression.MemberInit(Expression.New(keyType), keyBindings);
            }

            var keySelector = Expression.Lambda(
                typeof(Func<,>).MakeGenericType(elementType, keyType),
                keySelectorBody,
                elementParam);

            // GroupBy(source, keySelector)  =>  IQueryable<IGrouping<TKey, TElement>>
            var groupByMethod = QueryableMethods.GroupBy.MakeGenericMethod(elementType, keyType);
            var grouped = Expression.Call(groupByMethod, sourceExpr, keySelector);

            // ---- Build result selector: g => new OutputRow { ... } ----------------------
            var groupingType = typeof(IGrouping<,>).MakeGenericType(keyType, elementType);
            var groupParam = Expression.Parameter(groupingType, "g");
            var outputType = CalciteTypeMapper.ToClrType(getRowType());
            var aggCalls = getAggCallList();
            var outputFieldCount = outputFields.size();
            var bindings = new MemberBinding[outputFieldCount];

            // First: group key fields
            for (int i = 0; i < groupKeyIndices.Count; i++)
            {
                var outputField = (RelDataTypeField)outputFields.get(i);
                var dstProp = outputType.GetProperty(outputField.getName(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;

                Expression keyValue;
                if (groupKeyIndices.Count == 1)
                {
                    // g.Key is the value itself
                    keyValue = Expression.Property(groupParam, groupingType.GetProperty("Key")!);
                }
                else
                {
                    var keyProp = keyType.GetProperty(outputField.getName(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
                    keyValue = Expression.Property(Expression.Property(groupParam, groupingType.GetProperty("Key")!), keyProp);
                }

                if (keyValue.Type != dstProp.PropertyType)
                    keyValue = Expression.Convert(keyValue, dstProp.PropertyType);

                bindings[i] = Expression.Bind(dstProp, keyValue);
            }

            // Then: aggregate call fields
            for (int a = 0; a < aggCalls.size(); a++)
            {
                var aggCall = (AggregateCall)aggCalls.get(a);
                var outputField = (RelDataTypeField)outputFields.get(groupKeyIndices.Count + a);
                var dstProp = outputType.GetProperty(outputField.getName(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
                var aggExpr = BuildAggregateExpression(aggCall, groupParam, groupingType, elementType, inputFields, dstProp.PropertyType);
                bindings[groupKeyIndices.Count + a] = Expression.Bind(dstProp, aggExpr);
            }

            var resultSelector = Expression.Lambda(
                typeof(Func<,>).MakeGenericType(groupingType, outputType),
                Expression.MemberInit(Expression.New(outputType), bindings),
                groupParam);

            // IQueryable<IGrouping<TKey, TElement>>.Select(g => new OutputRow { ... })
            var selectMethod = QueryableMethods.Select.MakeGenericMethod(groupingType, outputType);
            return Expression.Call(selectMethod, grouped, resultSelector);
        }

        /// <summary>
        /// Builds the LINQ expression for a single aggregate call applied to a group parameter.
        /// </summary>
        static Expression BuildAggregateExpression(AggregateCall aggCall, ParameterExpression groupParam, Type groupingType, Type elementType, java.util.List inputFields, Type targetType)
        {
            var kind = (SqlKind.__Enum)aggCall.getAggregation().getKind().ordinal();
            var argList = aggCall.getArgList();

            return kind switch
            {
                SqlKind.__Enum.COUNT => BuildCount(aggCall, groupParam, elementType, inputFields, targetType),
                SqlKind.__Enum.SUM or SqlKind.__Enum.SUM0 => BuildSum(groupParam, elementType, inputFields, argList, targetType),
                SqlKind.__Enum.MIN => BuildMin(groupParam, elementType, inputFields, argList, targetType),
                SqlKind.__Enum.MAX => BuildMax(groupParam, elementType, inputFields, argList, targetType),
                SqlKind.__Enum.AVG => BuildAvg(groupParam, elementType, inputFields, argList, targetType),
                _ => throw new NotImplementedException($"EfCoreGroupBy: aggregate function '{aggCall.getAggregation().getName()}' (kind={kind}) is not yet implemented.")
            };
        }

        /// <summary>
        /// Translates <c>COUNT(*)</c>, <c>COUNT(col)</c>, and <c>COUNT(DISTINCT col)</c>.
        /// Distinct form emits <c>Select(field).Distinct().Count()</c>.
        /// </summary>
        static Expression BuildCount(AggregateCall aggCall, Expression groupParam, Type elementType, java.util.List inputFields, Type targetType)
        {
            Expression source = groupParam;

            if (aggCall.getArgList().size() > 0)
            {
                var (_, selector) = BuildFieldSelector(elementType, inputFields, ((java.lang.Integer)aggCall.getArgList().get(0)).intValue());
                var fieldType = selector.ReturnType;

                // g.Select(x => x.Field)
                source = Expression.Call(
                    EnumerableMethods.Select.MakeGenericMethod(elementType, fieldType),
                    groupParam,
                    selector);

                if (aggCall.isDistinct())
                {
                    // .Distinct()
                    source = Expression.Call(
                        EnumerableMethods.Distinct.MakeGenericMethod(fieldType),
                        source);
                }

                var countOfField = EnumerableMethods.Count.MakeGenericMethod(fieldType);
                Expression expr = Expression.Call(countOfField, source);
                if (expr.Type != targetType)
                    expr = Expression.Convert(expr, targetType);

                return expr;
            }

            // COUNT(*) — no column arg
            var method = EnumerableMethods.Count.MakeGenericMethod(elementType);
            Expression result = Expression.Call(method, source);
            if (result.Type != targetType)
                result = Expression.Convert(result, targetType);

            return result;
        }

        /// <summary>
        /// Translates <c>SUM</c> / <c>SUM0</c> to <see cref="Enumerable.Sum"/>.
        /// </summary>
        static Expression BuildSum(Expression groupParam, Type elementType, java.util.List inputFields, java.util.List argList, Type targetType)
        {
            if (argList.size() == 0)
                throw new NotSupportedException("EfCoreGroupBy: SUM requires exactly one argument.");

            var (fieldProp, selector) = BuildFieldSelector(elementType, inputFields, ((java.lang.Integer)argList.get(0)).intValue());
            var sumOpenMethod = fieldProp.PropertyType switch
            {
                var t when t == typeof(int) => EnumerableMethods.SumInt32,
                var t when t == typeof(long) => EnumerableMethods.SumInt64,
                var t when t == typeof(float) => EnumerableMethods.SumSingle,
                var t when t == typeof(double) => EnumerableMethods.SumDouble,
                var t when t == typeof(decimal) => EnumerableMethods.SumDecimal,
                var t when t == typeof(int?) => EnumerableMethods.SumNInt32,
                var t when t == typeof(long?) => EnumerableMethods.SumNInt64,
                var t when t == typeof(float?) => EnumerableMethods.SumNSingle,
                var t when t == typeof(double?) => EnumerableMethods.SumNDouble,
                var t when t == typeof(decimal?) => EnumerableMethods.SumNDecimal,
                _ => throw new NotSupportedException($"EfCoreGroupBy: SUM is not supported for field type '{fieldProp.PropertyType.Name}'.")
            };

            Expression expr = Expression.Call(sumOpenMethod.MakeGenericMethod(elementType), groupParam, selector);
            if (expr.Type != targetType)
                expr = Expression.Convert(expr, targetType);

            return expr;
        }

        /// <summary>
        /// Translates <c>MIN</c> to <see cref="Enumerable.Min"/>.
        /// </summary>
        static Expression BuildMin(Expression groupParam, Type elementType, java.util.List inputFields, java.util.List argList, Type targetType)
        {
            if (argList.size() == 0)
                throw new NotSupportedException("EfCoreGroupBy: MIN requires exactly one argument.");

            var (_, selector) = BuildFieldSelector(elementType, inputFields, ((java.lang.Integer)argList.get(0)).intValue());
            var method = EnumerableMethods.Min.MakeGenericMethod(elementType, selector.ReturnType);

            Expression expr = Expression.Call(method, groupParam, selector);
            if (expr.Type != targetType)
                expr = Expression.Convert(expr, targetType);

            return expr;
        }

        /// <summary>
        /// Translates <c>MAX</c> to <see cref="Enumerable.Max"/>.
        /// </summary>
        static Expression BuildMax(Expression groupParam, Type elementType, java.util.List inputFields, java.util.List argList, Type targetType)
        {
            if (argList.size() == 0)
                throw new NotSupportedException("EfCoreGroupBy: MAX requires exactly one argument.");

            var (_, selector) = BuildFieldSelector(elementType, inputFields, ((java.lang.Integer)argList.get(0)).intValue());
            var method = EnumerableMethods.Max.MakeGenericMethod(elementType, selector.ReturnType);

            Expression expr = Expression.Call(method, groupParam, selector);
            if (expr.Type != targetType)
                expr = Expression.Convert(expr, targetType);

            return expr;
        }

        /// <summary>
        /// Translates <c>AVG</c> to <see cref="Enumerable.Average"/>.
        /// </summary>
        static Expression BuildAvg(Expression groupParam, Type elementType, java.util.List inputFields, java.util.List argList, Type targetType)
        {
            if (argList.size() == 0)
                throw new NotSupportedException("EfCoreGroupBy: AVG requires exactly one argument.");

            var (fieldProp, selector) = BuildFieldSelector(elementType, inputFields, ((java.lang.Integer)argList.get(0)).intValue());
            var avgOpenMethod = fieldProp.PropertyType switch
            {
                var t when t == typeof(int) => EnumerableMethods.AverageInt32,
                var t when t == typeof(long) => EnumerableMethods.AverageInt64,
                var t when t == typeof(float) => EnumerableMethods.AverageSingle,
                var t when t == typeof(double) => EnumerableMethods.AverageDouble,
                var t when t == typeof(decimal) => EnumerableMethods.AverageDecimal,
                var t when t == typeof(int?) => EnumerableMethods.AverageNInt32,
                var t when t == typeof(long?) => EnumerableMethods.AverageNInt64,
                var t when t == typeof(float?) => EnumerableMethods.AverageNSingle,
                var t when t == typeof(double?) => EnumerableMethods.AverageNDouble,
                var t when t == typeof(decimal?) => EnumerableMethods.AverageNDecimal,
                _ => throw new NotSupportedException($"EfCoreGroupBy: AVG is not supported for field type '{fieldProp.PropertyType.Name}'.")
            };

            Expression expr = Expression.Call(avgOpenMethod.MakeGenericMethod(elementType), groupParam, selector);
            if (expr.Type != targetType)
                expr = Expression.Convert(expr, targetType);

            return expr;
        }

        /// <summary>
        /// Returns a field-access property and a <c>Func&lt;TElement, TField&gt;</c> lambda for <paramref name="fieldIndex"/>.
        /// </summary>
        static (PropertyInfo Prop, LambdaExpression Lambda) BuildFieldSelector(Type elementType, java.util.List inputFields, int fieldIndex)
        {
            var fieldName = ((RelDataTypeField)inputFields.get(fieldIndex)).getName();
            var prop = elementType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"EfCoreGroupBy: property '{fieldName}' not found on '{elementType.Name}'.");

            var param = Expression.Parameter(elementType, "x");
            var lambda = Expression.Lambda(
                typeof(Func<,>).MakeGenericType(elementType, prop.PropertyType),
                Expression.Property(param, prop),
                param);

            return (prop, lambda);
        }

            }

        }
