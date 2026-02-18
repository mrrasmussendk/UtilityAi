using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using Xunit;

namespace Tests;

public class CapabilitiesAttributeTests
{
    // Test data classes
    public class TestFact { }
    public class TestConsideration : IConsideration
    {
        public string Name => "test";
        public double Evaluate(UtilityAi.Utils.Runtime rt) => 0.5;
    }

    [Fact]
    public void ActiveWhenAttribute_FactType_StoresCorrectly()
    {
        var attr = new ActiveWhenAttribute(typeof(TestFact));
        Assert.Equal(typeof(TestFact), attr.FactType);
        Assert.Null(attr.IntentSlot);
        Assert.Null(attr.AllowedValues);
    }

    [Fact]
    public void ActiveWhenAttribute_IntentSlot_StoresCorrectly()
    {
        var attr = new ActiveWhenAttribute("priority", "high", "urgent");
        Assert.Null(attr.FactType);
        Assert.Equal("priority", attr.IntentSlot);
        Assert.Equal(new[] { "high", "urgent" }, attr.AllowedValues);
    }

    [Fact]
    public void ActiveWhenAttribute_NullFactType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ActiveWhenAttribute((Type)null!));
    }

    [Fact]
    public void ActiveWhenAttribute_NullIntentSlot_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ActiveWhenAttribute(null!, "value"));
    }

    [Fact]
    public void ActiveWhenAttribute_NullAllowedValues_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ActiveWhenAttribute("slot", null!));
    }

    [Fact]
    public void CapabilityAttribute_DefaultValues()
    {
        var attr = new CapabilityAttribute();
        Assert.Equal(0, attr.Priority);
        Assert.Null(attr.Domain);
        Assert.Null(attr.DependsOn);
        Assert.True(attr.Enabled);
    }

    [Fact]
    public void CapabilityAttribute_WithInitializers()
    {
        var attr = new CapabilityAttribute
        {
            Priority = 100,
            Domain = "test-domain",
            DependsOn = new[] { typeof(TestFact) },
            Enabled = false
        };

        Assert.Equal(100, attr.Priority);
        Assert.Equal("test-domain", attr.Domain);
        Assert.Single(attr.DependsOn);
        Assert.Equal(typeof(TestFact), attr.DependsOn![0]);
        Assert.False(attr.Enabled);
    }

    [Fact]
    public void ConsiderAttribute_ValidType_StoresCorrectly()
    {
        var attr = new ConsiderAttribute(typeof(TestConsideration));
        Assert.Equal(typeof(TestConsideration), attr.ConsiderationType);
        Assert.Equal(1.0, attr.Weight);
    }

    [Fact]
    public void ConsiderAttribute_WithWeight()
    {
        var attr = new ConsiderAttribute(typeof(TestConsideration)) { Weight = 0.5 };
        Assert.Equal(0.5, attr.Weight);
    }

    [Fact]
    public void ConsiderAttribute_NullType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ConsiderAttribute(null!));
    }

    [Fact]
    public void ConsiderAttribute_NonConsiderationType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ConsiderAttribute(typeof(string)));
    }

    [Fact]
    public void ConsiderationWeightAttribute_BasicConstruction()
    {
        var attr = new ConsiderationWeightAttribute("priority", 0.7);
        Assert.Equal("priority", attr.Name);
        Assert.Equal(0.7, attr.Weight);
        Assert.Equal(CurveType.Linear, attr.Curve);
        Assert.Null(attr.MinThreshold);
        Assert.Null(attr.MaxThreshold);
    }

    [Fact]
    public void ConsiderationWeightAttribute_WeightClamping()
    {
        var attrLow = new ConsiderationWeightAttribute("test", -1.0);
        Assert.Equal(0.0, attrLow.Weight);

        var attrHigh = new ConsiderationWeightAttribute("test", 2.0);
        Assert.Equal(1.0, attrHigh.Weight);
    }

    [Fact]
    public void ConsiderationWeightAttribute_WithAllProperties()
    {
        var attr = new ConsiderationWeightAttribute("age", 0.5)
        {
            Curve = CurveType.Logistic,
            MinThreshold = 0.2,
            MaxThreshold = 0.8
        };

        Assert.Equal("age", attr.Name);
        Assert.Equal(0.5, attr.Weight);
        Assert.Equal(CurveType.Logistic, attr.Curve);
        Assert.Equal(0.2, attr.MinThreshold);
        Assert.Equal(0.8, attr.MaxThreshold);
    }

    [Fact]
    public void ConsiderationWeightAttribute_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ConsiderationWeightAttribute(null!, 0.5));
    }

    [Fact]
    public void ProposalActionAttribute_BasicConstruction()
    {
        var attr = new ProposalActionAttribute("action-{id}");
        Assert.Equal("action-{id}", attr.IdPattern);
        Assert.Null(attr.Description);
        Assert.Equal(0.0, attr.MinUtility);
    }

    [Fact]
    public void ProposalActionAttribute_WithProperties()
    {
        var attr = new ProposalActionAttribute("send-message-{recipient}")
        {
            Description = "Send a message to recipient",
            MinUtility = 0.3
        };

        Assert.Equal("send-message-{recipient}", attr.IdPattern);
        Assert.Equal("Send a message to recipient", attr.Description);
        Assert.Equal(0.3, attr.MinUtility);
    }

    [Fact]
    public void ProposalActionAttribute_NullIdPattern_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ProposalActionAttribute(null!));
    }

    [Fact]
    public void RequiresFactAttribute_DefaultOptional()
    {
        var attr = new RequiresFactAttribute<TestFact>();
        Assert.False(attr.Optional);
    }

    [Fact]
    public void RequiresFactAttribute_WithOptional()
    {
        var attr = new RequiresFactAttribute<TestFact> { Optional = true };
        Assert.True(attr.Optional);
    }

    [Fact]
    public void CurveType_AllValuesAccessible()
    {
        Assert.Equal(CurveType.Linear, CurveType.Linear);
        Assert.Equal(CurveType.Exponential, CurveType.Exponential);
        Assert.Equal(CurveType.Logarithmic, CurveType.Logarithmic);
        Assert.Equal(CurveType.Logistic, CurveType.Logistic);
        Assert.Equal(CurveType.Power, CurveType.Power);
    }
}
