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

        /// <inheritdoc />
        public override RelNode? convert(RelNode rn)
        {
            return null;
        }

    }

}

