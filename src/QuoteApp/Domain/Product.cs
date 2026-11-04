namespace QuoteApp.Domain;

public sealed record Product(string Sku, string Name, decimal UnitPrice, int Stock, bool Active);
