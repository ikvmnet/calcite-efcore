using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class OptimisticConcurrencyULongCalciteTest(F1ULongCalciteFixture fixture) :
    OptimisticConcurrencyCalciteTestBase<F1ULongCalciteFixture, ulong?>(fixture)
{

}

public partial class OptimisticConcurrencyCalciteTest(F1CalciteFixture fixture) :
    OptimisticConcurrencyCalciteTestBase<F1CalciteFixture, byte[]>(fixture)
{

}

public abstract class OptimisticConcurrencyCalciteTestBase<TFixture, TRowVersion>(TFixture fixture) :
    OptimisticConcurrencyRelationalTestBase<TFixture, TRowVersion>(fixture)
    where TFixture : F1RelationalFixture<TRowVersion>, new()
{

}
