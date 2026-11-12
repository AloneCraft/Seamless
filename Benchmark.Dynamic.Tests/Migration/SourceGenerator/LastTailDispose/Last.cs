using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.FLinq;

namespace Test.SourceGenerator.LastTailDispose;

[LinqIntercept(false)]
public sealed class Last{
    [Fact]
    public void TailSelectorAndSourceDispose(){
        var failures=new List<string>();
        for(var shape=0;shape<2;shape++){
            foreach(var orDefault in new[]{false,true}){
                foreach(var none in new[]{false,true}){
                    var source=Source(shape,orDefault,none);
                    var stage=shape+"-"+orDefault+"-"+none;
                    var options=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([
                        new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")
                    ]);
                    var compilation=GeneratorTestHelpers.CreateCompilation([("TailDispose.cs",source)]).WithAssemblyName("LastTailDispose"+stage);
                    compilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),CSharpSyntaxTree.ParseText(source,options,path:"TailDispose.cs"));
                    GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:options);
                    driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var generatorDiagnostics);
                    var generated=string.Concat(output.SyntaxTrees.Skip(1).Select(tree=>tree.ToString()));
                    var diagnostics=generatorDiagnostics.Concat(output.GetDiagnostics()).ToList();
                    var errors=diagnostics.Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(diagnostic=>diagnostic.ToString()).ToList();
                    var interceptions=generated.Split("InterceptsLocationAttribute(1,").Length-1;
                    if(interceptions!=1||!generated.Contains("OptimizedLinq_Query_",StringComparison.Ordinal)){
                        errors.Add("Interception: expected one Query interceptor, actual="+interceptions);
                    }
                    var emitSucceeded=false;
                    var observations=new List<object>();
                    if(errors.Count==0){
                        using var pe=new MemoryStream();
                        var emit=output.Emit(pe);
                        emitSucceeded=emit.Success;
                        diagnostics.AddRange(emit.Diagnostics);
                        if(!emit.Success){errors.AddRange(emit.Diagnostics.Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(diagnostic=>diagnostic.ToString()));}
                        else{
                            pe.Position=0;
                            var context=new global::System.Runtime.Loader.AssemblyLoadContext("LastTailDispose"+stage,isCollectible:true);
                            try{
                                var type=context.LoadFromStream(pe).GetType("Sample.Probe")!;
                                for(var scenario=0;scenario<2;scenario++){
                                    var observation=(object[])type.GetMethod("Run")!.Invoke(null,[scenario])!;
                                    observations.Add(new{Scenario=scenario,Observation=observation});
                                    if(!Equals(observation[0],observation[1])){errors.Add("System difference scenario="+scenario+": "+observation[0]+" / "+observation[1]);}
                                    if((long)observation[2]!=0){errors.Add("EnumerableFallback scenario="+scenario+": "+observation[2]);}
                                    if((long)observation[3]!=0){errors.Add("PhysicalDelegate scenario="+scenario+": "+observation[3]);}
                                }
                            }catch(Exception exception){errors.Add("Execution: "+exception);}
                            finally{context.Unload();}
                        }
                    }
                    Save(stage,source,generated,new{Shape=shape,OrDefault=orDefault,None=none,Interceptions=interceptions,EmitSucceeded=emitSucceeded,Observations=observations,Diagnostics=diagnostics.Select(diagnostic=>diagnostic.ToString()).ToArray(),Errors=errors});
                    failures.AddRange(errors.Select(error=>stage+": "+error));
                }
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine,failures));
    }

    private static string Source(int shape,bool orDefault,bool none){
        var attribute="[LinqIntercept(true,LinqInstrumentation.Counter)]";
        if(none){attribute="[LinqIntercept(true,LinqInstrumentation.Counter,Optimizations=LinqOptimizations.None)]";}
        var expression="values.Select(item=>this.Choose(item))";
        if(shape==1){expression="values.Where(item=>this.Keep(item)).Select(item=>this.Choose(item))";}
        if(orDefault){expression+=".LastOrDefault()";}
        else{expression+=".Last()";}
        return $$$"""
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Probe{
                private readonly List<string> Trace=new();
                private int Scenario;
                private bool Disposed;
                public static object[] Run(int scenario){
                    var actual=new Probe{Scenario=scenario};
                    var oracle=new Probe{Scenario=scenario};
                    using var scope=LinqCounterScope.Start();
                    var observed=actual.Capture(false);
                    var fallback=scope.Counter.EnumerableFallback;
                    var physical=scope.Counter.PhysicalDelegate;
                    var reference=oracle.Capture(true);
                    return new object[]{observed,reference,fallback,physical};
                }
                private string Capture(bool system){
                    var value="";var error="";var message="";
                    try{
                        var source=new Input(this);
                        int result;
                        if(system){result=this.QuerySystem(source);}
                        else{result=this.Query(source);}
                        value=result.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }catch(Exception exception){error=exception.GetType().FullName!;message=exception.Message;}
                    return value+"|"+error+"|"+message+"|"+string.Join(",",this.Trace)+"|Disposed="+this.Disposed;
                }
                {{{attribute}}}
                private int Query(IEnumerable<int> values)=>{{{expression}}};
                [LinqIntercept(false)]
                private int QuerySystem(IEnumerable<int> values)=>{{{expression}}};
                private bool Keep(int item){this.Trace.Add("P:"+item);return item%2==0;}
                private int Choose(int item){
                    this.Trace.Add("S:"+item+":"+this.Disposed);
                    if(this.Scenario==1){throw new ArgumentException("selector");}
                    if(this.Disposed){return item+100;}
                    return item;
                }
                private sealed class Input(Probe owner):IEnumerable<int>{
                    public IEnumerator<int> GetEnumerator(){owner.Trace.Add("G");return new Enumerator(owner);}
                    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
                }
                // MoveNext(false)ではDisposeを呼ばず、利用側usingの順序を可視化する。
                private sealed class Enumerator(Probe owner):IEnumerator<int>{
                    private int Index;
                    public int Current{get{owner.Trace.Add("R:"+this.Index);return this.Index;}}
                    object IEnumerator.Current=>this.Current;
                    public bool MoveNext(){this.Index++;owner.Trace.Add("N:"+this.Index);return this.Index<=4;}
                    public void Dispose(){
                        owner.Trace.Add("D");owner.Disposed=true;
                        if(owner.Scenario==1){throw new InvalidOperationException("dispose");}
                    }
                    public void Reset()=>throw new NotSupportedException();
                }
            }
            """;
    }

    private static void Save(string stage,string source,string generated,object observation){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_LAST_TAIL_DISPOSE_ARTIFACT_DIR");
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
