using QuoteApp.Common;
using QuoteApp.Domain;
using QuoteApp.Infrastructure;

namespace QuoteApp.Tests;

internal static class Helpers
{
    public static Catalog SampleCatalog() => new(
        new[]
        {
            new Product("LAPTOP-15", "Laptop", 1200.00m, 10, true),
            new Product("MOUSE-01", "Mouse", 25.50m, 100, true),
            new Product("CABLE-USB", "Cable", 5.00m, 0, true),
            new Product("STICKER-FREE", "Sticker", 0m, 50, true),
            new Product("KEYBOARD-OLD", "Old keyboard", 40m, 5, false),
        },
        new Dictionary<string, int> { ["WELCOME10"] = 10, ["PLAIN"] = 0 });

    public static AppError? ErrorOf<T>(Result<T> r) => r.Match<AppError?>(_ => null, e => e);
    public static (bool ok, T? value) ValueOf<T>(Result<T> r) => r.Match(v => (true, (T?)v), _ => (false, default));

    public static string TempPath() => Path.Combine(Path.GetTempPath(), "quoteapp-" + Guid.NewGuid().ToString("N") + ".json");

    public static void WithTempFile(string content, Action<string> body)
    {
        var path = TempPath();
        File.WriteAllText(path, content);
        try { body(path); } finally { File.Delete(path); }
    }

    public const string ValidJson =
        """{ "products": [ { "sku": "A", "name": "A", "unitPrice": 1, "stock": 1, "active": true } ], "discountCodes": {} }""";
}

public static class DomainOutcomeTests
{
    [Test]
    public static void CompleteSuccess_ChainProducesCorrectValue()
    {
        var r = QuoteWorkflow.RunKeepingCatalog(Helpers.SampleCatalog(), new QuoteRequest("LAPTOP-15", 2, "WELCOME10"));
        var (ok, done) = Helpers.ValueOf(r);
        Assert.True(ok, "chain should succeed");
        Assert.Equal(2160.00m, done!.Quote.Total, "total");
        Assert.Equal(QuoteStatus.Confirmed, done.Quote.Status, "status");
        Assert.Equal(8, done.UpdatedCatalog.FindProduct("LAPTOP-15").Match(p => p.Stock, () => -1), "stock after reserve");
    }

    [Test]
    public static void CompleteSuccess_MapProducesDisplayModel()
    {
        var r = QuoteWorkflow.Run(Helpers.SampleCatalog(), new QuoteRequest("MOUSE-01", 2, null));
        var summary = r.Match(v => v.Summary, e => "FAILED " + e);
        Assert.Equal("2 x Mouse (MOUSE-01) @ 25.50, discount 0% => total 51.00 [Confirmed]", summary, "summary");
    }

    [Test]
    public static void DomainFailure_InvalidQuantity()
    {
        foreach (var q in new[] { 0, -3 })
        {
            var e = Helpers.ErrorOf(QuoteWorkflow.Run(Helpers.SampleCatalog(), new QuoteRequest("MOUSE-01", q, null)));
            Assert.Equal("invalid_quantity", e!.Code, "code");
            Assert.Equal(ErrorKind.Domain, e.Kind, "kind");
            Assert.True(e.Message.Contains(q.ToString()), "message names the bad quantity");
        }
    }

    [Test]
    public static void DomainFailure_ItemNotFound()
    {
        var e = Helpers.ErrorOf(QuoteWorkflow.Run(Helpers.SampleCatalog(), new QuoteRequest("NOPE", 1, null)));
        Assert.Equal("item_not_found", e!.Code, "code");
        Assert.True(e.Message.Contains("NOPE"), "message names the SKU");
    }

    [Test]
    public static void DomainFailure_InsufficientStock()
    {
        var stock = Helpers.ErrorOf(QuoteWorkflow.Run(Helpers.SampleCatalog(), new QuoteRequest("LAPTOP-15", 11, null)));
        Assert.Equal("insufficient_stock", stock!.Code, "stock code");
        Assert.True(stock.Message.Contains("11") && stock.Message.Contains("10"), "message has requested and available");

        var old = Helpers.ErrorOf(QuoteWorkflow.Run(Helpers.SampleCatalog(), new QuoteRequest("KEYBOARD-OLD", 1, null)));
        Assert.Equal("item_discontinued", old!.Code, "discontinued code");
    }

