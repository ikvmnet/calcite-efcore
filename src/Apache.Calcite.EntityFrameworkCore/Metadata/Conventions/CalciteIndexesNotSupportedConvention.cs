using System.Linq;

using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Apache.Calcite.EntityFrameworkCore.Metadata.Conventions
{

    /// <summary>
    /// A convention that checks for indexes in the model and errors if any are present.
    /// </summary>
    public class CalciteIndexesNotSupportedConvention : IModelFinalizingConvention
    {

        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
                foreach (var index in entityType.GetIndexes().ToList())
                    entityType.RemoveIndex(index);
        }
    }

}
