using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities
{

    /// <summary>
    /// <see cref="RelationalModelCustomizer"/> that enables Calcite's HiLo entity sequence value
    /// generation strategy on every model created by the functional test contexts. The Calcite
    /// provider does not support an identity-style default for value-generated keys, so ad-hoc
    /// test contexts that rely on auto-generated integer keys require a sequence-based strategy
    /// to be applied globally for inserts to succeed. A default schema is also assigned when one
    /// has not already been configured, since the backing entity sequence requires a writable
    /// schema.
    /// </summary>
    public class HiLoEntitySequenceModelCustomizer : RelationalModelCustomizer
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        public HiLoEntitySequenceModelCustomizer(ModelCustomizerDependencies dependencies) :
            base(dependencies)
        {

        }

        /// <inheritdoc/>
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            modelBuilder.UseHiLoEntitySequence();
        }

    }

}
