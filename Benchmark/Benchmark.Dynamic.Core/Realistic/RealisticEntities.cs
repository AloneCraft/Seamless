namespace Benchmark.Dynamic.Core.Realistic;

public enum TradeStatus : byte {
    Pending = 0,
    Submitted = 1,
    Completed = 2,
    Canceled = 3,
}

public readonly record struct Trade(
    long Id,
    long AccountId,
    string Symbol,
    int Quantity,
    decimal Price,
    TradeStatus Status,
    long TimestampTicks,
    bool IsCash);

public readonly record struct Account(
    long Id,
    long CustomerId,
    long BrokerId,
    string Name,
    decimal Balance,
    int Tier);

public readonly record struct Security(
    string Symbol,
    string Sector,
    long CompanyId,
    decimal LastPrice);

public enum OrderStatus {
    New = 0,
    Paid = 1,
    Shipped = 2,
    Delivered = 3,
    Returned = 4,
}

public sealed class Customer {
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string City { get; init; } = "";
    public int Age { get; init; }
    public bool IsPremium { get; init; }
}

public sealed class SalesOrder {
    public int Id { get; init; }
    public int CustomerId { get; init; }
    public decimal Amount { get; init; }
    public long OrderedAtTicks { get; init; }
    public OrderStatus Status { get; init; }
    public string? CouponCode { get; init; }
}
