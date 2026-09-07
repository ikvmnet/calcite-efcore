namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

/// <summary>
/// The sizes the seeded store comes in. Feature benchmarks run on <see cref="Small"/>, where translation and
/// planning dominate and the differences between query shapes are visible; the scale sweeps grow the store to
/// separate per-query cost from per-row cost.
/// </summary>
public enum BenchmarkScale
{

    /// <summary>
    /// 1,000 order lines. Small enough that a full scan is not what is being measured.
    /// </summary>
    Small,

    /// <summary>
    /// 10,000 order lines.
    /// </summary>
    Medium,

    /// <summary>
    /// 50,000 order lines. Materialization dominates at this size.
    /// </summary>
    Large,

}
