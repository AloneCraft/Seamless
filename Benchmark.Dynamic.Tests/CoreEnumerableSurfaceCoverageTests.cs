using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// System.Linq.Enumerable の公開メソッドを、チェイン1(単体の回帰ソース)とランダム生成の演算子表が
/// どこまで覆っているかを、リフレクションで数える。手書きの一覧に頼らず、LINQ側に増えたメソッドを検出する。
/// </summary>
public sealed class EnumerableSurfaceCoverageTests{
    // チェイン1に対応ファイルを置かなくてよい名前(Enumerable の公開メソッドではない補助)。
    private static readonly string[] Chain1Auxiliary=["LinqChain1TestBase","AsQueryable"];

    // 演算子ID → それが呼ぶ Enumerable のメソッド名。新しい演算子を足したら必ずここへも足す。
    private static readonly Dictionary<string,string[]> OperatorToMethods=new(StringComparer.Ordinal){
        [QueryOperatorIds.Where]=["Where"],
        [QueryOperatorIds.WhereIndexed]=["Where"],
        [QueryOperatorIds.Select]=["Select"],
        [QueryOperatorIds.SelectIndexed]=["Select"],
        [QueryOperatorIds.Distinct]=["Distinct"],
        [QueryOperatorIds.DistinctBy]=["DistinctBy"],
        [QueryOperatorIds.Take]=["Take"],
        [QueryOperatorIds.Skip]=["Skip"],
        [QueryOperatorIds.Reverse]=["Reverse"],
        [QueryOperatorIds.OrderBy]=["OrderBy","OrderByDescending"],
        [QueryOperatorIds.ThenBy]=["ThenBy","ThenByDescending"],
        [QueryOperatorIds.OrderByKey]=["OrderBy","OrderByDescending"],
        [QueryOperatorIds.TakeLast]=["TakeLast"],
        [QueryOperatorIds.SkipLast]=["SkipLast"],
        [QueryOperatorIds.TakeWhile]=["TakeWhile"],
        [QueryOperatorIds.SkipWhile]=["SkipWhile"],
        [QueryOperatorIds.Append]=["Append"],
        [QueryOperatorIds.Prepend]=["Prepend"],
        [QueryOperatorIds.DefaultIfEmpty]=["DefaultIfEmpty"],
        [QueryOperatorIds.GroupBySum]=["GroupBy"],
        [QueryOperatorIds.GroupByCount]=["GroupBy"],
        [QueryOperatorIds.GroupByKeyOnly]=["GroupBy"],
        [QueryOperatorIds.Order]=["Order","OrderDescending"],
        [QueryOperatorIds.ShuffleOrder]=["Shuffle","Order"],
        [QueryOperatorIds.Cast]=["Cast"],
        [QueryOperatorIds.OfType]=["OfType","Cast"],
        [QueryOperatorIds.AsEnumerable]=["AsEnumerable"],
        [QueryOperatorIds.ChunkFlatten]=["Chunk","SelectMany"],
        [QueryOperatorIds.IndexXor]=["Index","Select"],
        [QueryOperatorIds.LeftJoinXor]=["LeftJoin"],
        [QueryOperatorIds.RightJoinXor]=["RightJoin"],
        [QueryOperatorIds.Single]=["Single","Take"],
        [QueryOperatorIds.SingleOrDefault]=["SingleOrDefault","Take"],
        [QueryOperatorIds.Concat]=["Concat"],
        [QueryOperatorIds.Union]=["Union"],
        [QueryOperatorIds.Except]=["Except"],
        [QueryOperatorIds.Intersect]=["Intersect"],
        [QueryOperatorIds.UnionByIdentity]=["UnionBy"],
        [QueryOperatorIds.ExceptByIdentity]=["ExceptBy"],
        [QueryOperatorIds.IntersectByIdentity]=["IntersectBy"],
        [QueryOperatorIds.Zip]=["Zip"],
        [QueryOperatorIds.SelectManyXor]=["SelectMany"],
        [QueryOperatorIds.JoinXor]=["Join"],
        [QueryOperatorIds.GroupJoinXorCount]=["GroupJoin"],
        [QueryOperatorIds.Count]=["Count"],
        [QueryOperatorIds.CountWhere]=["Count"],
        [QueryOperatorIds.LongCount]=["LongCount"],
        [QueryOperatorIds.Any]=["Any"],
        [QueryOperatorIds.AnyWhere]=["Any"],
        [QueryOperatorIds.All]=["All"],
        [QueryOperatorIds.Contains]=["Contains"],
        [QueryOperatorIds.Sum]=["Sum"],
        [QueryOperatorIds.Min]=["Min"],
        [QueryOperatorIds.Max]=["Max"],
        [QueryOperatorIds.MinBy]=["MinBy"],
        [QueryOperatorIds.MaxBy]=["MaxBy"],
        [QueryOperatorIds.Average]=["Average"],
        [QueryOperatorIds.AverageSelector]=["Average"],
        [QueryOperatorIds.AggregateXor]=["Aggregate"],
        [QueryOperatorIds.AggregateSum]=["Aggregate"],
        [QueryOperatorIds.ToIntPair]=[],
        [QueryOperatorIds.ToIntGroup]=[],
        [QueryOperatorIds.First]=["First"],
        [QueryOperatorIds.FirstOrDefault]=["FirstOrDefault"],
        [QueryOperatorIds.Last]=["Last"],
        [QueryOperatorIds.LastOrDefault]=["LastOrDefault"],
        [QueryOperatorIds.ElementAt]=["ElementAt"],
        [QueryOperatorIds.ElementAtOrDefault]=["ElementAtOrDefault"],
        [QueryOperatorIds.ToArray]=["ToArray"],
        [QueryOperatorIds.ToList]=["ToList"],
        [QueryOperatorIds.ToHashSet]=["ToHashSet"],
        [QueryOperatorIds.ToDictionary]=["ToDictionary"],
        [QueryOperatorIds.ToLookup]=["ToLookup"],
        [QueryOperatorIds.CountBy]=["CountBy"],
        [QueryOperatorIds.AggregateBy]=["AggregateBy"],
        [QueryOperatorIds.SequenceEqual]=["SequenceEqual"],
    };

