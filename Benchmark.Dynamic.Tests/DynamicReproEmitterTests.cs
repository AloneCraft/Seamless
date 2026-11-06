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
    public void 直したFLinqの不具合の再現テストは通る(){
        // 以前は最適化ありの FLinq が、2件のはずの所で3件を返していた「prepend > take > toArray」と同じクエリ。
        // 2026-10-22 に FLinq を直したので、再現テストは通る。
        var seed=Seed256.Parse("0xC0DE");
        var target=SourceTarget.SystemArray;
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var spec=ExhaustiveQueryEnumerator.Enumerate(2,intermediates,[QueryOperatorIds.ToArray,QueryOperatorIds.Count,QueryOperatorIds.Sum,QueryOperatorIds.FirstOrDefault,QueryOperatorIds.Any])
            .First(static spec=>spec.Operators.SequenceEqual([QueryOperatorIds.Prepend,QueryOperatorIds.Take])&&spec.Terminal==QueryOperatorIds.ToArray);
        Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var document));

        var outcome=CompileAndRun(document!,seed,(int)spec.Ordinal);

        Assert.True(outcome.Compiled,outcome.Diagnostics);
        Assert.Null(outcome.Failure);
        Assert.True(outcome.Unloaded);
    }

    [Fact,Trait("Category","Integration")]
    public void Reverse_ToIntPairが展開できてSystemLinqと一致する(){
        var seed=Seed256.Parse("0xFA01");
        var target=SourceTarget.SystemArray;
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var terminals=ExhaustiveQueryEnumerator.GetTerminalOperatorIds(target);
        var spec=ExhaustiveQueryEnumerator.Enumerate(1,intermediates,terminals)
            .First(static spec=>spec.Operators.SequenceEqual([QueryOperatorIds.Reverse])&&spec.Terminal==QueryOperatorIds.ToIntPair);
        Assert.True(ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var document));

        var outcome=CompileAndRun(document!,seed,(int)spec.Ordinal);

        Assert.True(outcome.Compiled,outcome.Diagnostics);
        Assert.Null(outcome.Failure);
        Assert.True(outcome.Unloaded);
    }

    [Fact,Trait("Category","ExhaustiveScan")]
    public async Task ペア全組み合わせでExpansionIncompleteが発生しないことを検証する(){
        var seed=Seed256.Parse("0xFA01");
        var target=SourceTarget.SystemArray;
        var runner=new DynamicWorkerExecutionRunner();
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var terminals=ExhaustiveQueryEnumerator.GetTerminalOperatorIds(target);
        var specs=ExhaustiveQueryEnumerator.Enumerate(1,intermediates,terminals).ToArray();
        var failures=new List<string>();

        foreach(var spec in specs){
            if(!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)||query is null){
                continue;
            }
            var request=new WorkerRunRequest(
                $"pair-{spec.Ordinal}",
                seed,
                1,
                query.RequestedOperatorCount,
                query.MaximumDepth,
                InputElementCount,
                OperatorSetMode.Maximum,
                [target],
                new WorkerMeasurementProfile("PairScan",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString());
            var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
            if(result.Status==WorkerExecutionStatus.ExpansionIncomplete){
                failures.Add($"#{spec.Ordinal} {string.Join(" > ",spec.Operators)} > {spec.Terminal}: {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query!)}");
            }
        }

        Assert.True(failures.Count==0,$"ExpansionIncomplete occurred in {failures.Count} queries:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    [Fact,Trait("Category","TripleScan")]
    public async Task 三段全組み合わせ網羅スキャン前半でExpansionIncompleteを検出する(){
        var seed=Seed256.Parse("0xFA01");
        var target=SourceTarget.SystemArray;
        var runner=new DynamicWorkerExecutionRunner();
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var terminals=ExhaustiveQueryEnumerator.GetTerminalOperatorIds(target);
        // 三段全組み合わせの先頭300件をスキャン
        var specs=ExhaustiveQueryEnumerator.Enumerate(2,intermediates,terminals).Take(300).ToArray();
        var failures=new List<string>();

        foreach(var spec in specs){
            if(!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)||query is null){
                continue;
            }
            var request=new WorkerRunRequest(
                $"triple-{spec.Ordinal}",
                seed,
                1,
                query.RequestedOperatorCount,
                query.MaximumDepth,
                InputElementCount,
                OperatorSetMode.Maximum,
                [target],
                new WorkerMeasurementProfile("TripleScan",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString());
            var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
            if(result.Status==WorkerExecutionStatus.ExpansionIncomplete){
                failures.Add($"#{spec.Ordinal} {string.Join(" > ",spec.Operators)} > {spec.Terminal}: {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}");
            }
        }

        Assert.True(failures.Count==0,$"ExpansionIncomplete occurred in {failures.Count} queries:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    [Fact,Trait("Category","RandomScan")]
    public async Task ランダム探索複合クエリでExpansionIncompleteが発生しないことを検証する(){
        var seed=Seed256.Parse("0x10182026");
        var runner=new DynamicWorkerExecutionRunner();
        var targets=Enum.GetValues<SourceTarget>();
        var failures=new List<string>();
        var generator=new RandomQueryGenerator();

        for(var i=0;i<500;i++){
            var target=targets[i%targets.Length];
            var opCount=3+(i%7); // 3〜9段
            var doc=generator.Generate(new RandomQueryGenerationOptions{
                MasterSeed=seed,
                QueryIndex=i,
                QueryId=$"random-scan-{i:D4}",
                SourceTarget=target,
                OperatorCount=opCount,
                MaximumDepth=opCount,
                InputElementCount=InputElementCount,
                EnableBinaryOperators=true,
                EnableNestedOperators=true,
            });
            var request=new WorkerRunRequest(
                $"random-scan-{i:D4}",
                seed,
                1,
                doc.RequestedOperatorCount,
                doc.MaximumDepth,
                InputElementCount,
                OperatorSetMode.Maximum,
                [target],
                new WorkerMeasurementProfile("RandomScan",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString());
            var result=await runner.ExecuteAsync(request,doc,i,target,0,CancellationToken.None);
            if(result.Status==WorkerExecutionStatus.ExpansionIncomplete){
                failures.Add($"#{i} [{target}] {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(doc)}: {result.DiagnosticsJson}");
            }
        }

        Assert.True(failures.Count==0,$"ExpansionIncomplete occurred in {failures.Count} queries:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    [Fact,Trait("Category","TripleScan")]
    public async Task 三段全組み合わせ網羅スキャン後半でExpansionIncompleteを検出する(){
        var seed=Seed256.Parse("0xFA01");
        var target=SourceTarget.SystemArray;
        var runner=new DynamicWorkerExecutionRunner();
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var terminals=ExhaustiveQueryEnumerator.GetTerminalOperatorIds(target);
        // 三段全組み合わせの300〜600件目をスキャン
        var specs=ExhaustiveQueryEnumerator.Enumerate(2,intermediates,terminals).Skip(300).Take(300).ToArray();
        var failures=new List<string>();

        foreach(var spec in specs){
            if(!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)||query is null){
                continue;
            }
            var request=new WorkerRunRequest(
                $"triple-{spec.Ordinal}",
                seed,
                1,
                query.RequestedOperatorCount,
                query.MaximumDepth,
                InputElementCount,
                OperatorSetMode.Maximum,
                [target],
                new WorkerMeasurementProfile("TripleScan",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString());
            var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
            if(result.Status==WorkerExecutionStatus.ExpansionIncomplete){
                failures.Add($"#{spec.Ordinal} {string.Join(" > ",spec.Operators)} > {spec.Terminal}: {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}");
            }
        }

        Assert.True(failures.Count==0,$"ExpansionIncomplete occurred in {failures.Count} queries:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    [Fact,Trait("Category","MultiSourceScan")]
    public async Task MultiSourcePairScan_Detects_ExpansionIncomplete(){
        var seed=Seed256.Parse("0xFA01");
        var runner=new DynamicWorkerExecutionRunner();
        var testTargets=new[]{
            SourceTarget.SystemList,
            SourceTarget.SystemRange,
            SourceTarget.SeamlessTsavorite,
        };
        var failures=new List<string>();

        foreach(var target in testTargets){
            var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
            var terminals=ExhaustiveQueryEnumerator.GetTerminalOperatorIds(target);
            var specs=ExhaustiveQueryEnumerator.Enumerate(1,intermediates,terminals).ToArray();

            foreach(var spec in specs){
                if(!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)||query is null){
                    continue;
                }
                var request=new WorkerRunRequest(
                    $"multi-{target}-{spec.Ordinal}",
                    seed,
                    1,
                    query.RequestedOperatorCount,
                    query.MaximumDepth,
                    InputElementCount,
                    OperatorSetMode.Maximum,
                    [target],
                    new WorkerMeasurementProfile("MultiSourceScan",0,1,0,1),
                    RandomQueryGenerator.GeneratorVersion,
                    QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                    BuiltInOperatorCatalog.OperatorSetVersion.ToString());
                var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
                if(result.Status==WorkerExecutionStatus.ExpansionIncomplete){
                    failures.Add($"[{target}] #{spec.Ordinal} {string.Join(" > ",spec.Operators)} > {spec.Terminal}: {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}");
                }
            }
        }

        Assert.True(failures.Count==0,$"ExpansionIncomplete occurred in {failures.Count} queries:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    [Fact,Trait("Category","TripleScan_Part3")]
    public async Task TripleScan_600_to_1200(){
        var seed=Seed256.Parse("0xFA01");
        var target=SourceTarget.SystemArray;
        var runner=new DynamicWorkerExecutionRunner();
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var terminals=ExhaustiveQueryEnumerator.GetTerminalOperatorIds(target);
        // 三段全組み合わせの600〜1200件目をスキャン
        var specs=ExhaustiveQueryEnumerator.Enumerate(2,intermediates,terminals).Skip(600).Take(600).ToArray();
        var failures=new List<string>();

        foreach(var spec in specs){
            if(!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)||query is null){
                continue;
            }
            var request=new WorkerRunRequest(
                $"triple-{spec.Ordinal}",
                seed,
                1,
                query.RequestedOperatorCount,
                query.MaximumDepth,
                InputElementCount,
                OperatorSetMode.Maximum,
                [target],
                new WorkerMeasurementProfile("TripleScan",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString());
            var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
            if(result.Status==WorkerExecutionStatus.ExpansionIncomplete){
                failures.Add($"#{spec.Ordinal} {string.Join(" > ",spec.Operators)} > {spec.Terminal}: {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}");
            }
        }

        Assert.True(failures.Count==0,$"ExpansionIncomplete occurred in {failures.Count} queries:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    [Fact,Trait("Category","MultiSourceTripleScan")]
    public async Task MultiSourceTripleScan_Detects_ExpansionIncomplete(){
        var seed=Seed256.Parse("0xFA01");
        var runner=new DynamicWorkerExecutionRunner();
        var testTargets=new[]{
            SourceTarget.SystemList,
            SourceTarget.SystemRange,
            SourceTarget.SeamlessTsavorite,
        };
        var failures=new List<string>();

        foreach(var target in testTargets){
            var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
            var terminals=ExhaustiveQueryEnumerator.GetTerminalOperatorIds(target);
            // 各異種ソースの三段全組み合わせ先頭200件ずつ（計600件）をスキャン
            var specs=ExhaustiveQueryEnumerator.Enumerate(2,intermediates,terminals).Take(200).ToArray();

            foreach(var spec in specs){
                if(!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)||query is null){
                    continue;
                }
                var request=new WorkerRunRequest(
                    $"multi-triple-{target}-{spec.Ordinal}",
                    seed,
                    1,
                    query.RequestedOperatorCount,
                    query.MaximumDepth,
                    InputElementCount,
                    OperatorSetMode.Maximum,
                    [target],
                    new WorkerMeasurementProfile("MultiSourceTripleScan",0,1,0,1),
                    RandomQueryGenerator.GeneratorVersion,
                    QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                    BuiltInOperatorCatalog.OperatorSetVersion.ToString());
                var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
                if(result.Status==WorkerExecutionStatus.ExpansionIncomplete){
                    failures.Add($"[{target}] #{spec.Ordinal} {string.Join(" > ",spec.Operators)} > {spec.Terminal}: {Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}: {result.DiagnosticsJson}");
                }
            }
        }

        Assert.True(failures.Count==0,$"ExpansionIncomplete occurred in {failures.Count} queries:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
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
