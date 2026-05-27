using org.apache.calcite.rel;
using org.apache.calcite.rel.convert;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel.Convert
{

    /// <summary>
    /// Base class for EF Core converter rules.
    /// </summary>
    public abstract class EfCoreConverterRule : ConverterRule
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="config">Rule configuration.</param>
        protected EfCoreConverterRule(Config config) : base(config) { }

        /// <summary>
        /// Default implementation of <see cref="convert"/> that returns null to indicate that the rule does not apply.
        /// </summary>
        /// <param name="rn"></param>
        /// <returns></returns>
        public override RelNode? convert(RelNode rn)
        {
            return null;
        }

    }

}
