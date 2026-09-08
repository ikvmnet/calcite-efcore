using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Diagnostics.Internal;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

public class CalciteTestHelpers : RelationalTestHelpers
{
    protected CalciteTestHelpers()
    {
    }

    public static CalciteTestHelpers Instance { get; } = new();

    public override IServiceCollection AddProviderServices(IServiceCollection services)
        => services.AddEntityFrameworkCalcite();

    public override DbContextOptionsBuilder UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseCalcite(new CalciteConnection("schema=Test;conformance=LENIENT;pooling=false;parserFactory=org.apache.calcite.server.ServerDdlExecutor#PARSER_FACTORY"));

    public override LoggingDefinitions LoggingDefinitions { get; } = new CalciteLoggingDefinitions();
}
