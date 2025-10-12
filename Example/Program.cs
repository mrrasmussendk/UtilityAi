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
    Query: "latest news",
    Delivery: "sms",
    Topic: "Tech");

var orch = new UtilityAiOrchestrator()
    // Sensors
    .AddSensor(new TopicSensor(openai))
    // Modules
    .AddModule(new OutputModule(new TwilloOutputAction()))
    .AddModule(new SearchAndSummarizeModule(new NewsSearchAction(http), new SummarizerAction(openai)));


Console.WriteLine("== Utility-AI (Sensors + Considerations) Demo ==\n");
await orch.RunAsync(bus, intent, maxTicks: 12, ct: CancellationToken.None);

Console.WriteLine("\n== Final Facts ==");