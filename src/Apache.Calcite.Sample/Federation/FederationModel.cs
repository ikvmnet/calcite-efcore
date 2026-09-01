using System.Text.Json;
using System.Text.Json.Nodes;

using Apache.Calcite.Sample.Sources;

namespace Apache.Calcite.Sample.Federation;

/// <summary>
/// Builds the inline Calcite model that defines the federated <c>northwind</c> schema.
/// </summary>
/// <remarks>
/// <para>
/// Four physical sources sit underneath the federation. Three are EF Core contexts registered on the root schema
/// at connection time by <see cref="FederationConnectionFactory"/> — <c>catalog</c>, <c>sales</c> and <c>hr</c>,
/// each a SQLite database. The fourth, <c>ref</c>, is a stock Calcite CSV adapter pointed at a directory of files,
/// and is declared in the model below because nothing about it involves EF Core.
/// </para>
/// <para>
/// Every table the federated <see cref="FederatedDbContext"/> maps is a view in this model. The eleven entity views
/// are pass-throughs that rename source columns into one coherent vocabulary; the three report views aggregate
/// across sources, and are the ones that make a single EF Core query fan out to SQLite and CSV at once.
/// </para>
/// <para>
/// Report views cast every computed column explicitly. The row type of a view is fixed at expansion time and the
/// EF Core model has to agree with it exactly, so leaving the width of a <c>SUM</c> or the type of a <c>RANK</c>
/// to inference is how a working federation turns into a materialisation error.
/// </para>
/// </remarks>
public static class FederationModel
{

    /// <summary>
    /// The name of the schema the federated context queries.
    /// </summary>
    public const string SchemaName = "northwind";

    /// <summary>
    /// The revenue of an order line, before it is summed. Repeated often enough in the report views to be worth naming.
    /// </summary>
    const string LineRevenue = """d."UnitPrice" * d."Quantity" * (1 - d."Discount")""";

    /// <summary>
    /// Builds the JSON model document handed to Calcite in the connection string.
    /// </summary>
    /// <param name="referenceDirectory">The directory holding the reference CSV files.</param>
    /// <returns>The serialized model.</returns>
    public static string Build(string referenceDirectory)
    {
        var views = new JsonArray();

        foreach (var view in Views)
        {
            views.Add(new JsonObject
            {
                ["name"] = view.Name,
                ["type"] = "view",
                ["sql"] = view.Sql,
            });
        }

        var model = new JsonObject
        {
            ["version"] = "1.0",
            ["defaultSchema"] = SchemaName,
            ["schemas"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "ref",
                    ["type"] = "custom",
                    ["factory"] = "org.apache.calcite.adapter.csv.CsvSchemaFactory",
                    ["operand"] = new JsonObject
                    {
                        // Java accepts forward slashes on Windows, and they survive JSON without escaping.
                        ["directory"] = referenceDirectory.Replace('\\', '/'),
                        ["flavor"] = "TRANSLATABLE",
                    },
                },
                new JsonObject
                {
                    ["name"] = SchemaName,
                    ["tables"] = views,
                },
            },
        };

