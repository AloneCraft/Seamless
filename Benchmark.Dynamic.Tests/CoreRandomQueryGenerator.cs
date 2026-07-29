using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class RandomQueryGeneratorTests{
    [Fact,Trait("Category","Unit")]
    public void CommonSetは全8Sourceの積集合を保ちつつIDへ選択Sourceを含める(){
        var arrayOnly=QueryCapabilityProfiles.CreateSet(
            OperatorSetMode.Common,
            [SourceTarget.SystemArray]);
        var persistedOnly=QueryCapabilityProfiles.CreateSet(
            OperatorSetMode.Common,
            [SourceTarget.SystemPersisted]);
        var reordered=QueryCapabilityProfiles.CreateSet(
            OperatorSetMode.Common,
            [SourceTarget.SystemPersisted,SourceTarget.SystemArray]);
        var canonicalOrder=QueryCapabilityProfiles.CreateSet(
            OperatorSetMode.Common,
            [SourceTarget.SystemArray,SourceTarget.SystemPersisted]);

        Assert.Equal(arrayOnly.OperatorIds,persistedOnly.OperatorIds);
        Assert.Equal([SourceTarget.SystemArray],arrayOnly.SourceTargets);
        Assert.Equal([SourceTarget.SystemPersisted],persistedOnly.SourceTargets);
        Assert.NotEqual(arrayOnly.CapabilityId,persistedOnly.CapabilityId);
        Assert.Equal(canonicalOrder.CapabilityId,reordered.CapabilityId);
        Assert.Equal(canonicalOrder.SourceTargets,reordered.SourceTargets);
    }

    [Fact,Trait("Category","Unit")]
    public void N64のIRJSONは派生Childrenを重複出力しない(){
        var capability=QueryCapabilityProfiles.CreateSet(
            OperatorSetMode.Maximum,
            [SourceTarget.SystemArray]);
        var generated=new Core.RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0x6400640064006400"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=64,
            MaximumDepth=32,
            InputElementCount=32,
            EnableBinaryOperators=true,
            EnableNestedOperators=true,
            AllowedOperatorIds=capability.OperatorIds,
        });

        var json=QueryIrJson.Serialize(generated);

        Assert.DoesNotContain("\"children\"",json,StringComparison.OrdinalIgnoreCase);
        Assert.InRange(System.Text.Encoding.UTF8.GetByteCount(json),1,256_000);
        Assert.Equal(64,QueryIrJson.Deserialize(json).ActualOperatorCount);
    }

    [Fact,Trait("Category","Unit")]
    public void N64でも上限内の深いNested木をretryなしで生成できる(){
        var generatedDocuments=Enumerable.Range(0,64).Select(index=>
            new Core.RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
                MasterSeed=Core.Seed256.FromUInt64((ulong)index+1),
                QueryIndex=index,
                SourceTarget=SourceTarget.SystemArray,
                OperatorCount=64,
                MaximumDepth=32,
                InputElementCount=32,
                MaximumCardinality=100_000,
                EnableBinaryOperators=true,
                EnableNestedOperators=true,
                AllowedOperatorIds=[
                    QueryOperatorIds.Select,
                    QueryOperatorIds.Concat,
                    QueryOperatorIds.JoinXor,
                    QueryOperatorIds.Count,
                ],
            })).ToArray();

        Assert.All(generatedDocuments,static document=>{
            Assert.Equal(64,document.ActualOperatorCount);
            Assert.InRange(document.ActualDepth,1,32);
            Assert.Empty(QueryIrValidator.Validate(document));
        });
        Assert.Contains(
            generatedDocuments,
            static document=>Enumerate(document.Root).Any(static node=>node is NestedQueryNode));
    }

    [Fact,Trait("Category","Corpus")]
    public void 固定SeedCorpusはexact生成してJSON往復できる(){
        var generator=new Core.RandomQueryGenerator();
        var configured=Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_RANDOM_CORPUS");
        var corpusCount=int.TryParse(configured,out var parsed)?Math.Clamp(parsed,256,10_000):256;
        var extended=corpusCount>256;
        var targets=Enum.GetValues<SourceTarget>();
        for(var index=0;index<corpusCount;index++){
            var operatorCount=1+(index%(extended?64:16));
            var mode=index%2==0?OperatorSetMode.Common:OperatorSetMode.Maximum;
            var target=extended?targets[index%targets.Length]:SourceTarget.SystemArray;
            var capability=QueryCapabilityProfiles.CreateSet(mode,[target]);
            var options=new RandomQueryGenerationOptions{
                MasterSeed=Core.Seed256.FromUInt64(unchecked(0x9E3779B97F4A7C15UL+(ulong)index)),
                QueryIndex=index,
                SourceTarget=target,
                OperatorCount=operatorCount,
                MaximumDepth=Math.Min(operatorCount,extended?32:8),
                InputElementCount=32,
                MaximumCardinality=100_000,
                EnableBinaryOperators=true,
                EnableNestedOperators=true,
                AllowedOperatorIds=capability.OperatorIds,
            };

            var generated=generator.Generate(options);
            var json=QueryIrJson.Serialize(generated);
            var restored=QueryIrJson.Deserialize(json);

            Assert.Equal(operatorCount,generated.ActualOperatorCount);
            Assert.InRange(generated.ActualDepth,1,options.MaximumDepth);
            Assert.Equal(json,QueryIrJson.Serialize(restored));
            Assert.Equal(QueryIrJson.ComputeHash(generated),QueryIrJson.ComputeHash(restored));
            Assert.DoesNotContain(
                Enumerate(generated.Root).OfType<UnaryQueryNode>(),
                static node=>node.Arguments is SelectOperatorArguments {Selector.Kind:QuerySelectorKind.Multiply});
        }
    }

    [Fact,Trait("Category","Unit")]
    public void BEEFの非空TerminalをJSON往復してもCardinalityを保持する(){
        var generator=new Core.RandomQueryGenerator();
        var generated=generator.Generate(new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0xBEEF"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=1,
            MaximumDepth=1,
            InputElementCount=8,
            EnableBinaryOperators=true,
            EnableNestedOperators=true,
        });

        var json=QueryIrJson.Serialize(generated);
        var restored=QueryIrJson.Deserialize(json);

        Assert.Equal(generated.Root.Arguments.Kind,restored.Root.Arguments.Kind);
        Assert.Equal(QueryCardinalityRange.Exact(8),restored.Root.Input.Cardinality);
        Assert.Empty(QueryIrValidator.Validate(restored));
    }

    [Fact,Trait("Category","Unit")]
    public void IRValidatorは偽装cardinalityと実行入力件数不一致を拒否する(){
        var generated=new Core.RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0xBADCA4D"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=2,
            MaximumDepth=2,
            InputElementCount=8,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Where,QueryOperatorIds.Count],
        });
        var where=Assert.IsType<UnaryQueryNode>(generated.Root.Input);
        var forgedWhere=where with{Cardinality=QueryCardinalityRange.Exact(8)};
        var forgedRoot=generated.Root with{Input=forgedWhere};
        var forged=generated with{Root=forgedRoot};

        var metadataErrors=QueryIrValidator.Validate(forged);
        var executionErrors=QueryIrValidator.ValidateForExecution(generated,7);

        Assert.Contains(metadataErrors,static error=>error.Contains("cardinality",StringComparison.OrdinalIgnoreCase));
        Assert.Contains(executionErrors,static error=>error.Contains("input count",StringComparison.OrdinalIgnoreCase));
    }

    [Fact,Trait("Category","Unit")]public void Generate(){
        var generator=new Core.RandomQueryGenerator();
        var seed=Core.Seed256.Parse("0x123456789ABCDEF0");
        var options=new RandomQueryGenerationOptions{
            MasterSeed=seed,
            QueryIndex=17,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=8,
            MaximumDepth=8,
            InputElementCount=32,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
        };
        {
            var first=generator.Generate(options);
            var second=generator.Generate(options);
            Assert.Equal(QueryIrJson.Serialize(first),QueryIrJson.Serialize(second));
            Assert.Equal(QueryIrJson.ComputeHash(first),QueryIrJson.ComputeHash(second));
            Assert.Equal(8,first.ActualOperatorCount);
            Assert.InRange(first.ActualDepth,1,8);
            Assert.Equal(QueryNodeKind.Terminal,first.Root.NodeKind);
            Assert.Empty(QueryIrValidator.Validate(first));
        }
        {
            for(var count=1;count<=16;count++){
                var document=generator.Generate(options with{
                    QueryIndex=count,
                    OperatorCount=count,
                    MaximumDepth=count,
                });
                Assert.Equal(count,document.ActualOperatorCount);
                Assert.InRange(document.ActualDepth,1,count);
                Assert.All(Enumerate(document.Root),node=>Assert.True(Enum.IsDefined(node.OutputType)));
            }
        }
        {
            Assert.Throws<ArgumentException>(()=>generator.Generate(options with{
                OperatorCount=3,
                MaximumDepth=2,
                EnableBinaryOperators=false,
            }));
        }
    }

    private static IEnumerable<QueryNode> Enumerate(QueryNode node){
        yield return node;
        foreach(var child in node.Children){
            foreach(var descendant in Enumerate(child)){
                yield return descendant;
            }
        }
    }
}
