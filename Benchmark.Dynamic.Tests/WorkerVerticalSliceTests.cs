using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class WorkerVerticalSliceTests{
    [Fact,Trait("Category","Stress")]
    public void 動的Assemblyを既定100回か手動指定1000回compileして全ALCを解放する(){
        var repeatCount=Environment.GetEnvironmentVariable("BENCHMARK_DYNAMIC_ALC_STRESS") is { }text
            &&int.TryParse(text,out var parsed)
                ?Math.Clamp(parsed,1,1_000)
                :100;
        var query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Seed256.Parse("0xA1C"),
            QueryIndex=0,
            QueryId="alc-stress-q000000",
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=1,
            MaximumDepth=1,
            InputElementCount=32,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Count],
        });
        var source=new QuerySourceEmitter().Emit(query,new QuerySourceEmissionOptions{
            GeneratedNamespace="Benchmark.Dynamic.Generated",
            ClassName="AlcStressQuery",
        });
        var compiler=new DynamicQueryCompiler();
        var primary=Enumerable.Range(0,32).ToArray();
        var secondary=Enumerable.Range(32,32).ToArray();

        Assert.True(CompileRunUnload(compiler,source,primary,secondary,-1).Unloaded);
        ForceFullCollection();
        using var process=Process.GetCurrentProcess();
        process.Refresh();
        var beforePrivateBytes=process.PrivateMemorySize64;
        for(var index=0;index<repeatCount;index++){
            var unload=CompileRunUnload(compiler,source,primary,secondary,index);
            Assert.True(unload.Unloaded,$"ALC {index} remained alive after {unload.CollectionAttempts} GC attempts.");
        }
        ForceFullCollection();
        process.Refresh();
        var privateByteGrowth=process.PrivateMemorySize64-beforePrivateBytes;

        Assert.True(
            privateByteGrowth<512L*1024*1024,
            $"Private bytes grew by {privateByteGrowth:N0} bytes across {repeatCount} collectible compilations.");
    }

    [Fact,Trait("Category","Integration")]
    public async Task SystemArrayを4経路検証し3系列計測してALCを解放する(){
        var Seed=Seed256.Parse("0x2A");
        var Query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Seed,
            QueryIndex=0,
            QueryId="vertical-q000000",
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=3,
            MaximumDepth=3,
            InputElementCount=32,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[
                QueryOperatorIds.Where,
                QueryOperatorIds.Select,
                QueryOperatorIds.Count,
            ],
        });
        var 要求=new WorkerRunRequest(
            "vertical-run",
            Seed,
            1,
            3,
            3,
            32,
            OperatorSetMode.Maximum,
            [SourceTarget.SystemArray],
            new WorkerMeasurementProfile("Vertical-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,
            QueryCapabilityProfile.CurrentProfileVersion.ToString(),
            BuiltInOperatorCatalog.OperatorSetVersion.ToString());

        var 結果=await new DynamicWorkerExecutionRunner().ExecuteAsync(
            要求,
            Query,
            0,
            SourceTarget.SystemArray,
            0,
            CancellationToken.None);

        Assert.True(
            結果.Status==WorkerExecutionStatus.Succeeded,
            $"Status={結果.Status}{Environment.NewLine}{結果.DiagnosticsJson}{Environment.NewLine}{結果.SourceText}");
        Assert.True(結果.AssemblyUnloaded);
        Assert.False(string.IsNullOrWhiteSpace(結果.SourceText));
        Assert.False(string.IsNullOrWhiteSpace(結果.ResultJson));
        Assert.False(string.IsNullOrWhiteSpace(結果.OptimizationTraceJson));
        Assert.False(string.IsNullOrWhiteSpace(結果.MeasurementsJson));
        using var Artifact=JsonDocument.Parse(結果.MeasurementsJson!);
        Assert.True(Artifact.RootElement.GetProperty("reachedFixedPoint").GetBoolean());
        Assert.Equal(
            3,
            Artifact.RootElement
                .GetProperty("measurement")
                .GetProperty("measurements")
                .GetArrayLength());
    }

    [Fact,Trait("Category","Integration")]
    public async Task SystemArrayのN64再帰木を4経路検証し測定してALCを解放する(){
        QueryIrDocument? query=null;
        Seed256 selectedSeed=default;
        for(ulong seedValue=1;seedValue<=256&&query is null;seedValue++){
            var seed=Seed256.FromUInt64(seedValue);
            var candidate=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
                MasterSeed=seed,
                QueryIndex=0,
                QueryId="recursive-n64-q000000",
                SourceTarget=SourceTarget.SystemArray,
                OperatorCount=64,
                MaximumDepth=32,
                InputElementCount=8,
                MaximumCardinality=100_000,
                EnableBinaryOperators=true,
                EnableNestedOperators=true,
                AllowedOperatorIds=[
                    QueryOperatorIds.Where,
                    QueryOperatorIds.Select,
                    QueryOperatorIds.DistinctBy,
                    QueryOperatorIds.Concat,
                    QueryOperatorIds.JoinXor,
                    QueryOperatorIds.GroupJoinXorCount,
                    QueryOperatorIds.Count,
                ],
            });
            var nodes=Enumerate(candidate.Root).ToArray();
            if(nodes.Any(static node=>node is BinaryQueryNode)
                &&nodes.Any(static node=>node is NestedQueryNode)){
                query=candidate;
                selectedSeed=seed;
            }
        }
        Assert.NotNull(query);
        var request=new WorkerRunRequest(
            "recursive-n64-run",
            selectedSeed,
            1,
            64,
            32,
            8,
            OperatorSetMode.Maximum,
            [SourceTarget.SystemArray],
            new WorkerMeasurementProfile("Recursive-N64-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,
            QueryCapabilityProfile.CurrentProfileVersion.ToString(),
            BuiltInOperatorCatalog.OperatorSetVersion.ToString());

        var result=await new DynamicWorkerExecutionRunner().ExecuteAsync(
            request,
            query!,
            0,
            SourceTarget.SystemArray,
            0,
            CancellationToken.None);

        Assert.True(
            result.Status==WorkerExecutionStatus.Succeeded,
            $"Seed={selectedSeed}{Environment.NewLine}Status={result.Status}{Environment.NewLine}{result.DiagnosticsJson}{Environment.NewLine}{result.ResultJson}{Environment.NewLine}{result.SourceText}");
        Assert.True(result.AssemblyUnloaded);
        Assert.Equal(64,query!.ActualOperatorCount);
        Assert.InRange(query.ActualDepth,1,32);
    }

    [Fact,Trait("Category","Integration")]
    public void ZLinq途中からSystemLinqへfallbackしたSourceをコンパイル前gateで拒否する(){
        var query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Seed256.Parse("0xFA11BAC"),
            QueryIndex=0,
            QueryId="fallback-q000000",
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=1,
            MaximumDepth=1,
            InputElementCount=4,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=[QueryOperatorIds.Count],
        });
        var emitted=new QuerySourceEmitter().Emit(query,new QuerySourceEmissionOptions{
            ClassName="FallbackQuery",
        });
        const string expected="(global::ZLinq.ValueEnumerable.AsValueEnumerable(primarySource)).Count()";
        const string fallback="global::System.Linq.Enumerable.Count((global::ZLinq.ValueEnumerable.AsValueEnumerable(primarySource)).ToArray())";
        Assert.Contains(expected,emitted.SourceText,StringComparison.Ordinal);
        var source=emitted.SourceText.Replace(expected,fallback,StringComparison.Ordinal);

        var result=new DynamicQueryCompiler().Compile(new DynamicCompilationRequest(
            "Benchmark.Dynamic.FallbackProbe",
            emitted.QualifiedTypeName,
            source,
            "FallbackQuery.g.cs"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,static diagnostic=>
            diagnostic.ID=="InvocationTargetMismatch"
            &&diagnostic.Message.Contains("RunZLinq",StringComparison.Ordinal));
        Assert.Null(result.LoadedQuery);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static AssemblyUnloadResult CompileRunUnload(
        DynamicQueryCompiler compiler,
        QuerySourceEmissionResult source,
        int[] primary,
        int[] secondary,
        int index){
        var result=compiler.Compile(new DynamicCompilationRequest(
            $"Benchmark.Dynamic.AlcStress.{index:D4}",
            source.QualifiedTypeName,
            source.SourceText,
            $"AlcStressQuery.{index:D4}.g.cs"));
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine,result.Diagnostics.Select(static diagnostic=>$"{diagnostic.ID}: {diagnostic.Message}")));
        var loaded=Assert.IsType<LoadedDynamicQuery>(result.LoadedQuery);
        var value=loaded.Run(DynamicImplementationKind.System,primary,secondary);
        var atom=Assert.IsType<IntQueryAtom>(Assert.Single(value.Items));
        Assert.Equal(32,atom.Value);
        return loaded.UnloadAndVerify();
    }

    private static void ForceFullCollection(){
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
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
