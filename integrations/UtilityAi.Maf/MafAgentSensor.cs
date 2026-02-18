using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace UtilityAi.Maf;

/// <summary>
/// A UtilityAI sensor that publishes the catalog of available MAF agents to the EventBus.
/// This sensor makes agent availability information accessible to considerations and eligibility checks.
/// </summary>
/// <remarks>
/// Add this sensor to the orchestrator when using MAF agents. It runs at the start of each tick
/// and updates the <see cref="MafAgentCatalog"/> fact on the EventBus.
/// </remarks>
public sealed class MafAgentSensor : ISensor
{
    private readonly List<MafAgentRegistration> _registrations = new();

    /// <summary>
    /// Registers a MAF agent with the sensor.
    /// </summary>
    /// <param name="registration">The agent registration describing the agent and its state.</param>
    /// <returns>This sensor instance for fluent chaining.</returns>
    public MafAgentSensor Register(MafAgentRegistration registration)
    {
        _registrations.Add(registration ?? throw new ArgumentNullException(nameof(registration)));
        return this;
    }

    /// <inheritdoc />
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        rt.Bus.Publish(new MafAgentCatalog(_registrations.AsReadOnly()));
        return Task.CompletedTask;
    }
}
