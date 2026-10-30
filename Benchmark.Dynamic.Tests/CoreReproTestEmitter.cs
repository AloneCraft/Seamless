using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class ReproTestEmitterTests{
    private static QueryIrDocument Generate(SourceTarget target,params string[] operators){
        return new Core.RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0x5E9"),
            QueryIndex=0,
            QueryId="repro-query-1",
            SourceTarget=target,
            OperatorCount=operators.Length,
            MaximumDepth=operators.Length,
            InputElementCount=4,
            EnableBinaryOperators=true,
            EnableNestedOperators=false,
            AllowedOperatorIds=operators,
        });
    }

    [Fact,Trait("Category","Unit")]
    public void 再現テストは生成クラスとxUnitのテストを一つのファイルにまとめる(){
        var document=Generate(SourceTarget.SystemArray,QueryOperatorIds.Select,QueryOperatorIds.ToArray);

        var text=ReproTestEmitter.Emit(document,[1,2,3,4],[5,6,7,8],new ReproTestOptions{Note="Status=SemanticMismatch"});

        Assert.Contains("namespace Test.System.Linq.Enumerable.FLinq.FailedQueries;",text,StringComparison.Ordinal);
        Assert.Contains("using Xunit;",text,StringComparison.Ordinal);
        Assert.Contains("public sealed class Probe{",text,StringComparison.Ordinal);
        Assert.Contains("public sealed class Repro_repro_query_1{",text,StringComparison.Ordinal);
        Assert.Contains("[Fact,Trait(\"Category\",\"FuzzFailure\")]",text,StringComparison.Ordinal);
        Assert.Contains("var primary=new int[]{1,2,3,4};",text,StringComparison.Ordinal);
        Assert.Contains("var secondary=new int[]{5,6,7,8};",text,StringComparison.Ordinal);
        Assert.Contains("Probe.RunSystem(primary,secondary)",text,StringComparison.Ordinal);
        Assert.Contains("// Status=SemanticMismatch",text,StringComparison.Ordinal);
        Assert.Contains("source.Select(",text,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void 再現テストはZLinqを参照しない(){
        var document=Generate(SourceTarget.SystemArray,QueryOperatorIds.Where,QueryOperatorIds.Count);

        var text=ReproTestEmitter.Emit(document,[1,2,3,4],[5,6,7,8]);

        Assert.DoesNotContain("ZLinq",text,StringComparison.Ordinal);
        Assert.DoesNotContain("RunZLinq",text,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void 既定の生成クラスは従来どおりZLinqを含む(){
        var document=Generate(SourceTarget.SystemArray,QueryOperatorIds.Where,QueryOperatorIds.Count);

        var emitted=new Core.QuerySourceEmitter().Emit(document);

        Assert.Contains("using ZLinq;",emitted.SourceText,StringComparison.Ordinal);
        Assert.Equal(4,emitted.MethodNames.Count);
    }

    [Fact,Trait("Category","Unit")]
    public void SourceごとにFixtureの書き方が変わり永続系は対象外(){
        var list=Generate(SourceTarget.SystemList,QueryOperatorIds.Where,QueryOperatorIds.Count);
        Assert.Contains(
            "var primary=new global::System.Collections.Generic.List<int>{3,4,5};",
            ReproTestEmitter.Emit(list,[3,4,5],[1]),
            StringComparison.Ordinal);

        var range=Generate(SourceTarget.SystemRange,QueryOperatorIds.Where,QueryOperatorIds.Count);
        Assert.Contains(
            "new global::Benchmark.Dynamic.Core.RangeSourceFixture(10,3)",
            ReproTestEmitter.Emit(range,[10,11,12],[20,21]),
            StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(()=>ReproTestEmitter.Emit(range,[10,12],[20]));

        var persisted=Generate(SourceTarget.SystemPersisted,QueryOperatorIds.Where,QueryOperatorIds.Count);
        Assert.Throws<NotSupportedException>(()=>ReproTestEmitter.Emit(persisted,[1],[2]));
    }
}
