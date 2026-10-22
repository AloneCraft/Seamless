using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Seamless.FLinq;
using Seamless.FLinq.Generator;

namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed partial class OptimizationFixedPointTests{
    private const string FixedPointSource="""
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Container{
            [LinqIntercept(
                true,
                Optimizations=LinqOptimizations.All,
                ConvergenceMode=LinqConvergenceMode.GlobalFixedPoint,
                EmitOptimizationTrace=true)]
            private static bool Query(int[] source)=>source
                .Select(static value=>value)
                .Select(static value=>value)
                .Select(static value=>value)
                .Select(static value=>value)
                .Select(static value=>value)
                .Select(static value=>value)
                .Select(static value=>value)
                .Select(static value=>value)
                .Select(static value=>value)
                .Select(static value=>value)
                .Where(static value=>value>0)
                .Count()>0&&source.Where(static value=>value<100).Count()>0;
            public bool Run(int[] source)=>Query(source);
        }
        """;

    private const string PipelineSource="""
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Container{
            [LinqIntercept(
                true,
                Optimizations=LinqOptimizations.All,
                ConvergenceMode=LinqConvergenceMode.GlobalFixedPoint,
                EmitOptimizationTrace=true)]
            private static int[] Query(int[] source)=>source
                .Distinct()
                .Reverse()
                .Reverse()
                .Distinct()
                .ToArray();
            public int[] Run(int[] source)=>Query(source);
        }
        """;

    private const string OptimizedSource="""
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Container{
            [LinqIntercept(
                true,
                Optimizations=LinqOptimizations.All,
                ConvergenceMode=LinqConvergenceMode.GlobalFixedPoint,
                EmitOptimizationTrace=true)]
            private static int[] Query(int[] source)=>source
                .Where(static value=>value>0)
                .ToArray();
            public int[] Run(int[] source)=>Query(source);
        }
        """;

    private const string LegacySource="""
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Container{
            [LinqIntercept(true,Optimizations=LinqOptimizations.All)]
            private static int Query(int[] source)=>source
                .Select(static value=>value)
                .Count();
            public int Run(int[] source)=>Query(source);
        }
        """;

    [Fact]
    public void FixedPointRunnerStopsDeterministically(){
        const string probeSource="""
            using Seamless.FLinq.Generator;
            public static class Probe{
                private static FixedPointRoundResult<int> Next(int state,int round){
                    _=round;
                    if(state>=12){
                        return FixedPointRoundResult<int>.Unchanged(state);
                    }
                    return FixedPointRoundResult<int>.ChangedState(state+1);
                }
                public static string Run(){
                    var fixedPoint=FixedPointRunner.Run(
                        0,
                        Next,
                        static state=>state.ToString());
                    var cycle=FixedPointRunner.Run(
                        false,
                        static (state,_)=>FixedPointRoundResult<bool>.ChangedState(!state),
                        static state=>state.ToString());
                    var limit=FixedPointRunner.Run(
                        0,
                        static (state,_)=>FixedPointRoundResult<int>.ChangedState(state+1),
                        static state=>state.ToString(),
                        maximumRounds:10);
                    var rebind=FixedPointRunner.Run(
                        0,
                        static (state,_)=>FixedPointRoundResult<int>.RebindFailure(state),
                        static state=>state.ToString());
                    return $"{fixedPoint.Termination}:{fixedPoint.RoundCount}:{fixedPoint.State}|{cycle.Termination}:{cycle.RoundCount}|{limit.Termination}:{limit.RoundCount}|{rebind.Termination}:{rebind.State}";
                }
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Probe.cs",probeSource)])
            .WithAssemblyName("Benchmark.Dynamic.Tests")
            .AddReferences(MetadataReference.CreateFromFile(typeof(FLinqIncrementalGenerator).Assembly.Location));
        using var pe=new MemoryStream();
        var emit=compilation.Emit(pe);
        Assert.True(emit.Success,string.Join(Environment.NewLine,emit.Diagnostics));
        pe.Position=0;
        var loadContext=new global::System.Runtime.Loader.AssemblyLoadContext("FixedPointRunnerProbe",isCollectible:true);
        try{
            var assembly=loadContext.LoadFromStream(pe);
            var value=(string)assembly.GetType("Probe")!.GetMethod("Run")!.Invoke(null,null)!;
            Assert.Equal("FixedPoint:13:12|CycleDetected:2|IterationLimit:10|RebindFailed:0",value);
        }finally{
            loadContext.Unload();
        }
    }
    [Fact]
    public void GlobalFixedPoint(){
        var result=Generate(FixedPointSource);
        Assert.Empty(result.Errors);
        Assert.Single(result.Traces);
        using var document=JsonDocument.Parse(result.Traces[0]);
        var root=document.RootElement;
        Assert.Equal(1,root.GetProperty("SchemaVersion").GetInt32());
        Assert.True(root.GetProperty("Summary").GetProperty("ReachedFixedPoint").GetBoolean());
        Assert.True(root.GetProperty("Summary").GetProperty("RewriteCount").GetInt32()>0);
        Assert.True(root.GetProperty("Summary").GetProperty("MaxRewriteDepth").GetInt32()>=2);
        Assert.True(root.GetProperty("Summary").GetProperty("RevisitedAfterRewrite").GetBoolean());
        Assert.True(root.GetProperty("Summary").GetProperty("LeftSubtreeOptimized").GetBoolean());
        Assert.True(root.GetProperty("Summary").GetProperty("RightSubtreeOptimized").GetBoolean());
        Assert.Contains("Algebraic.CountComparisonToAny",root.GetProperty("Summary").GetProperty("AppliedRuleNames").EnumerateArray().Select(static rule=>rule.GetString()));
        Assert.Contains(
            root.GetProperty("LayerTerminations").EnumerateArray(),
            static termination=>termination.GetProperty("Layer").GetString()=="Syntax"
                &&termination.GetProperty("Termination").GetString()=="FixedPoint");
    }

    [Fact]
    public void PipelineGlobalFixedPoint(){
        var result=Generate(PipelineSource);
        Assert.Empty(result.Errors);
        Assert.Single(result.Traces);
        using var document=JsonDocument.Parse(result.Traces[0]);
        var pipelineTerminations=document.RootElement
            .GetProperty("LayerTerminations")
            .EnumerateArray()
            .Where(static termination=>termination.GetProperty("Layer").GetString()=="Pipeline")
            .ToArray();
        Assert.NotEmpty(pipelineTerminations);
        Assert.Contains(
            pipelineTerminations,
            static termination=>termination.GetProperty("RoundCount").GetInt32()>=2
                &&termination.GetProperty("Termination").GetString()=="FixedPoint");
        Assert.Contains(
            document.RootElement.GetProperty("Events").EnumerateArray(),
            static traceEvent=>traceEvent.GetProperty("RuleId").GetString() is "Pipeline.Reverse" or "Algebraic.Reverse"); // Reverse の打ち消しは、構文の層(Algebraic)が先に行うことがある。
    }

    [Fact]
    public void TraceIsDeterministicAndOptimizedInputIsIdempotent(){
        var first=Generate(FixedPointSource);
        var second=Generate(FixedPointSource);
        Assert.Empty(first.Errors);
        Assert.Empty(second.Errors);
        Assert.Equal(first.Traces,second.Traces);

        var optimized=Generate(OptimizedSource);
        Assert.Empty(optimized.Errors);
        Assert.Single(optimized.Traces);
        using var document=JsonDocument.Parse(optimized.Traces[0]);
        var summary=document.RootElement.GetProperty("Summary");
        Assert.Equal(0,summary.GetProperty("RewriteCount").GetInt32());
        Assert.True(summary.GetProperty("ReachedFixedPoint").GetBoolean());
    }

    [Fact]
    public void LegacyModeDoesNotEmitOptimizationTrace(){
        var result=Generate(LegacySource);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Traces);
    }

    [Fact]
    public void GlobalFixedPointPreservesMeaning(){
        var compilation=GenerateExecutable(FixedPointSource);
        using var pe=new MemoryStream();
        var emit=compilation.Emit(pe);
        Assert.True(emit.Success,string.Join(Environment.NewLine,emit.Diagnostics));
        pe.Position=0;
        var loadContext=new global::System.Runtime.Loader.AssemblyLoadContext(
            "GlobalFixedPointMeaningProbe",
            isCollectible:true);
        try{
            var assembly=loadContext.LoadFromStream(pe);
            var container=Activator.CreateInstance(assembly.GetType("Sample.Container")!)!;
            var run=container.GetType().GetMethod("Run")!;
            int[][] samples=[[],[-4,-1,0],[1],[1,101],[101,200],[1,2,3,99]];
            foreach(var sample in samples){
                var expected=sample.Where(static value=>value>0).Any()
                    &&sample.Where(static value=>value<100).Any();
                var actual=(bool)run.Invoke(container,[sample])!;
                Assert.Equal(expected,actual);
            }
        }finally{
            loadContext.Unload();
        }
    }

    private static GenerationResult Generate(string source){
        var compilation=GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var outputCompilation,out var generatorDiagnostics);
        var runResult=driver.GetRunResult();
        var generated=runResult.Results
            .SelectMany(static result=>result.GeneratedSources)
            .Select(static source=>source.SourceText.ToString())
            .ToArray();
        var errors=generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error)
            .Where(static diagnostic=>diagnostic.Id!="CS9137")
            .ToArray();
        var traces=generated
            .SelectMany(static text=>TraceLine().Matches(text).Select(static match=>match.Groups[1].Value))
            .Select(static encoded=>Encoding.UTF8.GetString(Convert.FromBase64String(encoded)))
            .ToArray();
        return new(errors,traces);
    }

    private static CSharpCompilation GenerateExecutable(string source){
        var parseOptions=new CSharpParseOptions(LanguageVersion.Preview)
            .WithFeatures([
                new("InterceptorsPreview","true"),
                new("InterceptorsNamespaces","Intercept"),
                new("InterceptorsPreviewNamespaces","Intercept"),
            ]);
        var compilation=GeneratorTestHelpers.CreateCompilation([("Container.cs",source)])
            .WithAssemblyName("GlobalFixedPointMeaningProbe");
        var originalTree=compilation.SyntaxTrees.Single();
        var executableTree=CSharpSyntaxTree.ParseText(source,parseOptions,path:"Container.cs");
        compilation=compilation.ReplaceSyntaxTree(originalTree,executableTree);
        GeneratorDriver driver=CSharpGeneratorDriver.Create(
            [new FLinqIncrementalGenerator().AsSourceGenerator()],
            parseOptions:parseOptions,
            driverOptions:new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps:false));
        driver=driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);
        var errors=generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
        return (CSharpCompilation)outputCompilation;
    }

    [GeneratedRegex(@"// FLinqOptimizationTrace:([A-Za-z0-9+/=]+)")]
    private static partial Regex TraceLine();

    private sealed record GenerationResult(Diagnostic[] Errors,string[] Traces);
}
