using System;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Diagnostics.Internal;
using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Infrastructure;
using Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class LoggingCalciteTest : LoggingRelationalTestBase<CalciteDbContextOptionsBuilder, CalciteOptionsExtension>
{

    /// <inheritdoc />
    protected override DbContextOptionsBuilder CreateOptionsBuilder(
        IServiceCollection services,
        Action<RelationalDbContextOptionsBuilder<CalciteDbContextOptionsBuilder, CalciteOptionsExtension>> relationalAction)
        => new DbContextOptionsBuilder()
            .UseInternalServiceProvider(services.AddEntityFrameworkCalcite().BuildServiceProvider(validateScopes: true))
            .UseCalcite("schema=Test", relationalAction);

    /// <inheritdoc />
    protected override TestLogger CreateTestLogger() => new TestLogger<CalciteLoggingDefinitions>();

    /// <inheritdoc />
    protected override string ProviderName => "Apache.Calcite.EntityFrameworkCore";

    /// <inheritdoc />
    protected override string ProviderVersion
        => typeof(CalciteOptionsExtension).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

}
