using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public abstract partial class ConnectionInterceptionCalciteTestBase(ConnectionInterceptionCalciteTestBase.InterceptionCalciteFixtureBase fixture) : ConnectionInterceptionTestBase(fixture)
{

    protected override DbContextOptionsBuilder ConfigureProvider(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseCalcite();

    protected override BadUniverseContext CreateBadUniverse(DbContextOptionsBuilder optionsBuilder) => new(optionsBuilder.UseCalcite("Data Source=file:data.db?mode=invalidmode").Options);

    public abstract class InterceptionCalciteFixtureBase : InterceptionFixtureBase
    {

        protected override string StoreName => "ConnectionInterception";

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        protected override IServiceCollection InjectInterceptors(IServiceCollection serviceCollection, IEnumerable<IInterceptor> injectedInterceptors) => base.InjectInterceptors(serviceCollection.AddEntityFrameworkCalcite(), injectedInterceptors);

    }

    public partial class ConnectionInterceptionCalciteTest(ConnectionInterceptionCalciteTest.InterceptionCalciteFixture fixture) :
        ConnectionInterceptionCalciteTestBase(fixture),
        IClassFixture<ConnectionInterceptionCalciteTest.InterceptionCalciteFixture>
    {

        public class InterceptionCalciteFixture : InterceptionCalciteFixtureBase
        {

            protected override bool ShouldSubscribeToDiagnosticListener => false;

        }

    }

    public partial class ConnectionInterceptionWithDiagnosticsCalciteTest(ConnectionInterceptionWithDiagnosticsCalciteTest.InterceptionCalciteFixture fixture) :
        ConnectionInterceptionCalciteTestBase(fixture),
        IClassFixture<ConnectionInterceptionWithDiagnosticsCalciteTest.InterceptionCalciteFixture>
    {

        public class InterceptionCalciteFixture : InterceptionCalciteFixtureBase
        {

            protected override bool ShouldSubscribeToDiagnosticListener => true;

        }

    }

}

