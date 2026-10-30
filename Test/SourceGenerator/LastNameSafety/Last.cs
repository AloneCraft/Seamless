using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.FLinq;

namespace Test.SourceGenerator.LastNameSafety;

// 名前だけの独立回帰候補。既存10800/414の入力・assertには変更を加えない。
[LinqIntercept(false)]
public sealed class Last{
    [Fact]
    public void CapturedMethodArgumentNames(){
        var failures=new List<string>();
        for(var query=0;query<2;query++){
            foreach(var none in new[]{false,true}){
                var source=Source(query,none);
                var stage=query+"-"+none;
                var options=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([
                    new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")
                ]);
                var compilation=GeneratorTestHelpers.CreateCompilation([("NameSafety.cs",source)]).WithAssemblyName("LastNameSafety"+stage);
                compilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),CSharpSyntaxTree.ParseText(source,options,path:"NameSafety.cs"));
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
                        var context=new global::System.Runtime.Loader.AssemblyLoadContext("LastNameSafety"+stage,isCollectible:true);
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
        var parameters="int[] values,int __i";
        var arguments="values,2";
        var expression="values.Last(p=>p>__i)";
        if(query==1){
            parameters="int[] values,int __lastSource0,int __lastIndex0,int __lastKeep0_Terminal";
            arguments="values,1,0,1";
            expression="values.Last(p=>p>__lastSource0+__lastIndex0+__lastKeep0_Terminal)";
        }
        return $$"""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Probe{
                public static object[] Run(){
                    var probe=new Probe();
                    var values=new[]{1,2,3,4};
                    using var scope=LinqCounterScope.Start();
                    var actual=probe.Query({{arguments}});
                    var fallback=scope.Counter.EnumerableFallback;
                    var physical=scope.Counter.PhysicalDelegate;
                    var expected=probe.QuerySystem({{arguments}});
                    return new object[]{actual,expected,fallback,physical};
                }
                {{attribute}}
                private int Query({{parameters}})=>{{expression}};
                [LinqIntercept(false)]
                private int QuerySystem({{parameters}})=>{{expression}};
            }
            """;
    }

    private static void Save(string stage,string source,string generated,object observation){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_LAST_NAME_SAFETY_ARTIFACT_DIR");
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
