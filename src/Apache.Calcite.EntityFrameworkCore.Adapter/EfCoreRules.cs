using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Rules;

using org.apache.calcite.plan;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// Rules and relational operators for the <see cref="EfCoreConvention"/> calling convention.
    /// </summary>
    public static class EfCoreRules
    {

        /// <summary>
        /// Returns all planner rules that should be registered for the given
        /// <see cref="EfCoreConvention"/> instance.
        /// </summary>
        /// <param name="convention">The EF Core convention instance.</param>
        /// <returns>An enumerable of <see cref="RelOptRule"/> instances.</returns>
        public static IEnumerable<RelOptRule> GetRules(EfCoreConvention convention)
        {
            yield return EfCoreToEnumerableConverterRule.Create(convention);
            yield return EfCoreSelectRule.Create(convention);
            yield return EfCoreInheritanceJoinRule.Instance;
        }

    }

}
