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

    // チェーン検証の移植で見つかった FLinq(最適化あり)の不具合。固定シード 0xC0DE・入力16件・SystemArray で再現した。
    // 最適化なしの FLinq と ZLinq は System.Linq と一致しているので、不具合は最適化の段階にある(原因は未調査)。
    // 直ったらこの表から外す。外さないと「失敗するはずの並びが通った」ことでテストが落ちる。
    // 引数の値はシードで決まるので、生成器を変えて並びの引数が変わったら、表を取り直す。
    private static readonly Dictionary<string,string> KnownFlinqDefects=new(StringComparer.Ordinal){
        ["append > takeWhile > toArray"]="Append(54).TakeWhile(p=>p>16): 結果が1件。System.Linq は0件。",
        ["prepend > take > toArray"]="Prepend(-58).Take(2): 結果が3件。System.Linq は2件。",
        ["prepend > groupBy.keyOnly > toArray"]="Prepend(-1).GroupBy(p=>(p%5+5)%5).Select(g=>g.Key): キーが6個。System.Linq は5個。",
        ["where > union > toArray"]="Where(偶数).Union(inner): 結果が24件。System.Linq は20件。",
        ["concat > take > toArray"]="Concat(inner).Take(15): 生成コードが CS0103(__postTake__r3_2 が未宣言)でコンパイルできない。",
        ["zip > take > toArray"]="Zip(inner,(p,i)=>p+i).Take(3): 生成コードが CS0165(__linq0 が未代入)でコンパイルできない。",
    };

    // 夜間の3段全組み合わせで見つかり、直さずに記録した並び(Corpus/known-triple-defects.txt)。
    // 上の表と違い、抜き取りの実行では通る並びもあるので「今回も失敗すること」は求めない。失敗しても新しい不具合とは数えない。
    private static readonly HashSet<string> RecordedTripleDefects=File
        .ReadAllLines(Path.Combine(AppContext.BaseDirectory,"Corpus","known-triple-defects.txt"))
        .Where(static line=>line.Length>0&&line[0]!='#')
        .ToHashSet(StringComparer.Ordinal);

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
                if(intermediateCount==2&&target==SourceTarget.SystemArray&&RecordedTripleDefects.Contains(key)){
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
