// See https://aka.ms/new-console-template for more information

using Example.Action;
using Example.Openai;
using Example.Orchestrator;
using Example.Sensor;
using UtilityAi.Actions;
using UtilityAi.Policies;
using UtilityAi.Sensor;
using UtilityAi.Utils;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
var bb = new Blackboard();
bb.Set("prompt", "Get the latest tech news");
bb.Set("context:locale", "en-US");
bb.Set("orchestrator:max_ticks", 8);

var http = new HttpClient {Timeout = TimeSpan.FromSeconds(60)};
var openai = new OpenAiClient(apiKey);

var sensors = new ISensor[]
{
    new RecencySensor(),
#pragma warning disable OPENAI001
    new OutputModeSensorEnsemble(openai),
#pragma warning restore OPENAI001
    new SafetySensor(),
    new BudgetSlaSensors(),
    new UncertaintySensor(),
};

var agents = new IAction[]
{
    new NewsSearchAction(http), // stubbed web/news search
    new SummarizerAction(openai), // LLM
#pragma warning disable OPENAI001
    new VerifierAction(openai), // LLM
#pragma warning restore OPENAI001
    new TtsNaturalAction(), // stubbed TTS
    new TtsFastAction(), // stubbed TTS
};

var policy = new LinearEpsilonGreedyPolicy();
var reward = new DefaultReward();
var orchestrator = new Orchestrator(sensors, agents, policy, reward);

try
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    var result = await orchestrator.RunAsync(bb, cts.Token);
    Console.Write(result);
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] {ex.Message}");
}

// Show final outputs
Console.WriteLine("\n=== FINAL BLACKBOARD ===");
foreach (var kv in bb.Snapshot().OrderBy(k => k.Key))
    Console.WriteLine($"{kv.Key}: {kv.Value}");

Console.WriteLine("\nDone.");