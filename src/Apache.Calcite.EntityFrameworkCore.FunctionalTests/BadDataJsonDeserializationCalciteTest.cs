using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class BadDataJsonDeserializationCalciteTest : BadDataJsonDeserializationTestBase
{

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => base.OnConfiguring(optionsBuilder.UseCalcite("schema=Test", b => b.UseNetTopologySuite()));

}
