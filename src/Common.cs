using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;

static class Setup
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };
    public static WebApplication Create(string[] args)
    {
        var b = WebApplication.CreateBuilder(args);
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
            b.WebHost.UseUrls("http://127.0.0.1:8080");
        b.Services.AddHttpClient("public", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("ItinessityPortfolio/1.0 (+https://itinessity.com)");
        });
        b.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 65536);
        var app = b.Build();
        app.Use(async (ctx, next) =>
        {
            if (HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Headers.TryGetValue("Origin", out var origin) && origin.ToString() != $"{ctx.Request.Scheme}://{ctx.Request.Host}")
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsJsonAsync(new { error = "Cross-origin writes are not accepted." });
                return;
            }

            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            try
            {
                await next();
            }
            catch (ArgumentException ex)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
            catch (HttpRequestException)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new { error = "The public data provider is unavailable. Please retry later." });
            }
            catch (TaskCanceledException)
            {
                if (!ctx.RequestAborted.IsCancellationRequested)
                {
                    ctx.Response.StatusCode = 503;
                    await ctx.Response.WriteAsJsonAsync(new { error = "The public data provider timed out. Please retry later." });
                }
            }
        });
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapGet("/api/health", () => new { status = "ok", mode = Fixture(app) ? "test fixtures" : "live public APIs" });
        return app;
    }

    public static bool Fixture(WebApplication app) => app.Environment.IsEnvironment("Testing") && app.Configuration["TestFixtures"] == "true";
    public static HttpClient Http(WebApplication app) => app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("public");
}

sealed class Store<T>
{
    readonly string dir;
    readonly object gate = new();
    public Store(string name)
    {
        dir = Path.Combine(Environment.GetEnvironmentVariable("DATA_DIR") ?? ".data", name);
        Directory.CreateDirectory(dir);
    }

    public string Save(T value)
    {
        lock (gate)
        {
            if (Directory.GetFiles(dir, "*.json").Length >= 200)
                throw new ArgumentException("Local storage is full (200 records). Export and remove old records before continuing.");
            var id = Guid.NewGuid().ToString("N");
            var path = Path.Combine(dir, id + ".json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value, Setup.Json));
            File.Move(path + ".tmp", path);
            return id;
        }
    }

    public T? Get(string id) => Guid.TryParseExact(id, "N", out _) && File.Exists(Path.Combine(dir, id + ".json")) ? JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(dir, id + ".json")), Setup.Json) : default;
    public string[] List(int limit = 30) => Directory.GetFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(limit).Select(Path.GetFileNameWithoutExtension).OfType<string>().ToArray();
}
