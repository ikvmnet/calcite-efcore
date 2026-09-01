namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

/// <summary>
/// The literals the benchmarks filter on, in one place so the SQL the adapter benchmarks send and the LINQ the
/// provider benchmarks compose select the same rows. Every one of them is chosen against the seeded distribution
/// in <see cref="BenchmarkSeed"/> so it matches a useful fraction of the table rather than none of it or all of it.
/// </summary>
public static class BenchmarkValues
{

    /// <summary>
    /// A product identifier that exists at every scale.
    /// </summary>
    public const int ProductId = 7;

    /// <summary>
    /// A category identifier that exists at every scale. Roughly an eighth of products carry it.
    /// </summary>
    public const int CategoryId = 3;

    /// <summary>
    /// A price every scale has products on both sides of. Roughly half the products are above it.
    /// </summary>
    public const decimal PriceThreshold = 50m;

    /// <summary>
    /// The low end of the price range predicates.
    /// </summary>
    public const decimal PriceRangeLow = 20m;

    /// <summary>
    /// The high end of the price range predicates.
    /// </summary>
    public const decimal PriceRangeHigh = 60m;

    /// <summary>
    /// A product name prefix. One product name in eight starts with it.
    /// </summary>
    public const string NamePrefix = "Alpha";

    /// <summary>
    /// A fragment that appears inside one product name in six, away from either end.
    /// </summary>
    public const string NameFragment = "Bracket";

    /// <summary>
    /// A product name suffix shared by a fixed fraction of rows.
    /// </summary>
    public const string NameSuffix = "0";

    /// <summary>
    /// A country every scale has customers in.
    /// </summary>
    public const string Country = "Germany";

    /// <summary>
    /// A customer segment every scale has customers in.
    /// </summary>
    public const string Segment = "Retail";

    /// <summary>
    /// A quantity the seeded order lines straddle.
    /// </summary>
    public const int Quantity = 10;

    /// <summary>
    /// The number of rows the paging benchmarks skip.
    /// </summary>
    public const int PageOffset = 20;

    /// <summary>
    /// The number of rows the paging benchmarks take.
    /// </summary>
    public const int PageSize = 25;

}
