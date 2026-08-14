using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public abstract partial class CommandInterceptionCalciteTestBase(CommandInterceptionCalciteTestBase.InterceptionCalciteFixtureBase fixture) : CommandInterceptionTestBase(fixture)
{

    public abstract class InterceptionCalciteFixtureBase : InterceptionFixtureBase
    {
        protected override string StoreName => "CommandInterception";

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        protected override IServiceCollection InjectInterceptors(IServiceCollection serviceCollection, IEnumerable<IInterceptor> injectedInterceptors) => base.InjectInterceptors(serviceCollection.AddEntityFrameworkCalcite(), injectedInterceptors);
    }

    public partial class CommandInterceptionCalciteTest(CommandInterceptionCalciteTest.InterceptionCalciteFixture fixture) :
        CommandInterceptionCalciteTestBase(fixture), IClassFixture<CommandInterceptionCalciteTest.InterceptionCalciteFixture>
    {
        public class InterceptionCalciteFixture : InterceptionCalciteFixtureBase
        {

            protected override bool ShouldSubscribeToDiagnosticListener => false;

        }

    }

    public partial class CommandInterceptionWithDiagnosticsCalciteTest(CommandInterceptionWithDiagnosticsCalciteTest.InterceptionCalciteFixture fixture) :
        CommandInterceptionCalciteTestBase(fixture), IClassFixture<CommandInterceptionWithDiagnosticsCalciteTest.InterceptionCalciteFixture>
    {
        public class InterceptionCalciteFixture : InterceptionCalciteFixtureBase
        {

            protected override bool ShouldSubscribeToDiagnosticListener => true;

        }

    }
}

