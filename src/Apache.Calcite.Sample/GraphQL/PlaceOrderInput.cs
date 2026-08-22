namespace Apache.Calcite.Sample.GraphQL;

/// <summary>
/// The order to place.
/// </summary>
/// <param name="CustomerId">The identifier of the ordering customer.</param>
/// <param name="EmployeeId">The identifier of the employee taking the order.</param>
/// <param name="ShipperId">The identifier of the shipping company.</param>
/// <param name="ShipCity">The destination city.</param>
/// <param name="ShipCountry">The destination country.</param>
/// <param name="Lines">The lines to place on the order.</param>
public sealed record PlaceOrderInput(
    int CustomerId,
    int EmployeeId,
    int ShipperId,
    string ShipCity,
    string ShipCountry,
    IReadOnlyList<PlaceOrderLineInput> Lines);

/// <summary>
/// One line of an order to place.
/// </summary>
/// <param name="ProductId">The identifier of the product ordered.</param>
/// <param name="Quantity">The number of units ordered.</param>
/// <param name="Discount">The fractional discount to apply.</param>
public sealed record PlaceOrderLineInput(int ProductId, int Quantity, decimal Discount = 0m);
