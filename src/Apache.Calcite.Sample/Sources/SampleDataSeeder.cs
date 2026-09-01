using Apache.Calcite.Sample.Sources.Catalog;
using Apache.Calcite.Sample.Sources.HumanResources;
using Apache.Calcite.Sample.Sources.Sales;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.Sample.Sources;

/// <summary>
/// Fills the three SQLite source stores with a deterministic Northwind shaped data set. The generator is seeded
/// with a fixed value so every run of the sample produces byte for byte identical stores, which keeps query
/// results stable enough to assert against by hand.
/// </summary>
public static class SampleDataSeeder
{

    /// <summary>
    /// The seed of the pseudo random generator. Changing it changes every generated row.
    /// </summary>
    const int Seed = 20260815;

    /// <summary>
    /// The number of suppliers to generate.
    /// </summary>
    public const int SupplierCount = 40;

    /// <summary>
    /// The number of products to generate.
    /// </summary>
    public const int ProductCount = 300;

    /// <summary>
    /// The number of customers to generate.
    /// </summary>
    public const int CustomerCount = 400;

    /// <summary>
    /// The number of order headers to generate.
    /// </summary>
    public const int OrderCount = 6000;

    /// <summary>
    /// The first instant an order may be placed at.
    /// </summary>
    static readonly DateTime OrderWindowStart = new(2023, 1, 2, 8, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// The number of days the order window spans.
    /// </summary>
    const int OrderWindowDays = 1250;

    static readonly string[] CategoryNames =
    [
        "Beverages", "Condiments", "Confections", "Dairy Products",
        "Grains/Cereals", "Meat/Poultry", "Produce", "Seafood",
    ];

    static readonly string[] CategoryDescriptions =
    [
        "Soft drinks, coffees, teas, beers and ales",
        "Sweet and savoury sauces, relishes, spreads and seasonings",
        "Desserts, candies and sweet breads",
        "Cheeses, milks, yoghurts and butters",
        "Breads, crackers, pasta and cereal",
        "Prepared meats and poultry",
        "Dried fruit, bean curd and fresh vegetables",
        "Seaweed, fish and shellfish",
    ];

    static readonly string[][] CategoryNouns =
    [
        ["Chai", "Lager", "Cold Brew", "Espresso", "Cider", "Tonic", "Stout", "Ginger Beer"],
        ["Relish", "Chutney", "Hot Sauce", "Mustard", "Curry Paste", "Syrup", "Marinade", "Pesto"],
        ["Nougat", "Biscuits", "Truffles", "Marzipan", "Shortbread", "Caramels", "Wafers", "Brittle"],
        ["Gouda", "Camembert", "Butter", "Yoghurt", "Cream", "Halloumi", "Kefir", "Mozzarella"],
        ["Fusilli", "Rye Bread", "Couscous", "Oat Flakes", "Ravioli", "Polenta", "Muesli", "Crispbread"],
        ["Bratwurst", "Pastrami", "Chorizo", "Duck Confit", "Meatballs", "Prosciutto", "Rillettes", "Jerky"],
        ["Dried Apples", "Tofu", "Shiitake", "Pears", "Chestnuts", "Sun-dried Tomatoes", "Olives", "Artichokes"],
        ["Kelp", "Crab Meat", "Smoked Salmon", "Anchovies", "Clam Chowder", "Herring", "Prawns", "Scallops"],
    ];

    static readonly string[] Brands =
    [
        "Northwind Traders", "Exotic Liquids", "Grandma Kelly", "Tokyo Traders", "Cooperativa",
        "Mayumi", "Pavlova", "Specialty Biscuits", "PB Knackebrod", "Refrescos Americanas",
        "Heli Wurst", "Plutzer", "Nord-Ost", "Formaggi Fortini", "Norske Meierier",
        "Bigfoot Breweries", "Svensk Sjofoda", "Aux Joyeux", "New England Seafood", "Leka Trading",
        "Lyngbysild", "Zaanse Snoepfabriek", "Karkki Oy", "G'day Mate", "Ma Maison",
        "Pasta Buttini", "Escargots Nouveaux", "Gai Paturage", "Forets d'erables", "Mikkelsen",
    ];

    static readonly (string City, string Country, string RegionCode)[] Places =
    [
        ("Seattle", "USA", "NA"), ("Portland", "USA", "NA"), ("Boston", "USA", "NA"),
        ("Chicago", "USA", "NA"), ("Montreal", "Canada", "NA"), ("Vancouver", "Canada", "NA"),
        ("Mexico City", "Mexico", "NA"), ("Sao Paulo", "Brazil", "SA"), ("Rio de Janeiro", "Brazil", "SA"),
        ("Buenos Aires", "Argentina", "SA"), ("Santiago", "Chile", "SA"), ("Caracas", "Venezuela", "SA"),
        ("London", "UK", "EU"), ("Manchester", "UK", "EU"), ("Paris", "France", "EU"),
        ("Lyon", "France", "EU"), ("Berlin", "Germany", "EU"), ("Munich", "Germany", "EU"),
        ("Madrid", "Spain", "EU"), ("Barcelona", "Spain", "EU"), ("Milan", "Italy", "EU"),
        ("Rome", "Italy", "EU"), ("Stockholm", "Sweden", "EU"), ("Oslo", "Norway", "EU"),
        ("Warsaw", "Poland", "EU"), ("Lisbon", "Portugal", "EU"), ("Tokyo", "Japan", "AP"),
        ("Osaka", "Japan", "AP"), ("Singapore", "Singapore", "AP"), ("Sydney", "Australia", "AP"),
        ("Melbourne", "Australia", "AP"), ("Auckland", "New Zealand", "AP"),
    ];

    static readonly string[] GivenNames =
    [
        "Nancy", "Andrew", "Janet", "Margaret", "Steven", "Michael", "Robert", "Laura", "Anne",
        "Ingrid", "Yoshi", "Carlos", "Elena", "Petra", "Hanna", "Mateo", "Sofia", "Karl",
        "Amara", "Nadia", "Tomas", "Lucia", "Henrik", "Priya", "Diego", "Freya", "Omar", "Mei",
    ];

    static readonly string[] FamilyNames =
    [
        "Davolio", "Fuller", "Leverling", "Peacock", "Buchanan", "Suyama", "King", "Callahan",
        "Dodsworth", "Nagy", "Lindqvist", "Moreno", "Rossi", "Schmidt", "Virtanen", "Okafor",
        "Fernandez", "Novak", "Tanaka", "Bergstrom", "Haddad", "Kowalski", "Silva", "Marchetti",
    ];

    static readonly string[] CompanySuffixes =
    [
        "Markets", "Delikatessen", "Trading", "Bodega", "Provisions", "Grocers", "Imports",
        "Distributors", "Emporium", "Larder", "Pantry", "Merchants",
    ];

    static readonly string[] Segments = ["Enterprise", "Midmarket", "Small Business", "Reseller"];

    static readonly string[] Titles =
    [
        "Vice President, Sales", "Sales Manager", "Sales Representative", "Inside Sales Coordinator",
    ];

    /// <summary>
    /// Creates the source databases if they do not exist and fills them with generated data.
    /// </summary>
    /// <param name="logger">The logger to report progress to.</param>
    /// <param name="cancellationToken">A token that cancels the seed.</param>
    public static async Task SeedAsync(ILogger logger, CancellationToken cancellationToken = default)
    {
        SampleDatabases.EnsureRoot();

        using var catalog = new CatalogDbContext();
        using var sales = new SalesDbContext();
        using var hr = new HumanResourcesDbContext();

        var created = await catalog.Database.EnsureCreatedAsync(cancellationToken);
        created |= await sales.Database.EnsureCreatedAsync(cancellationToken);
        created |= await hr.Database.EnsureCreatedAsync(cancellationToken);

        if (created == false && await catalog.Products.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Source stores already seeded; skipping generation.");
            return;
        }

        var random = new Random(Seed);

        catalog.ChangeTracker.AutoDetectChangesEnabled = false;
        sales.ChangeTracker.AutoDetectChangesEnabled = false;
        hr.ChangeTracker.AutoDetectChangesEnabled = false;

        var categories = BuildCategories();
        var suppliers = BuildSuppliers(random);
        var products = BuildProducts(random, categories.Count, suppliers.Count);
        catalog.Categories.AddRange(categories);
        catalog.Suppliers.AddRange(suppliers);
        catalog.Products.AddRange(products);
        await catalog.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded catalog store: {Categories} categories, {Suppliers} suppliers, {Products} products.", categories.Count, suppliers.Count, products.Count);

        var employees = BuildEmployees(random);
        hr.Employees.AddRange(employees);
        await hr.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded human resources store: {Employees} employees.", employees.Count);

        var customers = BuildCustomers(random);
        sales.Customers.AddRange(customers);
        await sales.SaveChangesAsync(cancellationToken);

        var (orders, details) = BuildOrders(random, customers, employees.Count, products);
        sales.Orders.AddRange(orders);
        await sales.SaveChangesAsync(cancellationToken);
        sales.OrderDetails.AddRange(details);
        await sales.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded sales store: {Customers} customers, {Orders} orders, {Details} order lines.", customers.Count, orders.Count, details.Count);
    }

    /// <summary>
    /// Builds the fixed category rows.
    /// </summary>
    /// <returns>The generated categories.</returns>
    static List<Category> BuildCategories()
    {
        var list = new List<Category>();

        for (var i = 0; i < CategoryNames.Length; i++)
            list.Add(new Category { Id = i + 1, Name = CategoryNames[i], Description = CategoryDescriptions[i] });

        return list;
    }

    /// <summary>
    /// Builds the supplier rows.
    /// </summary>
    /// <param name="random">The generator to draw from.</param>
    /// <returns>The generated suppliers.</returns>
    static List<Supplier> BuildSuppliers(Random random)
    {
        var list = new List<Supplier>();

        for (var i = 0; i < SupplierCount; i++)
        {
            var place = Places[random.Next(Places.Length)];

            list.Add(new Supplier
            {
                Id = i + 1,
                CompanyName = $"{Brands[i % Brands.Length]} {CompanySuffixes[random.Next(CompanySuffixes.Length)]}",
                ContactName = $"{GivenNames[random.Next(GivenNames.Length)]} {FamilyNames[random.Next(FamilyNames.Length)]}",
                City = place.City,
                Country = place.Country,
                RegionCode = place.RegionCode,
                OnboardedOn = new DateOnly(2015, 1, 1).AddDays(random.Next(0, 3200)),
                IsActive = random.Next(0, 10) > 0,
            });
        }

        return list;
    }

    /// <summary>
    /// Builds the product rows.
    /// </summary>
    /// <param name="random">The generator to draw from.</param>
    /// <param name="categoryCount">The number of categories that exist.</param>
    /// <param name="supplierCount">The number of suppliers that exist.</param>
    /// <returns>The generated products.</returns>
    static List<Product> BuildProducts(Random random, int categoryCount, int supplierCount)
    {
        var list = new List<Product>();

        for (var i = 0; i < ProductCount; i++)
        {
            var categoryId = random.Next(1, categoryCount + 1);
            var noun = CategoryNouns[categoryId - 1][random.Next(CategoryNouns[categoryId - 1].Length)];
            var brand = Brands[random.Next(Brands.Length)];
            var price = Math.Round((decimal)(random.NextDouble() * 118.0 + 2.5), 2);

            list.Add(new Product
            {
                Id = i + 1,
                Sku = $"NW-{categoryId:D2}-{i + 1:D4}",
                Name = $"{brand} {noun}",
                CategoryId = categoryId,
                SupplierId = random.Next(1, supplierCount + 1),
                QuantityPerUnit = $"{random.Next(1, 49)} x {random.Next(2, 24) * 25} g",
                UnitPrice = price,
                UnitsInStock = random.Next(0, 180),
                UnitsOnOrder = random.Next(0, 5) == 0 ? random.Next(10, 100) : 0,
                ReorderLevel = random.Next(0, 4) * 5,
                DiscontinuedOn = random.Next(0, 12) == 0 ? new DateOnly(2024, 1, 1).AddDays(random.Next(0, 900)) : null,
            });
        }

        return list;
    }

    /// <summary>
    /// Builds the employee rows as a three level reporting hierarchy.
    /// </summary>
    /// <param name="random">The generator to draw from.</param>
    /// <returns>The generated employees.</returns>
    static List<Employee> BuildEmployees(Random random)
    {
        var list = new List<Employee>();

        // one vice president, three managers reporting to them, and eight representatives spread across the managers
        for (var i = 0; i < 12; i++)
        {
            var place = Places[random.Next(Places.Length)];
            var titleIndex = i == 0 ? 0 : i <= 3 ? 1 : i <= 9 ? 2 : 3;
            int? reportsTo = i == 0 ? null : i <= 3 ? 1 : 2 + (i % 3);

            list.Add(new Employee
            {
                Id = i + 1,
                FirstName = GivenNames[i % GivenNames.Length],
                LastName = FamilyNames[i % FamilyNames.Length],
                Title = Titles[titleIndex],
                ReportsToId = reportsTo,
                BirthDate = new DateOnly(1968, 1, 1).AddDays(random.Next(0, 9000)),
                HiredOn = new DateOnly(2012, 1, 1).AddDays(random.Next(0, 4000)),
                City = place.City,
                Country = place.Country,
                Extension = $"{random.Next(1000, 9999)}",
                Quota = 250000m + random.Next(0, 40) * 12500m,
            });
        }

        return list;
    }

    /// <summary>
    /// Builds the customer rows.
    /// </summary>
    /// <param name="random">The generator to draw from.</param>
    /// <returns>The generated customers.</returns>
    static List<Customer> BuildCustomers(Random random)
    {
        var list = new List<Customer>();

        for (var i = 0; i < CustomerCount; i++)
        {
            var place = Places[random.Next(Places.Length)];
            var company = $"{FamilyNames[random.Next(FamilyNames.Length)]} {CompanySuffixes[random.Next(CompanySuffixes.Length)]}";

            list.Add(new Customer
            {
                Id = i + 1,
                CustomerCode = $"{new string(company.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant()}{i:D3}",
                CompanyName = company,
                ContactName = $"{GivenNames[random.Next(GivenNames.Length)]} {FamilyNames[random.Next(FamilyNames.Length)]}",
                City = place.City,
                Country = place.Country,
                RegionCode = place.RegionCode,
                Segment = Segments[random.Next(Segments.Length)],
                SignedUpOn = new DateOnly(2018, 1, 1).AddDays(random.Next(0, 2500)),
                DiscountRate = Math.Round((decimal)random.Next(0, 12) / 100m, 2),
            });
        }

        return list;
    }

    /// <summary>
    /// Builds the order headers and their lines.
    /// </summary>
    /// <param name="random">The generator to draw from.</param>
    /// <param name="customers">The customers orders may be placed by.</param>
    /// <param name="employeeCount">The number of employees that exist.</param>
    /// <param name="products">The products that may be ordered.</param>
    /// <returns>The generated orders and order lines.</returns>
    static (List<Order> Orders, List<OrderDetail> Details) BuildOrders(Random random, List<Customer> customers, int employeeCount, List<Product> products)
    {
        var orders = new List<Order>(OrderCount);
        var details = new List<OrderDetail>(OrderCount * 3);
        var detailId = 1;

        for (var i = 0; i < OrderCount; i++)
        {
            var customer = customers[random.Next(customers.Count)];
            var orderedAt = OrderWindowStart.AddDays(random.Next(0, OrderWindowDays)).AddMinutes(random.Next(0, 600));
            var orderedOn = DateOnly.FromDateTime(orderedAt);
            var shipped = random.Next(0, 10) > 1;
            var cancelled = shipped == false && random.Next(0, 4) == 0;

            orders.Add(new Order
            {
                Id = i + 1,
                CustomerId = customer.Id,
                EmployeeId = random.Next(1, employeeCount + 1),
                ShipperId = random.Next(1, 6),
                OrderedAt = orderedAt,
                RequiredOn = orderedOn.AddDays(random.Next(7, 45)),
                ShippedOn = shipped ? orderedOn.AddDays(random.Next(1, 21)) : null,
                Freight = Math.Round((decimal)(random.NextDouble() * 240.0 + 4.0), 2),
                ShipCity = customer.City,
                ShipCountry = customer.Country,
                Status = cancelled ? "Cancelled" : shipped ? "Shipped" : "Pending",
            });

            var lineCount = 1 + random.Next(0, 5);
            var used = new HashSet<int>();

            for (var j = 0; j < lineCount; j++)
            {
                var product = products[random.Next(products.Count)];
                if (used.Add(product.Id) == false)
                    continue;

                details.Add(new OrderDetail
                {
                    Id = detailId++,
                    OrderId = i + 1,
                    ProductId = product.Id,
                    UnitPrice = Math.Round(product.UnitPrice * (decimal)(0.92 + random.NextDouble() * 0.16), 2),
                    Quantity = 1 + random.Next(0, 60),
                    Discount = random.Next(0, 3) == 0 ? Math.Round((decimal)random.Next(1, 5) * 0.05m, 2) : 0m,
                });
            }
        }

        return (orders, details);
    }

}
