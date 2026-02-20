using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Consideration.Intent;
using UtilityAi.Sensor.LLM;
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
    private IntentMatchSpec? _intentMatch;
    private readonly List<IntentParameterUsage> _intentParameters = new();

    internal ProposalBuilder(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Proposal id cannot be null or whitespace.", nameof(id));
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
    /// Useful for LLM context, debugging, and introspection.
    /// </summary>
    public ProposalBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Declares what intent pattern this proposal handles.
    /// Used for LLM prompt generation and intent-based filtering.
    /// </summary>
    public ProposalBuilder ForIntent(string pattern, IntentMatchType matchType = IntentMatchType.Exact)
    {
        _intentMatch = new IntentMatchSpec(pattern, matchType);
        return this;
    }

    /// <summary>
    /// Declares that this proposal uses an intent parameter.
    /// Registers metadata for LLM prompt generation without adding a consideration.
    /// </summary>
    public ProposalBuilder UsesIntentParameter(
        string name,
        string type,
        string? description = null,
        ValueRange? range = null,
        string[]? allowedValues = null)
    {
        _intentParameters.Add(new IntentParameterUsage(
            ParameterName: name,
            Type: type,
            Description: description,
            Range: range,
            AllowedValues: allowedValues
        ));
        return this;
    }

    /// <summary>
    /// Declares an intent parameter AND adds a consideration that scores based on it.
    /// Shorthand for UsesIntentParameter + WithConsideration.
    /// </summary>
    public ProposalBuilder ScoreByIntentParameter(
        string paramName,
        Func<double, double> curve,
        (double min, double max) range,
        string? description = null)
    {
        // Register parameter metadata
        _intentParameters.Add(new IntentParameterUsage(
            ParameterName: paramName,
            Type: "number",
            Description: description,
            Range: new ValueRange(range.min, range.max),
            ConsiderationName: $"intent-param-{paramName}"
        ));

        // Add consideration that uses it
        _considerations.Add(new SignalConsideration<IntentAnalysis>(
            name: $"intent-param-{paramName}",
            selector: intent => intent.GetParameter<double>(paramName, 0),
            curve: curve,
            inputDomain: range
        ));

        return this;
    }

    /// <summary>
    /// Builds the proposal.
    /// </summary>
    public Proposal Build()
    {
        if (_action == null)
            throw new InvalidOperationException($"Cannot build proposal '{_id}': missing required field 'action'.");

        // Automatically add eligibility check for intent parameters
        var eligibilities = new List<IEligibility>(_eligibilities);
        if (_intentParameters.Count > 0)
        {
            var requiredParams = _intentParameters.Select(p => p.ParameterName).ToList();
            eligibilities.Add(new HasIntentParametersEligible(requiredParams));
        }

        return new Proposal(
            id: _id,
            cons: _considerations,
            act: _action,
            eligibilities: eligibilities.Count > 0 ? eligibilities : null
        )
        {
            Prior = _prior,
            Temperature = _temperature,
            Description = _description,
            IntentMatch = _intentMatch,
            IntentParameters = _intentParameters.Count > 0 ? _intentParameters : null
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
