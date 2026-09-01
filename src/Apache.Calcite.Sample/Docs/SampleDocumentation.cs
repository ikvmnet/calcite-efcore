using System.Net;
using System.Text;

using Apache.Calcite.Sample.Federation;

namespace Apache.Calcite.Sample.Docs;

/// <summary>
/// Renders the pages that describe the sample to whoever opened it.
/// </summary>
/// <remarks>
/// A federation is largely invisible from the outside: both APIs advertise one flat model and say nothing about
/// the four stores underneath. These pages say what is underneath, view by view, so the SQL a request turns into
/// can be read against the SQL a view is defined by.
/// </remarks>
public static class SampleDocumentation
{

    /// <summary>
    /// One documented source store.
    /// </summary>
    /// <param name="Schema">The name the source is registered under in Calcite.</param>
    /// <param name="Kind">How the source is reached.</param>
    /// <param name="Tables">The tables the source exposes.</param>
    public sealed record Source(string Schema, string Kind, IReadOnlyList<string> Tables);

    /// <summary>
    /// Gets the source stores the federation is built from.
    /// </summary>
    public static IReadOnlyList<Source> Sources { get; } =
    [
        new("catalog", "SQLite through EF Core", ["Category", "Supplier", "Product"]),
        new("sales", "SQLite through EF Core", ["Customer", "Order", "OrderDetail"]),
        new("hr", "SQLite through EF Core", ["Employee"]),
        new("ref", "CSV through Calcite's file adapter", ["Region", "Territory", "Shipper", "EmployeeTerritory"]),
    ];

    /// <summary>
    /// One documented endpoint.
    /// </summary>
    /// <param name="Path">The path the endpoint is served at.</param>
    /// <param name="Title">The name of the endpoint.</param>
    /// <param name="Description">What the endpoint is for.</param>
    public sealed record Endpoint(string Path, string Title, string Description);

    /// <summary>
    /// Gets the endpoints the sample serves.
    /// </summary>
    public static IReadOnlyList<Endpoint> Endpoints { get; } =
    [
        new("/swagger", "Swagger UI", "The JSON:API surface: one read only resource per federated view, with its filters, sort keys and includes."),
        new("/graphql", "GraphQL IDE", "The GraphQL surface, with the schema browser and a query editor."),
        new("/graphql/sdl", "GraphQL schema", "The schema as SDL, for tooling that wants the document rather than introspection."),
        new("/docs/federation", "Federation reference", "Every source, every view, and the SQL each view is defined by."),
        new("/docs/federation.json", "Federation reference (JSON)", "The same, for machines."),
        new("/diagnostics/model", "Calcite model", "The model document handed to Calcite when a connection opens."),
        new("/diagnostics/plans", "Recent plans", "The SQL EF Core sent and the plans Calcite produced, newest first."),
        new("/diagnostics/sql", "SQL probe", "POST a statement to run it against the federation below EF Core, with Calcite's suppressed causes unwrapped."),
    ];

    /// <summary>
    /// Renders the index page.
    /// </summary>
    /// <returns>The rendered HTML.</returns>
    public static string Index()
    {
        var builder = new StringBuilder();

        builder.Append(Head("Apache Calcite EF Core sample"));
        builder.Append("<h1>Apache Calcite EF Core sample</h1>");
        builder.Append("""
            <p class="lede">A Northwind shaped federation over four sources, published twice over one EF Core model.
            Neither API layer was written for this provider: both compose <code>IQueryable</code> from the shape of the
            request, and whatever they compose is what the Calcite provider has to answer.</p>
            """);

        builder.Append("<h2>Sources</h2><table><tr><th>Schema</th><th>Kind</th><th>Tables</th></tr>");
        foreach (var source in Sources)
            builder.Append($"<tr><td><code>{Encode(source.Schema)}</code></td><td>{Encode(source.Kind)}</td><td>{Encode(string.Join(", ", source.Tables))}</td></tr>");
        builder.Append("</table>");

        builder.Append("<h2>Endpoints</h2><table><tr><th>Path</th><th>What it serves</th></tr>");
        foreach (var endpoint in Endpoints)
            builder.Append($"""<tr><td><a href="{Encode(endpoint.Path)}">{Encode(endpoint.Path)}</a><br><small>{Encode(endpoint.Title)}</small></td><td>{Encode(endpoint.Description)}</td></tr>""");
        builder.Append("</table>");

        builder.Append($"""
            <h2>Where to start</h2>
            <p>Open <a href="/swagger">Swagger UI</a> and read a resource, then open <a href="/graphql">the GraphQL IDE</a>
            and ask for the same thing nested, then look at <a href="/diagnostics/plans">the plans</a> to see what the two
            requests turned into. The <a href="/docs/federation">federation reference</a> has the {Views().Count} views
            those queries were answered from.</p>
            """);

        return builder.Append(Foot()).ToString();
    }

