using UtilityAi.Capabilities.BuiltIn;
using UtilityAi.Utils;
using Xunit;

namespace Tests.Capabilities;

public class IdleModuleTests
{
    [Fact]
    public void IdleModule_AlwaysProposesIdleAction()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);
        var module = new IdleModule();

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("idle", proposals[0].Id);
    }

    [Fact]
    public void IdleModule_HasLowUtility()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);
        var module = new IdleModule(idleUtility: 0.001);

        var proposals = module.Propose(rt).ToList();
        var utility = proposals[0].Utility(rt);

        Assert.True(utility < 0.01);
    }

    [Fact]
    public async Task IdleModule_ActionCompletesSuccessfully()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);
        var module = new IdleModule();

        var proposals = module.Propose(rt).ToList();

        // Should not throw
        await proposals[0].Act(CancellationToken.None);
    }
}
