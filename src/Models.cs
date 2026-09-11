using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

record Candle(long Time, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);
record Level(decimal Price, decimal Qty);
record Instrument(string Symbol, Level[] Bids, Level[] Asks, decimal? Last, decimal? Spread);
record Frame(DateTimeOffset ObservedAt, string Status, string Source, int Reconnects, int ChecksumFailures, Instrument[] Instruments);
record Recording(DateTimeOffset SavedAt, string Source, Frame[] Frames);
