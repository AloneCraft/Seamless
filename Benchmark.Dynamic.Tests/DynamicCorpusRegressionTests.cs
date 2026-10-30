using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// チェーン検証(Sample.FLinqChainVerifier)が見ていたチェーンを、Benchmark.Dynamic の実Worker経路で回す。
/// 項目は <c>Corpus/*.json</c>(メソッド名の並び)で、<see cref="CorpusChainBuilder"/> が固定の引数でIRにする。
/// System / FLinq(最適化なし・あり) / ZLinq の各経路を、空の入力などで例外になる並びも含めて比べる。
/// </summary>
[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicCorpusRegressionTests{
    private const int InputElementCount=16;

    // 回帰コーパスで見つかった FLinq の不具合。直ったら表から外す(外さないと「失敗するはずが通った」で落ちる)。
    // キーはコーパスの識別子。値は症状(実測した事実だけを書く)。
    private static readonly Dictionary<string,string> KnownFlinqDefects=new(StringComparer.Ordinal){
    };

    [Theory,Trait("Category","ChainCorpus")]
    [InlineData("representative-chains.json",23)]
    [InlineData("regressions.json",121)]
    public async Task 回帰コーパスの変換できた全項目が全経路で一致する(string fileName,int expectedExecuted){
        var corpus=CorpusChainBuilder.Load(fileName);
        var seed=Seed256.Parse("0xC0DE5");
        var runner=new DynamicWorkerExecutionRunner();
        var failures=new List<string>();
        var knownFailed=new HashSet<string>(StringComparer.Ordinal);
        var executed=0;
        for(var index=0;index<corpus.Entries.Count;index++){
            var entry=corpus.Entries[index];
            QueryIrDocument query;
            try{
                query=CorpusChainBuilder.Build(entry,SourceTarget.SystemArray,InputElementCount);
            }catch(CorpusChainNotConvertibleException){
                continue;
            }
            var request=new WorkerRunRequest(
                $"corpus-{entry.Id}",
                seed,
                1,
                query.RequestedOperatorCount,
                query.MaximumDepth,
                InputElementCount,
                OperatorSetMode.Maximum,
                [SourceTarget.SystemArray],
                new WorkerMeasurementProfile("Corpus-v1",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString(),
                ExpectExceptions:true);
            var result=await runner.ExecuteAsync(request,query,index,SourceTarget.SystemArray,0,CancellationToken.None);
            executed++;
            if(result.Status!=WorkerExecutionStatus.Succeeded||!result.AssemblyUnloaded){
                if(KnownFlinqDefects.ContainsKey(entry.Id)){
                    knownFailed.Add(entry.Id);
                    continue;
                }
                var difference=ExtractDifference(result);
                failures.Add(
                    $"{entry.Id}: Status={result.Status}{Environment.NewLine}"+
                    $"  {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}{Environment.NewLine}  {difference}");
            }
        }
        // 変換できた件数が変わったら(変換表や順序の規則が変わったら)、ここで気づく。0件の空振りも防ぐ。
        Assert.Equal(expectedExecuted,executed);
        var fixedAlready=KnownFlinqDefects.Keys
            .Where(id=>corpus.Entries.Any(entry=>entry.Id==id))
            .Except(knownFailed)
            .ToArray();
        Assert.True(
            fixedAlready.Length==0,
            $"既知の不具合として表にあるが、今回は通った(直ったなら表から外す): {string.Join(", ",fixedAlready)}");
        Assert.True(
            failures.Count==0,
            $"{failures.Count}/{executed} failed:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    private static string ExtractDifference(WorkerSourceExecutionCompletedEvent result){
        var json=result.ResultJson??string.Empty;
        var at=json.IndexOf("\"comparisons\"",StringComparison.Ordinal);
        if(at>=0){
            return json[at..Math.Min(json.Length,at+420)];
        }
        var diagnostics=result.DiagnosticsJson??string.Empty;
        var error=diagnostics.IndexOf("\"severity\":\"Error\"",StringComparison.Ordinal);
        return error>=0?diagnostics[error..Math.Min(diagnostics.Length,error+420)]:diagnostics[..Math.Min(diagnostics.Length,300)];
    }
}
