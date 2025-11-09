using Example.Openai;
using Example.OutputModule;
using Example.OutputModule.Actions;
using Example.SearchAndSummerizeModule;
using Example.SearchAndSummerizeModule.Actions;
using Example.Sensor;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

var http = new HttpClient {Timeout = TimeSpan.FromSeconds(60)};
var openai = new OpenAiClient();
var bus = new EventBus();
var intent = new UserIntent(
    Goal: new IntentGoal("search-and-summarize"),
    Slots: new Dictionary<string, object?>
    {
        ["query"] = "latest news",
        ["delivery"] = "sms"
    }
);

var orch = new UtilityAiOrchestrator(null, true, bus)
    // Sensors
    .AddSensor(new IntentSensor())
    .AddSensor(new TopicSensor(openai))
    .AddSensor(new SummaryToOutputAdapter())
    //Modules
    .AddModule(new OutputModule(new TwilloOutputAction()))
    .AddModule(new SearchAndSummarizeModule(new NewsSearchAction(http), new SummarizerAction(openai)));

Console.WriteLine("== Utility-AI (Sensors + Considerations) Demo ==\n");
await orch.RunAsync(intent, 12, CancellationToken.None);

Console.WriteLine("\n== Final Facts ==");