        return model.ToJsonString(JsonSerializerOptions.Default);
    }

    /// <summary>
    /// Builds the model against the reference directory that ships with the sample.
    /// </summary>
    /// <returns>The serialized model.</returns>
    public static string Build()
    {
        return Build(SampleDatabases.ReferenceDirectory);
    }

    /// <summary>
    /// One view of the federated schema.
    /// </summary>
    /// <param name="Name">The name the view is registered under.</param>
    /// <param name="Sql">The SQL the view is defined by.</param>
    public sealed record View(string Name, string Sql)
    {

        /// <summary>
        /// Gets the source schemas this view reads, read off its defining SQL.
        /// </summary>
        public IReadOnlyList<string> Sources { get; } = SourceSchemas.Where(x => Sql.Contains($"\"{x}\".")).ToArray();

        /// <summary>
        /// Gets a value indicating whether this view aggregates, rather than passing its source through.
        /// </summary>
        public bool IsReport { get; } = Sql.Contains("GROUP BY");

    }

    /// <summary>
    /// The schemas a view may read from.
    /// </summary>
    static readonly string[] SourceSchemas = ["catalog", "sales", "hr", "ref"];

    /// <summary>
    /// The views, built on first use.
    /// </summary>
    /// <remarks>
    /// Deferred rather than initialized inline: the report views are static fields declared further down this file,
    /// and a static initializer running in textual order would capture them before they are assigned.
    /// </remarks>
    static IReadOnlyList<View>? _views;

    /// <summary>
    /// Gets the views that make up the federated schema, in declaration order.
    /// </summary>
    public static IReadOnlyList<View> Views => _views ??= BuildViews().Select(x => new View(x.Name, x.Sql)).ToArray();

    /// <summary>
    /// Enumerates the views that make up the federated schema, in declaration order.
    /// </summary>
    /// <returns>The name and defining SQL of each view.</returns>
    static IEnumerable<(string Name, string Sql)> BuildViews()
    {
        yield return ("Category", Category);
        yield return ("Supplier", Supplier);
        yield return ("Product", Product);
        yield return ("Customer", Customer);
        yield return ("SalesOrder", SalesOrder);
        yield return ("OrderLine", OrderLine);
        yield return ("Employee", Employee);
        yield return ("Shipper", Shipper);
        yield return ("Region", Region);
        yield return ("Territory", Territory);
        yield return ("EmployeeTerritory", EmployeeTerritory);
        yield return ("ProductSalesSummary", ProductSalesSummary);
        yield return ("CustomerValue", CustomerValue);
        yield return ("EmployeeScorecard", EmployeeScorecard);
    }

    /// <summary>
    /// The categories, straight out of the catalog store.
    /// </summary>
    const string Category = """
        SELECT c."Id" AS "Id",
               c."Name" AS "Name",
               c."Description" AS "Description"
        FROM "catalog"."Category" AS c
        """;

    /// <summary>
    /// The suppliers, straight out of the catalog store.
    /// </summary>
    const string Supplier = """
        SELECT s."Id" AS "Id",
               s."CompanyName" AS "CompanyName",
               s."ContactName" AS "ContactName",
               s."City" AS "City",
               s."Country" AS "Country",
               s."RegionCode" AS "RegionCode",
               s."OnboardedOn" AS "OnboardedOn",
               s."IsActive" AS "IsActive"
        FROM "catalog"."Supplier" AS s
        """;

    /// <summary>
    /// The products, straight out of the catalog store.
    /// </summary>
    const string Product = """
        SELECT p."Id" AS "Id",
               p."Sku" AS "Sku",
               p."Name" AS "Name",
               p."CategoryId" AS "CategoryId",
               p."SupplierId" AS "SupplierId",
               p."QuantityPerUnit" AS "QuantityPerUnit",
               p."UnitPrice" AS "UnitPrice",
               p."UnitsInStock" AS "UnitsInStock",
               p."UnitsOnOrder" AS "UnitsOnOrder",
               p."ReorderLevel" AS "ReorderLevel",
               p."DiscontinuedOn" AS "DiscontinuedOn"
        FROM "catalog"."Product" AS p
        """;

    /// <summary>
    /// The customers, straight out of the sales store.
    /// </summary>
    const string Customer = """
        SELECT c."Id" AS "Id",
               c."CustomerCode" AS "CustomerCode",
               c."CompanyName" AS "CompanyName",
               c."ContactName" AS "ContactName",
               c."City" AS "City",
               c."Country" AS "Country",
               c."RegionCode" AS "RegionCode",
               c."Segment" AS "Segment",
               c."SignedUpOn" AS "SignedUpOn",
               c."DiscountRate" AS "DiscountRate"
        FROM "sales"."Customer" AS c
        """;

    /// <summary>
    /// The order headers. Renamed away from the source table so the federated vocabulary never needs the
    /// <c>ORDER</c> keyword quoted.
    /// </summary>
    const string SalesOrder = """
        SELECT o."Id" AS "Id",
               o."CustomerId" AS "CustomerId",
               o."EmployeeId" AS "EmployeeId",
               o."ShipperId" AS "ShipperId",
               o."OrderedAt" AS "OrderedAt",
               o."RequiredOn" AS "RequiredOn",
               o."ShippedOn" AS "ShippedOn",
               o."Freight" AS "Freight",
               o."ShipCity" AS "ShipCity",
               o."ShipCountry" AS "ShipCountry",
               o."Status" AS "Status"
        FROM "sales"."Order" AS o
        """;

    /// <summary>
    /// The order lines, with the extended amount the source store does not carry computed on the way through.
    /// </summary>
    const string OrderLine = """
        SELECT d."Id" AS "Id",
               d."OrderId" AS "OrderId",
               d."ProductId" AS "ProductId",
               d."UnitPrice" AS "UnitPrice",
               d."Quantity" AS "Quantity",
               d."Discount" AS "Discount",
               CAST(d."UnitPrice" * d."Quantity" * (1 - d."Discount") AS DECIMAL(19, 4)) AS "ExtendedPrice"
        FROM "sales"."OrderDetail" AS d
        """;

    /// <summary>
    /// The employees, straight out of the human resources store.
    /// </summary>
    const string Employee = """
        SELECT e."Id" AS "Id",
               e."FirstName" AS "FirstName",
               e."LastName" AS "LastName",
               e."Title" AS "Title",
               e."ReportsToId" AS "ReportsToId",
               e."BirthDate" AS "BirthDate",
               e."HiredOn" AS "HiredOn",
               e."City" AS "City",
               e."Country" AS "Country",
               e."Extension" AS "Extension",
               e."Quota" AS "Quota"
        FROM "hr"."Employee" AS e
        """;

    /// <summary>
    /// The shippers, out of the reference CSV store.
    /// </summary>
    const string Shipper = """
        SELECT s."Id" AS "Id",
               s."CompanyName" AS "CompanyName",
               s."Phone" AS "Phone",
               s."ServiceLevel" AS "ServiceLevel",
               s."AverageTransitDays" AS "AverageTransitDays"
        FROM "ref"."Shipper" AS s
        """;

    /// <summary>
    /// The regions, out of the reference CSV store.
    /// </summary>
    const string Region = """
        SELECT r."Id" AS "Id",
               r."Code" AS "Code",
               r."Name" AS "Name",
               r."Headquarters" AS "Headquarters"
        FROM "ref"."Region" AS r
        """;

    /// <summary>
    /// The territories, out of the reference CSV store.
    /// </summary>
    const string Territory = """
        SELECT t."Id" AS "Id",
               t."Name" AS "Name",
               t."RegionId" AS "RegionId",
               t."TimeZone" AS "TimeZone"
        FROM "ref"."Territory" AS t
        """;

    /// <summary>
    /// The employee to territory assignments, out of the reference CSV store.
    /// </summary>
    const string EmployeeTerritory = """
        SELECT a."Id" AS "Id",
               a."EmployeeId" AS "EmployeeId",
               a."TerritoryId" AS "TerritoryId",
               a."AssignedOn" AS "AssignedOn"
        FROM "ref"."EmployeeTerritory" AS a
        """;

    /// <summary>
    /// Sales rolled up per product, ranked within the category of the product. Joins the catalog store to the
    /// sales store and adds a window function over the aggregate.
    /// </summary>
    static readonly string ProductSalesSummary = $"""
        SELECT p."Id" AS "Id",
               p."Id" AS "ProductId",
               p."Name" AS "ProductName",
               p."CategoryId" AS "CategoryId",
               CAST(COUNT(DISTINCT d."OrderId") AS INTEGER) AS "OrderCount",
               CAST(SUM(d."Quantity") AS INTEGER) AS "UnitsSold",
               CAST(SUM({LineRevenue}) AS DECIMAL(19, 4)) AS "Revenue",
               CAST(AVG(d."Discount") AS DECIMAL(19, 4)) AS "AverageDiscount",
               CAST(RANK() OVER (PARTITION BY p."CategoryId" ORDER BY SUM({LineRevenue}) DESC) AS INTEGER) AS "CategoryRank"
        FROM "catalog"."Product" AS p
        INNER JOIN "sales"."OrderDetail" AS d ON d."ProductId" = p."Id"
        GROUP BY p."Id", p."Name", p."CategoryId"
        """;

    /// <summary>
    /// Lifetime value per customer, over the whole order history. Customers that have never ordered survive the
    /// outer joins with zeroed totals.
    /// </summary>
    static readonly string CustomerValue = $"""
        SELECT c."Id" AS "Id",
               c."Id" AS "CustomerId",
               c."CompanyName" AS "CompanyName",
               c."Segment" AS "Segment",
               c."Country" AS "Country",
               CAST(COUNT(DISTINCT o."Id") AS INTEGER) AS "OrderCount",
               CAST(COALESCE(SUM({LineRevenue}), 0) AS DECIMAL(19, 4)) AS "LifetimeValue",
               CAST(COALESCE(SUM(o."Freight"), 0) AS DECIMAL(19, 4)) AS "FreightPaid",
               MIN(o."OrderedAt") AS "FirstOrderedAt",
               MAX(o."OrderedAt") AS "LastOrderedAt"
        FROM "sales"."Customer" AS c
        LEFT JOIN "sales"."Order" AS o ON o."CustomerId" = c."Id"
        LEFT JOIN "sales"."OrderDetail" AS d ON d."OrderId" = o."Id"
        GROUP BY c."Id", c."CompanyName", c."Segment", c."Country"
        """;

    /// <summary>
    /// Per employee sales and territory coverage. The only view that reaches all three source kinds at once:
    /// the employee row comes from SQLite by way of EF Core, the revenue from a second SQLite store, and the
    /// territory count from the CSV files.
    /// </summary>
    /// <remarks>
    /// The two roll-ups are joined as derived tables rather than piled into one <c>GROUP BY</c>: joining orders
    /// and territories in the same query multiplies the order rows by the territory count and quietly inflates
    /// every revenue figure.
    /// </remarks>
    static readonly string EmployeeScorecard = $"""
        SELECT e."Id" AS "Id",
               e."Id" AS "EmployeeId",
               e."FirstName" AS "FirstName",
               e."LastName" AS "LastName",
               e."Title" AS "Title",
               e."Quota" AS "Quota",
               CAST(COALESCE(s."OrderCount", 0) AS INTEGER) AS "OrderCount",
               CAST(COALESCE(s."Revenue", 0) AS DECIMAL(19, 4)) AS "Revenue",
               CAST(COALESCE(t."TerritoryCount", 0) AS INTEGER) AS "TerritoryCount"
        FROM "hr"."Employee" AS e
        LEFT JOIN (SELECT o."EmployeeId" AS "EmployeeId",
                          COUNT(DISTINCT o."Id") AS "OrderCount",
                          SUM({LineRevenue}) AS "Revenue"
                   FROM "sales"."Order" AS o
                   INNER JOIN "sales"."OrderDetail" AS d ON d."OrderId" = o."Id"
                   GROUP BY o."EmployeeId") AS s ON s."EmployeeId" = e."Id"
        LEFT JOIN (SELECT a."EmployeeId" AS "EmployeeId",
                          COUNT(*) AS "TerritoryCount"
                   FROM "ref"."EmployeeTerritory" AS a
                   GROUP BY a."EmployeeId") AS t ON t."EmployeeId" = e."Id"
        """;

}
