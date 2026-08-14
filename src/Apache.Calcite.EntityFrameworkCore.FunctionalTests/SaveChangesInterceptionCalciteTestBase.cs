using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public abstract partial class SaveChangesInterceptionCalciteTestBase(SaveChangesInterceptionCalciteTestBase.InterceptionCalciteFixtureBase fixture) :
    SaveChangesInterceptionTestBase(fixture)
{

    public abstract class InterceptionCalciteFixtureBase : InterceptionFixtureBase
    {

        protected override string StoreName => "SaveChangesInterception";

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        protected override IServiceCollection InjectInterceptors(IServiceCollection serviceCollection, IEnumerable<IInterceptor> injectedInterceptors) => base.InjectInterceptors(serviceCollection.AddEntityFrameworkCalcite(), injectedInterceptors);

    }

    public partial class SaveChangesInterceptionCalciteTest(SaveChangesInterceptionCalciteTest.InterceptionCalciteFixture fixture) :
        SaveChangesInterceptionCalciteTestBase(fixture),
        IClassFixture<SaveChangesInterceptionCalciteTest.InterceptionCalciteFixture>
    {

        public class InterceptionCalciteFixture : InterceptionCalciteFixtureBase
        {

            protected override bool ShouldSubscribeToDiagnosticListener => false;

        }

    }

    public partial class SaveChangesInterceptionWithDiagnosticsCalciteTest(SaveChangesInterceptionWithDiagnosticsCalciteTest.InterceptionCalciteFixture fixture) :
        SaveChangesInterceptionCalciteTestBase(fixture),
        IClassFixture<SaveChangesInterceptionWithDiagnosticsCalciteTest.InterceptionCalciteFixture>
    {

        public class InterceptionCalciteFixture : InterceptionCalciteFixtureBase
        {
            protected override bool ShouldSubscribeToDiagnosticListener => true;
        }

    }

}
