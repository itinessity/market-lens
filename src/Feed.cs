using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

sealed class Feed(bool fixture)
{
    readonly object gate = new();
    readonly Dictionary<string, Book> books = new();
    readonly Dictionary<string, decimal> prices = new();
    readonly Queue<Frame> history = new();
    DateTimeOffset observed;
    DateTimeOffset lastSample;
    string state = "connecting";
    int reconnects, checksumFailures;
    public Frame View()
    {
        lock (gate)
        {
            var status = state == "live" && DateTimeOffset.UtcNow - observed > TimeSpan.FromSeconds(15) ? "stale" : state;
            return new(observed, status, fixture ? "test fixtures" : "Kraken WebSocket v2", reconnects, checksumFailures, new[] { "BTC/USD", "ETH/USD" }.Select(s =>
            {
                books.TryGetValue(s, out var b);
                var bids = b?.Bids ?? [];
                var asks = b?.Asks ?? [];
                return new Instrument(s, bids, asks, prices.TryGetValue(s, out var p) ? p : null, bids.Length > 0 && asks.Length > 0 ? asks[0].Price - bids[0].Price : null);
            }).ToArray());
        }
    }

    public Frame[] History()
    {
        lock (gate)
            return history.ToArray();
    }

    void Sample()
    {
        if (DateTimeOffset.UtcNow - lastSample < TimeSpan.FromSeconds(1))
            return;
        lastSample = DateTimeOffset.UtcNow;
        history.Enqueue(View());
        while (history.Count > 120)
            history.Dequeue();
    }

    public async Task Run(CancellationToken stop)
    {
        if (fixture)
        {
            lock (gate)
            {
                foreach (var s in new[]
                {
                    "BTC/USD",
                    "ETH/USD"
                }

                )
                {
                    var book = new Book();
                    using var doc = JsonDocument.Parse("{\"bids\":[{\"price\":100.0,\"qty\":2.0}],\"asks\":[{\"price\":101.0,\"qty\":3.0}]}");
                    book.Apply(doc.RootElement, true);
                    books[s] = book;
                    prices[s] = 100.5m;
                }

                observed = DateTimeOffset.UtcNow;
                state = "test fixtures";
                Sample();
            }

            return;
        }

        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
                await ws.ConnectAsync(new Uri("wss://ws.kraken.com/v2"), stop);
                lock (gate)
                {
                    books.Clear();
                    prices.Clear();
                    state = "connecting";
                }

                foreach (var channel in new[]
                {
                    "book",
                    "ticker"
                }

                )
                {
                    var message = channel == "book" ? "{\"method\":\"subscribe\",\"params\":{\"channel\":\"book\",\"symbol\":[\"BTC/USD\",\"ETH/USD\"],\"depth\":10}}" : "{\"method\":\"subscribe\",\"params\":{\"channel\":\"ticker\",\"symbol\":[\"BTC/USD\",\"ETH/USD\"]}}";
                    await ws.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, stop);
                }

                var buffer = new byte[16384];
                while (ws.State == WebSocketState.Open && !stop.IsCancellationRequested)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    using var memory = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                            throw new IOException("Feed closed.");
                        memory.Write(buffer, 0, result.Count);
                        if (memory.Length > 1_000_000)
                            throw new InvalidDataException("Oversized feed message.");
                    }
                    while (!result.EndOfMessage);
                    using var json = JsonDocument.Parse(memory.ToArray());
                    var root = json.RootElement;
                    if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
                        throw new IOException("Subscription rejected.");
                    if (!root.TryGetProperty("channel", out var channelName))
                        continue;
                    var channel = channelName.GetString();
                    lock (gate)
                    {
                        if (channel is "book" or "ticker")
                        {
                            foreach (var data in root.GetProperty("data").EnumerateArray())
                            {
                                var symbol = data.GetProperty("symbol").GetString()!;
                                if (channel == "book")
                                {
                                    if (!books.TryGetValue(symbol, out var book))
                                    {
                                        book = new();
                                        books[symbol] = book;
                                    }

                                    try
                                    {
                                        book.Apply(data, root.GetProperty("type").GetString() == "snapshot");
                                    }
                                    catch (InvalidDataException)
                                    {
                                        checksumFailures++;
                                        throw;
                                    }
                                }
                                else if (data.TryGetProperty("last", out var price))
                                    prices[symbol] = price.GetDecimal();
                            }
                        }

                        observed = DateTimeOffset.UtcNow;
                        if (books.Count == 2)
                            state = "live";
                        Sample();
                    }
                }
            }
            catch (Exception ex)when (ex is WebSocketException or IOException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
            {
                lock (gate)
                {
                    state = "reconnecting";
                    books.Clear();
                    prices.Clear();
                    reconnects++;
                }

                if (stop.IsCancellationRequested)
                    break;
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, 2 + reconnects)), stop);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
