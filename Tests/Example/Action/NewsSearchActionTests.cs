using System.Net;
using System.Net.Http;
using System.Text;
using Example.Action;
using Newtonsoft.Json.Linq;
using UtilityAi.Utils;

namespace Tests.Example.Action;

public class NewsSearchActionTests
{
    private sealed class TestHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;
        public Uri? LastRequestUri { get; private set; }
        public TestHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        {
            _responder = responder;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            // Let caller simulate cancel/exception by throwing from responder
            var resp = _responder(request, cancellationToken);
            return Task.FromResult(resp);
        }
    }

    private static (NewsSearchAction action, Blackboard bb, TestHandler handler) MakeSut(string responseJson, HttpStatusCode code = HttpStatusCode.OK)
    {
        var handler = new TestHandler((_, __) => new HttpResponseMessage(code)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });
        var http = new HttpClient(handler);
        var action = new NewsSearchAction(http, apiKey: "test-key");
        var bb = new Blackboard();
        return (action, bb, handler);
    }

    [Fact]
    public void Gate_True_When_Safety_Below_Threshold_And_False_Otherwise()
    {
        var (_, bb, _) = MakeSut("{\"status\":\"ok\",\"articles\":[]}");
        var http = new HttpClient(new HttpClientHandler());
        var action = new NewsSearchAction(http, apiKey: "k");

        bb.Set("risk:safety", 0.69);
        Assert.True(action.Gate(bb));
        bb.Set("risk:safety", 0.70);
        Assert.False(action.Gate(bb));
        bb.Set("risk:safety", 0.71);
        Assert.False(action.Gate(bb));
    }

    [Fact]
    public async Task ActAsync_Success_Writes_Results_Count_Source_And_Freshness()
    {
        var now = DateTimeOffset.UtcNow;
        var articleTime = now.AddHours(-2).ToString("O");
        var json = $"{{\n  \"status\": \"ok\",\n  \"articles\": [{{\n     \"title\": \"T1\", \"url\": \"https://ex.com/a\", \"publishedAt\": \"{articleTime}\"\n  }}]\n}}";

        var (sut, bb, _) = MakeSut(json);

        var outcome = await sut.ActAsync(bb, CancellationToken.None);
        Assert.True(outcome.Success);
        Assert.Equal("newsapi", bb.GetOr("search:source", ""));
        Assert.Equal(1, bb.GetOr("search:count", 0));
        var resultsJson = bb.GetOr("search:results", "[]");
        var arr = JArray.Parse(resultsJson);
        Assert.Single(arr);
        Assert.True(bb.GetOr("evidence:freshness", 0.0) > 0.0);
    }

    [Fact]
    public async Task ActAsync_Non2xx_Sets_Error_And_Fails()
    {
        var (sut, bb, _) = MakeSut("{}", HttpStatusCode.BadRequest);
        var outcome = await sut.ActAsync(bb, CancellationToken.None);
        Assert.False(outcome.Success);
        Assert.Equal("NEWSAPI_400", bb.GetOr("search:error", ""));
    }

    [Fact]
    public async Task ActAsync_Status_Not_Ok_Sets_Error_And_Fails()
    {
        var (sut, bb, _) = MakeSut("{\"status\":\"error\"}");
        var outcome = await sut.ActAsync(bb, CancellationToken.None);
        Assert.False(outcome.Success);
        Assert.Equal("NEWSAPI_STATUS:error", bb.GetOr("search:error", ""));
    }

    [Fact]
    public async Task ActAsync_Exception_Sets_Generic_Error_And_Fails()
    {
        var handler = new TestHandler((_, __) => throw new HttpRequestException("boom"));
        var sut = new NewsSearchAction(new HttpClient(handler), apiKey: "k");
        var bb = new Blackboard();
        var outcome = await sut.ActAsync(bb, CancellationToken.None);
        Assert.False(outcome.Success);
        Assert.Equal("NEWSAPI_EXCEPTION", bb.GetOr("search:error", ""));
    }

    [Fact]
    public async Task ActAsync_Cancellation_Returns_False_Without_Error()
    {
        var handler = new TestHandler((_, __) => throw new OperationCanceledException());
        var sut = new NewsSearchAction(new HttpClient(handler), apiKey: "k");
        var bb = new Blackboard();
        var outcome = await sut.ActAsync(bb, new CancellationToken(true));
        Assert.False(outcome.Success);
        Assert.False(bb.Has("search:error"));
    }

    [Fact]
    public async Task ActAsync_PageSize_Clamped_And_Language_Mapped()
    {
        var (sut, bb, handler) = MakeSut("{\"status\":\"ok\",\"articles\":[]}");
        bb.Set("context:topic", "machine learning");
        bb.Set("context:locale", "pt-BR");
        bb.Set("search:k", 100); // should clamp to 20
        var outcome = await sut.ActAsync(bb, CancellationToken.None);
        Assert.False(outcome.Success); // no articles
        var uri = handler.LastRequestUri!;
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal("pt", query.Get("language"));
        Assert.Equal("20", query.Get("pageSize"));
        Assert.Equal("machine%20learning", uri.Query.Contains("q=machine%20learning") ? "machine%20learning" : null);
    }

    [Fact]
    public async Task ActAsync_Filters_Articles_Without_Url()
    {
        var json = "{\n  \"status\": \"ok\",\n  \"articles\": [\n    { \"title\": \"A\", \"url\": \"\" },\n    { \"title\": \"B\", \"url\": null }\n  ]\n}";
        var (sut, bb, _) = MakeSut(json);
        var outcome = await sut.ActAsync(bb, CancellationToken.None);
        Assert.False(outcome.Success); // no valid URL items
        Assert.Equal(0, bb.GetOr("search:count", -1));
        var resultsJson = bb.GetOr("search:results", "[]");
        var arr = JArray.Parse(resultsJson);
        Assert.Empty(arr);
    }
}
