using Apache.Calcite.Sample.Docs;
using Apache.Calcite.Sample.Federation;
using Apache.Calcite.Sample.GraphQL;
using Apache.Calcite.Sample.Sources;
using Apache.Calcite.Sample.Sources.Catalog;
using Apache.Calcite.Sample.Sources.HumanResources;
using Apache.Calcite.Sample.Sources.Sales;

using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.OpenApi.Swashbuckle;

using Microsoft.EntityFrameworkCore;

// Calcite logs through slf4j, which has to be visible to the JVM before anything touches it.
ikvm.runtime.Startup.addBootClassPathAssembly(typeof(org.slf4j.simple.SimpleLoggerFactory).Assembly);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// The three SQLite sources. Each configures itself in OnConfiguring, so no options action is needed here; the
// scoped registrations exist because mutations and JSON:API resolve contexts from the request scope.
builder.Services.AddDbContextFactory<CatalogDbContext>();
builder.Services.AddDbContextFactory<SalesDbContext>();
builder.Services.AddDbContextFactory<HumanResourcesDbContext>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<CatalogDbContext>>().CreateDbContext());
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<SalesDbContext>>().CreateDbContext());
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<HumanResourcesDbContext>>().CreateDbContext());

// The federation. One Calcite connection per context, opened by the factory; both API surfaces share the model.
builder.Services.AddSingleton<QueryPlanRecorder>();
builder.Services.AddSingleton<FederationConnectionFactory>();
builder.Services.AddSingleton<IDbContextFactory<FederatedDbContext>, FederatedDbContextFactory>();
builder.Services.AddSingleton<FederationProbe>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<FederatedDbContext>>().CreateDbContext());

// JSON:API reflects over the federated model and generates read only controllers for all fourteen resources.
builder.Services.AddJsonApi<FederatedDbContext>(options =>
{
    options.Namespace = "api";
    options.UseRelativeLinks = true;
    options.IncludeTotalResourceCount = true;
    options.DefaultPageSize = new PageSize(25);
    options.MaximumPageSize = new PageSize(200);
    options.MaximumIncludeDepth = 3;
    options.IncludeExceptionStackTraceInErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddOpenApiForJsonApi();

// GraphQL reflects over the same model. AddSampleTypes is generated from the [Module] attribute in Properties.
builder.Services
    .AddGraphQLServer()
    .AddSampleTypes()
    .RegisterDbContextFactory<FederatedDbContext>()
    .RegisterDbContextFactory<CatalogDbContext>()
    .RegisterDbContextFactory<SalesDbContext>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddInMemorySubscriptions()
    .AddErrorFilter<FederationErrorFilter>()
    .ModifyPagingOptions(options =>
    {
        options.DefaultPageSize = 25;
        options.MaxPageSize = 200;
        options.IncludeTotalCount = true;
    })
    .ModifyRequestOptions(options =>
    {
        // Without this a provider failure reaches the client as "Unexpected Execution Error" and nothing else,
        // which is the opposite of what a sample built to surface provider failures wants.
        options.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    });

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Apache.Calcite.Sample");

// Generate the source data before anything reads through the federation.
await SampleDataSeeder.SeedAsync(logger);
app.Services.GetRequiredService<FederationConnectionFactory>().ValidateReferenceData();

app.UseWebSockets();

// The pages that describe what is underneath the two APIs: the four sources, and every view with its SQL.
app.MapGet("/", () => Results.Content(SampleDocumentation.Index(), "text/html"))
    .ExcludeFromDescription();

app.MapGet("/docs/federation", () => Results.Content(SampleDocumentation.Federation(), "text/html"))
    .ExcludeFromDescription();

app.MapGet("/docs/federation.json", () => Results.Json(SampleDocumentation.FederationJson()))
    .WithTags("Documentation")
    .WithSummary("The federated schema: every source, every view, and the SQL each view is defined by.");

// What the federation is made of, and what it most recently planned. Both are read only.
app.MapGet("/diagnostics/model", (FederationConnectionFactory federation) => Results.Text(federation.Model, "application/json"))
    .WithTags("Diagnostics")
    .WithSummary("The model document handed to Calcite when a connection opens.");

app.MapGet("/diagnostics/plans", (QueryPlanRecorder recorder, int count = 10) => Results.Json(recorder.Recent(count)))
    .WithTags("Diagnostics")
    .WithSummary("The SQL EF Core sent and the plans Calcite produced, newest first.");

// Runs SQL straight at the federated schema, below EF Core, with Calcite's suppressed causes unwrapped. This is
// how to tell a provider translation failure apart from a query the federation genuinely cannot answer.
app.MapPost("/diagnostics/sql", async (FederationProbe probe, SqlProbeRequest request, CancellationToken cancellationToken) =>
{
    var result = await probe.RunAsync(request.Sql, request.MaxRows, cancellationToken);
    return result.Error is null ? Results.Json(result) : Results.Json(result, statusCode: 400);
})
    .WithTags("Diagnostics")
    .WithSummary("Runs one statement against the federation, below EF Core, and unwraps the cause when it fails.");

app.MapPost("/diagnostics/plans/clear", (QueryPlanRecorder recorder) =>
{
    recorder.Clear();
    return Results.NoContent();
})
    .WithTags("Diagnostics")
    .WithSummary("Discards the captured plans.");

app.MapControllers();
app.UseJsonApi();

// The OpenAPI document JsonApiDotNetCore generates from the federated model, and the UI that reads it.
app.MapSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Federated Northwind (JSON:API)");
    options.DocumentTitle = "Apache Calcite EF Core sample";
    options.DisplayRequestDuration();
});

// The GraphQL endpoint serves its own IDE; the SDL is published beside it for tooling that wants the document.
app.MapGraphQL();
app.MapGraphQLSchema("/graphql/sdl");

app.Run();

/// <summary>
/// The statement to run against the federated schema.
/// </summary>
/// <param name="Sql">The statement to run.</param>
/// <param name="MaxRows">The greatest number of rows to read back.</param>
record SqlProbeRequest(string Sql, int MaxRows = 25);
