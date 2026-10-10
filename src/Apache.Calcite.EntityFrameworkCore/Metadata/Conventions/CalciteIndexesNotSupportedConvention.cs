using System.Linq;

using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Apache.Calcite.EntityFrameworkCore.Metadata.Conventions
{

    /// <summary>
    /// A convention that removes every index from the model when it is finalized, because Calcite has none.
    /// </summary>
    public class CalciteIndexesNotSupportedConvention : IModelFinalizingConvention
    {

        /// <inheritdoc />
        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
                foreach (var index in entityType.GetDeclaredIndexes().ToList())
                    entityType.RemoveIndex(index);
        }
    }

}
