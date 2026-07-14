namespace Test.System.Linq.Intercept.Optimization;

public sealed class JoinHashing:OptimizationTestBase {
    [Fact, Trait("Category","Unit")]
    public static void ComputedOuterKey() {
        AssertSequenceEqual(Outers().Join(Inners(),o => o.Key+GetOffset(),i => i.Key,(o,i) => o.Id+i.Id).ToList(),ComputedOuterKeyOptimized());
        AssertSequenceEqual(Outers().Join(Inners(),o => o.Key+GetOffset(),i => i.Key,(o,i) => o.Id+i.Id).ToList(),ComputedOuterKeyNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ComputedOuterKeyOptimized() =>
        Outers().Join(Inners(),o => o.Key+GetOffset(),i => i.Key,(o,i) => o.Id+i.Id).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> ComputedOuterKeyNoOptimized() =>
        Outers().Join(Inners(),o => o.Key+GetOffset(),i => i.Key,(o,i) => o.Id+i.Id).ToList();

    [Fact, Trait("Category","Unit")]
    public static void DuplicateInnerKeysMultiBucket() {
        AssertSequenceEqual(Outers().Join(Inners(),o => o.Key,i => i.Key,(o,i) => o.Id*100+i.Id).ToList(),DuplicateInnerKeysMultiBucket_MatchOptimized());
        AssertSequenceEqual(Outers().Join(Inners(),o => o.Key,i => i.Key,(o,i) => o.Id*100+i.Id).ToList(),DuplicateInnerKeysMultiBucket_MatchNoOptimized());
        AssertSequenceEqual(Outers().Join(Inners(),o => o.Key+1000,i => i.Key,(o,i) => o.Id*100+i.Id).ToList(),DuplicateInnerKeysMultiBucket_NoMatchOptimized());
        AssertSequenceEqual(Outers().Join(Inners(),o => o.Key+1000,i => i.Key,(o,i) => o.Id*100+i.Id).ToList(),DuplicateInnerKeysMultiBucket_NoMatchNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> DuplicateInnerKeysMultiBucket_MatchOptimized() =>
        Outers().Join(Inners(),o => o.Key,i => i.Key,(o,i) => o.Id*100+i.Id).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> DuplicateInnerKeysMultiBucket_MatchNoOptimized() =>
        Outers().Join(Inners(),o => o.Key,i => i.Key,(o,i) => o.Id*100+i.Id).ToList();

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> DuplicateInnerKeysMultiBucket_NoMatchOptimized() =>
        Outers().Join(Inners(),o => o.Key+1000,i => i.Key,(o,i) => o.Id*100+i.Id).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> DuplicateInnerKeysMultiBucket_NoMatchNoOptimized() =>
        Outers().Join(Inners(),o => o.Key+1000,i => i.Key,(o,i) => o.Id*100+i.Id).ToList();

    [Fact, Trait("Category","Unit")]
    public static void JoinAnonymousResult() {
        var expected = Outers().Join(Inners(),o => o.Key,i => i.Key,(o,i) => new { O = o.Id,I = i.Id })
            .Select(static item => $"{item.O}:{item.I}")
            .ToList();
        AssertSequenceEqual(expected,JoinAnonymousResultOptimized());
        AssertSequenceEqual(expected,JoinAnonymousResultNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> JoinAnonymousResultOptimized() =>
        Outers().Join(Inners(),o => o.Key,i => i.Key,(o,i) => $"{o.Id}:{i.Id}").ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<string> JoinAnonymousResultNoOptimized() =>
        Outers().Join(Inners(),o => o.Key,i => i.Key,(o,i) => $"{o.Id}:{i.Id}").ToList();

    [Fact, Trait("Category","Unit")]
    public static void GroupJoinWithEmptyGroups() {
        AssertSequenceEqual(Outers().GroupJoin(Inners(),o => o.Key,i => i.Key,(o,g) => o.Id*1000+g.Select(static x => x.Id).Sum()).ToList(),GroupJoinWithEmptyGroupsOptimized());
        AssertSequenceEqual(Outers().GroupJoin(Inners(),o => o.Key,i => i.Key,(o,g) => o.Id*1000+g.Select(static x => x.Id).Sum()).ToList(),GroupJoinWithEmptyGroupsNoOptimized());
    }

    [global::Seamless.FLinq.LinqIntercept(true,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GroupJoinWithEmptyGroupsOptimized() =>
        Outers().GroupJoin(Inners(),o => o.Key,i => i.Key,(o,g) => o.Id*1000+g.Select(static x => x.Id).Sum()).ToList();

    [global::Seamless.FLinq.LinqIntercept(false,global::Seamless.FLinq.LinqInstrumentation.Counter)]
    private static List<int> GroupJoinWithEmptyGroupsNoOptimized() =>
        Outers().GroupJoin(Inners(),o => o.Key,i => i.Key,(o,g) => o.Id*1000+g.Select(static x => x.Id).Sum()).ToList();
}
