using Example.OutputModule.DTO;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.TwiML;
using Twilio.TwiML.Voice;
using UtilityAi.Actions;
using UtilityAi.Utils;

namespace Example.OutputModule.Actions;

public class TwilloOutputAction : IAction<SmsMessage, bool>
{
    private string _sid = Environment.GetEnvironmentVariable("TWILLO_SID")?.Trim() ?? "";
    private string _token = Environment.GetEnvironmentVariable("TWILLO_TOKEN")?.Trim() ?? "";
    private string _phoneFrom = Environment.GetEnvironmentVariable("PHONE_FROM")?.Trim() ?? "";
    private string _phoneTo = Environment.GetEnvironmentVariable("PHONE_TO")?.Trim() ?? "";


    public async Task<bool> ActAsync(SmsMessage request, CancellationToken ct)
    {
        TwilioClient.Init(_sid, _token);
        var text = request.Text;
        var vr = new VoiceResponse();
        vr.Say(text, voice: Say.VoiceEnum.Woman);

        var call = await CallResource.CreateAsync(
            twiml: vr.ToString(),
            to: new Twilio.Types.PhoneNumber(_phoneTo),
            from: new Twilio.Types.PhoneNumber(_phoneFrom));
        return true;
    }
}