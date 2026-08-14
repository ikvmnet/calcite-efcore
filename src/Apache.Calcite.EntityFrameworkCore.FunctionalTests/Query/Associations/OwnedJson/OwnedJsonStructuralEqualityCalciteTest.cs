using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Query.Associations.OwnedJson;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Associations.OwnedJson;

public partial class OwnedJsonStructuralEqualityCalciteTest(OwnedJsonCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    OwnedJsonStructuralEqualityRelationalTestBase<OwnedJsonCalciteFixture>(fixture, testOutputHelper)
{

}
