using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.FLinq;

namespace Test.SourceGenerator.SupplementThisCapture;

// Rangeを含まない独立capture回帰候補。既存10800/414の入力・assertは変更しない。
[LinqIntercept(false)]
public sealed class Last{
    [Fact]
    public void PipelineCapturesCompileAndMatchSystem(){
        var failures=new List<string>();
        for(var query=0;query<2;query++){
            foreach(var none in new[]{false,true}){
                var source=Source(query,none);
                var stage=query+"-"+none;
                var options=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([
                    new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")
                ]);
                var compilation=GeneratorTestHelpers.CreateCompilation([("ThisCapture.cs",source)]).WithAssemblyName("SupplementThisCapture"+stage);
                compilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),CSharpSyntaxTree.ParseText(source,options,path:"ThisCapture.cs"));
                GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:options);
                driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var generatorDiagnostics);
                var generated=string.Concat(output.SyntaxTrees.Skip(1).Select(tree=>tree.ToString()));
                var diagnostics=generatorDiagnostics.Concat(output.GetDiagnostics()).ToList();
                var errors=diagnostics.Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(diagnostic=>diagnostic.ToString()).ToList();
                var interceptions=generated.Split("InterceptsLocationAttribute(1,").Length-1;
                if(interceptions!=1||!generated.Contains("OptimizedLinq_Query_",StringComparison.Ordinal)){
                    errors.Add("Interception: expected exactly one Query interceptor, actual="+interceptions);
                }
                var emitSucceeded=false;
                var executed=false;
                object[]? observation=null;
                if(errors.Count==0){
                    using var pe=new MemoryStream();
                    var emit=output.Emit(pe);
                    diagnostics.AddRange(emit.Diagnostics);
                    emitSucceeded=emit.Success;
                    if(!emit.Success){
                        errors.AddRange(emit.Diagnostics.Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(diagnostic=>diagnostic.ToString()));
                    }else{
                        pe.Position=0;
                        var context=new global::System.Runtime.Loader.AssemblyLoadContext("SupplementThisCapture"+stage,isCollectible:true);
                        try{
                            var type=context.LoadFromStream(pe).GetType("Sample.Probe")!;
                            observation=(object[])type.GetMethod("Run")!.Invoke(null,null)!;
                            executed=true;
                            if(!Equals(observation[0],observation[1])){errors.Add("Value: actual="+observation[0]+", System="+observation[1]);}
                            if((long)observation[2]!=0){errors.Add("EnumerableFallback: "+observation[2]);}
                            if((long)observation[3]!=0){errors.Add("PhysicalDelegate: "+observation[3]);}
                        }catch(Exception exception){errors.Add("Execution: "+exception);}
                        finally{context.Unload();}
                    }
                }
                var mode="All";
                if(none){mode="None";}
                Save(stage,source,generated,new{Query=query,Mode=mode,Interceptions=interceptions,EmitSucceeded=emitSucceeded,Executed=executed,Observation=observation,Diagnostics=diagnostics.Select(diagnostic=>diagnostic.ToString()).ToArray(),Errors=errors});
                failures.AddRange(errors.Select(error=>stage+": "+error));
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine,failures));
    }

    private static string Source(int query,bool none){
        var attribute="[LinqIntercept(true,LinqInstrumentation.Counter)]";
        if(none){attribute="[LinqIntercept(true,LinqInstrumentation.Counter,Optimizations=LinqOptimizations.None)]";}
        var expression="values.Select(item=>this.Hit(\"S\",item)).Where(item=>this.Hit(\"W\",item)>0).Last()";
        if(query==1){expression="values.Where(item=>this.Hit(\"W1\",item)>0).Select(item=>this.Hit(\"S\",item)).Where(item=>this.Hit(\"W2\",item)>0).Last()";}
        return $$$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Probe{
                private readonly List<string> Trace=new();
                public static object[] Run(){
                    using var scope=LinqCounterScope.Start();
                    var actual=new Probe().Snapshot(false);
                    var fallback=scope.Counter.EnumerableFallback;
                    var physical=scope.Counter.PhysicalDelegate;
                    var expected=new Probe().Snapshot(true);
                    return new object[]{actual,expected,fallback,physical};
                }
                private string Snapshot(bool system){
                    var value="";var error="";var message="";
                    try{
                        var values=new[]{1,2,3,4};
                        int result;
                        if(system){result=this.QuerySystem(values);}
                        else{result=this.Query(values);}
                        value=result.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }catch(Exception exception){error=exception.GetType().FullName!;message=exception.Message;}
                    return value+"|"+error+"|"+message+"|"+string.Join(",",this.Trace);
                }
                {{{attribute}}}
                private int Query(int[] values)=>{{{expression}}};
                [LinqIntercept(false)]
                private int QuerySystem(int[] values)=>{{{expression}}};
                public int Hit(string tag,int item){this.Trace.Add(tag+":"+item);return item;}
            }
            """;
    }

    private static void Save(string stage,string source,string generated,object observation){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_SUPPLEMENT_THIS_CAPTURE_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(root)){return;}
        var directory=Path.Combine(Path.GetFullPath(root),stage);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"input.cs"),source);
        File.WriteAllText(Path.Combine(directory,"generated.cs"),generated);
        File.WriteAllText(Path.Combine(directory,"observations.json"),JsonSerializer.Serialize(observation,new JsonSerializerOptions{WriteIndented=true}));
        static string Hash(string text)=>Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        File.WriteAllText(Path.Combine(directory,"hashes.json"),JsonSerializer.Serialize(new{SourceSHA256=Hash(source),GeneratedSHA256=Hash(generated),GeneratorSHA256=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location))),Runtime=Environment.Version.ToString()}));
    }
}
