using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UtilityAi.Actions;
using UtilityAi.Utils;

namespace Example.Action;


public sealed class NewsSearchAction : IAction
{
      private readonly HttpClient _http;
    private readonly string _apiKey;

    // You can inject HttpClient via DI; it should have a sane Timeout set.
    public NewsSearchAction(HttpClient http, string? apiKey = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("NEWSAPI_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("NEWSAPI_KEY not set.");
    }

    public string Id => "news_search";

    // Keep it permissive; safety is handled by sensors/gates elsewhere.
    public bool Gate(IBlackboard bb) => bb.GetOr("risk:safety", 0.0) < 0.7;

    public async Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;

        // Derive intent
        var locale     = bb.GetOr("context:locale", "en-US");
        var language   = ToNewsApiLanguage(locale);   // e.g., "en"
        var topic      = bb.GetOr("context:topic", "technology");
        var pageSize   = Math.Clamp(bb.GetOr("search:k", 6), 3, 20);
        var recency    = bb.GetOr("signal:recency", 0.0); // 0..1
        var fromDate   = recency >= 0.75 ? DateTime.UtcNow.AddDays(-1)
                        : recency >= 0.4 ? DateTime.UtcNow.AddDays(-3)
                        : DateTime.UtcNow.AddDays(-14);

        // Build URL (Everything endpoint: query + sort by recency)
        // Docs: https://newsapi.org/docs/endpoints/everything
        var url = new StringBuilder("https://newsapi.org/v2/everything?")
            .Append("q=").Append(Uri.EscapeDataString(topic))
            .Append("&sortBy=publishedAt")
            .Append("&pageSize=").Append(pageSize)
            .Append("&language=").Append(Uri.EscapeDataString(language))
            .Append("&from=").Append(Uri.EscapeDataString(fromDate.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")))
            .Append("&apiKey=").Append(_apiKey)
            .ToString();

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("user-agent", "UtilityAi/1.0");
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            // Write a minimal error for observability; return a failed outcome (no throw inside agent)
            bb.Set("search:error", $"NEWSAPI_{(int)resp.StatusCode}");
            return new AgentOutcome(false, 0.0, DateTimeOffset.UtcNow - t0);
        }

        // Parse JSON
        var root = JObject.Parse(body);
        var status = root["status"]?.Value<string>() ?? "error";
        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            bb.Set("search:error", $"NEWSAPI_STATUS:{status}");
            return new AgentOutcome(false, 0.0, DateTimeOffset.UtcNow - t0);
        }

        var articles = (JArray?)root["articles"] ?? new JArray();
        var now = DateTimeOffset.UtcNow;

        // Normalize to your BB items (title/url/publishedAt). You can add source.host if needed.
        var items = articles.Take(pageSize).Select(a => new
        {
            title       = a["title"]?.Value<string>() ?? "(untitled)",
            url         = a["url"]?.Value<string>() ?? "",
            publishedAt = ParseIso8601(a["publishedAt"]?.Value<string>()) ?? now
        })
        .Where(i => !string.IsNullOrWhiteSpace(i.url))
        .ToList();

        var it = JsonConvert.SerializeObject(items);
        // Write into BB
        bb.Set("search:results", it);
        bb.Set("search:count", items.Count);
        bb.Set("search:source", "newsapi");

        // Light freshness heuristic → can inform downstream “freshness” if you like
        var freshness = items.Count == 0 ? 0.0 :
            items.Average(i =>
            {
                var ageHours = (now - i.publishedAt).TotalHours;
                return ageHours <= 1 ? 1.0 : ageHours <= 24 ? 0.8 : ageHours <= 168 ? 0.6 : 0.4;
            });
        bb.Set("evidence:freshness", Math.Clamp(freshness, 0, 1));

        // Outcome: assign a small notional cost for accounting
        var latency = DateTimeOffset.UtcNow - t0;
        return new AgentOutcome(items.Count > 0, 0.01, latency);
    }

    private static DateTimeOffset? ParseIso8601(string? s)
        => DateTimeOffset.TryParse(s, out var dto) ? dto : null;

    private static string ToNewsApiLanguage(string locale)
    {
        // coarse mapping; NewsAPI expects 2-letter codes (e.g., "en", "da", "de")
        // You can improve this or pull from CultureInfo.
        var dash = locale.IndexOf('-');
        return (dash > 0 ? locale[..dash] : locale).ToLowerInvariant();
    }
}