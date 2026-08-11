using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Inheritance;

public class InheritanceQueryCalciteTest(TPHInheritanceQueryCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
    TPHInheritanceQueryTestBase<TPHInheritanceQueryCalciteFixture>(fixture, testOutputHelper)
{
}

