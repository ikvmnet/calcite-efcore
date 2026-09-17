using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Query.Internal;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Storage.Internal;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

/// <summary>
/// Registers the NetTopologySuite services the provider reads geometries through.
/// </summary>
public static class CalciteNetTopologySuiteServiceCollectionExtensions
{

    /// <summary>
    /// Adds the services that map NetTopologySuite geometries onto Calcite's <c>GEOMETRY</c>.
    /// </summary>
    /// <param name="serviceCollection"></param>
    /// <returns></returns>
    public static IServiceCollection AddEntityFrameworkCalciteNetTopologySuite(this IServiceCollection serviceCollection)
    {
        new EntityFrameworkRelationalServicesBuilder(serviceCollection)
            .TryAdd<IRelationalTypeMappingSourcePlugin, CalciteNetTopologySuiteTypeMappingSourcePlugin>()
            .TryAdd<IMemberTranslatorPlugin, CalciteNetTopologySuiteMemberTranslatorPlugin>()
            .TryAdd<IMethodCallTranslatorPlugin, CalciteNetTopologySuiteMethodCallTranslatorPlugin>()
            .TryAdd<IAggregateMethodCallTranslatorPlugin, CalciteNetTopologySuiteAggregateMethodCallTranslatorPlugin>();

        return serviceCollection;
    }

}
