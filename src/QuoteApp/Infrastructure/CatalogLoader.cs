using System.Text.Json;
using QuoteApp.Common;
using QuoteApp.Domain;

namespace QuoteApp.Infrastructure;

public static class CatalogLoader
{
    private sealed class ProductDto
    {
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public decimal? UnitPrice { get; set; }
        public int? Stock { get; set; }
        public bool? Active { get; set; }
    }

    private sealed class CatalogDto
    {
        public List<ProductDto>? Products { get; set; }
        public Dictionary<string, int>? DiscountCodes { get; set; }
    }

    public static Result<Catalog> Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Result.Fail<Catalog>(OperationalErrors.FileNotFound(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail<Catalog>(OperationalErrors.FileUnreadable(path, ex.Message));
        }

        return Parse(json, path);
    }

    public static Result<Catalog> Parse(string json, string source)
    {
        CatalogDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CatalogDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return Result.Fail<Catalog>(OperationalErrors.MalformedContent(source, $"invalid JSON ({ex.Message})"));
        }

        if (dto?.Products is null)
            return Result.Fail<Catalog>(OperationalErrors.MalformedContent(source, "missing required 'products' array"));

        var products = new List<Product>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < dto.Products.Count; i++)
        {
            var p = dto.Products[i];
            if (p is null || string.IsNullOrWhiteSpace(p.Sku) || p.Name is null ||
                p.UnitPrice is null || p.Stock is null || p.Active is null)
                return Result.Fail<Catalog>(OperationalErrors.MalformedContent(source,
                    $"product #{i + 1} is missing one of: sku, name, unitPrice, stock, active"));
            if (p.UnitPrice < 0 || p.Stock < 0)
                return Result.Fail<Catalog>(OperationalErrors.MalformedContent(source,
                    $"product '{p.Sku}' has a negative unitPrice or stock"));
            if (!seen.Add(p.Sku))
                return Result.Fail<Catalog>(OperationalErrors.MalformedContent(source, $"duplicate sku '{p.Sku}'"));
            products.Add(new Product(p.Sku, p.Name, p.UnitPrice.Value, p.Stock.Value, p.Active.Value));
        }

        var codes = dto.DiscountCodes ?? new Dictionary<string, int>();
        var bad = codes.FirstOrDefault(kv => kv.Value is < 0 or > 100);
        if (bad.Key is not null)
            return Result.Fail<Catalog>(OperationalErrors.MalformedContent(source,
                $"discount code '{bad.Key}' must be between 0 and 100"));

        return Result.Ok(new Catalog(products, codes));
    }
}

public sealed class CatalogStore
{
    public Catalog Current { get; private set; }

    public CatalogStore(Catalog initial) => Current = initial;

    public Result<Catalog> Reload(string path) =>
        CatalogLoader.Load(path).Map(fresh => Current = fresh);
}
