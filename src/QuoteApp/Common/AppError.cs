namespace QuoteApp.Common;

public enum ErrorKind { Domain, Operational }

public sealed record AppError(ErrorKind Kind, string Code, string Message)
{
    public override string ToString() => $"[{Kind}/{Code}] {Message}";
}

public static class DomainErrors
{
    public static AppError InvalidQuantity(int quantity) =>
        new(ErrorKind.Domain, "invalid_quantity",
            $"Quantity must be a positive whole number, but was {quantity}.");

    public static AppError ItemNotFound(string sku) =>
        new(ErrorKind.Domain, "item_not_found", $"No item with SKU '{sku}' exists in the catalog.");

    public static AppError InsufficientStock(string sku, int requested, int available) =>
        new(ErrorKind.Domain, "insufficient_stock",
            $"Item '{sku}': requested {requested} but only {available} in stock.");

    public static AppError ItemDiscontinued(string sku) =>
        new(ErrorKind.Domain, "item_discontinued", $"Item '{sku}' is discontinued and cannot be quoted.");

    public static AppError UnknownDiscountCode(string code) =>
        new(ErrorKind.Domain, "unknown_discount_code", $"Discount code '{code}' does not exist.");

    public static AppError InvalidStateTransition(string from, string to) =>
        new(ErrorKind.Domain, "invalid_state_transition",
            $"A quote in state {from} cannot move to {to}.");
}

public static class OperationalErrors
{
    public static AppError FileNotFound(string path) =>
        new(ErrorKind.Operational, "file_not_found", $"Catalog file '{path}' was not found.");

    public static AppError FileUnreadable(string path, string reason) =>
        new(ErrorKind.Operational, "file_unreadable", $"Catalog file '{path}' could not be read: {reason}");

    public static AppError MalformedContent(string source, string reason) =>
        new(ErrorKind.Operational, "malformed_content", $"Catalog data from '{source}' is malformed: {reason}");
}
