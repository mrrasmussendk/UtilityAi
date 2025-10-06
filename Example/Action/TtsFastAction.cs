using UtilityAi.Actions;
using UtilityAi.Utils;

namespace Example.Action;

public class TtsFastAction: IAction
{
    public string Id => "tts_fast";
    public bool Gate(IBlackboard bb) => (bb.GetOr("task:output_mode", "text") is "audio" or "both") && bb.Has("answer:text");

    public Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
    {
        var t0 = DateTimeOffset.UtcNow;
        // TODO: replace with real TTS. Stub URL:
        var url = $"https://example.com/audio/fast-{Guid.NewGuid():N}.mp3";
        bb.Set("answer:audio_url", url);
        bb.Set("tts:voice", "FastEN");
        bb.Set("tts:duration", 60);
        var latency = DateTimeOffset.UtcNow - t0;
        return Task.FromResult(new AgentOutcome(true, 0.01, latency));
    }
}
