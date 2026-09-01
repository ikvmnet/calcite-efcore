using System;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

/// <summary>
/// The row counts a <see cref="BenchmarkScale"/> seeds.
/// </summary>
/// <param name="Categories">The number of categories.</param>
/// <param name="Products">The number of products.</param>
/// <param name="Customers">The number of customers.</param>
/// <param name="Orders">The number of order headers.</param>
/// <param name="OrderLines">The number of order lines.</param>
public readonly record struct BenchmarkRowCounts(int Categories, int Products, int Customers, int Orders, int OrderLines)
{

    /// <summary>
    /// Gets the row counts for the given scale.
    /// </summary>
    /// <param name="scale">The scale to size.</param>
    /// <returns>The row counts to seed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not one of the defined values.</exception>
    public static BenchmarkRowCounts For(BenchmarkScale scale) => scale switch
    {
        BenchmarkScale.Small => new BenchmarkRowCounts(8, 40, 50, 200, 1_000),
        BenchmarkScale.Medium => new BenchmarkRowCounts(8, 200, 500, 2_000, 10_000),
        BenchmarkScale.Large => new BenchmarkRowCounts(8, 1_000, 2_500, 10_000, 50_000),
        _ => throw new ArgumentOutOfRangeException(nameof(scale)),
    };

}
