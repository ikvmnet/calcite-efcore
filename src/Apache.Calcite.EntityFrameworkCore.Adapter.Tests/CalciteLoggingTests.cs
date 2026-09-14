using IKVM.Extensions.Logging.Slf4j;

using Microsoft.Extensions.Logging;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests;

/// <summary>
/// Tests that Calcite's logging is actually wired up in this assembly.
/// </summary>
/// <remarks>
/// Binding slf4j fails quietly: with nothing bound it hands back a <c>NOPLogger</c>, every Calcite log
/// statement becomes a no-op, and the diagnostics Calcite guards behind a level never run. Nothing else in
/// the suite would notice, which is exactly why these assert it directly.
/// </remarks>
public class CalciteLoggingTests
{

    [Fact]
    public void Should_bind_slf4j_to_the_bridge()
    {
        Assert.True(Slf4jBridge.IsBound);
    }

    [Fact]
    public void Should_give_calcite_a_logger_that_is_not_a_no_op()
    {
        var planner = org.apache.calcite.util.trace.CalciteTrace.getPlannerTracer();

        Assert.IsNotType<org.slf4j.helpers.NOPLogger>(planner);
    }

    [Fact]
    public void Should_forward_to_an_installed_factory()
    {
        Assert.NotNull(Slf4jBridge.Factory);
    }

    [Fact]
    public void Should_report_calcite_the_level_the_factory_was_built_with()
    {
        // The lever the graph-consistency diagnostics hang off: Calcite asks, and the bridge answers from the
        // installed factory. Asserted against the level actually configured rather than the default, so that
        // raising CALCITE_TEST_LOG_LEVEL to reproduce a CI planner failure does not fail this test.
        var configured = CalciteLoggingInitializer.ConfiguredLevel;
        var planner = org.apache.calcite.util.trace.CalciteTrace.getPlannerTracer();

        Assert.Equal(configured <= LogLevel.Trace, planner.isTraceEnabled());
        Assert.Equal(configured <= LogLevel.Warning, planner.isWarnEnabled());
        Assert.Equal(configured <= LogLevel.Error, planner.isErrorEnabled());
    }

}
