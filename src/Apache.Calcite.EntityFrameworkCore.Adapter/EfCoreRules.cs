using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.RelFactories;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules;

using org.apache.calcite.plan;
using org.apache.calcite.tools;

using static org.apache.calcite.rel.core.RelFactories;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Rules and relational operators for the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    public static class EfCoreRules
    {

        static readonly ProjectFactory PROJECT_FACTORY = new EfCoreSelectFactory();
        static readonly FilterFactory FILTER_FACTORY = new EfCoreFilterFactory();
        static readonly JoinFactory JOIN_FACTORY = new EfCoreJoinFactory();
        static readonly SortFactory SORT_FACTORY = new EfCoreOrderByFactory();
        static readonly ExchangeFactory EXCHANGE_FACTORY = new EfCoreExchangeFactory();
        static readonly SortExchangeFactory SORT_EXCHANGE_FACTORY = new EfCoreSortExchangeFactory();
        static readonly AggregateFactory AGGREGATE_FACTORY = new EfCoreGroupByFactory();
        static readonly MatchFactory MATCH_FACTORY = new EfCoreMatchFactory();
        static readonly SetOpFactory SET_OP_FACTORY = new EfCoreSetOpFactory();
        static readonly ValuesFactory VALUES_FACTORY = new EfCoreValuesFactory();
        static readonly TableScanFactory TABLE_SCAN_FACTORY = new EfCoreTableScanFactory();
        static readonly SnapshotFactory SNAPSHOT_FACTORY = new EfCoreSnapshotFactory();

        /// <summary>
        /// A <see cref="RelBuilderFactory"/> that creates a <see cref="RelBuilder"/> that will create EF Core relational expressions for everything.
        /// </summary>
        public static readonly RelBuilderFactory Builder = RelBuilder.proto(Contexts.of(
            PROJECT_FACTORY,
            FILTER_FACTORY,
            JOIN_FACTORY,
            SORT_FACTORY,
            EXCHANGE_FACTORY,
            SORT_EXCHANGE_FACTORY,
            AGGREGATE_FACTORY,
            MATCH_FACTORY,
            SET_OP_FACTORY,
            VALUES_FACTORY,
            TABLE_SCAN_FACTORY,
            SNAPSHOT_FACTORY));

        /// <summary>
        /// Returns all planner rules that should be registered for the given <see cref="EfCoreConvention"/> instance.
        /// </summary>
        /// <param name="convention">The EF Core convention instance.</param>
        /// <returns>An enumerable of <see cref="RelOptRule"/> instances.</returns>
        public static IEnumerable<RelOptRule> GetRules(EfCoreConvention convention)
        {
            //yield return EfCoreToEnumerableConverterRule.Create(convention);
            yield return EfCoreToBindableConverterRule.Create(convention);
            yield return EfCoreSelectRule.Create(convention);
            yield return EfCoreWhereRule.Create(convention);
            yield return EfCoreGroupByRule.Create(convention);
            yield return EfCoreOrderByRule.Create(convention);
            yield return EfCoreJoinRule.Create(convention);
            yield return EfCoreUnionRule.Create(convention);
            yield return EfCoreIntersectRule.Create(convention);
            yield return EfCoreMinusRule.Create(convention);
            yield return EfCoreValuesRule.Create(convention);
            yield return EfCoreInheritanceJoinRule.Instance;
        }

        /// <summary>
        /// Returns all planner rules that should be registered for the given <see cref="EfCoreConvention"/> instance,
        /// using the supplied <see cref="RelBuilderFactory"/> to construct intermediate rel nodes inside each rule.
        /// </summary>
        /// <param name="convention">The EF Core convention instance.</param>
        /// <param name="relBuilderFactory">The factory used to construct rel nodes within rules.</param>
        /// <returns>An enumerable of <see cref="RelOptRule"/> instances.</returns>
        public static IEnumerable<RelOptRule> GetRules(EfCoreConvention convention, RelBuilderFactory relBuilderFactory)
        {
            //yield return EfCoreToEnumerableConverterRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreToBindableConverterRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreSelectRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreWhereRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreGroupByRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreOrderByRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreJoinRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreUnionRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreIntersectRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreMinusRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreValuesRule.Create(convention).config.withRelBuilderFactory(relBuilderFactory).toRule();
            yield return EfCoreInheritanceJoinRule.Instance;
        }

    }

}