    /// <summary>
    /// Renders the federation reference page.
    /// </summary>
    /// <returns>The rendered HTML.</returns>
    public static string Federation()
    {
        var builder = new StringBuilder();

        builder.Append(Head("Federation reference"));
        builder.Append("<h1>Federation reference</h1>");
        builder.Append($"""
            <p class="lede">The <code>{Encode(FederationModel.SchemaName)}</code> schema is {Views().Count} views over
            the four sources below. Eleven pass the source through under one vocabulary; the rest aggregate across
            stores. A view marked with more than one source is answered by joining stores inside Calcite.</p>
            <p><a href="/">Back to the index</a> · <a href="/docs/federation.json">as JSON</a> ·
            <a href="/diagnostics/model">the model document</a></p>
            """);

        builder.Append("<h2>Sources</h2><table><tr><th>Schema</th><th>Kind</th><th>Tables</th></tr>");
        foreach (var source in Sources)
            builder.Append($"<tr><td><code>{Encode(source.Schema)}</code></td><td>{Encode(source.Kind)}</td><td>{Encode(string.Join(", ", source.Tables))}</td></tr>");
        builder.Append("</table>");

        builder.Append("<h2>Views</h2><table><tr><th>View</th><th>Reads</th><th>Shape</th></tr>");
        foreach (var view in Views())
        {
            builder.Append($"""
                <tr>
                    <td><a href="#{Encode(view.Name)}"><code>{Encode(view.Name)}</code></a></td>
                    <td>{Encode(string.Join(", ", view.Sources))}</td>
                    <td>{(view.IsReport ? "report" : "pass-through")}</td>
                </tr>
                """);
        }

        builder.Append("</table>");

        foreach (var view in Views())
        {
            builder.Append($"""
                <h3 id="{Encode(view.Name)}">{Encode(view.Name)}</h3>
                <p class="meta">reads {Encode(string.Join(", ", view.Sources))} · {(view.IsReport ? "aggregates" : "passes its source through")}</p>
                <pre><code>{Encode(view.Sql)}</code></pre>
                """);
        }

        return builder.Append(Foot()).ToString();
    }

    /// <summary>
    /// Builds the machine readable form of the federation reference.
    /// </summary>
    /// <returns>The object to serialize.</returns>
    public static object FederationJson()
    {
        return new
        {
            schema = FederationModel.SchemaName,
            sources = Sources,
            views = Views().Select(x => new { x.Name, x.Sources, report = x.IsReport, sql = x.Sql }),
        };
    }

    /// <summary>
    /// Gets the views of the federated schema.
    /// </summary>
    /// <returns>The views.</returns>
    static IReadOnlyList<FederationModel.View> Views()
    {
        return FederationModel.Views;
    }

    /// <summary>
    /// Renders the opening of a page.
    /// </summary>
    /// <param name="title">The title of the page.</param>
    /// <returns>The rendered HTML.</returns>
    static string Head(string title)
    {
        // A doubled interpolation marker, because the stylesheet below is full of braces the single form would eat.
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{Encode(title)}}</title>
            <style>
              :root { color-scheme: light dark; }
              body { margin: 0 auto; padding: 2rem 1.25rem 4rem; max-width: 60rem; line-height: 1.55;
                     font-family: ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif; }
              h1 { margin-bottom: .25rem; }
              h2 { margin-top: 2.5rem; border-bottom: 1px solid color-mix(in srgb, currentColor 20%, transparent); padding-bottom: .3rem; }
              h3 { margin-top: 2rem; }
              .lede { font-size: 1.05rem; }
              .meta { opacity: .7; font-size: .9rem; margin-top: -.6rem; }
              table { border-collapse: collapse; width: 100%; margin: 1rem 0; }
              th, td { text-align: left; vertical-align: top; padding: .5rem .6rem;
                       border-bottom: 1px solid color-mix(in srgb, currentColor 15%, transparent); }
              th { font-size: .85rem; text-transform: uppercase; letter-spacing: .04em; opacity: .7; }
              code { font-family: ui-monospace, "Cascadia Code", Consolas, monospace; font-size: .9em; }
              pre { overflow-x: auto; padding: .9rem 1rem; border-radius: .4rem;
                    background: color-mix(in srgb, currentColor 7%, transparent); }
              small { opacity: .7; }
              a { color: inherit; }
            </style>
            </head>
            <body>
            """;
    }

    /// <summary>
    /// Renders the closing of a page.
    /// </summary>
    /// <returns>The rendered HTML.</returns>
    static string Foot()
    {
        return "</body></html>";
    }

    /// <summary>
    /// Escapes a value for inclusion in HTML.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value.</returns>
    static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

}
