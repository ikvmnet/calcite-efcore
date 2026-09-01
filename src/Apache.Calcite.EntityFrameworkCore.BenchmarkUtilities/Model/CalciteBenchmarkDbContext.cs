using System;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities.Model;

/// <summary>
/// The same model, mapped onto the Calcite schema the adapter publishes the SQLite store through. Queries on this
/// context leave EF Core as SQL, are planned by Calcite, and come back through the adapter as LINQ over the SQLite
/// context — the whole round trip the provider benchmarks time.
/// </summary>
/// <remarks>
/// The tables are schema-qualified rather than reached through a default schema: the adapter schema is registered
/// on the root schema after the connection opens, which is too late for a connection string to name it. EF Core
/// caches a model per context type, so every instance has to be given the same schema name — which is the case
/// here, where it comes from a constant.
/// </remarks>
public sealed class CalciteBenchmarkDbContext : BenchmarkDbContext
{

    readonly CalciteConnection _connection;
    readonly string _schema;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connection">The open connection carrying the adapter schema. The context does not own it.</param>
    /// <param name="schema">The name the adapter schema is registered under.</param>
    public CalciteBenchmarkDbContext(CalciteConnection connection, string schema)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        // Tracking is left at the EF Core default on both backends; the benchmarks that care about it say so.
        optionsBuilder.UseCalcite(_connection, contextOwnsConnection: false);
    }

    /// <inheritdoc />
    protected override void MapTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().ToTable(nameof(Category), _schema);
        modelBuilder.Entity<Product>().ToTable(nameof(Product), _schema);
        modelBuilder.Entity<Customer>().ToTable(nameof(Customer), _schema);
        modelBuilder.Entity<SalesOrder>().ToTable(nameof(SalesOrder), _schema);
        modelBuilder.Entity<OrderLine>().ToTable(nameof(OrderLine), _schema);
    }

}
