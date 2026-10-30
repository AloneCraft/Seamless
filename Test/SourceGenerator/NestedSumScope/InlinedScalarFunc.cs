using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.FLinq;

namespace Test.SourceGenerator.NestedSumScope;

[LinqIntercept(false)]
public sealed class InlinedScalarFunc{
    private const string Header="""
        using System;
        using System.Linq;
        using System.Collections.Generic;
        using Seamless.FLinq;
        using Seamless.Linq;
        namespace Sample;
        [LinqIntercept(false)]
        public sealed class State{
            public readonly List<string> Trace=new();
            public int Disposes;
            public int Failure;
            public long Record(string stage,long value){
                this.Trace.Add(stage+":"+value);
                if(this.Failure==1&&stage=="S"&&value==2){throw new InvalidOperationException("selector");}
                return value;
            }
            public bool Guard(int value){this.Trace.Add("G:"+value);return value>0;}
            public IEnumerable<long> Begin(int key){this.Trace.Add("K:"+key);return this.Sequence(key);}
            public IEnumerable<long> Sequence(int key){
                try{
                    if(this.Failure==3&&key==2){
                        this.Trace.Add("N:"+long.MaxValue);yield return long.MaxValue;
                        this.Trace.Add("N:1");yield return 1;
                    }else{
                        this.Trace.Add("N:"+key);yield return key;
                    }
                }finally{
                    this.Disposes++;this.Trace.Add("D:"+key);
                    if(this.Failure==2&&key==2){throw new InvalidOperationException("dispose");}
                }
            }
        }
        """;
    private const string Queries="""
        [Query]
        public int Ordered(State state,int[] values)=>values.OrderBy(value=>state.Begin(value)
            .Where(item=>state.Record("W",item).Anchor>0)
            .Select(item=>state.Record("S",item).Anchor).Sum().Anchor).FirstOrDefault();
        [Query]
        public int Depth2(State state,int[] values)=>values.OrderBy(value=>state.Begin(value)
            .Where(item=>state.Record("W",item).Anchor>0)
            .Select(item=>state.Begin((int)item).Where(inner=>state.Record("W",inner).Anchor>0)
                .Select(inner=>state.Record("S",inner).Anchor).Sum().Anchor).Sum().Anchor).FirstOrDefault();
        [Query]
        public int ShortCircuit(State state,int[] values)=>values.OrderBy(value=>state.Guard(value).Anchor&&state.Begin(value)
            .Where(item=>state.Record("W",item).Anchor>0)
            .Select(item=>state.Record("S",item).Anchor).Sum().Anchor>0).FirstOrDefault();
        [Query]
        public int Composite(State state,int[] values)=>values.OrderBy(value=>state.Record("L",value).Anchor+state.Begin(value)
            .Where(item=>state.Record("W",item).Anchor>0)
            .Select(item=>state.Record("S",item).Anchor).Sum().Anchor+state.Record("R",value).Anchor).FirstOrDefault();
        """;

