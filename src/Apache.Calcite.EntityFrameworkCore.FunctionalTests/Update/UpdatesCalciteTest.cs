using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Update;

public class UpdatesCalciteTest(UpdatesCalciteTest.UpdatesCalciteFixture fixture) :
    UpdatesRelationalTestBase<UpdatesCalciteTest.UpdatesCalciteFixture>(fixture)
{

    /// <inheritdoc />
    /// <remarks>
    /// Disabled: the base test uses identifiers longer than Calcite's 128-character parser cap.
    /// </remarks>
    public override void Identifiers_are_generated_correctly()
    {
        // intentionally empty
    }

    public class UpdatesCalciteFixture : UpdatesRelationalFixture
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}

