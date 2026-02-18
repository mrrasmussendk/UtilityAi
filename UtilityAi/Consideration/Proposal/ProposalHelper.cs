using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Capabilities;

/// <summary>
/// Helper class for building proposals with less boilerplate.
/// </summary>
public static class ProposalHelper
{
    /// <summary>
    /// Creates a proposal builder for fluent configuration.
    /// </summary>
    public static ProposalBuilder For(string id) => new(id);
}

/// <summary>
/// Fluent builder for creating proposals with reduced boilerplate.
/// </summary>
public sealed class ProposalBuilder
{
    private readonly string _id;
    private readonly List<IConsideration> _considerations = new();
    private readonly List<IEligibility> _eligibilities = new();
    private Func<CancellationToken, Task>? _action;
    private double _prior = 1.0;
    private double _temperature = 1.0;
    private string? _description;

    internal ProposalBuilder(string id)
    {
        _id = id;
    }

    /// <summary>
    /// Adds a consideration to the proposal.
    /// </summary>
    public ProposalBuilder WithConsideration(IConsideration consideration)
    {
        _considerations.Add(consideration);
        return this;
    }

    /// <summary>
    /// Adds a simple fixed value consideration.
    /// </summary>
    public ProposalBuilder WithValue(string name, double value)
    {
        _considerations.Add(new FixedValueConsideration(name, value));
        return this;
    }

    /// <summary>
    /// Adds an eligibility condition.
    /// </summary>
    public ProposalBuilder WithEligibility(IEligibility eligibility)
    {
        _eligibilities.Add(eligibility);
        return this;
    }

    /// <summary>
    /// Sets the prior probability.
    /// </summary>
    public ProposalBuilder WithPrior(double prior)
    {
        _prior = prior;
        return this;
    }

    /// <summary>
    /// Sets the temperature for utility calculation.
    /// </summary>
    public ProposalBuilder WithTemperature(double temperature)
    {
        _temperature = temperature;
        return this;
    }

    /// <summary>
    /// Sets the action to execute when this proposal is chosen.
    /// </summary>
    public ProposalBuilder WithAction(Func<CancellationToken, Task> action)
    {
        _action = action;
        return this;
    }

    /// <summary>
    /// Sets a human-readable description of what this action does.
    /// Useful for LLM planning, debugging, and introspection.
    /// </summary>
    public ProposalBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Builds the proposal.
    /// </summary>
    public Proposal Build()
    {
        if (_action == null)
            throw new InvalidOperationException("Action must be set before building proposal");

        return new Proposal(
            id: _id,
            cons: _considerations,
            act: _action,
            eligibilities: _eligibilities.Count > 0 ? _eligibilities : null
        )
        {
            Prior = _prior,
            Temperature = _temperature,
            Description = _description
        };
    }

    /// <summary>
    /// Implicitly converts builder to Proposal.
    /// </summary>
    public static implicit operator Proposal(ProposalBuilder builder) => builder.Build();
}

/// <summary>
/// Internal fixed value consideration for ProposalHelper.
/// </summary>
file sealed class FixedValueConsideration : IConsideration
{
    private readonly string _name;
    private readonly double _value;

    public FixedValueConsideration(string name, double value)
    {
        _name = name;
        _value = Math.Clamp(value, 0.0, 1.0);
    }

    public string Name => _name;
    public double Evaluate(Runtime rt) => _value;
}