    // 演算子表ではなく「元の列」の種類として生成される名前。
    private static readonly string[] CoveredBySource=["Range"];

    // ランダム生成に載っていない Enumerable のメソッド名(増減とも検出する)。
    // TryGetNonEnumeratedCount は、列の作り方によって true/false が変わる性質上、System と FLinq の結果が正当に食い違うため比較対象にしない
    // (チェイン1の単体テストで確かめる)。Empty / Repeat / Sequence / InfiniteSequence は「元の列」の種類なので、載せるなら元の列の拡張になる。
    private static readonly string[] NotInRandomGeneration=[
        "Empty","InfiniteSequence","Repeat","Sequence","TryGetNonEnumeratedCount",
    ];

    private static string[] EnumerableMethodNames()=>typeof(Enumerable)
        .GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)
        .Select(method=>method.Name)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    [Fact,Trait("Category","Unit")]
    public void チェイン1はEnumerableの公開メソッドを名前の単位で全て含む(){
        var chain1=RegressionCaseSources.Paths
            .Where(path=>path.StartsWith("System/Linq/Intercept/Chain1/",StringComparison.Ordinal))
            .Select(path=>Path.GetFileNameWithoutExtension(path))
            .ToHashSet(StringComparer.Ordinal);
        var missing=EnumerableMethodNames().Where(name=>!chain1.Contains(name)).ToArray();
        Assert.True(missing.Length==0,"チェイン1に無い Enumerable のメソッド: "+string.Join(", ",missing));
        var unknown=chain1.Where(name=>!Chain1Auxiliary.Contains(name)&&!EnumerableMethodNames().Contains(name)).ToArray();
        Assert.True(unknown.Length==0,"Enumerable に存在しないチェイン1のファイル: "+string.Join(", ",unknown));
    }

    [Fact,Trait("Category","Unit")]
    public void 全ての演算子IDがメソッド名の対応表に載っている(){
        var unmapped=BuiltInOperatorCatalog.All.Select(item=>item.OperatorId).Where(id=>!OperatorToMethods.ContainsKey(id)).ToArray();
        Assert.True(unmapped.Length==0,"EnumerableSurfaceCoverageTests.OperatorToMethods に無い演算子ID: "+string.Join(", ",unmapped));
        var stale=OperatorToMethods.Keys.Where(id=>!BuiltInOperatorCatalog.TryGet(id,out _)).ToArray();
        Assert.True(stale.Length==0,"演算子表に無い対応表の演算子ID: "+string.Join(", ",stale));
    }

    [Fact,Trait("Category","Unit")]
    public void ランダム生成に載っていないメソッドは一覧と一致する(){
        var covered=BuiltInOperatorCatalog.All
            .SelectMany(item=>OperatorToMethods[item.OperatorId])
            .Concat(CoveredBySource)
            .ToHashSet(StringComparer.Ordinal);
        var notCovered=EnumerableMethodNames().Where(name=>!covered.Contains(name)).ToArray();
        Assert.Equal(NotInRandomGeneration.Order(StringComparer.Ordinal).ToArray(),notCovered);
    }
}
