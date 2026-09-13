namespace Apache.Calcite.EntityFrameworkCore.Adapter.Tests;

/// <summary>
/// <see cref="ProductDbContext"/> bound to a database of its own, so the model-JSON tests neither seed nor
/// truncate the store the other adapter fixtures share. Named from a model JSON by <c>dbContextType</c>, so it
/// needs the public parameterless constructor Calcite constructs it through.
/// </summary>
public class ModelJsonProductDbContext : ProductDbContext
{

    /// <summary>
    /// Connection string for the shared in-memory database this context binds to.
    /// </summary>
    public const string ConnectionString = "Data Source=schemafactory_model_tests;Mode=Memory;Cache=Shared";

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public ModelJsonProductDbContext() : base(ConnectionString) { }

}
