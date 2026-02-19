using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class UserIntentTests
{
    [Fact]
    public void UserIntent_PrimaryConstructor_SetsAllProperties()
    {
        var goal = new IntentGoal("test-goal");
        var slots = new Dictionary<string, object?> { { "key", "value" } };
        var requestId = "req-123";
        var locale = "en-US";

        var intent = new UserIntent(goal, slots, requestId, locale);

        Assert.Equal(goal, intent.Goal);
        Assert.Equal(slots, intent.Slots);
        Assert.Equal(requestId, intent.RequestId);
        Assert.Equal(locale, intent.Locale);
    }

    [Fact]
    public void UserIntent_StringConstructor_CreatesUnspecifiedGoalWithQuerySlot()
    {
        var intent = new UserIntent("test query");

        Assert.NotNull(intent.Goal);
        Assert.Equal("unspecified", intent.Goal.Name);
        Assert.NotNull(intent.Slots);
        Assert.True(intent.Slots.ContainsKey("query"));
        Assert.Equal("test query", intent.Slots["query"]);
    }

    [Fact]
    public void UserIntent_LegacyConstructor_CreatesLegacyGoalWithSlots()
    {
        var intent = new UserIntent("my query", "email", "support");

        Assert.NotNull(intent.Goal);
        Assert.Equal("legacy", intent.Goal.Name);
        Assert.NotNull(intent.Slots);
        Assert.Equal(3, intent.Slots.Count);
        Assert.Equal("my query", intent.Slots["query"]);
        Assert.Equal("email", intent.Slots["delivery"]);
        Assert.Equal("support", intent.Slots["topic"]);
    }

    [Fact]
    public void UserIntent_WithNullSlots_WorksCorrectly()
    {
        var goal = new IntentGoal("test");
        var intent = new UserIntent(goal, null, null, null);

        Assert.Equal(goal, intent.Goal);
        Assert.Null(intent.Slots);
        Assert.Null(intent.RequestId);
        Assert.Null(intent.Locale);
    }

    [Fact]
    public void IntentGoal_Name_IsStored()
    {
        var goal = new IntentGoal("my-goal");
        Assert.Equal("my-goal", goal.Name);
    }

    [Fact]
    public void UserIntent_IsRecord_SupportsEquality()
    {
        var goal1 = new IntentGoal("test");
        var intent1 = new UserIntent(goal1);
        var intent2 = new UserIntent(goal1);

        Assert.Equal(intent1, intent2);
    }

    [Fact]
    public void UserIntent_ForGoal_CreatesIntentWithGoalName()
    {
        var slots = new Dictionary<string, object?> { ["priority"] = 2 };
        var intent = UserIntent.ForGoal("triage", slots, "req-7", "en-US");

        Assert.Equal("triage", intent.Goal.Name);
        Assert.Equal(2, intent.GetSlotOrDefault<int>("priority"));
        Assert.Equal("req-7", intent.RequestId);
        Assert.Equal("en-US", intent.Locale);
    }

    [Fact]
    public void UserIntent_FromQuery_StoresQueryAndExposesQueryProperty()
    {
        var intent = UserIntent.FromQuery("find invoices", goalName: "search");

        Assert.Equal("search", intent.Goal.Name);
        Assert.Equal("find invoices", intent.Query);
        Assert.True(intent.TryGetSlot<string>("query", out var query));
        Assert.Equal("find invoices", query);
    }

    [Fact]
    public void UserIntent_TryGetSlot_WrongType_ReturnsFalse()
    {
        var intent = UserIntent.ForGoal("test", new Dictionary<string, object?> { ["count"] = 3 });

        Assert.False(intent.TryGetSlot<string>("count", out _));
    }
}
