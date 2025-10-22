using System.Text;
using Example.SearchAndSummerizeModule.DTO;
using Newtonsoft.Json.Linq;
using UtilityAi.Actions;
using UtilityAi.Utils;

namespace Example.SearchAndSummerizeModule.Actions;

public sealed class NewsSearchAction(HttpClient http) : IAction<SearchQuery, SearchResults>
{
    private const string UserAgent = "UtilityAi/1.0";

    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));
    private readonly string _apiKey = Environment.GetEnvironmentVariable("NEWSAPI_KEY") ?? "";

    public async Task<SearchResults> ActAsync(SearchQuery request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        // 2) Build request URL
        var url = BuildEverythingUrl(request.Text, "da-dk", 4, now.Date.AddDays(-1));

        // 3) Execute HTTP request
        string body;
        int statusCode;
        try
        {
            (body, statusCode) = await FetchAsync(url, ct);
        }
        catch (OperationCanceledException)
        {
            return new SearchResults(new List<NewsItem>());
        }
        catch (Exception)
        {
            return new SearchResults(new List<NewsItem>());
        }

        if (!TryParseStatusAndArticles(body, 1, now, out var items, out var error))
        {
            return new SearchResults(new List<NewsItem>());
        }

        return new SearchResults(items);
    }

    private string BuildEverythingUrl(string topic, string language, int pageSize, DateTime fromDate)
    {
        return new StringBuilder("https://newsapi.org/v2/everything?")
            .Append("q=").Append(Uri.EscapeDataString(topic))
            .Append("&sortBy=publishedAt")
            .Append("&pageSize=").Append(pageSize)
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

    private static DateTimeOffset? ParseIso8601(string? s)
        => DateTimeOffset.TryParse(s, out var dto) ? dto : null;
}