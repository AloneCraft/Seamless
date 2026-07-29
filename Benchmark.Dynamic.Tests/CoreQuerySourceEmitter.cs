using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class QuerySourceEmitterTests{
    [Fact,Trait("Category","Unit")]public void Emit(){
        var generator=new Core.RandomQueryGenerator();
        var emitter=new Core.QuerySourceEmitter();
        var options=new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("42"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=3,
            MaximumDepth=3,
            InputElementCount=32,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Where,QueryOperatorIds.Select,QueryOperatorIds.Count],
        };
        {
            var document=generator.Generate(options);
            var first=emitter.Emit(document);
            var second=emitter.Emit(document);
            Assert.Equal(first.SourceText,second.SourceText);
            Assert.Equal(first.SourceHash,second.SourceHash);
            Assert.Contains("RunSystem(object? primary,object? secondary)",first.SourceText,StringComparison.Ordinal);
            Assert.Contains("RunFLinqUnoptimized(object? primary,object? secondary)",first.SourceText,StringComparison.Ordinal);
            Assert.Contains("LinqOptimizations.None",first.SourceText,StringComparison.Ordinal);
            Assert.Contains("RunFLinq(object? primary,object? secondary)",first.SourceText,StringComparison.Ordinal);
            Assert.Contains("LinqConvergenceMode.GlobalFixedPoint",first.SourceText,StringComparison.Ordinal);
            Assert.Contains("EmitOptimizationTrace=true",first.SourceText,StringComparison.Ordinal);
            Assert.Contains("RunZLinq(object? primary,object? secondary)",first.SourceText,StringComparison.Ordinal);
            Assert.Contains("global::ZLinq.ValueEnumerable.AsValueEnumerable",first.SourceText,StringComparison.Ordinal);
            Assert.Contains("global::Benchmark.Dynamic.Core.DynamicQueryValue",first.SourceText,StringComparison.Ordinal);
        }
        {
            foreach(var target in Enum.GetValues<SourceTarget>()){
                var document=generator.Generate(options with{
                    SourceTarget=target,
                    OperatorCount=1,
                    MaximumDepth=1,
                    AllowedOperatorIds=[QueryOperatorIds.Count],
                });
                var result=emitter.Emit(document);
                Assert.Contains("global::System.Linq.Enumerable.Count",result.SourceText,StringComparison.Ordinal);
                Assert.Contains("global::ZLinq",result.SourceText,StringComparison.Ordinal);
                if(target==SourceTarget.SeamlessPersisted){
                    Assert.Contains("global::Seamless.Collections.Persisted.Set",result.SourceText,StringComparison.Ordinal);
                }
                if(target==SourceTarget.SeamlessTsavorite){
                    Assert.Contains("global::Seamless.Collections.Tsavorite.HashSet",result.SourceText,StringComparison.Ordinal);
                }
                if(target==SourceTarget.SystemRange){
                    Assert.Contains("global::Benchmark.Dynamic.Core.RangeSourceFixture",result.SourceText,StringComparison.Ordinal);
                    Assert.Contains("global::ZLinq.ValueEnumerable.Range",result.SourceText,StringComparison.Ordinal);
                }
            }
        }
    }

    [Theory,Trait("Category","Unit")]
    [InlineData(QueryOperatorIds.ToIntPair,"IntPairQueryAtom")]
    [InlineData(QueryOperatorIds.ToIntGroup,"IntGroupQueryAtom")]
    public void 閉じた複合型Terminalを4経路へ生成する(string operatorID,string atomType){
        var document=new Core.RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0xC105ED"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=1,
            MaximumDepth=1,
            InputElementCount=4,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[operatorID],
        });

        var result=new Core.QuerySourceEmitter().Emit(document);

        Assert.Equal(operatorID,document.Root.OperatorId);
        Assert.Contains(atomType,result.SourceText,StringComparison.Ordinal);
        Assert.Contains("global::System.Linq.Enumerable",result.SourceText,StringComparison.Ordinal);
        Assert.Contains("global::ZLinq",result.SourceText,StringComparison.Ordinal);
        Assert.Equal(4,result.MethodNames.Count);
    }

    [Theory,Trait("Category","Unit")]
    [InlineData(QueryOperatorIds.ToDictionary,"CreateCanonicalDictionaryPairs",QueryValueType.IntPair)]
    [InlineData(QueryOperatorIds.ToLookup,"CreateCanonicalLookupGroups",QueryValueType.IntGroup)]
    public void 辞書系Materializerを閉じた順序付きSequenceへ生成する(
        string operatorID,
        string helperName,
        QueryValueType outputType){
        var document=new Core.RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0xD1C7"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=1,
            MaximumDepth=1,
            InputElementCount=4,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[operatorID],
        });

        var result=new Core.QuerySourceEmitter().Emit(document);

        Assert.Equal(outputType,document.Root.OutputType);
        Assert.Equal(QueryCardinality.Sequence,document.Root.OutputCardinality);
        Assert.Equal(QueryOrderSemantics.TotalOrdered,document.Root.OrderSemantics);
        Assert.Contains(helperName,result.SourceText,StringComparison.Ordinal);
        Assert.Contains("global::System.Linq.Enumerable",result.SourceText,StringComparison.Ordinal);
        Assert.Contains("global::ZLinq",result.SourceText,StringComparison.Ordinal);
    }
}
