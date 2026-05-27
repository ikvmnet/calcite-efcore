using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Adapter.Query;
using Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using com.google.common.collect;

using java.util;

using org.apache.calcite.plan;
using org.apache.calcite.plan.volcano;
using org.apache.calcite.rel;
using org.apache.calcite.rel.core;
using org.apache.calcite.rel.metadata;
using org.apache.calcite.rel.type;
using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Core
{

    /// <summary>
    /// Implementation of <see cref="Project"/> in the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    public class EfCoreSelect : Project, EfCoreRel
    {

        readonly Lazy<Type> _clrElementType;

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
            _clrElementType = new Lazy<Type>(CreateClrElementType);
        }

        /// <inheritdoc />
        public Type ClrElementType => _clrElementType.Value;

        /// <summary>
        /// Resolves the output CLR shape by inspecting each project expression, then emits the DTO type via
        /// <see cref="DynamicRowType"/>. Invoked at most once by <see cref="_clrElementType"/>.
        /// </summary>
        Type CreateClrElementType()
        {
            var efRel = (EfCoreRel)getInput();
            var outputFields = getRowType().getFieldList();
            var projects = getProjects();
            var inputParam = Expression.Parameter(efRel.ClrElementType);
            var context = RexTranslationContext.ForSingleInput(efRel.getRowType().getFieldList(), inputParam);

            var n = projects.size();
            var shape = new (string Name, Type ClrType)[n];
            for (int i = 0; i < n; i++)
            {
                var outputField = (RelDataTypeField)outputFields.get(i);
                shape[i] = (outputField.getName(), RexToLinqTranslator.Default.ResolveType((RexNode)projects.get(i), context));
            }

            return DynamicRowType.GetOrCreate(shape);
        }

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
        public IQueryable implement()
        {
            var efRel = (EfCoreRel)getInput();
            var inputType = efRel.ClrElementType;
            var inputFields = efRel.getRowType().getFieldList();
            var outputFields = getRowType().getFieldList();
            var projects = getProjects();
            var param = Expression.Parameter(inputType, "e");
            var context = RexTranslationContext.ForSingleInput(inputFields, param);
            var clrElementType = ClrElementType;

            // Translate each project expression and bind it to the corresponding DTO property.
            var n = projects.size();
            var bindings = new MemberBinding[n];
            for (int i = 0; i < n; i++)
            {
                var prop = clrElementType.GetProperty(((RelDataTypeField)outputFields.get(i)).getName())!;
                var value = RexToLinqTranslator.Default.Translate((RexNode)projects.get(i), context);

                // Coerce when the translated expression type doesn't exactly match the property type (e.g. widening numerics).
                var coerced = value.Type == prop.PropertyType ? value : Expression.Convert(value, prop.PropertyType);
                bindings[i] = Expression.Bind(prop, coerced);
            }

            var selector = Expression.Lambda(
                typeof(Func<,>).MakeGenericType(inputType, clrElementType),
                Expression.MemberInit(Expression.New(clrElementType), bindings),
                param);

            return (IQueryable)QueryableMethods.Select.MakeGenericMethod(efRel.ClrElementType, ClrElementType).Invoke(null, [efRel.implement(), selector])!;
        }

    }

}
