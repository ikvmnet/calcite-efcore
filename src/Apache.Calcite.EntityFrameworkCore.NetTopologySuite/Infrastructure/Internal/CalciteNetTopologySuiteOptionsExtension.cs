using System;
using System.Collections.Generic;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Storage.Internal;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Infrastructure.Internal;

/// <summary>
/// Adds the NetTopologySuite services to a context that asked for them.
/// </summary>
public class CalciteNetTopologySuiteOptionsExtension : IDbContextOptionsExtension
{

    DbContextOptionsExtensionInfo? _info;

    /// <inheritdoc />
    public virtual DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    /// <inheritdoc />
    public virtual void ApplyServices(IServiceCollection services)
    {
        services.AddEntityFrameworkCalciteNetTopologySuite();
    }

    /// <inheritdoc />
    /// <remarks>
    /// A context given its own internal service provider has to have had the services added to it, and the
    /// failure otherwise is a geometry property that maps to nothing, which reads as a modelling mistake
    /// rather than a missing registration.
    /// </remarks>
    /// <param name="options"></param>
    public virtual void Validate(IDbContextOptions options)
    {
        var internalServiceProvider = options.FindExtension<CoreOptionsExtension>()?.InternalServiceProvider;
        if (internalServiceProvider is null)
            return;

        using var scope = internalServiceProvider.CreateScope();
        var plugins = scope.ServiceProvider.GetService<IEnumerable<IRelationalTypeMappingSourcePlugin>>();

        if (plugins?.Any(p => p is CalciteNetTopologySuiteTypeMappingSourcePlugin) != true)
            throw new InvalidOperationException(
                "UseNetTopologySuite requires AddEntityFrameworkCalciteNetTopologySuite on the internal service provider this context was given.");
    }

    sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {

        /// <inheritdoc />
        public override bool IsDatabaseProvider => false;

        /// <inheritdoc />
        public override string LogFragment => "using NetTopologySuite ";

        /// <inheritdoc />
        public override int GetServiceProviderHashCode() => 0;

        /// <inheritdoc />
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

        /// <inheritdoc />
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            debugInfo["Calcite:UseNetTopologySuite"] = "1";
        }

    }

}
