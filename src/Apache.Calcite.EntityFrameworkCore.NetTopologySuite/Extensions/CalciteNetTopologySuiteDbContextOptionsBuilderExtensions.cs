using Apache.Calcite.EntityFrameworkCore.Infrastructure;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Infrastructure.Internal;

using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

/// <summary>
/// Turns on NetTopologySuite mapping for a Calcite context.
/// </summary>
public static class CalciteNetTopologySuiteDbContextOptionsBuilderExtensions
{

    /// <summary>
    /// Maps NetTopologySuite geometry types onto Calcite's <c>GEOMETRY</c>.
    /// </summary>
    /// <remarks>
    /// The connection has to be one that can answer a spatial query, and that is the caller's to set up
    /// rather than this provider's: <c>fun</c> must name <c>spatial</c>, the conformance must be one that
    /// allows the <c>GEOMETRY</c> type, and Calcite 1.43's model class allowlist must name
    /// <c>org.apache.calcite.runtime.SpatialTypeFunctions</c> and
    /// <c>org.apache.calcite.sql.fun.SqlSpatialTypeFunctions</c>, which is how its own spatial operator table
    /// registers them. Without those the query fails in Calcite, with Calcite's own message.
    /// </remarks>
    /// <param name="optionsBuilder"></param>
    /// <returns></returns>
    public static CalciteDbContextOptionsBuilder UseNetTopologySuite(this CalciteDbContextOptionsBuilder optionsBuilder)
    {
        var coreOptionsBuilder = ((IRelationalDbContextOptionsBuilderInfrastructure)optionsBuilder).OptionsBuilder;
        var extension = coreOptionsBuilder.Options.FindExtension<CalciteNetTopologySuiteOptionsExtension>() ?? new CalciteNetTopologySuiteOptionsExtension();

        ((IDbContextOptionsBuilderInfrastructure)coreOptionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }

}