    [Fact]
    public void OrderedDestinationsAndDispose(){
        var source=CreateSource();
        var options=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([
            new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")]);
        var compilation=global::Test.SourceGenerator.GeneratorTestHelpers.CreateCompilation([("Scope.cs",source)]).WithAssemblyName("NestedSumScope");
        compilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),CSharpSyntaxTree.ParseText(source,options,path:"Scope.cs"));
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:options);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);
        var generated=string.Concat(output.SyntaxTrees.Skip(1).Select(tree=>tree.ToString()));
        using var pe=new MemoryStream();
        var emit=output.Emit(pe);
        var errors=diagnostics.Concat(output.GetDiagnostics()).Concat(emit.Diagnostics)
            .Where(item=>item.Severity==DiagnosticSeverity.Error).Select(item=>item.ToString()).ToArray();
        Save("input.cs",source);Save("generated.cs",generated);
        Save("compilation.json",JsonSerializer.Serialize(new{
            emit.Success,Errors=errors,
            InputSHA256=global::System.Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(source))),
            GeneratedSHA256=global::System.Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(generated))),
            GeneratorPath=typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location,
            GeneratorSHA256=global::System.Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location)))
        }));
        Assert.Empty(errors);Assert.True(emit.Success);
        Assert.DoesNotContain("UnsupportedLinqExpansion(",generated,StringComparison.Ordinal);
        Assert.True(generated.Split("InterceptsLocationAttribute(1,",StringSplitOptions.None).Length-1>=8);
        var context=new global::System.Runtime.Loader.AssemblyLoadContext("NestedSumScope",isCollectible:true);
        var records=new List<object>();var failures=new List<string>();
        try{
            pe.Position=0;
            var run=context.LoadFromStream(pe).GetType("Sample.Probe")!.GetMethod("Run")!;
            // scenario, failure, values, result, exception, trace, dispose, logical.
            var cases=new[]{
                (0,0,new[]{2,1},"1","","K:2,N:2,W:2,S:2,D:2,K:1,N:1,W:1,S:1,D:1",2,6L),
                (0,1,new[]{2,1},"","InvalidOperationException:selector","K:2,N:2,W:2,S:2,D:2",1,3L),
                (0,2,new[]{2,1},"","InvalidOperationException:dispose","K:2,N:2,W:2,S:2,D:2",1,3L),
                (0,3,new[]{2,1},"","OverflowException:","K:2,N:9223372036854775807,W:9223372036854775807,S:9223372036854775807,N:1,W:1,S:1,D:2",1,5L),
                (0,0,Array.Empty<int>(),"0","","",0,0L),
                (0,0,new[]{1},"1","","K:1,N:1,W:1,S:1,D:1",1,3L),
                (1,0,new[]{2,1},"1","","K:2,N:2,W:2,K:2,N:2,W:2,S:2,D:2,D:2,K:1,N:1,W:1,K:1,N:1,W:1,S:1,D:1,D:1",4,10L),
                (2,0,new[]{-2,-1},"-2","","G:-2,G:-1",0,2L),
                (3,0,new[]{2,1},"1","","L:2,K:2,N:2,W:2,S:2,D:2,R:2,L:1,K:1,N:1,W:1,S:1,D:1,R:1",2,6L)
            };
            foreach(var item in cases){
                var reference=(object[])run.Invoke(null,[0,item.Item1,item.Item2,item.Item3])!;
                var exceptionMatches=Equals(reference[1],item.Item5);
                if(item.Item2==3){exceptionMatches=((string)reference[1]).StartsWith(item.Item5,StringComparison.Ordinal);}
                if(!Equals(reference[0],item.Item4)||!exceptionMatches
                    ||!Equals(reference[2],item.Item6)||!Equals(reference[3],item.Item7)){
                    failures.Add("System independent oracle: scenario="+item.Item1+";failure="+item.Item2);
                }
                for(var mode=1;mode<3;mode++){
                    var actual=(object[])run.Invoke(null,[mode,item.Item1,item.Item2,item.Item3])!;
                    for(var column=0;column<4;column++){
                        if(!Equals(reference[column],actual[column])){failures.Add("Meaning/Dispose: mode="+mode+";scenario="+item.Item1+";failure="+item.Item2+";column="+column);}
                    }
                    if((long)actual[4]!=item.Item8||(long)actual[5]!=0||(long)actual[6]!=0){failures.Add("Counters: mode="+mode+";scenario="+item.Item1+";failure="+item.Item2);}
                    records.Add(new{Mode=mode,Scenario=item.Item1,Failure=item.Item2,Reference=reference,Actual=actual});
                }
            }
        }finally{context.Unload();}
        Save("observations.json",JsonSerializer.Serialize(new{Records=records,Failures=failures}));
        Assert.Empty(failures);
    }

    private static string CreateSource(){
        var builder=new StringBuilder(Header);
        foreach(var mode in new[]{"System","None","All"}){
            var attribute="[LinqIntercept(false)]";
            if(mode=="None"){attribute="[LinqIntercept(true,LinqInstrumentation.Counter,Optimizations=LinqOptimizations.None)]";}
            if(mode=="All"){attribute="[LinqIntercept(true,LinqInstrumentation.Counter)]";}
            builder.AppendLine("public sealed class "+mode+"Queries{");
            builder.AppendLine(Queries.Replace("[Query]",attribute,StringComparison.Ordinal));builder.AppendLine("}");
        }
        builder.AppendLine("[LinqIntercept(false)]public sealed class Probe{public static object[] Run(int mode,int scenario,int failure,int[] values){var state=new State{Failure=failure};using var scope=LinqCounterScope.Start();string result=\"\",error=\"\";try{");
        foreach(var mode in new[]{"System","None","All"}){
            var number=0;if(mode=="None"){number=1;}if(mode=="All"){number=2;}
            builder.AppendLine("if(mode=="+number+"){var queries=new "+mode+"Queries();switch(scenario){");
            var names=new[]{"Ordered","Depth2","ShortCircuit","Composite"};
            for(var index=0;index<names.Length;index++){builder.AppendLine("case "+index+":result=queries."+names[index]+"(state,values).ToString();break;");}
            builder.AppendLine("}}");
        }
        builder.AppendLine("}catch(Exception exception){error=exception.GetType().Name+\":\"+exception.Message;}return new object[]{result,error,string.Join(\",\",state.Trace),state.Disposes,scope.Counter.LogicalDelegate,scope.Counter.PhysicalDelegate,scope.Counter.EnumerableFallback};}}");
        return builder.ToString();
    }
    private static void Save(string name,string text){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_NESTED_SUM_SCOPE_ARTIFACT_DIR");if(string.IsNullOrWhiteSpace(root)){return;}
        Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,name),text,new UTF8Encoding(false));
    }
}
