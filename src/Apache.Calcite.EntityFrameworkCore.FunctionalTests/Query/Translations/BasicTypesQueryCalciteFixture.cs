using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations;

public class BasicTypesQueryCalciteFixture : BasicTypesQueryFixtureBase, ITestSqlLoggerFactory
{

    private CalciteBasicTypesData? _expectedData;

    protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

    protected override Task SeedAsync(BasicTypesContext context)
    {
        var data = new CalciteBasicTypesData();
        context.AddRange(data.BasicTypesEntities);
        context.AddRange(data.NullableBasicTypesEntities);
        return context.SaveChangesAsync();
    }

    public override ISetSource GetExpectedData()
        => _expectedData ??= new CalciteBasicTypesData();  // CalciteBasicTypesData implements ISetSource

}

