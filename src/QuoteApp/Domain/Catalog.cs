using QuoteApp.Common;

namespace QuoteApp.Domain;

public sealed class Catalog
{
    private readonly IReadOnlyDictionary<string, Product> _products;
    private readonly IReadOnlyDictionary<string, int> _discountCodes;

    public Catalog(IEnumerable<Product> products, IReadOnlyDictionary<string, int> discountCodes)
    {
        _products = products.ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);
        _discountCodes = new Dictionary<string, int>(discountCodes, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<Product> Products => _products.Values;

    public Maybe<Product> FindProduct(string sku) =>
        _products.TryGetValue(sku, out var p) ? Maybe.Some(p) : Maybe.None<Product>();

    public Maybe<int> FindDiscountPercent(string code) =>
        _discountCodes.TryGetValue(code, out var pct) ? Maybe.Some(pct) : Maybe.None<int>();

    public Result<Catalog> Reserve(string sku, int quantity) =>
        FindProduct(sku)
            .ToResult(() => DomainErrors.ItemNotFound(sku))
            .Bind(p => p.Stock >= quantity
                ? Result.Ok(WithProduct(p with { Stock = p.Stock - quantity }))
                : Result.Fail<Catalog>(DomainErrors.InsufficientStock(sku, quantity, p.Stock)));

    private Catalog WithProduct(Product updated) =>
        new(_products.Values.Select(p => p.Sku.Equals(updated.Sku, StringComparison.OrdinalIgnoreCase) ? updated : p),
            _discountCodes);
}
