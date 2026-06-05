using java.lang;
using java.lang.reflect;
using java.util;

using org.apache.calcite;
using org.apache.calcite.adapter.enumerable;
using org.apache.calcite.linq4j.tree;
using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;
using org.apache.calcite.rel.type;
using org.apache.calcite.runtime;
using org.apache.calcite.util;

using System;
using System.Linq;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Relational expression that converts from <see cref="EfCoreConvention"/> to <see cref="EnumerableConvention"/> by executing an EF Core query at runtime.
    /// </summary>
    public class EfCoreToEnumerableConverter : ConverterImpl, EnumerableRel
    {

        static readonly Method ExecuteArrayMethod =
            ((Class)typeof(EfCoreEnumerable)).getDeclaredMethod(
                nameof(EfCoreEnumerable.ExecuteArray),
                [(Class)typeof(EfCoreConvention), (Class)typeof(IQueryable), (Class)typeof(string[]), (Class)typeof(DataContext)]);

        static readonly Method ExecuteScalarMethod =
            ((Class)typeof(EfCoreEnumerable)).getDeclaredMethod(
                nameof(EfCoreEnumerable.ExecuteScalar),
                [(Class)typeof(EfCoreConvention), (Class)typeof(IQueryable), (Class)typeof(string[]), (Class)typeof(DataContext)]);

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="cluster">The query planning cluster.</param>
        /// <param name="traits">Desired output trait set.</param>
        /// <param name="input">The EF Core relational input.</param>
        public EfCoreToEnumerableConverter(RelOptCluster cluster, RelTraitSet traits, RelNode input) :
            base(cluster, ConventionTraitDef.INSTANCE, traits, input)
        {

        }

        /// <inheritdoc />
        public override RelNode copy(RelTraitSet traitSet, List inputs)
        {
            return new EfCoreToEnumerableConverter(getCluster(), traitSet, (RelNode)sole(inputs));
        }

        /// <inheritdoc />
        public EnumerableRel.Result implement(EnumerableRelImplementor implementor, EnumerableRel.Prefer pref)
        {
            var list = new BlockBuilder();

            var input = getInput() as EfCoreRel;
            if (input is null)
                throw new InvalidOperationException("Input to EfCoreToEnumerableConverter must be an EfCoreRel.");

            var physType = PhysTypeImpl.of(
                implementor.getTypeFactory(),
                getRowType(),
                pref.prefer(JavaRowFormat.ARRAY));

            var efImplementor = new EfCoreRelImplementor();
            var queryable = efImplementor.visitChild(input);

            Hook.QUERY_PLAN.run(queryable);

            var convention = (EfCoreConvention?)input.getConvention();
            if (convention is null)
                throw new InvalidOperationException("Cannot resolve EfCoreConvention from input.");

            var fieldList = getRowType().getFieldList();
            var columnNames = new string[fieldList.size()];
            for (int i = 0; i < fieldList.size(); i++)
                columnNames[i] = ((RelDataTypeField)fieldList.get(i)).getName();

            // PhysTypeImpl.of internally calls format.optimize(rowType), which may promote ARRAY → SCALAR
            // (e.g. single-field row types). Read the resolved format from physType directly so the
            // iterator emits exactly the row shape that Calcite's generated parent code expects.
            var executeMethod = (JavaRowFormat.__Enum)physType.getFormat().ordinal() switch
            {
                JavaRowFormat.__Enum.ARRAY => ExecuteArrayMethod,
                JavaRowFormat.__Enum.SCALAR => ExecuteScalarMethod,
                var fmt => throw new NotSupportedException($"JavaRowFormat.{fmt} is not supported by EfCoreToEnumerableConverter.")
            };

            var enumerable_ = list.append("enumerable",
                Expressions.call(null, executeMethod,
                implementor.stash(convention, (Class)typeof(EfCoreConvention)),
                implementor.stash(queryable, (Class)typeof(IQueryable)),
                implementor.stash(columnNames, (Class)typeof(string[])),
                implementor.getRootExpression()));

            list.add(Expressions.return_(null, enumerable_));

            return implementor.result(physType, list.toBlock());
        }

        #region EnumerableRel default-method forwarding

        /// <inheritdoc />
        public Pair deriveTraits(RelTraitSet childTraits, int childId)
        {
            return EnumerableRel.__DefaultMethods.deriveTraits(this, childTraits, childId);
        }

        /// <inheritdoc />
        public DeriveMode getDeriveMode()
        {
            return EnumerableRel.__DefaultMethods.getDeriveMode(this);
        }

        /// <inheritdoc />
        public Pair passThroughTraits(RelTraitSet required)
        {
            return EnumerableRel.__DefaultMethods.passThroughTraits(this, required);
        }

        #endregion

        #region PhysicalNode default-method forwarding

        /// <inheritdoc />
        public RelNode derive(RelTraitSet childTraits, int childId)
        {
            return PhysicalNode.__DefaultMethods.derive(this, childTraits, childId);
        }

        /// <inheritdoc />
        public List derive(List inputTraits)
        {
            return PhysicalNode.__DefaultMethods.derive(this, inputTraits);
        }

        /// <inheritdoc />
        public RelNode passThrough(RelTraitSet required)
        {
            return PhysicalNode.__DefaultMethods.passThrough(this, required);
        }

        #endregion

    }

}
