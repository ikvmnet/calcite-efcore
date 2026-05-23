using org.apache.calcite.rel;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rel
{

    /// <summary>
    /// Relational expression that uses the EF Core calling convention.
    /// </summary>
    public interface EfCoreRel : RelNode
    {

        /// <summary>
        /// Invoked by the <see cref="EfCoreImplementor"/> during query implementation.
        /// </summary>
        /// <param name="implementor"></param>
        /// <returns></returns>
        public EfCoreImplementor.Result implement(EfCoreImplementor implementor)
        {
            return implementor.implement(this);
        }

    }

}
