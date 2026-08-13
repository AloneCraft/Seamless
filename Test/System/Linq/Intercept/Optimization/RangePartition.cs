using SystemEnumerable = System.Linq.Enumerable;

namespace Test.System.Linq.Intercept.Optimization;

public sealed class RangePartition:OptimizationTestBase {
    [Fact, Trait("Category","Unit")]
    public static void TakeAndSkipMatchSystemLinqAtBoundaries() {
        foreach(var (sourceCount,partitionCount) in new[] {
            (0,-1),(0,1),(1,-1),(1,0),(1,1),(4,-1),(4,0),(4,2),(4,4),(4,8),
        }) {
            var expectedTake = SystemEnumerable.Range(10,sourceCount).Take(partitionCount).ToArray();
            var expectedSkip = SystemEnumerable.Range(10,sourceCount).Skip(partitionCount).ToArray();
            Assert.Equal(expectedTake,TakeOptimized(10,sourceCount,partitionCount));
            Assert.Equal(expectedSkip,SkipOptimized(10,sourceCount,partitionCount));
        }
    }

    [Fact, Trait("Category","Unit")]
    public static void ConsecutiveSkipNormalizesEachNegativeCountBeforeFolding() {
        foreach(var (firstCount,secondCount) in new[] {
            (5,-10),(-10,2),(0,-1),(int.MaxValue,int.MaxValue),
        }) {
            var expected = SystemEnumerable.Range(10,6)
                .Skip(firstCount)
                .Skip(secondCount)
                .ToArray();
            Assert.Equal(expected,ConsecutiveSkipOptimized(10,6,firstCount,secondCount));
        }
    }

    [Fact, Trait("Category","Unit")]
    public static void TakeAndSkipEvaluateArgumentsOnceInSystemOrder() {
        var expectedTakeProbe = new RangeEvaluationProbe();
        var expectedTake = SystemEnumerable.Range(
                expectedTakeProbe.Value("start",10),
                expectedTakeProbe.Value("sourceCount",4))
            .Take(expectedTakeProbe.Value("takeCount",2))
            .ToArray();
        var actualTakeProbe = new RangeEvaluationProbe();
        var actualTake = TakeWithEvaluationOptimized(actualTakeProbe);
        Assert.Equal(expectedTake,actualTake);
        Assert.Equal(expectedTakeProbe.Events,actualTakeProbe.Events);

        var expectedSkipProbe = new RangeEvaluationProbe();
        var expectedSkip = SystemEnumerable.Range(
                expectedSkipProbe.Value("start",10),
                expectedSkipProbe.Value("sourceCount",4))
            .Skip(expectedSkipProbe.Value("skipCount",-1))
            .ToArray();
        var actualSkipProbe = new RangeEvaluationProbe();
        var actualSkip = SkipWithEvaluationOptimized(actualSkipProbe);
        Assert.Equal(expectedSkip,actualSkip);
        Assert.Equal(expectedSkipProbe.Events,actualSkipProbe.Events);
    }

    [Fact, Trait("Category","Unit")]
    public static void SkipWhileMatchesSystemLinqAndStopsInvokingPredicateAfterFirstFalse() {
        foreach(var (sourceCount,stopAt) in new[] {
            (0,12),(1,11),(1,10),(5,12),(5,20),
        }) {
            var expectedProbe = new RangePredicateProbe(stopAt);
            var expected = SystemEnumerable.Range(10,sourceCount).SkipWhile(expectedProbe.Invoke).ToArray();
            var actualProbe = new RangePredicateProbe(stopAt);
            var actual = SkipWhileOptimized(10,sourceCount,actualProbe);
            Assert.Equal(expected,actual);
            Assert.Equal(expectedProbe.Seen,actualProbe.Seen);
        }

        var shortCircuitProbe = new RangePredicateProbe(12);
        Assert.Equal([12,13,14],SkipWhileOptimized(10,5,shortCircuitProbe));
        Assert.Equal([10,11,12],shortCircuitProbe.Seen);
    }

    [Fact, Trait("Category","Unit")]
    public static void SkipWhilePropagatesPredicateExceptionAtTheSamePoint() {
        var expectedProbe = new RangePredicateProbe(int.MaxValue,11);
        var expectedException = Record.Exception(() =>
            SystemEnumerable.Range(10,5).SkipWhile(expectedProbe.Invoke).ToArray());
        var actualProbe = new RangePredicateProbe(int.MaxValue,11);
        var actualException = Record.Exception(() => SkipWhileOptimized(10,5,actualProbe));

        Assert.IsType<InvalidOperationException>(expectedException);
        Assert.IsType<InvalidOperationException>(actualException);
        Assert.Equal(expectedException!.Message,actualException!.Message);
        Assert.Equal(expectedProbe.Seen,actualProbe.Seen);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeOptimized(int start,int sourceCount,int takeCount) {
        var consumer = new Consumer<int>();
        SystemEnumerable.Range(start,sourceCount).Take(takeCount).Consume(consumer);
        return consumer.Items;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SkipOptimized(int start,int sourceCount,int skipCount) {
        var consumer = new Consumer<int>();
        SystemEnumerable.Range(start,sourceCount).Skip(skipCount).Consume(consumer);
        return consumer.Items;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ConsecutiveSkipOptimized(
        int start,
        int sourceCount,
        int firstCount,
        int secondCount) {
        var consumer = new Consumer<int>();
        SystemEnumerable.Range(start,sourceCount)
            .Skip(firstCount)
            .Skip(secondCount)
            .Consume(consumer);
        return consumer.Items;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> TakeWithEvaluationOptimized(RangeEvaluationProbe probe) {
        var consumer = new Consumer<int>();
        SystemEnumerable.Range(probe.Value("start",10),probe.Value("sourceCount",4))
            .Take(probe.Value("takeCount",2))
            .Consume(consumer);
        return consumer.Items;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SkipWithEvaluationOptimized(RangeEvaluationProbe probe) {
        var consumer = new Consumer<int>();
        SystemEnumerable.Range(probe.Value("start",10),probe.Value("sourceCount",4))
            .Skip(probe.Value("skipCount",-1))
            .Consume(consumer);
        return consumer.Items;
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> SkipWhileOptimized(int start,int sourceCount,RangePredicateProbe probe) {
        var consumer = new Consumer<int>();
        SystemEnumerable.Range(start,sourceCount).SkipWhile(probe.Invoke).Consume(consumer);
        return consumer.Items;
    }
}

public sealed class RangeEvaluationProbe {
    public List<string> Events { get; } = [];

    public int Value(string name,int value) {
        this.Events.Add(name);
        return value;
    }
}

public sealed class RangePredicateProbe(int stopAt,int? throwAt=null) {
    public List<int> Seen { get; } = [];

    public bool Invoke(int value) {
        this.Seen.Add(value);
        if(value==throwAt) {
            throw new InvalidOperationException($"predicate failed at {value}");
        }
        return value<stopAt;
    }
}
