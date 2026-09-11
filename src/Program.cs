using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var app = Setup.Create(args);
var feed = new Feed(Setup.Fixture(app));
var store = new Store<Recording>("recordings");
var http = Setup.Http(app);
app.Lifetime.ApplicationStarted.Register(() => _ = feed.Run(app.Lifetime.ApplicationStopping));
app.MapGet("/api/market", () => feed.View());
app.MapGet("/api/candles", async (string? symbol) =>
{
    var pair = symbol switch
    {
        null or "BTC/USD" => "XBTUSD",
        "ETH/USD" => "ETHUSD",
        _ => throw new ArgumentException("Choose BTC/USD or ETH/USD.")};
    if (Setup.Fixture(app))
        return Results.Ok(new { source = "test fixtures", completedOnly = true, candles = new[] { new Candle(1700000000, 100, 105, 98, 102, 12), new Candle(1700003600, 102, 108, 101, 107, 8) } });
    using var json = JsonDocument.Parse(await http.GetStringAsync("https://api.kraken.com/0/public/OHLC?pair=" + pair + "&interval=60"));
    if (json.RootElement.GetProperty("error").GetArrayLength() > 0)
        throw new HttpRequestException();
    var data = json.RootElement.GetProperty("result").EnumerateObject().First(x => x.Name != "last").Value;
    var rows = data.EnumerateArray().SkipLast(1).TakeLast(120).Select(x => new Candle(x[0].GetInt64(), DecimalValue(x[1]), DecimalValue(x[2]), DecimalValue(x[3]), DecimalValue(x[4]), DecimalValue(x[6]))).ToArray();
    return Results.Ok(new { source = "Kraken REST / 1-hour OHLC", completedOnly = true, candles = rows });
});
app.MapPost("/api/recordings", () =>
{
    var frames = feed.History();
    if (frames.Length == 0)
        return Results.Json(new { error = "No feed observations are available to record." }, statusCode: 409);
    var record = new Recording(DateTimeOffset.UtcNow, Setup.Fixture(app) ? "test fixtures" : "Kraken WebSocket v2", frames);
    var id = store.Save(record);
    return Results.Ok(new { id, frames = frames.Length });
});
app.MapGet("/api/recordings", () => store.List());
app.MapGet("/api/recordings/{id}", (string id) => store.Get(id)is { } r ? Results.Ok(r) : Results.NotFound(new { error = "Recording not found." }));
app.Run();
static decimal DecimalValue(JsonElement x) => x.ValueKind == JsonValueKind.String ? decimal.Parse(x.GetString()!, CultureInfo.InvariantCulture) : x.GetDecimal();
