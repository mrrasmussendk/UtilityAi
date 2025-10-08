using System.Text;
using Example.Action.Considerations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UtilityAi.Actions;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.Action;

public sealed class NewsSearchAction : IAction
{
    private const string UserAgent = "UtilityAi/1.0";

    private readonly HttpClient _http;
    private readonly string _apiKey;

    private List<IConsideration> _considerations = new List<IConsideration>();

    // You can inject HttpClient via DI; it should have a sane Timeout set.
    public NewsSearchAction(HttpClient http, string? apiKey = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("NEWSAPI_KEY") ?? "";
        _considerations = new List<IConsideration>()
        {
            new HasValueConsideration("answer:text", true),
            new HasValueConsideration("search:results", true),
            new HasValueConsideration("context:topic")
        };
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("NEWSAPI_KEY not set.");
    }

    public string Id => "news_search";

    public bool Gate(IBlackboard bb)
    {
        var safety = bb.GetOr("risk:safety", 0.0);
        return safety < 0.70; // True if below threshold
    }

    public async Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var now = DateTimeOffset.UtcNow;

        // 1) Derive intent
        var locale = bb.GetOr("context:locale", "en-US");
        var language = ToNewsApiLanguage(locale); // e.g., "en"
        var topic = bb.GetOr("context:topic", "technology");
        var pageSize = Math.Clamp(bb.GetOr("search:k", 6), 3, 20);
        var recency = bb.GetOr("signal:recency", 0.0); // 0..1
        var fromDate = DetermineFromDate(recency);

        // 2) Build request URL
        var url = BuildEverythingUrl(topic, language, pageSize, fromDate);

        // 3) Execute HTTP request
        string body;
        int statusCode;
        try
        {
            (body, statusCode) = await FetchAsync(url, ct);
        }
        catch (OperationCanceledException)
        {
            // Respect cancellation without marking as an error on the blackboard
            return new AgentOutcome(false, 0.0, DateTimeOffset.UtcNow - startedAt);
        }
        catch (Exception)
        {
            // Network/parse issues should not crash the agent; surface a generic error
            bb.Set("search:error", "NEWSAPI_EXCEPTION");
            return new AgentOutcome(false, 0.0, DateTimeOffset.UtcNow - startedAt);
        }

        if (statusCode < 200 || statusCode >= 300)
        {
            bb.Set("search:error", $"NEWSAPI_{statusCode}");
            return new AgentOutcome(false, 0.0, DateTimeOffset.UtcNow - startedAt);
        }

        // 4) Parse and normalize
        if (!TryParseStatusAndArticles(body, pageSize, now, out var items, out var error))
        {
            bb.Set("search:error", error ?? "NEWSAPI_STATUS:error");
            return new AgentOutcome(false, 0.0, DateTimeOffset.UtcNow - startedAt);
        }

        // 5) Write results to blackboard
        var serialized = JsonConvert.SerializeObject(items);
        bb.Set("search:results", serialized);
        bb.Set("search:count", items.Count);
        bb.Set("search:source", "newsapi");

        // 6) Freshness evidence
        var freshness = ComputeFreshness(items, now);
        bb.Set("evidence:freshness", Math.Clamp(freshness, 0, 1));

        // 7) Outcome: assign a small notional cost for accounting
        var latency = DateTimeOffset.UtcNow - startedAt;
        return new AgentOutcome(items.Count > 0, 0.01, latency);
    }

    public double Score(IBlackboard bb)
    {
        if (!Gate(bb)) return 0.0;
        return Scoring.AggregateWithMakeup(_considerations, bb);
    }

    private static DateTime DetermineFromDate(double recency)
        => recency >= 0.75 ? DateTime.UtcNow.AddDays(-1)
            : recency >= 0.4 ? DateTime.UtcNow.AddDays(-3)
            : DateTime.UtcNow.AddDays(-14);

    private string BuildEverythingUrl(string topic, string language, int pageSize, DateTime fromDate)
    {
        return new StringBuilder("https://newsapi.org/v2/everything?")
            .Append("q=").Append(Uri.EscapeDataString(topic))
            .Append("&sortBy=publishedAt")
            .Append("&pageSize=").Append(pageSize)
            .Append("&language=").Append(Uri.EscapeDataString(language))
            .Append("&from=").Append(Uri.EscapeDataString(fromDate.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")))
            .Append("&apiKey=").Append(_apiKey)
            .ToString();
    }

    private async Task<(string Body, int StatusCode)> FetchAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        // Use TryAddWithoutValidation to avoid rare header validation issues on some platforms
        req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        // Buffer the response content to ensure it's fully available when we read it
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        var content = resp.Content;
        var body = content is null ? string.Empty : await content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return (body, (int) resp.StatusCode);
    }

    private static bool TryParseStatusAndArticles(string body, int pageSize, DateTimeOffset now,
        out List<NewsItem> items, out string? error)
    {
        items = new List<NewsItem>();
        error = null;

        var root = JObject.Parse(body);
        var status = root["status"]?.Value<string>() ?? "error";
        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            error = $"NEWSAPI_STATUS:{status}";
            return false;
        }

        var articles = (JArray?) root["articles"] ?? new JArray();

        items = articles
            .Take(pageSize)
            .Select(a => new NewsItem(
                title: a["title"]?.Value<string>() ?? "(untitled)",
                url: a["url"]?.Value<string>() ?? string.Empty,
                publishedAt: ParseIso8601(a["publishedAt"]?.Value<string>()) ?? now
            ))
            .Where(i => !string.IsNullOrWhiteSpace(i.url))
            .ToList();

        return true;
    }

    private static double ComputeFreshness(IReadOnlyCollection<NewsItem> items, DateTimeOffset now)
    {
        if (items.Count == 0) return 0.0;
        return items.Average(i =>
        {
            var ageHours = (now - i.publishedAt).TotalHours;
            return ageHours <= 1 ? 1.0 : ageHours <= 24 ? 0.8 : ageHours <= 168 ? 0.6 : 0.4;
        });
    }

    private sealed class NewsItem
    {
        public string title { get; }
        public string url { get; }
        public DateTimeOffset publishedAt { get; }

        public NewsItem(string title, string url, DateTimeOffset publishedAt)
        {
            this.title = title;
            this.url = url;
            this.publishedAt = publishedAt;
        }
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