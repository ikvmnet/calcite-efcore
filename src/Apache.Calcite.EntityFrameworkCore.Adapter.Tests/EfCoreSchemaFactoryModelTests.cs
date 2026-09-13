using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

using Apache.Calcite.Data;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests;

/// <summary>
/// xUnit class fixture that seeds a database of its own and opens a <see cref="CalciteConnection"/> whose schema
/// comes from a Calcite model JSON naming <see cref="EfCoreSchemaFactory"/> — the path the adapter's
/// documentation publishes, and the one nothing else here covers.
/// </summary>
public sealed class SchemaFactoryModelFixture : IDisposable
{

    /// <summary>
    /// Name the model JSON registers the EF Core schema under.
    /// </summary>
    public const string SchemaName = "efcore";

    /// <summary>
    /// The <c>factory</c> value under test: the factory's assembly-qualified type name and nothing else. No
    /// <c>#Instance</c> suffix — resolving this form is what issue #32 was about.
    /// </summary>
    public const string FactoryName =
        "Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreSchemaFactory, Apache.Calcite.EntityFrameworkCore.Adapter";

    // Keeps the shared-cache in-memory database alive for the lifetime of the fixture.
    readonly SqliteConnection _keepAlive;
    readonly string _modelPath;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public SchemaFactoryModelFixture()
    {
        // Bootstrap IKVM so the Java types used by Calcite and the adapter are visible on the boot class-path.
        RuntimeHelpers.RunClassConstructor(typeof(EfCoreSchema).TypeHandle);
        ikvm.runtime.Startup.addBootClassPathAssembly(typeof(SqliteConnection).Assembly);

        // Calcite 1.43 filters every class a model names through ClassNameFilter, and its allowlist is empty by
        // default — deny-by-default, so the adapter's own factory has to be named before a model can load it.
        java.lang.System.setProperty("calcite.model.classes.allowed", "Apache.Calcite.EntityFrameworkCore.");

        _keepAlive = new SqliteConnection(ModelJsonProductDbContext.ConnectionString);
        _keepAlive.Open();

        using (var ctx = new ModelJsonProductDbContext())
        {
            ctx.Database.EnsureCreated();
            ctx.Database.ExecuteSqlRaw("DELETE FROM Products");

            ctx.Products.Add(new Product { Id = 1, Name = "Widget", Price = 9.99m, InStock = true });
            ctx.Products.Add(new Product { Id = 2, Name = "Gadget", Price = 24.95m, InStock = false });
            ctx.Products.Add(new Product { Id = 3, Name = "Doohickey", Price = 4.50m, InStock = true });
            ctx.SaveChanges();
        }

        _modelPath = WriteModel();

        Connection = new CalciteConnection($"model={_modelPath};caseSensitive=false");
        Connection.Open();
    }

    /// <summary>
    /// Writes the model JSON to a temporary file and returns its path.
    /// </summary>
    /// <returns>The path the model was written to.</returns>
    static string WriteModel()
    {
        var dir = Path.Combine(Path.GetTempPath(), "calcite-efcore-model-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, "model.json").Replace(Path.DirectorySeparatorChar, '/');
        File.WriteAllText(path, $$"""
            {
              "version": "1.0",
              "defaultSchema": "{{SchemaName}}",
              "schemas": [
                {
                  "name": "{{SchemaName}}",
                  "type": "custom",
                  "factory": "{{FactoryName}}",
                  "operand": {
                    "dbContextType": "Apache.Calcite.EntityFrameworkCore.Adapter.Tests.ModelJsonProductDbContext, Apache.Calcite.EntityFrameworkCore.Adapter.Tests"
                  }
                }
              ]
            }
            """);

        return path;
    }

    /// <summary>
    /// Gets the open connection whose schema was built from the model JSON.
    /// </summary>
    public CalciteConnection Connection { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Connection.Dispose();
        _keepAlive.Dispose();

        try
        {
            Directory.Delete(Path.GetDirectoryName(_modelPath)!, true);
        }
        catch (IOException)
        {
            // a leftover temp directory is not worth failing a test run over
        }
    }

}

/// <summary>
/// Tests that <see cref="EfCoreSchemaFactory"/> can be named from a Calcite model JSON by its class name alone.
/// </summary>
/// <remarks>
/// Regression cover for issue #32. Calcite resolves a <c>factory</c> through
/// <c>AvaticaUtils.instantiatePlugin</c>, which reaches a singleton only through a static field named exactly
/// <c>INSTANCE</c> and otherwise calls a public parameterless constructor. This type's field is
/// <c>Instance</c>, so a private constructor left the documented form resolvable by no path at all.
/// </remarks>
public class EfCoreSchemaFactoryModelTests : IClassFixture<SchemaFactoryModelFixture>
{

    readonly SchemaFactoryModelFixture _fixture;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="fixture">The shared fixture.</param>
    public EfCoreSchemaFactoryModelTests(SchemaFactoryModelFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Resolves a plugin name the way Calcite's model loader does.
    /// </summary>
    /// <param name="name">The <c>factory</c> value to resolve.</param>
    /// <returns>The resolved factory.</returns>
    static object InstantiatePlugin(string name)
    {
        var pluginClass = java.lang.Class.forName("org.apache.calcite.schema.SchemaFactory");
        return org.apache.calcite.avatica.AvaticaUtils.instantiatePlugin(pluginClass, name);
    }

    [Fact]
    public void Should_resolve_factory_by_class_name_alone()
    {
        Assert.IsType<EfCoreSchemaFactory>(InstantiatePlugin(SchemaFactoryModelFixture.FactoryName));
    }

    [Fact]
    public void Should_resolve_factory_by_explicit_instance_field()
    {
        // the form documented as a workaround while issue #32 was open; it must keep working
        var factory = InstantiatePlugin(SchemaFactoryModelFixture.FactoryName + "#Instance");
        Assert.Same(EfCoreSchemaFactory.Instance, factory);
    }

    [Fact]
    public void Should_query_schema_loaded_from_model_json()
    {
        using var cmd = _fixture.Connection.CreateCommand();
        cmd.CommandText = $@"SELECT ""Id"", ""Name"" FROM ""{SchemaFactoryModelFixture.SchemaName}"".""Product"" ORDER BY ""Id""";

        var names = new List<string>();
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
                names.Add(reader.GetString(1));

        Assert.Equal(new[] { "Widget", "Gadget", "Doohickey" }, names);
    }

    [Fact]
    public void Should_push_filter_into_schema_loaded_from_model_json()
    {
        using var cmd = _fixture.Connection.CreateCommand();
        cmd.CommandText = $@"SELECT ""Name"" FROM ""{SchemaFactoryModelFixture.SchemaName}"".""Product"" WHERE ""InStock"" = TRUE ORDER BY ""Id""";

        var names = new List<string>();
        using (var reader = cmd.ExecuteReader())
            while (reader.Read())
                names.Add(reader.GetString(0));

        Assert.Equal(new[] { "Widget", "Doohickey" }, names);
    }

}
