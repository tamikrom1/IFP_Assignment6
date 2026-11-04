using QuoteApp.Common;

namespace QuoteApp.Domain;

public static class QuoteWorkflow
{
    public static Result<Product> FindProduct(Catalog catalog, string sku) =>
        catalog.FindProduct(sku).ToResult(() => DomainErrors.ItemNotFound(sku));

    public static Result<Product> Validate(Product product, int quantity)
    {
        if (quantity <= 0) return Result.Fail<Product>(DomainErrors.InvalidQuantity(quantity));
        if (!product.Active) return Result.Fail<Product>(DomainErrors.ItemDiscontinued(product.Sku));
        if (product.Stock < quantity)
            return Result.Fail<Product>(DomainErrors.InsufficientStock(product.Sku, quantity, product.Stock));
        return Result.Ok(product);
    }

    public static Result<int> ResolveDiscount(Catalog catalog, string? code) =>
        code is null
            ? Result.Ok(0)
            : catalog.FindDiscountPercent(code).ToResult(() => DomainErrors.UnknownDiscountCode(code));

    public static Quote Price(Product product, int quantity, int discountPercent) =>
        new(product.Sku, product.Name, quantity, product.UnitPrice, discountPercent,
            Math.Round(product.UnitPrice * quantity * (100 - discountPercent) / 100m, 2, MidpointRounding.AwayFromZero),
            QuoteStatus.Draft);

    public static Result<Quote> CreateQuote(Catalog catalog, QuoteRequest request) =>
        FindProduct(catalog, request.Sku)
            .Bind(product => Validate(product, request.Quantity))
            .Bind(product => ResolveDiscount(catalog, request.DiscountCode)
                .Map(percent => (product, percent)))
            .Map(x => Price(x.product, request.Quantity, x.percent));

    public static Result<ConfirmedQuote> Confirm(Catalog catalog, Quote quote) =>
        quote.Confirm()
            .Bind(confirmed => catalog.Reserve(confirmed.Sku, confirmed.Quantity)
                .Map(updated => new ConfirmedQuote(confirmed, updated)));

    public static Result<QuoteView> Run(Catalog catalog, QuoteRequest request) =>
        RunKeepingCatalog(catalog, request).Map(QuoteView.From);

    public static Result<ConfirmedQuote> RunKeepingCatalog(Catalog catalog, QuoteRequest request) =>
        CreateQuote(catalog, request).Bind(quote => Confirm(catalog, quote));
}
