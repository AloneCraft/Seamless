namespace Test.System.Linq.Intercept.Optimization;

// 終端(Sum/Count/First/ToList など)が、ソースから取得した列挙子を System.Linq と同じく必ず破棄することを固定する。
// 列挙子の破棄で資源を返すソース(Seamless.Database の Table は読み取りセッションを閉じる)では、破棄漏れが
// DatabaseContainer を閉じられない不具合になる(BUG-0007)。最適化側は呼び出し箇所で傍受されるよう、必ず直接呼び出す。
[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ScalarTerminalDisposeSemantics:OptimizationTestBase {
    private static readonly int[] Values=[3,1,4,1,5];
    private static readonly int[] OneValue=[4];

    [Fact, Trait("Category","Unit")]
    public void AggregatesDisposeSourceEnumerator() {
        AssertDisposeMatches("Sum()",events => SumSystem(Source(events)),events => SumOptimized(Source(events)));
        AssertDisposeMatches("Sum(selector)",events => SumSelectorSystem(Source(events)),events => SumSelectorOptimized(Source(events)));
        AssertDisposeMatches("Count()",events => CountSystem(Source(events)),events => CountOptimized(Source(events)));
        AssertDisposeMatches("Count(predicate)",events => CountPredicateSystem(Source(events)),events => CountPredicateOptimized(Source(events)));
        AssertDisposeMatches("LongCount()",events => LongCountSystem(Source(events)),events => LongCountOptimized(Source(events)));
        AssertDisposeMatches("Average()",events => AverageSystem(Source(events)),events => AverageOptimized(Source(events)));
        AssertDisposeMatches("Min()",events => MinSystem(Source(events)),events => MinOptimized(Source(events)));
        AssertDisposeMatches("Max()",events => MaxSystem(Source(events)),events => MaxOptimized(Source(events)));
        AssertDisposeMatches("Aggregate(func)",events => AggregateSystem(Source(events)),events => AggregateOptimized(Source(events)));
    }

    [Fact, Trait("Category","Unit")]
    public void ElementTerminalsDisposeSourceEnumerator() {
        AssertDisposeMatches("First()",events => FirstSystem(Source(events)),events => FirstOptimized(Source(events)));
        AssertDisposeMatches("First(predicate)",events => FirstPredicateSystem(Source(events)),events => FirstPredicateOptimized(Source(events)));
        AssertDisposeMatches("FirstOrDefault()",events => FirstOrDefaultSystem(Source(events)),events => FirstOrDefaultOptimized(Source(events)));
        AssertDisposeMatches("Last()",events => LastSystem(Source(events)),events => LastOptimized(Source(events)));
        AssertDisposeMatches("LastOrDefault()",events => LastOrDefaultSystem(Source(events)),events => LastOrDefaultOptimized(Source(events)));
        AssertDisposeMatches("Single()",events => SingleSystem(SingleSource(events)),events => SingleOptimized(SingleSource(events)));
        AssertDisposeMatches("SingleOrDefault()",events => SingleOrDefaultSystem(SingleSource(events)),events => SingleOrDefaultOptimized(SingleSource(events)));
        AssertDisposeMatches("ElementAt(index)",events => ElementAtSystem(Source(events)),events => ElementAtOptimized(Source(events)));
    }

    [Fact, Trait("Category","Unit")]
    public void PredicatesDisposeSourceEnumerator() {
        AssertDisposeMatches("Any()",events => AnySystem(Source(events)),events => AnyOptimized(Source(events)));
        AssertDisposeMatches("Any(predicate)",events => AnyPredicateSystem(Source(events)),events => AnyPredicateOptimized(Source(events)));
        AssertDisposeMatches("All(predicate)",events => AllSystem(Source(events)),events => AllOptimized(Source(events)));
        AssertDisposeMatches("Contains(value)",events => ContainsSystem(Source(events)),events => ContainsOptimized(Source(events)));
    }

    [Fact, Trait("Category","Unit")]
    public void MaterializersDisposeSourceEnumerator() {
        AssertDisposeMatches("ToList()",events => ToListSystem(Source(events)),events => ToListOptimized(Source(events)));
        AssertDisposeMatches("ToArray()",events => ToArraySystem(Source(events)),events => ToArrayOptimized(Source(events)));
        AssertDisposeMatches("ToHashSet()",events => ToHashSetCountSystem(Source(events)),events => ToHashSetCountOptimized(Source(events)));
    }

    [Fact, Trait("Category","Unit")]
    public void PipelinesDisposeSourceEnumerator() {
        AssertDisposeMatches("Where().Sum()",events => WhereSumSystem(Source(events)),events => WhereSumOptimized(Source(events)));
        AssertDisposeMatches("Select().Count()",events => SelectCountSystem(Source(events)),events => SelectCountOptimized(Source(events)));
        AssertDisposeMatches("Where().First()",events => WhereFirstSystem(Source(events)),events => WhereFirstOptimized(Source(events)));
        AssertDisposeMatches("Where().ToList()",events => WhereToListSystem(Source(events)),events => WhereToListOptimized(Source(events)));
    }

    private static TrackingEnumerable<int> Source(List<string> events) => new("source",Values,events);
    private static TrackingEnumerable<int> SingleSource(List<string> events) => new("source",OneValue,events);

    // 結果が System.Linq と一致し、取得した列挙子の数と破棄した数が一致すること(破棄は取得のたびに1回)。
    private static void AssertDisposeMatches<TResult>(
        string name,
        Func<List<string>,TResult> system,
        Func<List<string>,TResult> optimized) {
        var expectedEvents=new List<string>();
        var expected=system(expectedEvents);
        var actualEvents=new List<string>();
        var actual=optimized(actualEvents);

        Assert.Equal(expected,actual);
        var expectedGets=CountEvents(expectedEvents,".GetEnumerator");
        var actualGets=CountEvents(actualEvents,".GetEnumerator");
        var actualDisposes=CountEvents(actualEvents,".Dispose");
        Assert.True(
            expectedGets==actualGets&&actualGets==actualDisposes,
            $"{name}: System.Linq GetEnumerator={expectedGets} / FLinq GetEnumerator={actualGets}, Dispose={actualDisposes}. events=[{string.Join(", ",actualEvents)}]");
    }

    private static int CountEvents(List<string> events,string suffix) {
        var count=0;
        foreach(var item in events) {
            if(item.EndsWith(suffix,StringComparison.Ordinal)) {
                count++;
            }
        }
        return count;
    }

    private static int SumSystem(IEnumerable<int> source) => source.Sum();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int SumOptimized(IEnumerable<int> source) => source.Sum();

    private static int SumSelectorSystem(IEnumerable<int> source) => source.Sum(value => value*2);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int SumSelectorOptimized(IEnumerable<int> source) => source.Sum(value => value*2);

    private static int CountSystem(IEnumerable<int> source) => source.Count();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int CountOptimized(IEnumerable<int> source) => source.Count();

    private static int CountPredicateSystem(IEnumerable<int> source) => source.Count(value => value>1);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int CountPredicateOptimized(IEnumerable<int> source) => source.Count(value => value>1);

    private static long LongCountSystem(IEnumerable<int> source) => source.LongCount();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static long LongCountOptimized(IEnumerable<int> source) => source.LongCount();

    private static double AverageSystem(IEnumerable<int> source) => source.Average();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static double AverageOptimized(IEnumerable<int> source) => source.Average();

    private static int MinSystem(IEnumerable<int> source) => source.Min();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int MinOptimized(IEnumerable<int> source) => source.Min();

    private static int MaxSystem(IEnumerable<int> source) => source.Max();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int MaxOptimized(IEnumerable<int> source) => source.Max();

    private static int AggregateSystem(IEnumerable<int> source) => source.Aggregate((left,right) => left*10+right);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int AggregateOptimized(IEnumerable<int> source) => source.Aggregate((left,right) => left*10+right);

    private static int FirstSystem(IEnumerable<int> source) => source.First();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int FirstOptimized(IEnumerable<int> source) => source.First();

    private static int FirstPredicateSystem(IEnumerable<int> source) => source.First(value => value>3);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int FirstPredicateOptimized(IEnumerable<int> source) => source.First(value => value>3);

    private static int FirstOrDefaultSystem(IEnumerable<int> source) => source.FirstOrDefault();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int FirstOrDefaultOptimized(IEnumerable<int> source) => source.FirstOrDefault();

    private static int LastSystem(IEnumerable<int> source) => source.Last();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int LastOptimized(IEnumerable<int> source) => source.Last();

    private static int LastOrDefaultSystem(IEnumerable<int> source) => source.LastOrDefault();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int LastOrDefaultOptimized(IEnumerable<int> source) => source.LastOrDefault();

    private static int SingleSystem(IEnumerable<int> source) => source.Single();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int SingleOptimized(IEnumerable<int> source) => source.Single();

    private static int SingleOrDefaultSystem(IEnumerable<int> source) => source.SingleOrDefault();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int SingleOrDefaultOptimized(IEnumerable<int> source) => source.SingleOrDefault();

    private static int ElementAtSystem(IEnumerable<int> source) => source.ElementAt(2);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int ElementAtOptimized(IEnumerable<int> source) => source.ElementAt(2);

    private static bool AnySystem(IEnumerable<int> source) => source.Any();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool AnyOptimized(IEnumerable<int> source) => source.Any();

    private static bool AnyPredicateSystem(IEnumerable<int> source) => source.Any(value => value>4);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool AnyPredicateOptimized(IEnumerable<int> source) => source.Any(value => value>4);

    private static bool AllSystem(IEnumerable<int> source) => source.All(value => value>0);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool AllOptimized(IEnumerable<int> source) => source.All(value => value>0);

    private static bool ContainsSystem(IEnumerable<int> source) => source.Contains(4);
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static bool ContainsOptimized(IEnumerable<int> source) => source.Contains(4);

    private static List<int> ToListSystem(IEnumerable<int> source) => source.ToList();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> ToListOptimized(IEnumerable<int> source) => source.ToList();

    private static int[] ToArraySystem(IEnumerable<int> source) => source.ToArray();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] ToArrayOptimized(IEnumerable<int> source) => source.ToArray();

    private static int ToHashSetCountSystem(IEnumerable<int> source) => source.ToHashSet().Count;
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int ToHashSetCountOptimized(IEnumerable<int> source) => source.ToHashSet().Count;

    private static int WhereSumSystem(IEnumerable<int> source) => source.Where(value => value>1).Sum();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int WhereSumOptimized(IEnumerable<int> source) => source.Where(value => value>1).Sum();

    private static int SelectCountSystem(IEnumerable<int> source) => source.Select(value => value+1).Count();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int SelectCountOptimized(IEnumerable<int> source) => source.Select(value => value+1).Count();

    private static int WhereFirstSystem(IEnumerable<int> source) => source.Where(value => value>3).First();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int WhereFirstOptimized(IEnumerable<int> source) => source.Where(value => value>3).First();

    private static List<int> WhereToListSystem(IEnumerable<int> source) => source.Where(value => value>1).ToList();
    [global::Seamless.FLinq.LinqIntercept(true)]
    private static List<int> WhereToListOptimized(IEnumerable<int> source) => source.Where(value => value>1).ToList();
}
