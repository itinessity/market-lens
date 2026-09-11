using System.Text.Json;

static void Check(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}

using var sample = JsonDocument.Parse("""
{"bids":[{"price":45283.5,"qty":0.10000000},{"price":45283.4,"qty":1.54582015},{"price":45282.1,"qty":0.10000000},{"price":45281.0,"qty":0.10000000},{"price":45280.3,"qty":1.54592586},{"price":45279.0,"qty":0.07990000},{"price":45277.6,"qty":0.03310103},{"price":45277.5,"qty":0.30000000},{"price":45277.3,"qty":1.54602737},{"price":45276.6,"qty":0.15445238}],"asks":[{"price":45285.2,"qty":0.00100000},{"price":45286.4,"qty":1.54571953},{"price":45286.6,"qty":1.54571109},{"price":45289.6,"qty":1.54560911},{"price":45290.2,"qty":0.15890660},{"price":45291.8,"qty":1.54553491},{"price":45294.7,"qty":0.04454749},{"price":45296.1,"qty":0.35380000},{"price":45297.5,"qty":0.09945542},{"price":45299.5,"qty":0.18772827}],"checksum":3310070434}
""");
var book = new Book();
book.Apply(sample.RootElement, true);
Check(book.Checksum() == 3310070434, "official Kraken checksum vector");
Check(book.Bids[0].Price == 45283.5m && book.Asks[0].Price == 45285.2m, "side ordering");
using var remove = JsonDocument.Parse("{\"bids\":[{\"price\":45283.5,\"qty\":0}]}");
book.Apply(remove.RootElement, false);
Check(book.Bids.Length == 9 && book.Bids[0].Price == 45283.4m, "zero quantity deletion");
using var bad = JsonDocument.Parse("{\"checksum\":123}");
try
{
    book.Apply(bad.RootElement, false);
    throw new Exception("bad checksum accepted");
}
catch (InvalidDataException)
{
}

book.Apply(sample.RootElement, true);
Check(book.Bids.Length == 10 && book.Checksum() == 3310070434, "snapshot resynchronization");
using var extra = JsonDocument.Parse("{\"bids\":[{\"price\":46000.0,\"qty\":1.0}]}");
book.Apply(extra.RootElement, false);
Check(book.Bids.Length == 10 && book.Bids[0].Price == 46000, "depth bounded");
Console.WriteLine("6 order-book integrity checks passed.");
