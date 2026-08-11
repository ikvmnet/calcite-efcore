using System;
using System.Linq;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Core;

using com.google.common.collect;

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
    /// Implementation of <see cref="Project"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    public class EfCoreSelect : Project, EfCoreRel
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query planning cluster.</param>
        /// <param name="traitSet">Trait set for this node.</param>
        /// <param name="input">The input relational expression.</param>
        /// <param name="projects">The project list (one expression per output field).</param>
        /// <param name="rowType">The output row type.</param>
        public EfCoreSelect(RelOptCluster cluster, RelTraitSet traitSet, RelNode input, List projects, RelDataType rowType) :
            base(cluster, traitSet, ImmutableList.of(), input, projects, rowType, ImmutableSet.of())
        {

        }

        /// <inheritdoc />
        public Type ClrElementType => CalciteTypeMapper.ToClrType(getRowType());

        /// <inheritdoc />
        public override Project copy(RelTraitSet traitSet, RelNode input, List projects, RelDataType rowType)
        {
            return new EfCoreSelect(getCluster(), traitSet, input, projects, rowType);
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

            // Determine input element type from the source expression
            var sourceType = sourceExpr.Type;
            Type inputType;
            if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            {
                inputType = sourceType.GetGenericArguments()[0];
            }
            else
            {
                throw new InvalidOperationException($"EfCoreSelect source expression type {sourceType.Name} is not IQueryable<T>");
            }

            var inputFields = efRel.getRowType().getFieldList();
            var outputFields = getRowType().getFieldList();
            var projects = getProjects();
            var param = Expression.Parameter(inputType, "e");
            var context = rexContext.WithReplacedInputs(new InputSegment(inputFields, param));
            var clrElementType = CalciteTypeMapper.ToClrType(getRowType());

            // Get the translator from the convention
            var convention = (EfCoreConvention)getTraitSet().getConvention();
            var translator = convention.TranslatorFactory.Create();

            // Translate each project expression and bind it to the corresponding DTO property.
            var n = projects.size();
            var bindings = new MemberBinding[n];
            for (int i = 0; i < n; i++)
            {
                var prop = clrElementType.GetProperty(((RelDataTypeField)outputFields.get(i)).getName())!;
                var value = translator.Translate((RexNode)projects.get(i), context);

                // Coerce when the translated expression type doesn't exactly match the property type (e.g. widening numerics).
                var coerced = value.Type == prop.PropertyType ? value : Expression.Convert(value, prop.PropertyType);
                bindings[i] = Expression.Bind(prop, coerced);
            }

            var selector = Expression.Lambda(
                typeof(Func<,>).MakeGenericType(inputType, clrElementType),
                Expression.MemberInit(Expression.New(clrElementType), bindings),
                param);

            // Build Expression.Call for Queryable.Select<TSource, TResult>(source, selector)
            var selectMethod = QueryableMethods.Select.MakeGenericMethod(inputType, clrElementType);
            return Expression.Call(selectMethod, sourceExpr, selector);
        }

    }

}
