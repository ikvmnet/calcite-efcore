using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public abstract partial class TransactionInterceptionCalciteTestBase(TransactionInterceptionCalciteTestBase.InterceptionCalciteFixtureBase fixture) :
    TransactionInterceptionTestBase(fixture)
{

    public abstract class InterceptionCalciteFixtureBase : InterceptionFixtureBase
    {

        protected override string StoreName => "TransactionInterception";

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        protected override IServiceCollection InjectInterceptors(
            IServiceCollection serviceCollection,
            IEnumerable<IInterceptor> injectedInterceptors)
            => base.InjectInterceptors(serviceCollection.AddEntityFrameworkCalcite(), injectedInterceptors);

    }

    public partial class TransactionInterceptionCalciteTest(TransactionInterceptionCalciteTest.InterceptionCalciteFixture fixture) :
        TransactionInterceptionCalciteTestBase(fixture),
        IClassFixture<TransactionInterceptionCalciteTest.InterceptionCalciteFixture>
    {

        public class InterceptionCalciteFixture : InterceptionCalciteFixtureBase
        {

            protected override bool ShouldSubscribeToDiagnosticListener => false;

        }

    }

    public partial class TransactionInterceptionWithDiagnosticsCalciteTest(TransactionInterceptionWithDiagnosticsCalciteTest.InterceptionCalciteFixture fixture) :
        TransactionInterceptionCalciteTestBase(fixture),
        IClassFixture<TransactionInterceptionWithDiagnosticsCalciteTest.InterceptionCalciteFixture>
    {

        public class InterceptionCalciteFixture : InterceptionCalciteFixtureBase
        {

            protected override bool ShouldSubscribeToDiagnosticListener => true;

        }

    }

}

