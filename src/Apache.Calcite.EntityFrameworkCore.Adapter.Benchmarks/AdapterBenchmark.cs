using System;
using System.Data.Common;
using System.IO;
using System.Text;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

using BenchmarkDotNet.Attributes;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Benchmarks;

/// <summary>
/// The base every adapter benchmark builds on: a seeded store, and a Calcite connection with that store registered
/// on its root schema by <see cref="EfCoreSchema"/>.
/// </summary>
/// <remarks>
/// What is being timed here is the path <em>into</em> EF Core: SQL arrives at Calcite, the planner converts the rel
/// tree through the <c>EfCoreConvention</c>, and the result is a LINQ expression run against a
/// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>. The provider suite times the other direction.
/// </remarks>
[Config(typeof(DefaultBenchmarkConfig))]
public abstract class AdapterBenchmark
{

    /// <summary>
    /// When set, benchmarks report the plan Calcite chose for their statement instead of executing it. The
    /// <c>--plans</c> switch uses this to say which shapes reach the EF Core convention and which fall back.
    /// </summary>
    public static TextWriter? PlanWriter { get; set; }

    /// <summary>
    /// Gets the scale of store to run against. Set before the store is opened, so a sweep can vary it.
    /// </summary>
    public BenchmarkScale Scale { get; set; } = BenchmarkScale.Small;

    /// <summary>
    /// Gets the seeded store.
    /// </summary>
    protected BenchmarkStore Store { get; private set; } = null!;

    /// <summary>
    /// Gets the connection statements are sent on.
    /// </summary>
    protected CalciteConnection Connection { get; private set; } = null!;

    /// <summary>
    /// Opens the store and the connection.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        Configure();

        Store = BenchmarkStore.Open(Scale);
        Connection = Store.OpenCalciteConnection();

        OnSetup();
    }

    /// <summary>
    /// Closes the connection.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        OnCleanup();
        Connection?.Dispose();
    }

    /// <summary>
    /// Runs before the store is opened, for a derived class that decides which store to open.
    /// </summary>
    protected virtual void Configure()
    {

    }

    /// <summary>
    /// Runs once the store and connection are up.
    /// </summary>
    protected virtual void OnSetup()
    {

    }

    /// <summary>
    /// Runs before the connection is closed.
    /// </summary>
    protected virtual void OnCleanup()
    {

    }

    /// <summary>
    /// Executes a statement on the Calcite connection and materializes every value of every row, so the
    /// measurement covers the whole path back out through the adapter and not just the planning in front of it.
    /// </summary>
    /// <param name="sql">The statement to run.</param>
    /// <returns>The number of rows read.</returns>
    protected int Query(string sql)
    {
        if (PlanWriter is not null)
            return ReportPlan(sql);

        using var command = Connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        return Drain(reader);
    }

    /// <summary>
    /// Executes a statement that returns a single value.
    /// </summary>
    /// <param name="sql">The statement to run.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    protected object? Scalar(string sql)
    {
        if (PlanWriter is not null)
        {
            ReportPlan(sql);
            return null;
        }

        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    /// <summary>
    /// Reads every value of every row of a reader.
    /// </summary>
    /// <param name="reader">The reader to drain.</param>
    /// <returns>The number of rows read.</returns>
    protected static int Drain(DbDataReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var rows = 0;

        while (reader.Read())
        {
            for (var i = 0; i < reader.FieldCount; i++)
                if (reader.IsDBNull(i) == false)
                    _ = reader.GetValue(i);

            rows++;
        }

        return rows;
    }

    /// <summary>
    /// Writes the plan Calcite chose for a statement to <see cref="PlanWriter"/>, flagging the ones that left the
    /// EF Core convention for the bindable fallback.
    /// </summary>
    /// <param name="sql">The statement to explain.</param>
    /// <returns>Zero, so a benchmark can return it as a row count.</returns>
    int ReportPlan(string sql)
    {
        var writer = PlanWriter!;
        var plan = new StringBuilder();

        try
        {
            using var command = Connection.CreateCommand();
            command.CommandText = "EXPLAIN PLAN FOR " + sql;

            using var reader = command.ExecuteReader();
            while (reader.Read())
                plan.AppendLine(reader.GetString(0));
        }
        catch (Exception e)
        {
            writer.WriteLine("    plan unavailable: " + CalciteDiagnostics.Describe(e));
            return 0;
        }

        var text = plan.ToString();
        var pushedDown = text.Contains("EfCore", StringComparison.OrdinalIgnoreCase);
        var fellBack = text.Contains("Bindable", StringComparison.OrdinalIgnoreCase) || text.Contains("EnumerableCalc", StringComparison.OrdinalIgnoreCase);

        writer.WriteLine($"    {(pushedDown ? "efcore" : "------")} {(fellBack ? "+fallback" : "         ")}");

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            writer.WriteLine("      " + line.TrimEnd());

        return 0;
    }

}
