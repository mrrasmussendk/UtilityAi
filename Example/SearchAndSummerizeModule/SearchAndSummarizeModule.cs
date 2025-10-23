using System.Diagnostics.CodeAnalysis;
using Example.SearchAndSummerizeModule.DTO;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Utils;

namespace Example.SearchAndSummerizeModule;

// Team A Module
public sealed class SearchAndSummarizeModule(UtilityAi.Actions.IAction<SearchQuery, SearchResults> search, UtilityAi.Actions.IAction<ISearchResults, Summary> sum) : ICapabilityModule
{
    [Experimental("OPENAI001")]
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Propose "Search"
        if (rt.Bus.GetOrDefault<SearchResults>() is null)
        {
            yield return new Proposal(
                id: "news.search",
                cons: new IConsideration[]
                {
                    new HasFact<SearchResults>(shouldHave: false),
                    new HasFact<Topic>(true)
                },
                act: async ct =>
                {
                    var  topic = rt.Bus.GetOrDefault<Topic>()!;
                    var res = await search.ActAsync(new SearchQuery(topic.Name), ct);
                    rt.Bus.Publish(res);
                },
                eligibilities: new IEligibility[]
                {
                    new HasFactEligible<Topic>(),
                    new NotHasFactEligible<SearchResults>()
                }
            );
        }

        // Propose "Summarize"
        if (rt.Bus.GetOrDefault<SearchResults>() is not null && rt.Bus.GetOrDefault<Summary>() is null)
        {
            var results = rt.Bus.GetOrDefault<SearchResults>()!;
            yield return new Proposal(
                id: "news.summarize",
                cons: new IConsideration[]
                {
                    new HasFact<SearchResults>(true),
                    new HasFact<Summary>(false),
                },
                act: async ct =>
                {
                    var sumRes = await sum.ActAsync(results, ct);
                    rt.Bus.Publish(sumRes);
                }
            );
        }
    }
}