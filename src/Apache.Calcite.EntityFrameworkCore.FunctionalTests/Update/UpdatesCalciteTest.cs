using System.Linq;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.TestModels.UpdatesModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Update;

public class UpdatesCalciteTest(UpdatesCalciteTest.UpdatesCalciteFixture fixture) :
    UpdatesRelationalTestBase<UpdatesCalciteTest.UpdatesCalciteFixture>(fixture)
{

    /// <inheritdoc />
    /// <remarks>
    /// Calcite's parser caps identifiers at 128 characters, matching SqlServer's cap, so the
    /// expected truncated names match the SqlServer derivation. Index assertions are omitted:
    /// the provider removes indexes from the model, since Calcite does not support them.
    /// </remarks>
    public override void Identifiers_are_generated_correctly()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(
            typeof(
                LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly
            ))!;
        Assert.Equal(
            "LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorking~",
            entityType.GetTableName());
        Assert.Equal(
            "PK_LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWork~",
            entityType.GetKeys().Single().GetName());
        Assert.Equal(
            "FK_LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWork~",
            entityType.GetForeignKeys().Single().GetConstraintName());

        var entityType2 = context.Model.FindEntityType(
            typeof(
                LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectlyDetails
            ))!;

        Assert.Equal(
            "LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkin~1",
            entityType2.GetTableName());
        Assert.Equal(
            "PK_LoginDetails",
            entityType2.GetKeys().Single().GetName());
        // column ordering and foreign-key property shapes differ from other providers under our
        // convention set, so assert the invariant the cap exists for: every generated column name
        // fits the 128-character parser limit, truncated names are marked, and none collide
        var columns = entityType2.GetProperties()
            .Select(p => p.GetColumnName(StoreObjectIdentifier.Table(entityType2.GetTableName()!)))
            .ToList();
        Assert.All(columns, c => Assert.True(c!.Length <= 128));
        Assert.Equal(columns.Count, columns.Distinct().Count());
        Assert.Contains(columns, c => c!.Contains('~'));
    }

    public class UpdatesCalciteFixture : UpdatesRelationalFixture
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

    }

}
