using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ModelBuilding;
using Microsoft.EntityFrameworkCore.TestModels.ManyToManyModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class ManyToManyTrackingCalciteTest(ManyToManyTrackingCalciteTest.ManyToManyTrackingCalciteFixture fixture) :
    ManyToManyTrackingRelationalTestBase<ManyToManyTrackingCalciteTest.ManyToManyTrackingCalciteFixture>(fixture)
{

    public class ManyToManyTrackingCalciteFixture : ManyToManyTrackingRelationalFixture, ITestSqlLoggerFactory
    {

        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
