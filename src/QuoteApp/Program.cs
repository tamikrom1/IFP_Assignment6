using QuoteApp.Common;
using QuoteApp.Domain;
using QuoteApp.Infrastructure;

const string DefaultCatalog = "data/catalog.json";

if (args.Length == 0 || args[0] is not ("demo" or "quote"))
{
    Console.WriteLine("Usage:\n  demo\n  quote <SKU> <QTY> [DISCOUNT_CODE] [--catalog <path>]");
    return 2;
}

return args[0] == "demo" ? RunDemo() : RunQuote(args.Skip(1).ToArray());

static int RunQuote(string[] a)
{
    var catalogPath = DefaultCatalog;
    var positional = new List<string>();
    for (var i = 0; i < a.Length; i++)
    {
        if (a[i] == "--catalog" && i + 1 < a.Length) catalogPath = a[++i];
        else positional.Add(a[i]);
    }

    if (positional.Count < 2 || !int.TryParse(positional[1], out var qty))
    {
        Console.WriteLine("Usage: quote <SKU> <QTY (integer)> [DISCOUNT_CODE] [--catalog <path>]");
        return 2;
    }

    var request = new QuoteRequest(positional[0], qty, positional.Count > 2 ? positional[2] : null);
    return Execute(catalogPath, request);
}

static int RunDemo()
{
    var demos = new (string Title, string Path, QuoteRequest Request)[]
    {
        ("1. Normal path",                         "data/catalog.json",   new("LAPTOP-15", 2, "WELCOME10")),
        ("2. Domain failure: invalid quantity",    "data/catalog.json",   new("MOUSE-01", 0, null)),
        ("3. Domain failure: item not found",      "data/catalog.json",   new("NOPE-999", 1, null)),
        ("4. Domain failure: insufficient stock",  "data/catalog.json",   new("CABLE-USB", 1, null)),
        ("5. Domain failure: item discontinued",   "data/catalog.json",   new("KEYBOARD-OLD", 1, null)),
        ("6. Lookup present: code with 0% (valid)", "data/catalog.json",  new("MOUSE-01", 2, "PLAIN")),
        ("7. Lookup missing: unknown discount code", "data/catalog.json", new("MOUSE-01", 2, "BOGUS")),
        ("8. File failure: missing file",          "data/does-not-exist.json", new("MOUSE-01", 1, null)),
        ("9. File failure: malformed content",     "data/malformed.json", new("MOUSE-01", 1, null)),
    };

    foreach (var (title, path, request) in demos)
    {
        Console.WriteLine($"--- {title}");
        Execute(path, request);
        Console.WriteLine();
    }

    Console.WriteLine("--- 10. State failure: confirming an already confirmed quote");
    var outcome = CatalogLoader.Load("data/catalog.json")
        .Bind(c => QuoteWorkflow.RunKeepingCatalog(c, new QuoteRequest("MOUSE-01", 1, null))
            .Bind(done => QuoteWorkflow.Confirm(done.UpdatedCatalog, done.Quote)));
    Report(outcome.Map(x => QuoteView.From(x)));
    return 0;
}

static int Execute(string catalogPath, QuoteRequest request) =>
    Report(CatalogLoader.Load(catalogPath).Bind(c => QuoteWorkflow.Run(c, request)));

static int Report(Result<QuoteView> outcome) =>
    outcome.Match(
        view => { Console.WriteLine($"OK: {view.Summary}"); return 0; },
        error =>
        {
            Console.WriteLine($"{(error.Kind == ErrorKind.Domain ? "REJECTED" : "PROBLEM")} {error}");
            return error.Kind == ErrorKind.Domain ? 1 : 3;
        });
