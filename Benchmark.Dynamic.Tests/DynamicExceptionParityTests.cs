using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// 例外を許す検証(<see cref="QueryIrDocument.ExpectExceptions"/>)。
/// System.Linq が例外を投げる入力では、他の実装も同じ型の例外を投げれば一致とする。
/// </summary>
[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicExceptionParityTests{
    private const int InputElementCount=16;

    [Theory,Trait("Category","ChainCorpus")]
    [InlineData(QueryOperatorIds.ElementAt)]
    [InlineData(QueryOperatorIds.First)]
    [InlineData(QueryOperatorIds.Min)]
    [InlineData(QueryOperatorIds.Last)]
    public async Task 空や範囲外で例外になるクエリでも全実装が同じ型の例外を投げて一致する(string terminal){
        var seed=Seed256.Parse("0xEC0");
        var runner=new DynamicWorkerExecutionRunner();
        var failures=new List<string>();
        var threw=0;
        var returned=0;
        for(var queryIndex=0;queryIndex<40;queryIndex++){
            var query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
                MasterSeed=seed,
                QueryIndex=queryIndex,
                QueryId=$"parity-{terminal}-{queryIndex}",
                SourceTarget=SourceTarget.SystemArray,
                OperatorCount=2,
                MaximumDepth=2,
                InputElementCount=InputElementCount,
                EnableBinaryOperators=false,
                EnableNestedOperators=false,
                AllowedOperatorIds=[QueryOperatorIds.Where,QueryOperatorIds.Select,terminal],
                ExpectExceptions=true,
            });
            var request=new WorkerRunRequest(
                $"parity-{terminal}",
                seed,
                1,
                2,
                2,
                InputElementCount,
                OperatorSetMode.Maximum,
                [SourceTarget.SystemArray],
                new WorkerMeasurementProfile("Parity-v1",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString(),
                ExpectExceptions:true);
            var result=await runner.ExecuteAsync(request,query,queryIndex,SourceTarget.SystemArray,0,CancellationToken.None);
            if(result.ResultJson?.Contains("\"exceptions\":{\"system\"",StringComparison.Ordinal)==true){
                threw++;
            }else{
                returned++;
            }
            if(result.Status!=WorkerExecutionStatus.Succeeded||!result.AssemblyUnloaded){
                failures.Add(
                    $"#{queryIndex} {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}: Status={result.Status}{Environment.NewLine}{result.ResultJson}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
        // 例外になる場合と、ならない場合の両方が実際に含まれていること(どちらかだけでは確かめたことにならない)。
        Assert.True(threw>0,$"System.Linq が例外を投げたクエリが無かった(返した={returned})。");
    }
}
