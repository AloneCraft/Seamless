using System.Reflection;
using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// 再現テスト出力が出すファイルを、実際にコンパイルし(FLinqの生成器つき)、テストメソッドを呼んで確かめる。
/// </summary>
[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicReproEmitterTests{
    private const int InputElementCount=16;
    private const string TestMethodName="FLinqはSystemLinqと同じ結果を返す";

    [Fact,Trait("Category","Integration")]
    public void 問題のないクエリの再現テストはコンパイルできて通る(){
        var document=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Seed256.Parse("0x5E9"),
            QueryIndex=0,
            QueryId="repro-clean",
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=3,
            MaximumDepth=3,
            InputElementCount=InputElementCount,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Where,QueryOperatorIds.Select,QueryOperatorIds.ToArray],
        });

        var outcome=CompileAndRun(document,Seed256.Parse("0x5E9"),0);

        Assert.True(outcome.Compiled,outcome.Diagnostics);
        Assert.Null(outcome.Failure);
        Assert.True(outcome.Unloaded);
    }

    [Fact,Trait("Category","Integration")]
    public void 既知のFLinqの不具合の再現テストは今は失敗する(){
        // DynamicChainCorpusTests.KnownFlinqDefects の「prepend > take > toArray」と同じクエリ。
        // このテストが通るようになったら、不具合が直っている。表と、このテストの両方を直す。
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var spec=ExhaustiveQueryEnumerator.Enumerate(2,intermediates,[QueryOperatorIds.ToArray,QueryOperatorIds.Count,QueryOperatorIds.Sum,QueryOperatorIds.FirstOrDefault,QueryOperatorIds.Any])
            .Where(static spec=>spec.Ordinal%25==0)
            .First(static spec=>spec.Operators.SequenceEqual([QueryOperatorIds.Prepend,QueryOperatorIds.Take])&&spec.Terminal==QueryOperatorIds.ToArray);
        Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var document));

        var outcome=CompileAndRun(document!,seed,(int)spec.Ordinal);

        Assert.True(outcome.Compiled,outcome.Diagnostics);
        Assert.NotNull(outcome.Failure);
        Assert.Contains("FLinq(最適化あり)",outcome.Failure,StringComparison.Ordinal);
        Assert.True(outcome.Unloaded);
    }

    private static ReproOutcome CompileAndRun(QueryIrDocument document,Seed256 baseSeed,int queryIndex){
        using var fixture=new SourceFixtureFactory().Create(
            document.SourceTarget,
            InputElementCount,
            SeedDerivation.Derive(baseSeed,"fixture",queryIndex));
        var primary=(int[])fixture.Primary;
        var secondary=(int[])fixture.Secondary;
        var text=ReproTestEmitter.Emit(document,primary,secondary,new ReproTestOptions{Note="Status=SemanticMismatch"});
        var result=new DynamicQueryCompiler().Compile(new DynamicCompilationRequest(
            "Benchmark.Dynamic.ReproProbe."+Guid.NewGuid().ToString("N"),
            "Test.System.Linq.Enumerable.FLinq.FailedQueries.Probe",
            text,
            "Repro.g.cs"));
        if(!result.Success||result.LoadedQuery is null){
            return new(
                false,
                string.Join(Environment.NewLine,result.Diagnostics.Where(static d=>d.Severity=="Error").Select(static d=>$"{d.ID}: {d.Message}")),
                null,
                true);
        }
        string? failure=null;
        var loaded=result.LoadedQuery;
        InvokeRepro(loaded,document.QueryId,out failure);
        var unloaded=loaded.UnloadAndVerify().Unloaded;
        return new(true,string.Empty,failure,unloaded);
    }

    private static void InvokeRepro(LoadedDynamicQuery loaded,string queryId,out string? failure){
        failure=null;
        var assembly=loaded.DynamicAssembly??throw new InvalidOperationException("The dynamic assembly was not loaded.");
        var className="Test.System.Linq.Enumerable.FLinq.FailedQueries.Repro_"+queryId.Replace('-','_');
        var type=assembly.GetType(className,throwOnError:true)!;
        var method=type.GetMethod(TestMethodName)!;
        try{
            method.Invoke(Activator.CreateInstance(type),null);
        }catch(TargetInvocationException exception){
            failure=exception.InnerException?.Message??exception.Message;
        }
    }

    private sealed record ReproOutcome(bool Compiled,string Diagnostics,string? Failure,bool Unloaded);
}
