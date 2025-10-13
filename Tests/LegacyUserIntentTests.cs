using System.Collections.Generic;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class LegacyUserIntentTests
{
    [Fact]
    public void Legacy_StringCtor_MapsToQuerySlot()
    {
        var intent = new UserIntent("hello world");
        Assert.NotNull(intent.Slots);
        Assert.True(intent.Slots!.TryGetValue("query", out var q));
        Assert.Equal("hello world", q as string);
    }

    [Fact]
    public void Legacy_TripleCtor_MapsSlots()
    {
        var intent = new UserIntent("q", "sms", "topicX");
        Assert.NotNull(intent.Slots);
        var slots = intent.Slots!;
        Assert.Equal("q", slots["query"] as string);
        Assert.Equal("sms", slots["delivery"] as string);
        Assert.Equal("topicX", slots["topic"] as string);
    }
}
