using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.FLinq;

namespace Test.SourceGenerator.LastTraversal;

// 候補。root へ取り込み、BGO の source S / build-dir X で実行するまで未検証。
[LinqIntercept(false)]
public sealed class Last{
    private static readonly string[] Shapes=["Array","List","Iterator","ArrayAsEnumerable","ListAsEnumerable"];
    private static readonly string[] Modes=["All","None","CounterOnly","System"];
    private static readonly string[] Queries=["LastPredicate","LastOrDefaultPredicate","NestedLast","NestedLastOrDefault","WhereLast","WhereLastOrDefault","WhereSelectLast","NestedLastTarget","NestedLastOrDefaultTarget"];

    [Fact]
    public void PredicateAndNestedSystemOracle(){
        (string Name,int[] Values,int Target)[] cases=[
            ("empty",[],4),("single-match",[7],7),("single-no-match",[7],8),
            ("tail-match",[1,2,3,4],4),("duplicates",[1,2,1,4,4],4),
            ("negative",[-4,-3,-2,-1],-2),("no-match",[1,3,5],2),("head-only-match",[1,2,3,4],1)
        ];
        var observations=new List<object>();
        var failures=new List<string>();
        foreach(var shape in Shapes){
            foreach(var anchored in new[]{false,true}){
                var source=BuildSource(shape,anchored);
                var (compilation,generated)=Generate(source,shape,anchored);
                ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>{
                    var run=probe.GetMethod("Run")!;
                    foreach(var item in cases){
                        for(var id=0;id<Queries.Length;id++){
                            // 0:通常、1:outer先頭throw、2:outer末尾throw、3:inner先頭throw、4:inner末尾throw。
                            for(var failure=0;failure<5;failure++){
                                var reference=(object[])run.Invoke(null,[3,id,item.Values,item.Target,failure])!;
                                for(var mode=0;mode<3;mode++){
                                    var actual=(object[])run.Invoke(null,[mode,id,item.Values,item.Target,failure])!;
                                    var context=$"{shape}/{anchored}/{Queries[id]}/{item.Name}/throw{failure}/{Modes[mode]}";
                                    var differences=Compare(reference,actual,anchored||mode!=0);
                                    foreach(var difference in differences){failures.Add(context+": "+difference);}
                                    var tracePolicy="PerEventUpperBound";
                                    if(anchored||mode!=0){tracePolicy="Exact";}
                                    observations.Add(new{Shape=shape,Anchored=anchored,Query=Queries[id],Case=item.Name,Input=item.Values,item.Target,Failure=failure,Mode=Modes[mode],TracePolicy=tracePolicy,Differences=differences,Actual=Snapshot(actual),Reference=Snapshot(reference)});
                                }
                            }
                        }
                    }
                    return true;
                });
                Save(shape+"-"+anchored,source,generated,observations,failures);
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine,failures));
    }

    private static string BuildSource(string shape,bool anchored){
        var source=new StringBuilder("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Probe{
                public int[] Data=Array.Empty<int>();
                public int Target;
                public int Failure;
                public readonly List<string> Trace=new();
                public int Hit(string tag,int value){
                    this.Trace.Add("H:"+tag+":"+value);
                    var inner=tag.StartsWith("I",StringComparison.Ordinal);
                    if(this.Data.Length>0&&((!inner&&this.Failure==1&&value==this.Data[0])||(!inner&&this.Failure==2&&value==this.Data[^1])||(inner&&this.Failure==3&&value==this.Data[0])||(inner&&this.Failure==4&&value==this.Data[^1]))){
                        throw new InvalidOperationException("hit:"+tag+":"+value);
                    }
                    return value;
                }
                private IEnumerable<int> Iterate(string tag){
                    try{
                        for(var index=0;index<this.Data.Length;index++){
                            this.Trace.Add("E:"+tag+":"+index);
                            yield return this.Data[index];
                        }
                    }finally{this.Trace.Add("D:"+tag);}
                }
            """);
        switch(shape){
            case "Array":source.AppendLine("public int[] Values(string tag){this.Trace.Add(\"F:\"+tag);return this.Data;}");break;
            case "List":source.AppendLine("public List<int> Values(string tag){this.Trace.Add(\"F:\"+tag);return new List<int>(this.Data);}");break;
            case "Iterator":source.AppendLine("public IEnumerable<int> Values(string tag){this.Trace.Add(\"F:\"+tag);return this.Iterate(tag);}");break;
            case "ArrayAsEnumerable":source.AppendLine("public IEnumerable<int> Values(string tag){this.Trace.Add(\"F:\"+tag);return this.Data;}");break;
            case "ListAsEnumerable":source.AppendLine("public IEnumerable<int> Values(string tag){this.Trace.Add(\"F:\"+tag);return new List<int>(this.Data);}");break;
            default:throw new ArgumentOutOfRangeException(nameof(shape));
        }
        string H(string tag,string value){
            var hit="this.Hit("+tag+","+value+")";
            if(anchored){hit+=".Anchor";}
            return hit;
        }
        var predicate=H("\"P\"","p")+"==this.Target";
        var inner=H("\"I\"+p","q")+"==p";
        var outer=H("\"O\"","p")+"==p&&";
        string[] expressions=[
            "this.Values(\"O\").Last(p=>"+predicate+")",
            "this.Values(\"O\").LastOrDefault(p=>"+predicate+")",
            "this.Values(\"O\").Last(p=>"+outer+"this.Values(\"I\"+p).Last(q=>"+inner+")==p)",
            "this.Values(\"O\").LastOrDefault(p=>"+outer+"this.Values(\"I\"+p).LastOrDefault(q=>"+inner+")==p)",
            "this.Values(\"O\").Where(p=>"+predicate+").Last()",
            "this.Values(\"O\").Where(p=>"+predicate+").LastOrDefault()",
            "this.Values(\"O\").Where(p=>"+predicate+").Select(p=>"+H("\"T\"","p")+").Last()",
            "this.Values(\"O\").Last(p=>"+outer+"this.Values(\"I\"+p).Last(q=>"+H("\"I\"+p","q")+"==this.Target)==p)",
            "this.Values(\"O\").LastOrDefault(p=>"+outer+"this.Values(\"I\"+p).LastOrDefault(q=>"+H("\"I\"+p","q")+"==this.Target)==p)"
        ];
        source.AppendLine("public static object[] Run(int mode,int id,int[] data,int target,int failure){var probe=new Probe{Data=data,Target=target,Failure=failure};using var scope=LinqCounterScope.Start();string value=\"\",error=\"\",message=\"\";try{int result=0;switch(mode){");
        for(var mode=0;mode<4;mode++){
            source.Append("case ").Append(mode).AppendLine(":switch(id){");
            for(var id=0;id<expressions.Length;id++){
                source.Append("case ").Append(id).Append(":result=probe.Query").Append(mode).Append('_').Append(id).AppendLine("();break;");
            }
            source.AppendLine("}break;");
        }
        source.AppendLine("}value=result.ToString(System.Globalization.CultureInfo.InvariantCulture);}catch(Exception exception){error=exception.GetType().FullName!;message=exception.Message;}return new object[]{value,error,message,probe.Trace.ToArray(),scope.Counter.LogicalDelegate,scope.Counter.PhysicalDelegate,scope.Counter.EnumerableFallback};}");
        for(var mode=0;mode<4;mode++){
            for(var id=0;id<expressions.Length;id++){
                if(mode==0){source.AppendLine("[LinqIntercept(true,LinqInstrumentation.Counter)]");}
                else if(mode==1){source.AppendLine("[LinqIntercept(true,LinqInstrumentation.Counter,Optimizations=LinqOptimizations.None)]");}
                else if(mode==2){source.AppendLine("[LinqIntercept(false,LinqInstrumentation.Counter)]");}
                else{source.AppendLine("[LinqIntercept(false)]");}
                source.Append("private int Query").Append(mode).Append('_').Append(id).Append("()=>").Append(expressions[id]).AppendLine(";");
            }
        }
        return source.AppendLine("}").ToString();
    }

    private static List<string> Compare(object[] expected,object[] actual,bool exactTrace){
        var differences=new List<string>();
        string[] columns=["Value","ExceptionType","ExceptionMessage"];
        for(var index=0;index<columns.Length;index++){
            if(!Equals(expected[index],actual[index])){differences.Add(columns[index]+": expected="+expected[index]+", actual="+actual[index]);}
        }
        var oracleTrace=(string[])expected[3];
        var actualTrace=(string[])actual[3];
        if(exactTrace){
            if(!oracleTrace.SequenceEqual(actualTrace)){differences.Add("Trace: expected="+string.Join(",",oracleTrace)+", actual="+string.Join(",",actualTrace));}
        }else{
            var remaining=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var token in oracleTrace){remaining.TryGetValue(token,out var count);remaining[token]=count+1;}
            foreach(var token in actualTrace){
                if(!remaining.TryGetValue(token,out var count)||count==0){differences.Add("EvaluationUpperBound: "+token);}
                else{remaining[token]=count-1;}
            }
        }
        // F は source expression 評価そのもの。各 root source を一度だけ取得する。
        if(actualTrace.Count(token=>token=="F:O")!=1){differences.Add("SourceExpressionOnce: outer factory must run once");}
        if((long)actual[6]!=0){differences.Add("EnumerableFallback: "+actual[6]);}
        // counter は観測として保存するだけ。throw 前後の加算時機は意味判定に混ぜない。
        return differences;
    }

    private static object Snapshot(object[] value)=>new{Value=value[0],ExceptionType=value[1],ExceptionMessage=value[2],Trace=value[3],LogicalDelegate=value[4],PhysicalDelegate=value[5],EnumerableFallback=value[6]};

    private static (CSharpCompilation Compilation,string Generated) Generate(string source,string shape,bool anchored){
        var options=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([
            new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")
        ]);
        var compilation=GeneratorTestHelpers.CreateCompilation([("LastOracle.cs",source)]).WithAssemblyName("LastOracle"+shape+anchored);
        compilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),CSharpSyntaxTree.ParseText(source,options,path:"LastOracle.cs"));
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:options);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);
        var allDiagnostics=diagnostics.Concat(output.GetDiagnostics()).ToArray();
        var generated=string.Concat(output.SyntaxTrees.Skip(1).Select(tree=>tree.ToString()));
        var errors=allDiagnostics.Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(diagnostic=>diagnostic.ToString()).ToList();
        // 合計数だけでは不足。各対象 query の差し替えを個別確認する。
        for(var mode=0;mode<3;mode++){
            for(var id=0;id<Queries.Length;id++){
                var prefix="OptimizedLinq_";
                if(mode==2){prefix="MeasuredLinq_";}
                if(!generated.Contains(prefix+"Query"+mode+"_"+id+"_",StringComparison.Ordinal)){errors.Add("InterceptionMiss: Query"+mode+"_"+id);}
            }
        }
        Save("generation-"+shape+"-"+anchored,source,generated,allDiagnostics.Select(diagnostic=>(object)new{diagnostic.Id,Severity=diagnostic.Severity.ToString(),Message=diagnostic.ToString()}).ToList(),errors);
        Assert.Empty(errors);
        return ((CSharpCompilation)output,generated);
    }

    private static void Save(string stage,string source,string generated,List<object> observations,List<string> failures){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_LAST_TRAVERSAL_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(root)){return;}
        var directory=Path.Combine(Path.GetFullPath(root),stage);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"input.cs"),source);
        File.WriteAllText(Path.Combine(directory,"generated.cs"),generated);
        File.WriteAllText(Path.Combine(directory,"observations.json"),JsonSerializer.Serialize(new{Stage=stage,Observations=observations,Failures=failures,CounterExceptionTiming="observed only; no acceptance comparison"},new JsonSerializerOptions{WriteIndented=true}));
        static string Hash(string text)=>Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        File.WriteAllText(Path.Combine(directory,"hashes.json"),JsonSerializer.Serialize(new{SourceSHA256=Hash(source),GeneratedSHA256=Hash(generated),GeneratorSHA256=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location))),Runtime=Environment.Version.ToString(),SystemLinqAssembly=typeof(global::System.Linq.Enumerable).Assembly.FullName}));
    }
}
