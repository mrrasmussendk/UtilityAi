using Example.OutputModule.Actions;
using Example.OutputModule.DTO;
using Example.Sensor;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Evaluators;
using UtilityAi.Utils;

namespace Example.OutputModule;

public sealed class OutputModule(TwilloOutputAction smsOutputAction) : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var haveSummary = rt.Bus.GetOrDefault<OutputTextMessage>() is not null;

        if (haveSummary && rt.Bus.GetOrDefault<SmsMessage>() is null)
        {
            yield return new Proposal(
                id: "output.sendSms",
                baseScore: 0.85,
                cons: new IConsideration[]
                {
                    new HasFact<OutputTextMessage>(true),
                    new CurveSignal<SignalOutputMode>("mode=sms", s => s.Mode == "sms" ? 1.0 : 0.3,
                        Curves.Identity())
                },
                act: async ct =>
                {
                    var sum = rt.Bus.GetOrDefault<OutputTextMessage>()!;
                    var req = new SmsMessage(sum.Text);
                    var draft = await smsOutputAction.ActAsync(req, null, ct);
                    rt.Bus.Publish(draft);
                }
            );
        }
    }
}