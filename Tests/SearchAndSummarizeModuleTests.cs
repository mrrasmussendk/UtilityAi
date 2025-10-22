using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Example.SearchAndSummerizeModule;
using Example.SearchAndSummerizeModule.DTO;
using UtilityAi.Actions;
using UtilityAi.Consideration;
using UtilityAi.Utils;
using Xunit;

#pragma warning disable OPENAI001
namespace Tests;

public class SearchAndSummarizeModuleTests
{
    private sealed class FakeSearch : IAction<SearchQuery, SearchResults>
    {
        public Task<SearchResults> ActAsync(SearchQuery request, CancellationToken ct)
            => Task.FromResult(new SearchResults(new List<NewsItem> { new("t","http://x", System.DateTimeOffset.UtcNow)}));
    }

    private sealed class FakeSum : IAction<ISearchResults, Summary>
    {
        public Task<Summary> ActAsync(ISearchResults request,  CancellationToken ct)
            => Task.FromResult(new Summary("ok"));
    }

    private static Runtime MakeRt() => new(new EventBus(), new UserIntent("news"), 0);

    [Fact]
    public void Propose_Search_WhenNoResultsButHasTopic()
    {
        var rt = MakeRt();
        rt.Bus.Publish(new Topic("AI"));
        var mod = new SearchAndSummarizeModule(new FakeSearch(), new FakeSum());
        var props = mod.Propose(rt).ToList();
        Assert.Single(props);
        Assert.Equal("news.search", props[0].Id);
    }

    [Fact]
    public async Task Propose_Summarize_WhenHasResultsAndNoSummary_ActPublishesSummary()
    {
        var rt = MakeRt();
        rt.Bus.Publish(new Topic("AI"));
        var mod = new SearchAndSummarizeModule(new FakeSearch(), new FakeSum());
        // first: search
        var search = mod.Propose(rt).Single();
        await search.Act(CancellationToken.None);
        Assert.NotNull(rt.Bus.GetOrDefault<SearchResults>());
        // second: summarize
        var sumProp = mod.Propose(rt).Single();
        Assert.Equal("news.summarize", sumProp.Id);
        await sumProp.Act(CancellationToken.None);
        Assert.Equal("ok", rt.Bus.GetOrDefault<Summary>()?.Text);
    }

    [Fact]
    public void Propose_None_WhenHasResultsAndSummary()
    {
        var rt = MakeRt();
        rt.Bus.Publish(new SearchResults(new List<NewsItem>()));
        rt.Bus.Publish(new Summary("y"));
        var mod = new SearchAndSummarizeModule(new FakeSearch(), new FakeSum());
        var props = mod.Propose(rt).ToList();
        Assert.Empty(props);
    }
}
