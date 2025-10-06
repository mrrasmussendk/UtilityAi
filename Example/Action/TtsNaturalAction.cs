using UtilityAi.Actions;
using UtilityAi.Utils;

namespace Example.Action;

public class TtsNaturalAction : IAction
{
    public string Id => "tts_natural";

    public bool Gate(IBlackboard bb) =>
        (bb.GetOr("task:output_mode", "text") is "audio" or "both") && bb.Has("answer:text");

    public Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        // TODO: replace with premium TTS
        var url = $"https://example.com/audio/natural-{Guid.NewGuid():N}.mp3";
        bb.Set("answer:audio_url", url);
        bb.Set("tts:voice", "NaturalFemaleEN");
        bb.Set("tts:duration", 75);
        var latency = DateTimeOffset.UtcNow - t0;
        return Task.FromResult(new AgentOutcome(true, 0.02, latency));
    }
}