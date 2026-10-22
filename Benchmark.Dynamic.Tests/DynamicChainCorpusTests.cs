using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// チェーン検証(Sample.FLinqChainVerifier)の「ペア網羅」を、Benchmark.Dynamic の実Worker経路で回す。
/// 中間演算子1個+終端1個の全組み合わせを、System / FLinq(最適化なし・あり) / ZLinq の各経路で
/// コンパイルして結果を比べる。
/// </summary>
[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicChainCorpusTests{
    private const int InputElementCount=16;

    // チェーン検証のペア網羅が使っていた終端(ToArray/Count/Sum/FirstOrDefault/Any)。
    private static readonly string[] PairTerminals=[
        QueryOperatorIds.ToArray,
        QueryOperatorIds.Count,
        QueryOperatorIds.Sum,
        QueryOperatorIds.FirstOrDefault,
        QueryOperatorIds.Any,
    ];

    // 2026-10-22 までに見つかった FLinq の不具合は全て直した(表は空)。
    // 新しく見つかった不具合は、直すか、ここに並びと理由を記録する(直ったら外す)。
    private static readonly Dictionary<string,string> KnownFlinqDefects=new(StringComparer.Ordinal);

    [Fact,Trait("Category","ChainCorpus")]
    public async Task 中間演算子1個と終端の全組み合わせがSystemArrayで全経路一致する(){
        await RunPairsAsync(SourceTarget.SystemArray);
    }

    // 中間演算子2個+終端の全組み合わせは数千通りになるので、通常は25件に1件を決定的に抜き取る。
    [Fact,Trait("Category","ChainCorpus")]
    public async Task 中間演算子2個と終端の組み合わせを抜き取ってSystemArrayで全経路一致する(){
        await RunAsync(SourceTarget.SystemArray,2,stride:25,requireKnownDefectsToFail:true);
    }

    // 夜間用。全組み合わせを回す(数千件、1時間前後)。
    [Fact,Trait("Category","ChainCorpusFull")]
    public async Task 中間演算子2個と終端の全組み合わせがSystemArrayで全経路一致する(){
        await RunAsync(SourceTarget.SystemArray,2,stride:1,requireKnownDefectsToFail:false);
    }

    [Fact,Trait("Category","ChainCorpus")]
    public void ペア網羅は成り立つ並びを十分な数で含む(){
        var target=SourceTarget.SystemArray;
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var generated=ExhaustiveQueryEnumerator.Enumerate(1,intermediates,PairTerminals)
            .Count(spec=>ExhaustiveQueryEnumerator.TryGenerate(spec,Seed256.Parse("0xC0DE"),target,InputElementCount,out _));
        Assert.True(generated>=140,$"generated={generated}");
    }

    private static Task RunPairsAsync(SourceTarget target)=>RunAsync(target,1,stride:1,requireKnownDefectsToFail:false);

    private static async Task RunAsync(SourceTarget target,int intermediateCount,int stride,bool requireKnownDefectsToFail){
        var seed=Seed256.Parse("0xC0DE");
        var runner=new DynamicWorkerExecutionRunner();
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var failures=new List<string>();
        var knownFailed=new HashSet<string>(StringComparer.Ordinal);
        var executed=0;
        foreach(var spec in ExhaustiveQueryEnumerator.Enumerate(intermediateCount,intermediates,PairTerminals)){
            if(spec.Ordinal%stride!=0
                ||!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)){
                continue;
            }
            var request=new WorkerRunRequest(
                $"corpus-{target}-{spec.Ordinal}",
                seed,
                1,
                query!.RequestedOperatorCount,
                query.MaximumDepth,
                InputElementCount,
                OperatorSetMode.Maximum,
                [target],
                new WorkerMeasurementProfile("Corpus-v1",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString());
            var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
            executed++;
            var key=$"{string.Join(" > ",spec.Operators)} > {spec.Terminal}";
            if(result.Status!=WorkerExecutionStatus.Succeeded||!result.AssemblyUnloaded){
                if(KnownFlinqDefects.ContainsKey(key)){
                    knownFailed.Add(key);
                    continue;
                }
                failures.Add(
                    $"{string.Join(" > ",spec.Operators)} > {spec.Terminal}: Status={result.Status}, ALC={result.AssemblyUnloaded}{Environment.NewLine}"+
                    $"{Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}{Environment.NewLine}{result.DiagnosticsJson}{Environment.NewLine}{result.ResultJson}");
            }
        }
        Assert.True(executed>0);
        if(requireKnownDefectsToFail){
            var fixedAlready=KnownFlinqDefects.Keys.Except(knownFailed).ToArray();
            Assert.True(
                fixedAlready.Length==0,
                $"既知の不具合として表にあるが、今回は通った(直ったなら表から外す): {string.Join(", ",fixedAlready)}");
        }
        Assert.True(
            failures.Count==0,
            $"{failures.Count}/{executed} failed:{Environment.NewLine}{string.Join(Environment.NewLine+Environment.NewLine,failures)}");
    }
}
