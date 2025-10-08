using Example.Action.Considerations;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.TwiML;
using Twilio.TwiML.Voice;
using Twilio.Types;
using UtilityAi.Actions;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.Action;

public class TwilloOutputAction : IAction
{
    private string _sid = Environment.GetEnvironmentVariable("TWILLO_SID")?.Trim() ?? "";
    private string _token = Environment.GetEnvironmentVariable("TWILLO_TOKEN")?.Trim() ?? "";
    private string _phoneFrom = Environment.GetEnvironmentVariable("PHONE_FROM")?.Trim() ?? "";
    private string _phoneTo = Environment.GetEnvironmentVariable("PHONE_TO")?.Trim() ?? "";

    private List<IConsideration> _considerations = new()
    {
        new HasValueConsideration("answer:text")
    };

    public string Id { get; } = "output_twillo";


    public double Score(IBlackboard bb)
    {
        return Scoring.AggregateWithMakeup(_considerations, bb);
    }

    public async Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
    {
        TwilioClient.Init(_sid, _token);
        var text = bb.GetOr("answer:text", "");
        var vr = new VoiceResponse();
        vr.Say(text, voice: Say.VoiceEnum.Woman);

        var call = await CallResource.CreateAsync(
            twiml: vr.ToString(),
            to: new Twilio.Types.PhoneNumber(_phoneTo),
            from: new Twilio.Types.PhoneNumber(_phoneFrom));


        bb.Set("done", true);

        return new AgentOutcome(true, 0.01, TimeSpan.Zero);
    }
}