namespace Example.SearchAndSummerizeModule.DTO;
public interface ISearchQuery { string Text { get; } }
public interface ISearchResults { IReadOnlyList<NewsItem> Items { get; } }

public sealed record SearchQuery(string Text) : ISearchQuery;
public sealed record SearchResults(IReadOnlyList<NewsItem> Items) : ISearchResults;
public sealed record Summary(string Text);

public record Topic(string Name);
public sealed class NewsItem
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