    [Test]
    public static void DomainFailure_InvalidStateTransition()
    {
        var cat = Helpers.SampleCatalog();
        var done = QuoteWorkflow.RunKeepingCatalog(cat, new QuoteRequest("MOUSE-01", 1, null))
            .Match(c => c, e => throw new AssertionException("setup failed " + e));
        var e = Helpers.ErrorOf(QuoteWorkflow.Confirm(done.UpdatedCatalog, done.Quote));
        Assert.Equal("invalid_state_transition", e!.Code, "code");
    }

    [Test]
    public static void ContradictoryStates_AreImpossibleToBuild()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Ok<string>(null!));
        Assert.Throws<ArgumentNullException>(() => Result.Fail<string>(null!));
        Assert.Throws<ArgumentNullException>(() => Maybe.Some<string>(null!));
    }
}

public static class LookupTests
{
    [Test]
    public static void Lookup_PresentAndAbsent()
    {
        var cat = Helpers.SampleCatalog();
        Assert.Equal("Mouse", cat.FindProduct("MOUSE-01").Match(p => p.Name, () => "<none>"), "present");
        Assert.Equal("<none>", cat.FindProduct("NOPE").Match(p => p.Name, () => "<none>"), "absent");
    }

    [Test]
    public static void Lookup_LegitimatePrimitiveValues()
    {
        var cat = Helpers.SampleCatalog();
        Assert.Equal("some:0", cat.FindDiscountPercent("PLAIN").Match(v => $"some:{v}", () => "none"), "zero discount is present");
        Assert.Equal("none", cat.FindDiscountPercent("BOGUS").Match(v => $"some:{v}", () => "none"), "unknown code is absent");
        Assert.Equal(0, cat.FindProduct("CABLE-USB").Match(p => p.Stock, () => -1), "stock 0 found");
        Assert.Equal(0m, cat.FindProduct("STICKER-FREE").Match(p => p.UnitPrice, () => -1m), "price 0 found");
        var ok = Helpers.ValueOf(QuoteWorkflow.CreateQuote(cat, new QuoteRequest("MOUSE-01", 1, "PLAIN"))).ok;
        Assert.True(ok, "zero-discount code accepted");
    }

    [Test]
    public static void Lookup_RequiredMissingBecomesFailure()
    {
        var cat = Helpers.SampleCatalog();
        Assert.Equal("unknown_discount_code", Helpers.ErrorOf(QuoteWorkflow.ResolveDiscount(cat, "BOGUS"))!.Code, "supplied but unknown");
        Assert.Equal(0, QuoteWorkflow.ResolveDiscount(cat, null).Match(v => v, _ => -1), "not supplied => 0%");
    }
}

public static class MapBindTests
{
    [Test]
    public static void Map_Success()
    {
        int? received = null;
        var r = Result.Ok(20).Map(x => { received = x; return x + 1; });
        Assert.Equal(20, received, "received");
        Assert.Equal(21, r.Match(v => v, _ => -1), "mapped");
    }

    [Test]
    public static void Bind_Success()
    {
        int? received = null;
        var ok = Result.Ok(5).Bind(x => { received = x; return Result.Ok(x.ToString()); });
        Assert.Equal(5, received, "received");
        Assert.Equal("5", ok.Match(v => v, _ => "?"), "ok preserved");

        var err = DomainErrors.InvalidQuantity(5);
        var failed = Result.Ok(5).Bind(_ => Result.Fail<string>(err));
        Assert.Equal(err, Helpers.ErrorOf(failed), "failure returned by bind is preserved");
    }

    [Test]
    public static void FailurePropagation_MapAndBind()
    {
        var calls = 0;
        var original = DomainErrors.ItemNotFound("X");
        var start = Result.Fail<int>(original);

        var mapped = start.Map(x => { calls++; return x * 2; });
        var bound = start.Bind(x => { calls++; return Result.Ok(x * 2); });

        Assert.Equal(0, calls, "supplied functions must not run after failure");
        Assert.Equal(original, Helpers.ErrorOf(mapped), "map keeps error");
        Assert.Equal(original, Helpers.ErrorOf(bound), "bind keeps error");
    }

    [Test]
    public static void FailurePropagation_MissingItem()
    {
        var e = Helpers.ErrorOf(QuoteWorkflow.CreateQuote(Helpers.SampleCatalog(), new QuoteRequest("NOPE", -5, "BOGUS")));
        Assert.Equal("item_not_found", e!.Code, "first failure wins, later checks are skipped");
    }
}
