using System;

using java.lang;
using java.lang.reflect;
using java.util;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.adapter.enumerable;
using org.apache.calcite.linq4j.tree;
using org.apache.calcite.plan;
using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;
using org.apache.calcite.rel.type;
using org.apache.calcite.schema;
using org.apache.calcite.util;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Relational expression that converts from <see cref="EfCoreConvention"/> to
    /// <see cref="EnumerableConvention"/> by executing an EF Core query at runtime.
    /// </summary>
    public class EfCoreToEnumerableConverter : ConverterImpl, EnumerableRel
    {

        static readonly Method ScanMethod =
            ((Class)typeof(EfCoreEnumerable)).getDeclaredMethod(
                nameof(EfCoreEnumerable.Scan),
                [(Class)typeof(EfCoreSchema), (Class)typeof(string), (Class)typeof(string[])]);

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

            var input = getInput() as EfCoreRel
                ?? throw new InvalidOperationException("Input to EfCoreToEnumerableConverter must be an EfCoreRel.");

            var physType = PhysTypeImpl.of(
                implementor.getTypeFactory(),
                getRowType(),
                pref.prefer(JavaRowFormat.ARRAY));

            // Walk the EfCoreRel tree to find the root table and convention.
            var efImplementor = new EfCoreImplementor();
            efImplementor.Visit(input);

            var convention = (EfCoreConvention?)
                (input as RelNode)?.getConvention()
                ?? throw new InvalidOperationException("Cannot resolve EfCoreConvention from input.");

            // Build the ordered column names array matching the Calcite row type.
            var fieldList = getRowType().getFieldList();
            var columnNames = new string[fieldList.size()];
            for (int i = 0; i < fieldList.size(); i++)
                columnNames[i] = ((RelDataTypeField)fieldList.get(i)).getName();

            var columnNamesExpr = Expressions.constant(columnNames, (Class)typeof(string[]));

            // Retrieve the EfCoreSchema from the schema expression embedded in the convention.
            var schemaExpr = Schemas.unwrap(convention.Expression, typeof(EfCoreSchema));

            // Determine the table name — always the CLR type name, matching schema discovery.
            var tableName = efImplementor.RootTable is EfCoreTable t
                ? t.EntityClrType.Name
                : throw new InvalidOperationException("Could not find an EfCoreEntityScan root node in the EfCoreRel tree.");

            // Emit: EfCoreEnumerable.Scan(schema, tableName, columnNames)
            var enumerable_ = list.append("enumerable",
                Expressions.call(
                    null,
                    ScanMethod,
                    schemaExpr,
                    Expressions.constant(tableName),
                    columnNamesExpr));

            list.add(Expressions.return_(null, enumerable_));

            return implementor.result(physType, list.toBlock());
        }

        #region EnumerableRel default-method forwarding

        /// <inheritdoc />
        public Pair deriveTraits(RelTraitSet childTraits, int childId) =>
            EnumerableRel.__DefaultMethods.deriveTraits(this, childTraits, childId);

        /// <inheritdoc />
        public DeriveMode getDeriveMode() =>
            EnumerableRel.__DefaultMethods.getDeriveMode(this);

        /// <inheritdoc />
        public Pair passThroughTraits(RelTraitSet required) =>
            EnumerableRel.__DefaultMethods.passThroughTraits(this, required);

        #endregion

        #region PhysicalNode default-method forwarding

        /// <inheritdoc />
        public RelNode derive(RelTraitSet childTraits, int childId) =>
            PhysicalNode.__DefaultMethods.derive(this, childTraits, childId);

        /// <inheritdoc />
        public List derive(List inputTraits) =>
            PhysicalNode.__DefaultMethods.derive(this, inputTraits);

        /// <inheritdoc />
        public RelNode passThrough(RelTraitSet required) =>
            PhysicalNode.__DefaultMethods.passThrough(this, required);

        #endregion

    }

}
