using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore.ModelBuilding;
using Microsoft.EntityFrameworkCore.TestUtilities;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.ModelBuilding;

public class CalciteModelBuilderTestBase : RelationalModelBuilderTest
{

    public abstract class CalciteNonRelationship(CalciteModelBuilderFixture fixture)
        : RelationalNonRelationshipTestBase(fixture), IClassFixture<CalciteModelBuilderFixture>;

    public abstract class CalciteComplexType(CalciteModelBuilderFixture fixture)
        : RelationalComplexTypeTestBase(fixture), IClassFixture<CalciteModelBuilderFixture>;

    public abstract class CalciteComplexCollection(CalciteModelBuilderFixture fixture)
        : RelationalComplexCollectionTestBase(fixture), IClassFixture<CalciteModelBuilderFixture>;

    public abstract class CalciteInheritance(CalciteModelBuilderFixture fixture)
        : RelationalInheritanceTestBase(fixture), IClassFixture<CalciteModelBuilderFixture>;

    public abstract class CalciteOneToMany(CalciteModelBuilderFixture fixture)
        : RelationalOneToManyTestBase(fixture), IClassFixture<CalciteModelBuilderFixture>;

    public abstract class CalciteManyToOne(CalciteModelBuilderFixture fixture)
        : RelationalManyToOneTestBase(fixture), IClassFixture<CalciteModelBuilderFixture>;

    public abstract class CalciteOneToOne(CalciteModelBuilderFixture fixture)
        : RelationalOneToOneTestBase(fixture), IClassFixture<CalciteModelBuilderFixture>;

    public abstract class CalciteManyToMany(CalciteModelBuilderFixture fixture)
        : RelationalManyToManyTestBase(fixture), IClassFixture<CalciteModelBuilderFixture>;

    public abstract class CalciteOwnedTypes(CalciteModelBuilderFixture fixture)
        : RelationalOwnedTypesTestBase(fixture), IClassFixture<CalciteModelBuilderFixture>;

    public class CalciteModelBuilderFixture : RelationalModelBuilderFixture
    {

        /// <inheritdoc />
        public override TestHelpers TestHelpers
            => CalciteTestHelpers.Instance;

    }

}
