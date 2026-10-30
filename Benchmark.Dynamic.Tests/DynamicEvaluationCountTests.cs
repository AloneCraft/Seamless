using System.Text.Json;
using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// 評価回数の検査(Take・Skip などで、System.Linq が評価しない要素に FLinq がラムダを実行しないか)の部品と、通しの確認。
/// 結果の一致だけでは見つからない不具合(TPC-E の測定で見つかった、Take の上限の次の要素の評価)を、回数で見つけるための検査。
/// </summary>
[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicEvaluationCountTests{
    private const int InputElementCount=16;

    private static readonly string[] Terminals=[
        QueryOperatorIds.ToArray,
        QueryOperatorIds.Count,
        QueryOperatorIds.Sum,
        QueryOperatorIds.FirstOrDefault,
        QueryOperatorIds.Any,
    ];

    [Fact]
    public void ラムダの式本体を数える呼び出しで包む(){
        var rewritten=EvaluationProbeRewriter.Instrument("class C{ void M(){ var a=x=>x%3==0; var b=(x,y)=>x^y; var c=x=>{ return x; }; } }");
        Assert.Contains("x=>global::Benchmark.Dynamic.Core.EvaluationProbe.Hit(x%3==0)",rewritten);
        Assert.Contains("(x,y)=>global::Benchmark.Dynamic.Core.EvaluationProbe.Hit(x^y)",rewritten);
        // ブロック本体は包まない。
        Assert.Contains("x=>{ return x; }",rewritten);
    }

    [Fact]
    public void 入れ子のラムダも内側と外側の両方を包む(){
        var rewritten=EvaluationProbeRewriter.Instrument("class C{ void M(){ var a=o=>s.Select(i=>o^i); } }");
        Assert.Contains("o=>global::Benchmark.Dynamic.Core.EvaluationProbe.Hit(s.Select(i=>global::Benchmark.Dynamic.Core.EvaluationProbe.Hit(o^i)))",rewritten);
    }

    [Theory]
    [InlineData(QueryOperatorIds.Take,true)]
    [InlineData(QueryOperatorIds.Skip,true)]
    [InlineData(QueryOperatorIds.TakeWhile,true)]
    [InlineData(QueryOperatorIds.Where,false)]
    [InlineData(QueryOperatorIds.Select,false)]
    public void 途中で止まる演算を含むときだけ検査する(string intermediate,bool expected){
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        var spec=ExhaustiveQueryEnumerator.Enumerate(1,[intermediate],[QueryOperatorIds.ToArray]).First();
        Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query));
        Assert.Equal(expected,QueryEvaluationCheck.Applies(query!));
    }

    [Fact]
    public void 終端が途中で止まるときも検査する(){
        var seed=Seed256.Parse("0xC0DE");
        var spec=ExhaustiveQueryEnumerator.Enumerate(1,[QueryOperatorIds.Select],[QueryOperatorIds.FirstOrDefault]).First();
        Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,SourceTarget.SystemArray,InputElementCount,out var query));
        Assert.True(QueryEvaluationCheck.Applies(query!));
    }

    [Fact]
    public void FLinqがSystemLinqより多く評価したら問題にする(){
        var counts=new Dictionary<DynamicImplementationKind,long>{
            [DynamicImplementationKind.System]=2,
            [DynamicImplementationKind.FLinqUnoptimized]=2,
            [DynamicImplementationKind.FLinq]=3,
        };
        var problem=EvaluationCountComparer.Compare(counts);
        Assert.NotNull(problem);
        Assert.Contains("FLinq ",problem);
        Assert.Contains("3 回 > 2 回",problem);
    }

    [Fact]
    public void FLinqの評価がSystemLinqと同じか少なければ問題にしない(){
        var counts=new Dictionary<DynamicImplementationKind,long>{
            [DynamicImplementationKind.System]=3,
            [DynamicImplementationKind.FLinqUnoptimized]=3,
            [DynamicImplementationKind.FLinq]=2,
            [DynamicImplementationKind.ZLinq]=9,
        };
        Assert.Null(EvaluationCountComparer.Compare(counts));
    }

    [Fact]
    public void 数える形にしたクエリを実行すると各経路の評価回数が取れて一致する(){
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        var spec=ExhaustiveQueryEnumerator.Enumerate(2,[QueryOperatorIds.Select,QueryOperatorIds.Take],[QueryOperatorIds.ToArray])
            .First(static spec=>spec.Operators.SequenceEqual([QueryOperatorIds.Select,QueryOperatorIds.Take]));
        Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query));
        var emitted=new QuerySourceEmitter().Emit(query!,new QuerySourceEmissionOptions{GeneratedNamespace="Counted",ClassName="Probe"});
        var counted=EvaluationProbeRewriter.Instrument(emitted.SourceText);
        var compiled=new DynamicQueryCompiler().Compile(new DynamicCompilationRequest("Counted."+Guid.NewGuid().ToString("N"),"Counted.Probe",counted,"Counted.g.cs"));
        Assert.True(compiled.Success,string.Join(Environment.NewLine,compiled.Diagnostics.Where(static d=>d.Severity=="Error").Select(static d=>$"{d.ID}: {d.Message}")));
        var loaded=compiled.LoadedQuery!;
        using var fixture=new SourceFixtureFactory().Create(target,InputElementCount,SeedDerivation.Derive(seed,"fixture",0));
        var counts=new Dictionary<DynamicImplementationKind,long>();
        foreach(var kind in new[]{DynamicImplementationKind.System,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq}){
            EvaluationProbe.Reset();
            loaded.Run(kind,fixture.Primary,fixture.Secondary);
            counts[kind]=EvaluationProbe.Count;
        }
        loaded.UnloadAndVerify();
        // Select の後の Take(n) は、Select を n 回だけ評価する(入力は 16 件なので、n は 16 より小さい)。
        Assert.InRange(counts[DynamicImplementationKind.System],1,InputElementCount-1);
        Assert.Equal(counts[DynamicImplementationKind.System],counts[DynamicImplementationKind.FLinqUnoptimized]);
        Assert.Equal(counts[DynamicImplementationKind.System],counts[DynamicImplementationKind.FLinq]);
    }

    // 厳密な比較から外す個別の並び(FLinq が System.Linq より多く評価する既知の差。直したらここから外す)。
    // groupBy > reverse > First: Reverse の後の First は Last になり、System.Linq は最後のグループの集約だけを評価するが、FLinq は全グループを評価する。
    // orderBy > thenBy > First: System.Linq は ThenBy の二次の鍵を同順位のときだけ評価する。
    // where > reverse > any: System.Linq は後ろから走査して1回、FLinq は前から走査して2回。
    // groupBy.keyOnly > exceptBy > any: 後ろの Distinct が消えて、後ろの選択子が重複する要素にも走る。
    // unionBy > selectMany > any: 第2引数のキーを先に集合へ入れるので、第1引数のキー選択子を System.Linq より多く評価する。
    private static readonly HashSet<string> NotStrictKeys=new(StringComparer.Ordinal){
        "groupBy.count > reverse > firstOrDefault",
        "groupBy.keyOnly > reverse > firstOrDefault",
        "groupBy.sum > reverse > firstOrDefault",
        "orderBy > thenBy > firstOrDefault",
        "where > reverse > any",
        "groupBy.keyOnly > exceptBy.identity > any",
        "unionBy.identity > selectMany.xor > any",
    };

    [Fact]
    public async Task 途中で止まる演算を含む組み合わせでFLinqの評価回数がSystemLinqを超えない(){
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        var runner=new DynamicWorkerExecutionRunner();
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target).ToArray();
        var failures=new List<string>();
        var checkedCount=0;
        foreach(var intermediateCount in new[]{1,2}){
            foreach(var spec in ExhaustiveQueryEnumerator.Enumerate(intermediateCount,intermediates,Terminals)){
                // 中間演算子が2個の並びは、全部は回さず間引く(全組み合わせは DynamicEvaluationCountSurvey で調べる)。
                if(intermediateCount==2&&spec.Ordinal%9!=0
                    ||NotStrictKeys.Contains($"{string.Join(" > ",spec.Operators)} > {spec.Terminal}")
                    ||!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)
                    ||!QueryEvaluationCheck.Applies(query!)){
                    continue;
                }
                var request=new WorkerRunRequest(
                    $"evalcount-{spec.Ordinal}",
                    seed,
                    1,
                    query!.RequestedOperatorCount,
                    query.MaximumDepth,
                    InputElementCount,
                    OperatorSetMode.Maximum,
                    [target],
                    new WorkerMeasurementProfile("EvalCount-v1",0,1,0,1),
                    RandomQueryGenerator.GeneratorVersion,
                    QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                    BuiltInOperatorCatalog.OperatorSetVersion.ToString(),
                    StrictEvaluationCounts:true);
                var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
                checkedCount++;
                if(result.Status!=WorkerExecutionStatus.Succeeded){
                    failures.Add($"{string.Join(" > ",spec.Operators)} > {spec.Terminal} (#{spec.Ordinal}): {result.Status} {EvaluationDiagnostics(result.DiagnosticsJson)} {Truncate(result.ResultJson)}");
                }
            }
        }
        Assert.True(checkedCount>0);
        Assert.True(failures.Count==0,$"{failures.Count}/{checkedCount} failed:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    [Fact]
    public async Task 既知の差がある並びは通常の実行では警告にとどめ厳密にすると失敗にする(){
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        // ThenBy の二次の鍵を、FLinq は全要素で評価する(System.Linq は同順位のときだけ)ので、評価が多い。直したら別の既知の差に替える。
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var spec=ExhaustiveQueryEnumerator.Enumerate(2,intermediates,[QueryOperatorIds.FirstOrDefault])
            .First(static spec=>spec.Operators.SequenceEqual([QueryOperatorIds.OrderBy,QueryOperatorIds.ThenBy]));
        Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query));
        WorkerRunRequest Request(bool strict)=>new(
            "evalcount-known-gap",seed,1,query!.RequestedOperatorCount,query.MaximumDepth,InputElementCount,
            OperatorSetMode.Maximum,[target],new WorkerMeasurementProfile("EvalCount-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,QueryCapabilityProfile.CurrentProfileVersion.ToString(),BuiltInOperatorCatalog.OperatorSetVersion.ToString(),
            StrictEvaluationCounts:strict);
        var lenient=await new DynamicWorkerExecutionRunner().ExecuteAsync(Request(false),query!,(int)spec.Ordinal,target,0,CancellationToken.None);
        var strictResult=await new DynamicWorkerExecutionRunner().ExecuteAsync(Request(true),query!,(int)spec.Ordinal,target,0,CancellationToken.None);
        Assert.Equal(WorkerExecutionStatus.Succeeded,lenient.Status);
        Assert.Contains("EvaluationCountExcess",lenient.DiagnosticsJson);
        Assert.Equal(WorkerExecutionStatus.SemanticMismatch,strictResult.Status);
        Assert.Contains("EvaluationCountMismatch",strictResult.DiagnosticsJson);
    }

    private static string Truncate(string? text)=>text is null?string.Empty:text.Length<=900?text:text[..900];

    private static string EvaluationDiagnostics(string? diagnosticsJson){
        if(string.IsNullOrWhiteSpace(diagnosticsJson)){
            return string.Empty;
        }
        using var document=JsonDocument.Parse(diagnosticsJson);
        return string.Join(" / ",document.RootElement.EnumerateArray()
            .Where(static item=>item.TryGetProperty("stage",out var stage)&&stage.GetString() is "Evaluation" or "Execution")
            .Select(static item=>$"{item.GetProperty("id").GetString()}: {item.GetProperty("message").GetString()}"));
    }

    [Fact]
    public async Task 結果の記録に評価回数が入る(){
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        var spec=ExhaustiveQueryEnumerator.Enumerate(1,[QueryOperatorIds.Take],[QueryOperatorIds.ToArray]).First();
        Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query));
        var request=new WorkerRunRequest(
            "evalcount-artifact",seed,1,query!.RequestedOperatorCount,query.MaximumDepth,InputElementCount,
            OperatorSetMode.Maximum,[target],new WorkerMeasurementProfile("EvalCount-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,QueryCapabilityProfile.CurrentProfileVersion.ToString(),BuiltInOperatorCatalog.OperatorSetVersion.ToString());
        var result=await new DynamicWorkerExecutionRunner().ExecuteAsync(request,query,0,target,0,CancellationToken.None);
        Assert.Equal(WorkerExecutionStatus.Succeeded,result.Status);
        using var document=JsonDocument.Parse(result.ResultJson!);
        var found=document.RootElement.EnumerateObject().Any(static property=>property.Name.Equals("EvaluationCounts",StringComparison.OrdinalIgnoreCase));
        Assert.True(found,result.ResultJson);
    }

    [Fact]
    public async Task 評価回数の検査を切ると結果の記録に入らない(){
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        var spec=ExhaustiveQueryEnumerator.Enumerate(1,[QueryOperatorIds.Take],[QueryOperatorIds.ToArray]).First();
        Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query));
        var request=new WorkerRunRequest(
            "evalcount-off",seed,1,query!.RequestedOperatorCount,query.MaximumDepth,InputElementCount,
            OperatorSetMode.Maximum,[target],new WorkerMeasurementProfile("EvalCount-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,QueryCapabilityProfile.CurrentProfileVersion.ToString(),BuiltInOperatorCatalog.OperatorSetVersion.ToString(),
            CheckEvaluationCounts:false);
        var result=await new DynamicWorkerExecutionRunner().ExecuteAsync(request,query,0,target,0,CancellationToken.None);
        Assert.Equal(WorkerExecutionStatus.Succeeded,result.Status);
        using var document=JsonDocument.Parse(result.ResultJson!);
        var counts=document.RootElement.EnumerateObject().FirstOrDefault(static property=>property.Name.Equals("EvaluationCounts",StringComparison.OrdinalIgnoreCase));
        Assert.True(counts.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null,result.ResultJson);
    }
}
