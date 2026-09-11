using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

sealed class Book
{
    readonly SortedDictionary<decimal, decimal> bids = new(), asks = new();
    public Level[] Bids => bids.Reverse().Take(10).Select(x => new Level(x.Key, x.Value)).ToArray();
    public Level[] Asks => asks.Take(10).Select(x => new Level(x.Key, x.Value)).ToArray();

    public void Apply(JsonElement data, bool snapshot)
    {
        if (snapshot)
        {
            bids.Clear();
            asks.Clear();
        }

        ApplySide(data, "bids", bids, true);
        ApplySide(data, "asks", asks, false);
        if (data.TryGetProperty("checksum", out var checksum) && Checksum() != checksum.GetUInt32())
            throw new InvalidDataException("Order-book checksum mismatch.");
    }

    static void ApplySide(JsonElement data, string key, SortedDictionary<decimal, decimal> side, bool descending)
    {
        if (!data.TryGetProperty(key, out var updates))
            return;
        foreach (var u in updates.EnumerateArray())
        {
            var p = u.GetProperty("price").GetDecimal();
            var q = u.GetProperty("qty").GetDecimal();
            if (p <= 0 || q < 0)
                throw new InvalidDataException("Invalid price or quantity.");
            if (q == 0)
                side.Remove(p);
            else
                side[p] = q;
        }

        foreach (var price in (descending ? side.Keys.Reverse() : side.Keys).Skip(10).ToArray())
            side.Remove(price);
    }

    public uint Checksum()
    {
        static string Digits(decimal v) => v.ToString(CultureInfo.InvariantCulture).Replace(".", "").TrimStart('0');
        var raw = string.Concat(Asks.Concat(Bids).Select(x => Digits(x.Price) + Digits(x.Qty)));
        uint crc = 0xffffffff;
        foreach (var b in Encoding.ASCII.GetBytes(raw))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
        }

        return ~crc;
    }
}
