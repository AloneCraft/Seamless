namespace Test.System.Linq.Enumerable.FLinq;

public sealed class GenericOptimizationTests {
    [Fact, Trait("Category","Unit")]
    public void SpanPartitionsMatchSystemLinq() {
        foreach(var values in CreateInputs()) {
            foreach(var count in new[] { -1,0,1,values.Length-1,values.Length,values.Length+1 }) {
                Assert.Equal(values.Skip(count),RunSkip(values,count));
                Assert.Equal(values.Take(count),RunTake(values,count));
                Assert.Equal(values.SkipLast(count),RunSkipLast(values,count));
                Assert.Equal(values.TakeLast(count),RunTakeLast(values,count));
            }
        }
    }

    [Fact, Trait("Category","Unit")]
    public void HashAndOrderingResultsMatchSystemLinq() {
        var values = new[] { 3,-1,3,0,2,-1,5,2 };
        Assert.Equal(values.Order(),RunOrder(values));
        Assert.Equal(
            values.AggregateBy(static value => value%3,0,static (sum,value) => sum+value),
            RunAggregateBy(values));
        Assert.Equal(
            values.Join(values.Reverse(),static outer => outer,static inner => inner,static (outer,inner) => (outer,inner)),
            RunJoin(values,values.Reverse().ToArray()));
        Assert.Equal(values.Intersect(new[] { -1,3,8 }),RunIntersect(values,new[] { -1,3,8 }));
        Assert.Equal(values.Except(new[] { -1,3,8 }),RunExcept(values,new[] { -1,3,8 }));
    }

    [Fact, Trait("Category","Unit")]
    public void LookupPreservesKeyAndElementOrder() {
        string?[] values = ["a",null,"A","b",null,"a"];
        var expected = values.ToLookup(static value => value,StringComparer.OrdinalIgnoreCase);
        var actual = RunLookup(values,StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expected.Select(static group => group.Key),actual.Select(static group => group.Key));
        foreach(var expectedGroup in expected) {
            Assert.Equal(expectedGroup,actual[expectedGroup.Key]);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void OrderingRemainsStableForEqualCustomKeys() {
        StableValue[] values = [new(2,"first"),new(1,"a"),new(2,"second"),new(1,"b")];
        Assert.Equal(values.OrderBy(static value => value.Key),RunStableOrder(values));
    }

    [Fact, Trait("Category","Unit")]
    public void SelectorExceptionIsPreserved() {
        var expected = Record.Exception(() => new[] { 1,2,3 }.ToLookup(static value => value==2 ? throw new InvalidOperationException("selector") : value));
        var actual = Record.Exception(() => RunThrowingLookup(new[] { 1,2,3 }));
        Assert.IsType(expected!.GetType(),actual);
        Assert.Equal(expected.Message,actual!.Message);
    }

    [Fact, Trait("Category","Architecture")]
    public void ProductCodeDoesNotDependOnBenchmarkIdentifiers() {
        var root = FindRepositoryRoot();
        var productFiles = new[] { "Seamless.FLinq.Generator","Seamless.Linq" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root,folder),"*.cs",SearchOption.AllDirectories))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",StringComparison.Ordinal)
                &&!path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",StringComparison.Ordinal));
        var forbidden = new[]{
            "Benchmark.System.Linq.Enumerable",
            "ExecutionPlan_FilterJoinAggregate",
            "ExecutionPlan_GroupJoinMaterialize",
            "AggregateBy_keySelector",
            "ToLookup_elementSelector",
            "Join_innerKey_Hoisting",
        };
        foreach(var file in productFiles) {
            var source = File.ReadAllText(file);
            foreach(var token in forbidden) {
                Assert.DoesNotContain(token,source,StringComparison.Ordinal);
            }
        }
    }

    private static IEnumerable<int[]> CreateInputs() {
        yield return [];
        yield return [1];
        yield return [0,-1,2,2,int.MinValue,int.MaxValue];
        yield return global::System.Linq.Enumerable.Range(0,37).Select(static value => (value*17)%11-5).ToArray();
    }

    private static string FindRepositoryRoot() {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while(current is not null&&!File.Exists(Path.Combine(current.FullName,"Seamless.slnx"))) {
            current=current.Parent;
        }
        return current?.FullName??throw new DirectoryNotFoundException("Repository root was not found.");
    }

    [LinqIntercept(true)] private static int[] RunSkip(int[] source,int count) => source.Skip(count).ToArray();
    [LinqIntercept(true)] private static int[] RunTake(int[] source,int count) => source.Take(count).ToArray();
    [LinqIntercept(true)] private static int[] RunSkipLast(int[] source,int count) => source.SkipLast(count).ToArray();
    [LinqIntercept(true)] private static int[] RunTakeLast(int[] source,int count) => source.TakeLast(count).ToArray();
    [LinqIntercept(true)] private static int[] RunOrder(int[] source) => source.Order().ToArray();
    [LinqIntercept(true)] private static KeyValuePair<int,int>[] RunAggregateBy(int[] source) => source.AggregateBy(static value => value%3,0,static (sum,value) => sum+value).ToArray();
    [LinqIntercept(true)] private static (int Outer,int Inner)[] RunJoin(int[] outer,int[] inner) => outer.Join(inner,static value => value,static value => value,static (left,right) => (left,right)).ToArray();
    [LinqIntercept(true)] private static int[] RunIntersect(int[] source,int[] second) => source.Intersect(second).ToArray();
    [LinqIntercept(true)] private static int[] RunExcept(int[] source,int[] second) => source.Except(second).ToArray();
    [LinqIntercept(true)] private static ILookup<string?,string?> RunLookup(string?[] source,IEqualityComparer<string?> comparer) => source.ToLookup(static value => value,comparer);
    [LinqIntercept(true)] private static StableValue[] RunStableOrder(StableValue[] source) => source.OrderBy(static value => value.Key).ToArray();
    [LinqIntercept(true)] private static ILookup<int,int> RunThrowingLookup(int[] source) => source.ToLookup(static value => value==2 ? throw new InvalidOperationException("selector") : value);

    private sealed record StableValue(int Key,string Name);
}
