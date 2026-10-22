using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class ExhaustiveQueryEnumeratorTests{
    [Fact,Trait("Category","Unit")]
    public void 番号の復号は混合基数で全組み合わせを重複なく列挙する(){
        string[] intermediates=["a","b","c"];
        string[] terminals=["x","y"];

        Assert.Equal(6,ExhaustiveQueryEnumerator.Count(1,intermediates,terminals));
        Assert.Equal(18,ExhaustiveQueryEnumerator.Count(2,intermediates,terminals));

        var all=ExhaustiveQueryEnumerator.Enumerate(2,intermediates,terminals).ToArray();
        Assert.Equal(18,all.Length);
        Assert.Equal(18,all.Select(static spec=>string.Join('>',spec.Operators)+"|"+spec.Terminal).Distinct().Count());
        Assert.Equal(["a","a"],all[0].Operators);
        Assert.Equal("x",all[0].Terminal);
        Assert.Equal(["c","c"],all[17].Operators);
        Assert.Equal("y",all[17].Terminal);
        Assert.Equal(all[7].Operators,ExhaustiveQueryEnumerator.Decode(7,2,intermediates,terminals).Operators);
        Assert.Equal(all.Skip(5).Select(static spec=>spec.Ordinal),ExhaustiveQueryEnumerator.Enumerate(2,intermediates,terminals,5).Select(static spec=>spec.Ordinal));
    }

    [Fact,Trait("Category","Unit")]
    public void 深さ2の全組み合わせは決定的に生成でき成り立たない並びだけが除かれる(){
        var target=SourceTarget.SystemArray;
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        string[] terminals=[QueryOperatorIds.ToArray,QueryOperatorIds.Count,QueryOperatorIds.Sum];
        Assert.Contains(QueryOperatorIds.Where,intermediates);
        Assert.Contains(QueryOperatorIds.Zip,intermediates);
        Assert.Contains(QueryOperatorIds.OrderByKey,intermediates);
        Assert.DoesNotContain(QueryOperatorIds.SelectManyXor,intermediates);
        Assert.DoesNotContain(QueryOperatorIds.Count,intermediates);

        var seed=Core.Seed256.Parse("0xE4A5");
        var generated=0;
        var skipped=new List<string>();
        foreach(var spec in ExhaustiveQueryEnumerator.Enumerate(1,intermediates,terminals)){
            if(!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,16,out var document)){
                skipped.Add(spec.Operators[0]+">"+spec.Terminal);
                continue;
            }
            generated++;
            Assert.Empty(QueryIrValidator.Validate(document!));
            Assert.Equal(spec.Operators[0],AssertSingleOperator(document!));
            Assert.Equal(spec.Terminal,document!.Root.OperatorId);
            Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,16,out var again));
            Assert.Equal(QueryIrJson.ComputeHash(document),QueryIrJson.ComputeHash(again!));
        }
        Assert.Equal(intermediates.Count*terminals.Length,generated+skipped.Count);
        Assert.True(generated>intermediates.Count,$"generated={generated}");
        // 配列の入力は順序付きなので、ThenBy(全順序の入力が要る)だけが成り立たない。
        Assert.All(skipped,static item=>Assert.StartsWith(QueryOperatorIds.ThenBy,item,StringComparison.Ordinal));
    }

    [Fact,Trait("Category","Unit")]
    public void 順序なしSourceでは位置依存の演算子の並びが除かれる(){
        var target=SourceTarget.SystemPersisted;
        var seed=Core.Seed256.Parse("0xE4A6");
        var spec=new ExhaustiveQuerySpec(0,[QueryOperatorIds.Take],QueryOperatorIds.Count);
        Assert.False(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,16,out var document));
        Assert.Null(document);
        Assert.DoesNotContain(QueryOperatorIds.Zip,ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target));
    }

    private static string AssertSingleOperator(QueryIrDocument document){
        var node=Assert.IsAssignableFrom<QueryNode>(document.Root.Input);
        return node.OperatorId;
    }
}
