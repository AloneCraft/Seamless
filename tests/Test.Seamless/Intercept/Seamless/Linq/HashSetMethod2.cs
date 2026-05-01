using System.Linq;
using System.Reflection;
using SeamlessEnumerable = Seamless.Linq.Enumerable;
using SeamlessHashSet = Seamless.Linq.HashSet;

namespace Test.Seamless.Intercept.Seamless.Linq;

public sealed class HashSetMethod2 {
    private static readonly string[] HashSetSequenceProducingMethods = [
        "AggregateBy",
        "Append",
        "AsEnumerable",
        "Chunk",
        "Concat",
        "CountBy",
        "DefaultIfEmpty",
        "Distinct",
        "DistinctBy",
        "Except",
        "ExceptBy",
        "GroupBy",
        "GroupJoin",
        "Index",
        "Intersect",
        "IntersectBy",
        "Join",
        "LeftJoin",
        "Order",
        "OrderBy",
        "OrderByDescending",
        "OrderDescending",
        "Prepend",
        "Reverse",
        "RightJoin",
        "Select",
        "SelectMany",
        "Shuffle",
        "Skip",
        "SkipLast",
        "SkipWhile",
        "Take",
        "TakeLast",
        "TakeWhile",
        "Union",
        "UnionBy",
        "Where",
        "Zip"
    ];

    private static readonly string[] EnumerableSourceAcceptingMethods = [
        "Aggregate",
        "AggregateBy",
        "All",
        "Any",
        "Append",
        "AsEnumerable",
        "Average",
        "Cast",
        "Chunk",
        "Concat",
        "Contains",
        "Count",
        "CountBy",
        "DefaultIfEmpty",
        "Distinct",
        "DistinctBy",
        "ElementAt",
        "ElementAtOrDefault",
        "Except",
        "ExceptBy",
        "First",
        "FirstOrDefault",
        "GroupBy",
        "GroupJoin",
        "Index",
        "Intersect",
        "IntersectBy",
        "Join",
        "Last",
        "LastOrDefault",
        "LeftJoin",
        "LongCount",
        "Max",
        "MaxBy",
        "Min",
        "MinBy",
        "OfType",
        "Order",
        "OrderBy",
        "OrderByDescending",
        "OrderDescending",
        "Prepend",
        "Reverse",
        "RightJoin",
        "Select",
        "SelectMany",
        "SequenceEqual",
        "Shuffle",
        "Single",
        "SingleOrDefault",
        "Skip",
        "SkipLast",
        "SkipWhile",
        "Sum",
        "Take",
        "TakeLast",
        "TakeWhile",
        "ToArray",
        "ToDictionary",
        "ToHashSet",
        "ToList",
        "ToLookup",
        "TryGetNonEnumeratedCount",
        "Union",
        "UnionBy",
        "Where",
        "Zip"
    ];

    private static readonly string[] OrderedHashSetSequenceProducingMethods = [
        "Order",
        "OrderBy",
        "OrderByDescending",
        "OrderDescending"
    ];

    private static readonly string[] OrderedEnumerableSourceAcceptingMethods = [
        "ThenBy",
        "ThenByDescending"
    ];

    public static IReadOnlyList<(string First,string Second)> ComposableMethodPairs { get; } = BuildComposableMethodPairs();

    public int Smoke(global::Seamless.Collections.IHashSet<int> source) {
        var total = 0;
        total+=Where_Select(source).Sum();
        total+=Select_Where(source).Sum();
        return total;
    }

    public IEnumerable<int> Where_Select(global::Seamless.Collections.IHashSet<int> source) => SeamlessEnumerable.Select(SeamlessHashSet.Where(source,static value => value%2==0),static value => value*10);

    public IEnumerable<int> Select_Where(global::Seamless.Collections.IHashSet<int> source) => SeamlessEnumerable.Where(SeamlessHashSet.Select(source,static value => value*10),static value => value>20);

    [Fact, Trait("Category","Unit")]
    public void SmokeTest() {
        using var store = global::Seamless.Stores.HashStore.Create().CreateManagedHashSet<int>();
        using var source = store.Create();
        foreach(var value in new[] { 1,2,3,4 }) {
            source.Add(value);
        }
        var sample = new HashSetMethod2();

        Assert.Equal(130,sample.Smoke(source));
        Assert.Contains(("Where","Select"),ComposableMethodPairs);
        Assert.Contains(("Select","Where"),ComposableMethodPairs);
        Assert.Contains(("OrderBy","ThenBy"),ComposableMethodPairs);
        Assert.DoesNotContain(("Where","ThenBy"),ComposableMethodPairs);
        Assert.True(HasGeneratedInterceptor(nameof(Where_Select)));
        Assert.True(HasGeneratedInterceptor(nameof(Select_Where)));
    }

    private static IReadOnlyList<(string First,string Second)> BuildComposableMethodPairs() {
        var pairs = new List<(string First,string Second)>();
        foreach(var first in HashSetSequenceProducingMethods) {
            foreach(var second in EnumerableSourceAcceptingMethods) {
                pairs.Add((first,second));
            }
        }
        foreach(var first in OrderedHashSetSequenceProducingMethods) {
            foreach(var second in OrderedEnumerableSourceAcceptingMethods) {
                pairs.Add((first,second));
            }
        }
        return pairs;
    }

    private static bool HasGeneratedInterceptor(string methodName) {
        var interceptorType = typeof(HashSetMethod2).Assembly.GetType("Seamless.SourceGenerator.Generated.ContainerMethodInterceptors");
        Assert.NotNull(interceptorType);
        return interceptorType.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .Any(method => method.Name.Contains($"HashSet2_{methodName}_",StringComparison.Ordinal));
    }
}
