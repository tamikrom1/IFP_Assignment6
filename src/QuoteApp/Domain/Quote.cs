using QuoteApp.Common;

namespace QuoteApp.Domain;

public enum QuoteStatus { Draft, Confirmed }

public sealed record QuoteRequest(string Sku, int Quantity, string? DiscountCode);

public sealed record Quote(
    string Sku, string ProductName, int Quantity,
    decimal UnitPrice, int DiscountPercent, decimal Total, QuoteStatus Status)
{
    public Result<Quote> Confirm() =>
        Status == QuoteStatus.Draft
            ? Result.Ok(this with { Status = QuoteStatus.Confirmed })
            : Result.Fail<Quote>(DomainErrors.InvalidStateTransition(Status.ToString(), QuoteStatus.Confirmed.ToString()));
}

public sealed record ConfirmedQuote(Quote Quote, Catalog UpdatedCatalog);

public sealed record QuoteView(string Summary)
{
    public static QuoteView From(ConfirmedQuote c) =>
        new($"{c.Quote.Quantity} x {c.Quote.ProductName} ({c.Quote.Sku}) @ {c.Quote.UnitPrice:0.00}, " +
            $"discount {c.Quote.DiscountPercent}% => total {c.Quote.Total:0.00} [{c.Quote.Status}]");
}
