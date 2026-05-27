using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;
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
using org.apache.calcite.sql.type;
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

        readonly Lazy<Type> _clrElementType;

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
            _clrElementType = new Lazy<Type>(BuildClrElementType);
        }

        /// <inheritdoc />
        public Type ClrElementType => _clrElementType.Value;

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
        public IQueryable implement()
        {
            var efRel = (EfCoreRel)getInput();
            var source = efRel.implement();
            var elementType = efRel.ClrElementType;
            var inputFields = efRel.getRowType().getFieldList();
            var outputFields = getRowType().getFieldList();

            // Collect the ordered list of group key field indices from the ImmutableBitSet.
            var groupKeyIndices = new List<int>();
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
                var keyShape = new (string Name, Type ClrType)[groupKeyIndices.Count];
                for (int i = 0; i < groupKeyIndices.Count; i++)
                {
                    var fieldName = ((RelDataTypeField)inputFields.get(groupKeyIndices[i])).getName();
                    var prop = elementType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
                    keyShape[i] = (fieldName, prop.PropertyType);
                }
                keyType = DynamicRowType.GetOrCreate(keyShape);
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
            var grouped = (IQueryable)QueryableMethods.GroupBy
                .MakeGenericMethod(elementType, keyType)
                .Invoke(null, [source, keySelector])!;

            // ---- Build result selector: g => new OutputRow { ... } ----------------------
            var groupingType = typeof(IGrouping<,>).MakeGenericType(keyType, elementType);
            var groupParam = Expression.Parameter(groupingType, "g");
            var outputType = ClrElementType;
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
            return (IQueryable)QueryableMethods.Select
                .MakeGenericMethod(groupingType, outputType)
                .Invoke(null, [grouped, resultSelector])!;
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
                    QueryableMethods.EnumerableSelect.MakeGenericMethod(elementType, fieldType),
                    groupParam,
                    selector);

                if (aggCall.isDistinct())
                {
                    // .Distinct()
                    source = Expression.Call(
                        QueryableMethods.Distinct.MakeGenericMethod(fieldType),
                        source);
                }

                var countOfField = QueryableMethods.Count.MakeGenericMethod(fieldType);
                Expression expr = Expression.Call(countOfField, source);
                if (expr.Type != targetType)
                    expr = Expression.Convert(expr, targetType);

                return expr;
            }

            // COUNT(*) — no column arg
            var method = QueryableMethods.Count.MakeGenericMethod(elementType);
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
                var t when t == typeof(int) => QueryableMethods.SumInt32,
                var t when t == typeof(long) => QueryableMethods.SumInt64,
                var t when t == typeof(float) => QueryableMethods.SumSingle,
                var t when t == typeof(double) => QueryableMethods.SumDouble,
                var t when t == typeof(decimal) => QueryableMethods.SumDecimal,
                var t when t == typeof(int?) => QueryableMethods.SumNInt32,
                var t when t == typeof(long?) => QueryableMethods.SumNInt64,
                var t when t == typeof(float?) => QueryableMethods.SumNSingle,
                var t when t == typeof(double?) => QueryableMethods.SumNDouble,
                var t when t == typeof(decimal?) => QueryableMethods.SumNDecimal,
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
            var method = QueryableMethods.Min.MakeGenericMethod(elementType, selector.ReturnType);

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
            var method = QueryableMethods.Max.MakeGenericMethod(elementType, selector.ReturnType);

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
                var t when t == typeof(int) => QueryableMethods.AverageInt32,
                var t when t == typeof(long) => QueryableMethods.AverageInt64,
                var t when t == typeof(float) => QueryableMethods.AverageSingle,
                var t when t == typeof(double) => QueryableMethods.AverageDouble,
                var t when t == typeof(decimal) => QueryableMethods.AverageDecimal,
                var t when t == typeof(int?) => QueryableMethods.AverageNInt32,
                var t when t == typeof(long?) => QueryableMethods.AverageNInt64,
                var t when t == typeof(float?) => QueryableMethods.AverageNSingle,
                var t when t == typeof(double?) => QueryableMethods.AverageNDouble,
                var t when t == typeof(decimal?) => QueryableMethods.AverageNDecimal,
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

        /// <summary>
        /// Builds the CLR element type for the output row shape from the row type's fields.
        /// </summary>
        Type BuildClrElementType()
        {
            var fields = getRowType().getFieldList();
            var n = fields.size();
            var shape = new (string Name, Type ClrType)[n];
            for (int i = 0; i < n; i++)
            {
                var field = (RelDataTypeField)fields.get(i);
                var sqlTypeName = (SqlTypeName.__Enum)field.getType().getSqlTypeName().ordinal();
                shape[i] = (field.getName(), CalciteTypeMapper.ToClrType(sqlTypeName) ?? typeof(object));
            }

            return DynamicRowType.GetOrCreate(shape);
        }

    }

}
