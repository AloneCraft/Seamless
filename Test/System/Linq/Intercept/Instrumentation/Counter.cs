using Seamless.Tsavorite.Collections;
using Seamless.FLinq;

using Test.System.Linq.Intercept;

namespace Test.System.Linq.Intercept.Instrumentation;

public sealed class Counter {
    [Fact,Trait("Category","Unit")]
    public static void SelectSelectSelectWhere_counts_logical_and_physical() {
        using var scope = LinqCounterScope.Start();

        var actual = SelectSelectSelectWhereOptimized();

        LinqCounterDiagnostics.EmitActual(nameof(SelectSelectSelectWhere_counts_logical_and_physical),scope.Counter);
        Assert.Equal([5,7,9],actual);
        Assert.Equal(5,scope.Counter.PhysicalMoveNext);
        Assert.Equal(15,scope.Counter.LogicalSelect);
        Assert.Equal(5,scope.Counter.LogicalWhere);
        Assert.Equal(20,scope.Counter.LogicalOperations);
        Assert.Equal(20,scope.Counter.LogicalDelegate);
        Assert.Equal(0,scope.Counter.PhysicalDelegate);
    }

    [Fact,Trait("Category","Unit")]
    public static void SelectSelectSelectWhere_NoOptimized_counts_physical_delegates() {
        using var scope = LinqCounterScope.Start();

        var actual = SelectSelectSelectWhereNoOptimized();

        LinqCounterDiagnostics.EmitActual(nameof(SelectSelectSelectWhere_NoOptimized_counts_physical_delegates),scope.Counter);
        Assert.Equal([5,7,9],actual);
        Assert.Equal(20,scope.Counter.LogicalDelegate);
        Assert.Equal(20,scope.Counter.PhysicalDelegate);
    }

    [Fact,Trait("Category","Unit")]
    public static void Join_counts_lookup_and_probe_work() {
        using var scope = LinqCounterScope.Start();

        var actual = JoinOptimized();

        LinqCounterDiagnostics.EmitActual(nameof(Join_counts_lookup_and_probe_work),scope.Counter);
        Assert.Equal([4,4,6],actual);
        Assert.Equal(4,scope.Counter.LogicalJoinInner);
        Assert.Equal(3,scope.Counter.LogicalJoinOuter);
        Assert.Equal(4,scope.Counter.HashBuild);
        Assert.Equal(4,scope.Counter.BufferAdd);
        Assert.Equal(3,scope.Counter.HashProbe);
        Assert.Equal(10,scope.Counter.LogicalDelegate);
        Assert.Equal(0,scope.Counter.PhysicalDelegate);
    }

    [Fact,Trait("Category","Unit")]
    public static void Join_NoOptimized_counts_physical_delegates() {
        using var scope = LinqCounterScope.Start();

        var actual = JoinNoOptimized();

        LinqCounterDiagnostics.EmitActual(nameof(Join_NoOptimized_counts_physical_delegates),scope.Counter);
        Assert.Equal([4,4,6],actual);
        Assert.Equal(10,scope.Counter.LogicalDelegate);
        Assert.Equal(10,scope.Counter.PhysicalDelegate);
    }

    [Fact,Trait("Category","Unit")]
    public static void TryGetNonEnumeratedCount_counts_explicit_call() {
        using var scope = LinqCounterScope.Start();

        var actual = TryGetNonEnumeratedCountOptimized();

        LinqCounterDiagnostics.EmitActual(nameof(TryGetNonEnumeratedCount_counts_explicit_call),scope.Counter);
        Assert.Equal((true,3),actual);
        Assert.Equal(1,scope.Counter.TryGetNonEnumeratedCount);
    }

    [Fact,Trait("Category","Unit")]
    public static async Task CounterScope_can_share_counter_across_parallel_tasks() {
        using var scope = LinqCounterScope.Start();
        var counter = scope.Counter;
        var tasks = Enumerable.Range(0,4)
            .Select(_ => Task.Run(() => {
                using var innerScope = LinqCounterScope.Start(counter);
                return SelectSelectSelectWhereOptimized();
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        LinqCounterDiagnostics.EmitActual(nameof(CounterScope_can_share_counter_across_parallel_tasks),scope.Counter);
        Assert.All(tasks,static task => Assert.Equal([5,7,9],task.Result));
        Assert.Equal(20,scope.Counter.PhysicalMoveNext);
        Assert.Equal(60,scope.Counter.LogicalSelect);
        Assert.Equal(20,scope.Counter.LogicalWhere);
        Assert.Equal(80,scope.Counter.LogicalOperations);
        Assert.Equal(80,scope.Counter.LogicalDelegate);
        Assert.Equal(0,scope.Counter.PhysicalDelegate);
    }

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static int[] SelectSelectSelectWhereOptimized() =>
        Enumerable.Range(0,5)
            .Select(static value => value+1)
            .Select(static value => value*2)
            .Select(static value => value-1)
            .Where(static value => value>4)
            .ToArray();

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static int[] SelectSelectSelectWhereNoOptimized() =>
        Enumerable.Range(0,5)
            .Select(static value => value+1)
            .Select(static value => value*2)
            .Select(static value => value-1)
            .Where(static value => value>4)
            .ToArray();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static List<int> JoinOptimized() =>
        new[]{1,2,3}
            .Join([2,2,3,4],static outer => outer,static inner => inner,static (outer,inner) => outer+inner)
            .ToList();

    [LinqIntercept(false,LinqInstrumentation.Counter)]
    private static List<int> JoinNoOptimized() =>
        new[]{1,2,3}
            .Join([2,2,3,4],static outer => outer,static inner => inner,static (outer,inner) => outer+inner)
            .ToList();

    [LinqIntercept(true,LinqInstrumentation.Counter)]
    private static (bool Success,int Count) TryGetNonEnumeratedCountOptimized() {
        var success = new[]{1,2,3}.TryGetNonEnumeratedCount(out var count);
        return (success,count);
    }
}
