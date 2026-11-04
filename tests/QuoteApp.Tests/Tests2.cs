// Member B: tests for the file/JSON exception boundary and state preservation.
using QuoteApp.Common;
using QuoteApp.Domain;
using QuoteApp.Infrastructure;

namespace QuoteApp.Tests;

public static class BoundaryTests
{
    [Test]
    public static void MissingFile_Reports()
    {
        var store = new CatalogStore(Helpers.SampleCatalog());
        var before = store.Current;
        var path = Helpers.TempPath();
        var e = Helpers.ErrorOf(store.Reload(path));
        Assert.Equal("file_not_found", e!.Code, "code");
        Assert.Equal(ErrorKind.Operational, e.Kind, "kind");
        Assert.True(e.Message.Contains(path), "message names the file");
        Assert.True(ReferenceEquals(before, store.Current), "state unchanged");
    }

    [Test]
    public static void MalformedJson_BecomesClearFailure()
    {
        Helpers.WithTempFile("""{ "products": [ { "sku": "A", "name": """, path =>
        {
            var store = new CatalogStore(Helpers.SampleCatalog());
            var before = store.Current;
            var e = Helpers.ErrorOf(store.Reload(path));
            Assert.Equal("malformed_content", e!.Code, "code");
            Assert.True(e.Message.Contains(path), "message names the file");
            Assert.True(ReferenceEquals(before, store.Current), "state unchanged");
        });
    }

    [Test]
    public static void MalformedContent_MissingFields()
    {
        var cases = new[]
        {
            """{ "products": [ { "sku": "A", "name": "A", "stock": 1, "active": true } ] }""",
            """{ "products": [ { "sku": "A", "name": "A", "unitPrice": -1, "stock": 1, "active": true } ] }""",
            """{ "products": [ { "sku": "A", "name": "A", "unitPrice": 1, "stock": 1, "active": true }, { "sku": "a", "name": "B", "unitPrice": 1, "stock": 1, "active": true } ] }""", // duplicate
            """{ "discountCodes": {} }""",
            """{ "products": [], "discountCodes": { "X": 101 } }""",
        };
        foreach (var json in cases)
            Assert.Equal("malformed_content", Helpers.ErrorOf(CatalogLoader.Parse(json, "test"))!.Code, json);
    }

    [Test]
    public static void ValidFile_LoadsAndReplacesState()
    {
        Helpers.WithTempFile(
            """{ "products": [ { "sku": "Z", "name": "Zero", "unitPrice": 0, "stock": 0, "active": false } ], "discountCodes": { "NONE": 0 } }""",
            path =>
            {
                var store = new CatalogStore(Helpers.SampleCatalog());
                Assert.True(Helpers.ValueOf(store.Reload(path)).ok, "reload succeeds");
                var z = store.Current.FindProduct("Z").Match(p => p, () => throw new AssertionException("Z missing"));
                Assert.Equal(0m, z.UnitPrice, "price 0 kept");
                Assert.Equal(0, z.Stock, "stock 0 kept");
                Assert.Equal(false, z.Active, "active false kept");
                Assert.True(store.Current.FindProduct("MOUSE-01").Match(_ => false, () => true), "old data replaced as a whole");
            });
    }

    [Test]
    public static void Reserve_DoesNotMutate()
    {
        var cat = Helpers.SampleCatalog();
        var failed = cat.Reserve("MOUSE-01", 1000);
        Assert.Equal("insufficient_stock", Helpers.ErrorOf(failed)!.Code, "rejected");
        Assert.Equal(100, cat.FindProduct("MOUSE-01").Match(p => p.Stock, () => -1), "original stock untouched after failure");
        var ok = cat.Reserve("MOUSE-01", 1);
        Assert.Equal(100, cat.FindProduct("MOUSE-01").Match(p => p.Stock, () => -1), "original stock untouched after success");
        Assert.Equal(99, ok.Match(c => c.FindProduct("MOUSE-01").Match(p => p.Stock, () => -1), _ => -2), "new catalog has reduced stock");
    }
}
