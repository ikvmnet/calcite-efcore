using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Scaffolding.Internal;

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

[assembly: DesignTimeProviderServices("Apache.Calcite.EntityFrameworkCore.Design.Internal.CalciteDesignTimeServices")]

namespace Apache.Calcite.EntityFrameworkCore.Design.Internal
{

    public class CalciteDesignTimeServices : IDesignTimeServices
    {

        public virtual void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddEntityFrameworkCalcite();

            new EntityFrameworkRelationalDesignServicesBuilder(serviceCollection)
                .TryAdd<IDatabaseModelFactory, CalciteDatabaseModelFactory>()
                .TryAddCoreServices();
        }

    }

}
