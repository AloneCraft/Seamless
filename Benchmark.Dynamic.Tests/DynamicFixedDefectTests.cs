using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// 2026-10-22 に見つけて同じ日に直した FLinq の不具合が、直ったままであることを確かめる。
/// 3段の全組み合わせ(夜間の実行)で見つかった110件は <c>Corpus/fixed-triple-defects.txt</c> に並びを記録してある。
/// 直した内容は設計書(docs/refactoring-designs/20261022-dynamic-verifier-consolidation.md)の「FLinq の不具合の修正」を参照。
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
    public async Task 夜間の3段全組み合わせで見つけて直した110件がSystemLinqと一致する(){
        var keys=File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"Corpus","fixed-triple-defects.txt"))
            .Where(static line=>line.Length>0&&line[0]!='#')
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(110,keys.Count);
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
        Assert.Equal(110,executed);
        Assert.True(failures.Count==0,$"{failures.Count}/{executed} failed:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    // 要素型の次元で見つけて直した6件(キーは DynamicElementTypeTests と同じ: 固定シード 0xE1E7・入力16件・SystemArray)。
    // new global::A.B.C(...) の型名を値として巻き上げていた(CS1526)、with 式の代入先を巻き上げていた、
    // 変換された定数を巻き上げて All の述語の宣言が届かなかった(CS0103)、Append / Prepend の後ろの段・Union 系を融合して結果がずれた、の4種類。
    [Theory,Trait("Category","ChainCorpus")]
    [InlineData(QueryElementType.CollidingKey,0)]
    [InlineData(QueryElementType.Int256Struct,27)]
    [InlineData(QueryElementType.Order,27)]
    [InlineData(QueryElementType.Order,0)]
    [InlineData(QueryElementType.String,0)]
    [InlineData(QueryElementType.String,2)]
    public async Task 要素型の次元で見つけて直した不具合は直ったまま(QueryElementType type,int index){
        var capability=QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[SourceTarget.SystemArray],type);
        var query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Seed256.Parse("0xE1E7"),
            QueryIndex=index,
            QueryId=$"typed-{type}-{index:D3}",
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=3,
            MaximumDepth=3,
            InputElementCount=InputElementCount,
            EnableBinaryOperators=true,
            EnableNestedOperators=true,
            AllowedOperatorIds=capability.OperatorIds,
            ElementType=type,
        });
        var request=new WorkerRunRequest(
            $"fixed-typed-{type}",
            Seed256.Parse("0xE1E7"),
            1,
            3,
            3,
            InputElementCount,
            OperatorSetMode.Maximum,
            [SourceTarget.SystemArray],
            new WorkerMeasurementProfile("Fixed-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,
            QueryCapabilityProfile.CurrentProfileVersion.ToString(),
            BuiltInOperatorCatalog.OperatorSetVersion.ToString(),
            ElementType:type);

        var result=await new DynamicWorkerExecutionRunner().ExecuteAsync(request,query,index,SourceTarget.SystemArray,0,CancellationToken.None);

        Assert.True(
            result.Status==WorkerExecutionStatus.Succeeded&&result.AssemblyUnloaded,
            $"{type} #{index} {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}: Status={result.Status}{Environment.NewLine}{result.DiagnosticsJson}{Environment.NewLine}{result.ResultJson}");
    }
}
