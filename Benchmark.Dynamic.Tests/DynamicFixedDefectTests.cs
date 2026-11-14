using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// 2026-10-22 に見つけて同じ日に直した FLinq の不具合が、直ったままであることを確かめる。
/// 3段の全組み合わせ(夜間の実行)で見つかった124件(最初の実行で110件、SelectMany・DefaultIfEmpty の対応後の再実行で14件)は <c>Corpus/fixed-triple-defects.txt</c> に並びを記録してある。
/// 直した内容は設計書(Document/20261114Argon.md)を参照。
/// </summary>
[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicFixedDefectTests{
    private const int InputElementCount=16;

    private static readonly string[] Terminals=[
        QueryOperatorIds.ToArray,
        QueryOperatorIds.Count,
        QueryOperatorIds.Sum,
        QueryOperatorIds.FirstOrDefault,
        QueryOperatorIds.Any,
    ];

    [Fact,Trait("Category","ChainCorpus")]
    public async Task 夜間の3段全組み合わせで見つけて直した124件がSystemLinqと一致する(){
        var keys=File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"Corpus","fixed-triple-defects.txt"))
            .Where(static line=>line.Length>0&&line[0]!='#')
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(124,keys.Count);
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        var runner=new DynamicWorkerExecutionRunner();
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var failures=new List<string>();
        var executed=0;
        foreach(var spec in ExhaustiveQueryEnumerator.Enumerate(2,intermediates,Terminals)){
            var key=$"{string.Join(" > ",spec.Operators)} > {spec.Terminal}";
            if(!keys.Contains(key)||!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)){
                continue;
            }
            var request=new WorkerRunRequest(
                $"fixed-{spec.Ordinal}",
                seed,
                1,
                query!.RequestedOperatorCount,
                query.MaximumDepth,
                InputElementCount,
                OperatorSetMode.Maximum,
                [target],
                new WorkerMeasurementProfile("Fixed-v1",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString());
            var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
            executed++;
            if(result.Status!=WorkerExecutionStatus.Succeeded||!result.AssemblyUnloaded){
                failures.Add($"{key}: Status={result.Status}, ALC={result.AssemblyUnloaded}  {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}");
            }
        }
        Assert.Equal(keys.Count,executed);
        Assert.True(failures.Count==0,$"{failures.Count}/{executed} failed:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

}
