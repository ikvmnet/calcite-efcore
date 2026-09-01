using System;
using System.Collections;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// An adapter benchmark that states its query twice — once as the SQL Calcite plans, once as the LINQ the adapter
/// would end up running — and is measured on both. Reading a row of results is then reading the adapter's overhead.
/// </summary>
/// <remarks>
/// The direct route opens a fresh context per invocation and leaves change tracking at its default, because that is
/// what the adapter does: its schema is built over a context factory, and every scan it answers gets a new context.
/// </remarks>
public abstract class ComparedAdapterBenchmark : AdapterBenchmark
{

    /// <summary>
    /// Gets or sets the route this run measures.
    /// </summary>
    [Params(AdapterRoute.Calcite, AdapterRoute.Direct)]
    public AdapterRoute Route { get; set; }

    /// <summary>
    /// Runs a row-returning query by the configured route.
    /// </summary>
    /// <param name="sql">The statement for the Calcite route.</param>
    /// <param name="query">The equivalent query for the direct route.</param>
    /// <returns>The number of rows the query returned.</returns>
    protected int Run(string sql, Func<SqliteBenchmarkDbContext, IQueryable> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (Route == AdapterRoute.Calcite)
            return Query(sql);

        using var context = Store.CreateSourceContext();

        var rows = 0;
        foreach (var row in (IEnumerable)query(context))
        {
            _ = row;
            rows++;
        }

        return rows;
    }

    /// <summary>
    /// Runs a single-value query by the configured route.
    /// </summary>
    /// <param name="sql">The statement for the Calcite route.</param>
    /// <param name="value">The equivalent query for the direct route.</param>
    /// <returns>The value the query returned.</returns>
    protected object? RunScalar(string sql, Func<SqliteBenchmarkDbContext, object?> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (Route == AdapterRoute.Calcite)
            return Scalar(sql);

        using var context = Store.CreateSourceContext();
        return value(context);
    }

}
