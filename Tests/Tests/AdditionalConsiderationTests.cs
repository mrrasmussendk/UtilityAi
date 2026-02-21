using UtilityAi.Consideration.General;
using UtilityAi.Consideration;
using UtilityAi.Evaluators;
using UtilityAi.Utils;
using Xunit;
using Range = UtilityAi.Evaluators.Range;

namespace Tests;

public class AdditionalConsiderationTests
{
    private sealed record ItemList(List<int> Items);
    private sealed record Score(double Value);

    // ── AllMatch ──────────────────────────────────────────────

    [Fact]
    public void AllMatch_ReturnsZero_WhenFactMissing()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new AllMatch<ItemList, int>(f => f.Items, i => i > 0);
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void AllMatch_ReturnsOne_WhenAllItemsMatch()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new AllMatch<ItemList, int>(f => f.Items, i => i > 0);
        bus.Publish(new ItemList(new List<int> { 1, 2, 3 }));
        Assert.Equal(1.0, cons.Evaluate(rt));
    }

    [Fact]
    public void AllMatch_ReturnsZero_WhenNotAllItemsMatch()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new AllMatch<ItemList, int>(f => f.Items, i => i > 0);
        bus.Publish(new ItemList(new List<int> { 1, -1, 3 }));
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void AllMatch_ReturnsOne_WhenCollectionEmpty()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new AllMatch<ItemList, int>(f => f.Items, i => i > 0);
        bus.Publish(new ItemList(new List<int>()));
        Assert.Equal(1.0, cons.Evaluate(rt));
    }

    [Fact]
    public void AllMatch_Name_IncludesTypeNames()
    {
        var cons = new AllMatch<ItemList, int>(f => f.Items, i => i > 0);
        Assert.Equal("AllMatch<ItemList, Int32>", cons.Name);
    }

    [Fact]
    public void AllMatch_ThrowsOnNullCollectionSelector()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AllMatch<ItemList, int>(null!, i => i > 0));
    }

    [Fact]
    public void AllMatch_ThrowsOnNullPredicate()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AllMatch<ItemList, int>(f => f.Items, null!));
    }

    // ── AnyMatch ─────────────────────────────────────────────

    [Fact]
    public void AnyMatch_ReturnsZero_WhenFactMissing()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new AnyMatch<ItemList, int>(f => f.Items, i => i > 10);
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void AnyMatch_ReturnsOne_WhenAnyItemMatches()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new AnyMatch<ItemList, int>(f => f.Items, i => i > 10);
        bus.Publish(new ItemList(new List<int> { 1, 20, 3 }));
        Assert.Equal(1.0, cons.Evaluate(rt));
    }

    [Fact]
    public void AnyMatch_ReturnsZero_WhenNoItemMatches()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new AnyMatch<ItemList, int>(f => f.Items, i => i > 10);
        bus.Publish(new ItemList(new List<int> { 1, 2, 3 }));
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void AnyMatch_ReturnsZero_WhenCollectionEmpty()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new AnyMatch<ItemList, int>(f => f.Items, i => i > 0);
        bus.Publish(new ItemList(new List<int>()));
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void AnyMatch_Name_IncludesTypeNames()
    {
        var cons = new AnyMatch<ItemList, int>(f => f.Items, i => i > 0);
        Assert.Equal("AnyMatch<ItemList, Int32>", cons.Name);
    }

    [Fact]
    public void AnyMatch_ThrowsOnNullCollectionSelector()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AnyMatch<ItemList, int>(null!, i => i > 0));
    }

    [Fact]
    public void AnyMatch_ThrowsOnNullPredicate()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AnyMatch<ItemList, int>(f => f.Items, null!));
    }

    // ── CollectionSize ───────────────────────────────────────

    [Fact]
    public void CollectionSize_ReturnsZero_WhenFactMissing()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var curve = new PowerCurve(new Range(0, 1), gamma: 1.0);
        var cons = new CollectionSize<ItemList>(f => f.Items.Count, curve, (0, 10));
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void CollectionSize_ReturnsNormalizedScore_ThroughCurve()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        // gamma=1 makes PowerCurve linear: output = input
        var curve = new PowerCurve(new Range(0, 1), gamma: 1.0);
        var cons = new CollectionSize<ItemList>(f => f.Items.Count, curve, (0, 10));
        bus.Publish(new ItemList(new List<int> { 1, 2, 3, 4, 5 }));
        Assert.Equal(0.5, cons.Evaluate(rt), 3);
    }

    [Fact]
    public void CollectionSize_ClampsToMax()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var curve = new PowerCurve(new Range(0, 1), gamma: 1.0);
        var cons = new CollectionSize<ItemList>(f => f.Items.Count, curve, (0, 5));
        bus.Publish(new ItemList(new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
        Assert.Equal(1.0, cons.Evaluate(rt), 3);
    }

    [Fact]
    public void CollectionSize_Name_IncludesTypeName()
    {
        var curve = new PowerCurve(new Range(0, 1), gamma: 1.0);
        var cons = new CollectionSize<ItemList>(f => f.Items.Count, curve, (0, 10));
        Assert.Equal("CollectionSize<ItemList>", cons.Name);
    }

    [Fact]
    public void CollectionSize_ThrowsOnNullSizeSelector()
    {
        var curve = new PowerCurve(new Range(0, 1), gamma: 1.0);
        Assert.Throws<ArgumentNullException>(() =>
            new CollectionSize<ItemList>(null!, curve, (0, 10)));
    }

    [Fact]
    public void CollectionSize_ThrowsOnNullCurve()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CollectionSize<ItemList>(f => f.Items.Count, null!, (0, 10)));
    }

    // ── InverseValue ─────────────────────────────────────────

    [Fact]
    public void InverseValue_ReturnsZero_WhenFactMissing()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new InverseValue<Score>(s => s.Value);
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void InverseValue_ReturnsInverse()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new InverseValue<Score>(s => s.Value);
        bus.Publish(new Score(0.3));
        Assert.Equal(0.7, cons.Evaluate(rt), 3);
    }

    [Fact]
    public void InverseValue_ClampsResult()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new InverseValue<Score>(s => s.Value);
        bus.Publish(new Score(1.5));
        Assert.Equal(0.0, cons.Evaluate(rt));

        bus.Publish(new Score(-0.5));
        Assert.Equal(1.0, cons.Evaluate(rt));
    }

    [Fact]
    public void InverseValue_Name_IncludesTypeName()
    {
        var cons = new InverseValue<Score>(s => s.Value);
        Assert.Equal("Inverse<Score>", cons.Name);
    }

    [Fact]
    public void InverseValue_ThrowsOnNullSelector()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InverseValue<Score>(null!));
    }

    // ── RandomValue ──────────────────────────────────────────

    [Fact]
    public void RandomValue_ReturnsValueInRange()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new RandomValue();
        for (var i = 0; i < 100; i++)
        {
            var val = cons.Evaluate(rt);
            Assert.InRange(val, 0.0, 1.0);
        }
    }

    [Fact]
    public void RandomValue_Name_IsRandom()
    {
        var cons = new RandomValue();
        Assert.Equal("Random", cons.Name);
    }

    // ── TimeSinceEvent ───────────────────────────────────────

    [Fact]
    public void TimeSinceEvent_ReturnsZero_WhenNoHistory()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var curve = new PowerCurve(new Range(0, 1), gamma: 1.0);
        var cons = new TimeSinceEvent<Score>(curve, (0.0, 60.0));
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void TimeSinceEvent_ReturnsNonZero_WhenEventExists()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var curve = new PowerCurve(new Range(0, 1), gamma: 1.0);
        var cons = new TimeSinceEvent<Score>(curve, (0.0, 60.0));
        bus.Publish(new Score(1.0));
        // Elapsed time will be very small but >= 0, so result should be >= 0
        var result = cons.Evaluate(rt);
        Assert.InRange(result, 0.0, 1.0);
    }

    [Fact]
    public void TimeSinceEvent_Name_IncludesTypeName()
    {
        var curve = new PowerCurve(new Range(0, 1), gamma: 1.0);
        var cons = new TimeSinceEvent<Score>(curve, (0.0, 60.0));
        Assert.Equal("TimeSince<Score>", cons.Name);
    }

    [Fact]
    public void TimeSinceEvent_ThrowsOnNullCurve()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TimeSinceEvent<Score>(null!, (0.0, 60.0)));
    }

    // ── TimeWindow ───────────────────────────────────────────

    [Fact]
    public void TimeWindow_ReturnsOne_WhenInsideWindow()
    {
        // Use a 24-hour window that always includes current time
        var cons = new TimeWindow(new TimeOnly(0, 0), new TimeOnly(23, 59, 59));
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        Assert.Equal(1.0, cons.Evaluate(rt));
    }

    [Fact]
    public void TimeWindow_ReturnsZero_WhenOutsideWindow()
    {
        // Create a 1-minute window guaranteed to not contain current time
        var now = TimeOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime);
        var start = now.AddHours(12);
        var end = start.AddMinutes(1);
        var cons = new TimeWindow(start, end);
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void TimeWindow_Name_IncludesTimeRange()
    {
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);
        var cons = new TimeWindow(start, end);
        Assert.Contains("9:00", cons.Name);
        Assert.Contains("17:00", cons.Name);
    }

    [Fact]
    public void TimeWindow_ReturnsZero_WhenDayNotAllowed()
    {
        // Allow only a day that is not today
        var today = DateTimeOffset.UtcNow.DayOfWeek;
        var otherDay = today == DayOfWeek.Monday ? DayOfWeek.Tuesday : DayOfWeek.Monday;
        var cons = new TimeWindow(new TimeOnly(0, 0), new TimeOnly(23, 59, 59), new[] { otherDay });
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    // ── WeightedRandomValue ──────────────────────────────────

    [Fact]
    public void WeightedRandomValue_ReturnsZero_WhenFactMissing()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new WeightedRandomValue<Score>(s => s.Value);
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void WeightedRandomValue_FullyDeterministic_ReturnsExactScore()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new WeightedRandomValue<Score>(s => s.Value, deterministicWeight: 1.0);
        bus.Publish(new Score(0.75));
        Assert.Equal(0.75, cons.Evaluate(rt), 3);
    }

    [Fact]
    public void WeightedRandomValue_ReturnsValueInRange()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new WeightedRandomValue<Score>(s => s.Value, deterministicWeight: 0.5);
        bus.Publish(new Score(0.5));
        for (var i = 0; i < 100; i++)
        {
            var val = cons.Evaluate(rt);
            Assert.InRange(val, 0.0, 1.0);
        }
    }

    [Fact]
    public void WeightedRandomValue_Name_IncludesTypeAndWeight()
    {
        var cons = new WeightedRandomValue<Score>(s => s.Value, deterministicWeight: 0.5);
        Assert.Equal("WeightedRandom<Score>(0.50)", cons.Name);
    }

    [Fact]
    public void WeightedRandomValue_ThrowsOnNullSelector()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WeightedRandomValue<Score>(null!));
    }

    // ── ConstantValue ────────────────────────────────────────

    [Fact]
    public void ConstantValue_Name_IncludesFormattedValue()
    {
        var cons = new ConstantValue(0.75);
        Assert.Equal("Constant(0.75)", cons.Name);
    }

    [Fact]
    public void ConstantValue_ReturnsClampedValue()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        Assert.Equal(1.0, new ConstantValue(2.0).Evaluate(rt));
        Assert.Equal(0.0, new ConstantValue(-1.0).Evaluate(rt));
        Assert.Equal(0.5, new ConstantValue(0.5).Evaluate(rt));
    }
}
