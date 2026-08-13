using SystemEnumerable = System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Optimization;

public sealed class RangeArgumentValidationDirect {
    [Theory, Trait("Category","Unit")]
    [InlineData(0,-1)]
    [InlineData(int.MaxValue,2)]
    public static void SumRejectsInvalidRangeLikeSystemLinq(int start,int count) =>
        AssertInvalid(start,count,probe=>SumExpected(probe),probe=>SumOptimized(probe));

    [Theory, Trait("Category","Unit")]
    [InlineData(0,-1)]
    [InlineData(int.MaxValue,2)]
    public static void SelectRejectsInvalidRangeBeforeConsumer(int start,int count) =>
        AssertInvalid(start,count,probe=>SelectExpected(probe),probe=>SelectOptimized(probe));

    [Theory, Trait("Category","Unit")]
    [InlineData(0,-1)]
    [InlineData(int.MaxValue,2)]
    public static void WhereRejectsInvalidRangeBeforeConsumer(int start,int count) =>
        AssertInvalid(start,count,probe=>WhereExpected(probe),probe=>WhereOptimized(probe));

    [Theory, Trait("Category","Unit")]
    [InlineData(0,-1)]
    [InlineData(int.MaxValue,2)]
    public static void TakeRejectsInvalidRangeBeforeOperatorAndConsumer(int start,int count) =>
        AssertInvalid(start,count,probe=>TakeExpected(probe),probe=>TakeOptimized(probe));

    [Theory, Trait("Category","Unit")]
    [InlineData(0,-1)]
    [InlineData(int.MaxValue,2)]
    public static void SkipRejectsInvalidRangeBeforeOperatorAndConsumer(int start,int count) =>
        AssertInvalid(start,count,probe=>SkipExpected(probe),probe=>SkipOptimized(probe));

    [Theory, Trait("Category","Unit")]
    [InlineData(0,-1)]
    [InlineData(int.MaxValue,2)]
    public static void SkipWhileRejectsInvalidRangeBeforeConsumerOrPredicate(int start,int count) =>
        AssertInvalid(start,count,probe=>SkipWhileExpected(probe),probe=>SkipWhileOptimized(probe));

    [Theory, Trait("Category","Unit")]
    [InlineData(int.MinValue,0)]
    [InlineData(int.MaxValue,1)]
    public static void DirectRangePathsAcceptSystemLinqBoundaries(int start,int count) {
        AssertValid(start,count,probe=>SumExpected(probe),probe=>SumOptimized(probe));
        AssertValid(start,count,probe=>SelectExpected(probe),probe=>SelectOptimized(probe));
        AssertValid(start,count,probe=>WhereExpected(probe),probe=>WhereOptimized(probe));
        AssertValid(start,count,probe=>TakeExpected(probe),probe=>TakeOptimized(probe));
        AssertValid(start,count,probe=>SkipExpected(probe),probe=>SkipOptimized(probe));
        AssertValid(start,count,probe=>SkipWhileExpected(probe),probe=>SkipWhileOptimized(probe));
    }

    private static void AssertInvalid(
        int start,
        int count,
        Action<RangeDirectArgumentProbe> expectedAction,
        Action<RangeDirectArgumentProbe> actualAction) {
        var expectedProbe = new RangeDirectArgumentProbe(start,count);
        var expectedException = Assert.IsType<ArgumentOutOfRangeException>(
            Record.Exception(()=>expectedAction(expectedProbe)));
        var actualProbe = new RangeDirectArgumentProbe(start,count);
        var actualException = Assert.IsType<ArgumentOutOfRangeException>(
            Record.Exception(()=>actualAction(actualProbe)));

        Assert.Equal(expectedException.ParamName,actualException.ParamName);
        Assert.Equal("count",actualException.ParamName);
        Assert.Equal(expectedProbe.Events,actualProbe.Events);
        Assert.Equal(["start","count"],actualProbe.Events);
        Assert.Empty(actualProbe.PredicateValues);
    }

    private static void AssertValid(
        int start,
        int count,
        Action<RangeDirectArgumentProbe> expectedAction,
        Action<RangeDirectArgumentProbe> actualAction) {
        var expectedProbe = new RangeDirectArgumentProbe(start,count);
        var expectedException = Record.Exception(()=>expectedAction(expectedProbe));
        var actualProbe = new RangeDirectArgumentProbe(start,count);
        var actualException = Record.Exception(()=>actualAction(actualProbe));

        Assert.Equal(expectedException?.GetType(),actualException?.GetType());
        Assert.Equal(expectedProbe.Events,actualProbe.Events);
        Assert.Equal(expectedProbe.PredicateValues,actualProbe.PredicateValues);
    }

    private static void SumExpected(RangeDirectArgumentProbe probe) =>
        _=SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count)).Sum();

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void SumOptimized(RangeDirectArgumentProbe probe) =>
        _=SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count)).Sum();

    private static void SelectExpected(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .Select(static value=>value)
            .Consume(probe.Consumer());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void SelectOptimized(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .Select(static value=>value)
            .Consume(probe.Consumer());

    private static void WhereExpected(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .Where(static _=>true)
            .Consume(probe.Consumer());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void WhereOptimized(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .Where(static _=>true)
            .Consume(probe.Consumer());

    private static void TakeExpected(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .Take(probe.Value("operator",0))
            .Consume(probe.Consumer());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void TakeOptimized(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .Take(probe.Value("operator",0))
            .Consume(probe.Consumer());

    private static void SkipExpected(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .Skip(probe.Value("operator",int.MaxValue))
            .Consume(probe.Consumer());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void SkipOptimized(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .Skip(probe.Value("operator",int.MaxValue))
            .Consume(probe.Consumer());

    private static void SkipWhileExpected(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .SkipWhile(probe.Predicate)
            .Consume(probe.Consumer());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void SkipWhileOptimized(RangeDirectArgumentProbe probe) =>
        SystemEnumerable.Range(probe.Value("start",probe.Start),probe.Value("count",probe.Count))
            .SkipWhile(probe.Predicate)
            .Consume(probe.Consumer());
}

public sealed class RangeDirectArgumentProbe(int start,int count) {
    public int Start { get; }=start;
    public int Count { get; }=count;
    public List<string> Events { get; }=[];
    public List<int> PredicateValues { get; }=[];

    public int Value(string name,int value) {
        this.Events.Add(name);
        return value;
    }

    public bool Predicate(int value) {
        this.PredicateValues.Add(value);
        return false;
    }

    internal Consumer<int> Consumer() {
        this.Events.Add("consumer");
        return new Consumer<int>();
    }
}
