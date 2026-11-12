using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.FLinq;

namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class CounterOnlyMethodBodyRewriter{
    private const string Contract="visible-query-lambdas-entry-total-v2";
    private const string OnExceptionTiming="entry; includes throwing calls; excludes unentered callbacks";
    private const string PreviousCounterDefinition="visible-query-lambdas-total-v1";
    private const string PreviousOnExceptionTiming="legacy path dependent; not unified";
    private static readonly string[] Modes=["All","None","CounterOnly","System"];
    private static readonly string[] Names=["All","Count","First","FirstOrDefault","Any","Last","Average"];
    private static readonly string[] Types=["bool","int","int","int","bool","int","double"];
    private static readonly string[] Queries=[
        "Values().All(p=>Values().All(q=>q>0))",
        "Values().Count(p=>Values().Count(q=>q==p)>0)",
        "Values().First(p=>Values().First(q=>q==p)==p)",
        "Values().FirstOrDefault(p=>Values().FirstOrDefault(q=>q==p)==p)",
        "Values().Any(p=>Values().Any(q=>q==p))",
        "Values().Last(p=>Values().Last(q=>q==p)==p)",
        "Values().Average(p=>Values().Average(q=>q+p))"
    ];

    [Fact]
    public void NestedPure(){
        var source=BuildMatrix(false);
        var (compilation,generated)=GenerateObserved(source,"NestedCounterPure",21);
        var observations=new List<object>();
        var failures=new List<string>();
        ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>{
            var run=probe.GetMethod("Run")!;
            // 独立算出: All/Average=4+4*4。First/FirstOrDefault/Any=1+1。
            // Count Allだけ既存Count>0→Anyにより4+(1+2+3+4)。None/CounterOnly=4+4*4。
            // Lastはarray末尾の4が外側/内側とも直ちに合格するため全modeで1+1。
            long[][] logical=[[20,14,2,2,2,2,20],[20,20,2,2,2,2,20],[20,20,2,2,2,2,20]];
            for(var id=0;id<Queries.Length;id++){
                var reference=(object[])run.Invoke(null,[3,id,0])!;
                for(var mode=0;mode<3;mode++){
                    var actual=(object[])run.Invoke(null,[mode,id,0])!;
                    var expectedPhysical=0L;
                    if(mode==2){expectedPhysical=logical[mode][id];}
                    observations.Add(new{Case=Names[id],Mode=Modes[mode],ExpectedLogical=logical[mode][id],ExpectedPhysical=expectedPhysical,Actual=Snapshot(actual),Reference=Snapshot(reference)});
                    CheckMeaning(Names[id],mode,reference,actual,failures);
                    if((long)actual[4]!=logical[mode][id]||(long)actual[5]!=expectedPhysical){
                        failures.Add($"{Names[id]}/{mode}: expected {logical[mode][id]}/{expectedPhysical}, actual {actual[4]}/{actual[5]}");
                    }
                }
            }
            return true;
        });
        Save("pure",source,generated,observations,failures);
        Assert.Empty(failures);
    }

    private static string[] BoundaryQueries()=>[
            "this.Values().All(p=>this.Values().All(q=>q>0))",
            "this.Values().Select(p=>new{Value=p}).All(p=>this.Values().All(q=>q>0))",
            "this.Values().All(p=>p>0?this.Values().All(q=>q>0):this.Values().Any(q=>this.Fail(q)>0))",
            "this.Values().Count(p=>this.Values().Count(q=>q==p)>0)>0",
            "this.Values().All(p=>{if(p==2){return false;}return this.Values().All(q=>q>0);})",
            "this.Values().Select(p=>new{Value=p}).All(p=>{if(p.Value==2){return false;}return this.Values().All(q=>q>0);})",
            "this.Values().All(p=>{bool Inner()=>this.Values().All(q=>q>0);if(p==2){return true;}return Inner();})",
            "this.Values().All(this.Opaque)",
            "this.Values().All(p=>this.Hit(\"O\",p)>0)",
            "this.Values().All(p=>this.Values().All(q=>this.Hit(\"I\",q)>0))",
            "this.Values().Select(p=>new{Value=p}).All(p=>{bool Inner()=>this.Values().All(q=>q>0);return Inner();})",
            "this.Values().All(delegate(int p){return this.Values().All(q=>q>0);})",
            "this.Values().All(p=>this.Invoke(()=>this.Values().All(q=>q>0)))",
            "this.Values().All((Func<int,bool>)(p=>this.Values().All(q=>q>0)))",
            "this.Values().Select(p=>new{Value=p}).All(p=>{return this.Values().All(q=>this.Hit(\"I\",q)>0);})",
            "this.Values().Select(p=>new{Value=p}).All(p=>{try{return this.Values().All(q=>q>0);}finally{this.Fail(p.Value);}})",
            "this.Values().All(p=>{try{return this.Values().All(q=>q>0);}finally{this.Fail(p);}})",
            "this.Values().All(p=>{bool Int(int[] values){return values.All(x=>x>0);}bool Long(long[] values){return values.All(x=>x>0);}return Int(new[]{p})&&Long(new[]{(long)p});})",
            "this.Values().All(p=>this.Skip(()=>this.Values().All(q=>q>0)))",
            "this.Values().All(p=>this.Twice(()=>this.Values().All(q=>q>0)))",
            "this.Values().All((PredicateAlias)(p=>this.Values().All(q=>q>0)))"
        ];

    // boundary12: 可視query qはouter4+inner16=20、非query callback自身は0。
    // boundary18/19: callback未呼出しならinner0、2回ずつ呼べばouter4+inner32=36。
    private static long[] BoundaryCounts()=>[20,24,20,20,6,8,16,4,2,3,24,20,20,20,4,6,5,12,4,36,20];

    [Fact]
    public void CounterOnlyBoundaries(){
        var queries=BoundaryQueries();
        // block early-false: outer2+inner4。anonymous版はSelect2も加算。
        // local functionを通らないearly-true: outer4+inner3*4。opaque実装の内側は対象外。
        var counts=BoundaryCounts();
        var source=BuildProbe(queries,global::System.Linq.Enumerable.Repeat("bool",queries.Length).ToArray(),false,true,true);
        var (compilation,generated)=GenerateObserved(source,"NestedCounterBoundaries",queries.Length);
        var observations=new List<object>();
        var failures=new List<string>();
        ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>{
            var run=probe.GetMethod("Run")!;
            for(var id=0;id<queries.Length;id++){
                var failure=0;
                if(id==8){failure=1;}
                else if(id is 9 or 14){failure=2;}
                var reference=(object[])run.Invoke(null,[3,id,failure])!;
                var actual=(object[])run.Invoke(null,[2,id,failure])!;
                observations.Add(new{Case=id,Mode=Modes[2],Query=queries[id],ExpectedLogical=counts[id],ExpectedPhysical=counts[id],Actual=Snapshot(actual),Reference=Snapshot(reference)});
                CheckMeaning("boundary"+id,2,reference,actual,failures);
                if((long)actual[4]!=counts[id]||(long)actual[5]!=counts[id]){
                    failures.Add($"boundary{id}: expected {counts[id]}/{counts[id]}, actual {actual[4]}/{actual[5]}");
                }
            }
            return true;
        });
        Save("boundaries",source,generated,observations,failures);
        Assert.Empty(failures);
    }

    [Fact]
    public void CrossModeBoundaries(){
        var queries=BoundaryQueries();
        var counts=BoundaryCounts();
        var observations=new List<object>();
        var failures=new List<string>();
        for(var id=0;id<queries.Length;id++){
            for(var mode=0;mode<3;mode++){
                var source=BuildProbe([queries[id]],["bool"],false,true,false,mode);
                var generated=string.Empty;
                var cell=new List<object>();
                var cellFailures=new List<string>();
                var stage="CompileUnsupported";
                try{
                    var result=GenerateObserved(source,$"NestedCounterBoundary{id}Mode{mode}",1);
                    generated=result.Generated;
                    stage="ExecutionInfrastructure";
                    ExecutableGeneration.Run(result.Compilation,"Sample.Probe",probe=>{
                        var run=probe.GetMethod("Run")!;
                        var failure=0;
                        if(id==8){failure=1;}
                        else if(id is 9 or 14){failure=2;}
                        var reference=(object[])run.Invoke(null,[3,0,failure])!;
                        var actual=(object[])run.Invoke(null,[mode,0,failure])!;
                        var meaning=new List<string>();
                        CheckMeaning("boundary"+id,mode,reference,actual,meaning,mode!=0);
                        var expected=counts[id];
                        // 外側の述語は入れ子の呼出しを含むため全件。純粋な内側だけAnyとなり4+(1+2+3+4)。
                        if(id==3&&mode==0){expected=14;}
                        var checkScope=true;
                        var scopeMismatch=checkScope&&(long)actual[4]!=expected;
                        if(scopeMismatch){cellFailures.Add($"ScopeMismatch boundary{id}/{Modes[mode]}: expected {expected}, actual {actual[4]}");}
                        if(mode==2&&(long)actual[5]!=expected){cellFailures.Add($"PhysicalMismatch boundary{id}/{Modes[mode]}: expected {expected}, actual {actual[5]}");}
                        foreach(var difference in meaning){cellFailures.Add("MeaningMismatch "+difference);}
                        cell.Add(new{Case=id,Mode=Modes[mode],Query=queries[id],Compile="Supported",ExpectedLogical=expected,ScopeCheck=checkScope,OnExceptionTiming,TracePolicy=TracePolicy(mode!=0),ScopeMismatch=scopeMismatch,MeaningDifferences=meaning,Actual=Snapshot(actual),Reference=Snapshot(reference)});
                        return true;
                    });
                }catch(Exception exception){
                    cellFailures.Add($"{stage} boundary{id}/{Modes[mode]}: {exception.GetType().Name}: {exception.Message}");
                    cell.Add(new{Case=id,Mode=Modes[mode],Query=queries[id],Status=stage,ExceptionType=exception.GetType().FullName,exception.Message});
                }
                observations.AddRange(cell);
                failures.AddRange(cellFailures);
                Save($"cross-mode-{id}-{Modes[mode]}",source,generated,cell,cellFailures);
            }
        }
        Save("cross-mode-summary",string.Join(Environment.NewLine,queries),string.Empty,observations,failures);
        Assert.Empty(failures);
    }

    [Fact]
    public void NestedMeaning()=>RunMeaning(false);

    [Fact]
    public void NestedAnchoredMeaning()=>RunMeaning(true);

    private static void RunMeaning(bool anchored){
        // counter scopeの受入と、値/例外・上限・Anchor付き厳密traceを分離する。
        var source=BuildMatrix(true,anchored);
        var name="NestedCounterMeaning";
        var stage="meaning";
        if(anchored){name+="Anchored";stage+="-anchored";}
        var (compilation,generated)=GenerateObserved(source,name,21);
        var observations=new List<object>();
        var failures=new List<string>();
        ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>{
            var run=probe.GetMethod("Run")!;
            for(var id=0;id<Queries.Length;id++){
                foreach(var failure in new[]{0,1,2}){
                    var reference=(object[])run.Invoke(null,[3,id,failure])!;
                    for(var mode=0;mode<3;mode++){
                        var actual=(object[])run.Invoke(null,[mode,id,failure])!;
                        CheckMeaning(Names[id]+"/failure"+failure,mode,reference,actual,failures,anchored||mode!=0);
                        // O/Iは各callback本体の先頭。AverageだけOがinner内なので、各yield直後の入口をSで数える。
                        var expectedLogical=TraceEntryCount((string)actual[3],id==6);
                        var expectedPhysical=0L;
                        if(mode==2){expectedPhysical=expectedLogical;}
                        observations.Add(new{Case=Names[id],Mode=Modes[mode],Failure=failure,Anchored=anchored,TracePolicy=TracePolicy(anchored||mode!=0),ExpectedLogical=expectedLogical,ExpectedPhysical=expectedPhysical,Actual=Snapshot(actual),Reference=Snapshot(reference)});
                        if((long)actual[4]!=expectedLogical||(long)actual[5]!=expectedPhysical){
                            failures.Add($"{Names[id]}/{Modes[mode]}/failure{failure}: entry expected {expectedLogical}/{expectedPhysical}, actual {actual[4]}/{actual[5]}");
                        }
                    }
                }
            }
            return true;
        });
        Save(stage,source,generated,observations,failures);
        Assert.Empty(failures);
    }

    [Fact]
    public void CastOuterTypeParameter(){
        const string source="""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Entry{public static object[] Run(int mode)=>GenericProbe<int>.Run(mode);}
            public sealed class GenericProbe<T>{
                public static object[] Run(int mode){
                    using var scope=LinqCounterScope.Start();var probe=new GenericProbe<T>();bool value;
                    if(mode==0){value=probe.QueryAll();}
                    else if(mode==1){value=probe.QueryNone();}
                    else if(mode==2){value=probe.QueryCounter();}
                    else{value=probe.QuerySystem();}
                    return new object[]{value.ToString(),"","","",scope.Counter.LogicalDelegate,scope.Counter.PhysicalDelegate,scope.Counter.EnumerableFallback};
                }
                [LinqIntercept(true,LinqInstrumentation.Counter)]private bool QueryAll()=>new[]{default(T)!}.All((Func<T,bool>)(p=>new[]{1,2}.All(q=>q>0)));
                [LinqIntercept(true,LinqInstrumentation.Counter,Optimizations=LinqOptimizations.None)]private bool QueryNone()=>new[]{default(T)!}.All((Func<T,bool>)(p=>new[]{1,2}.All(q=>q>0)));
                [LinqIntercept(false,LinqInstrumentation.Counter)]private bool QueryCounter()=>new[]{default(T)!}.All((Func<T,bool>)(p=>new[]{1,2}.All(q=>q>0)));
                [LinqIntercept(false)]private bool QuerySystem()=>new[]{default(T)!}.All((Func<T,bool>)(p=>new[]{1,2}.All(q=>q>0)));
            }
            """;
        var (compilation,generated)=GenerateObserved(source,"NestedCounterGenericCast",3);
        var observations=new List<object>();
        var failures=new List<string>();
        ExecutableGeneration.Run(compilation,"Sample.Entry",entry=>{
            var run=entry.GetMethod("Run")!;
            var reference=(object[])run.Invoke(null,[3])!;
            for(var mode=0;mode<3;mode++){
                var actual=(object[])run.Invoke(null,[mode])!;
                CheckMeaning("generic-cast",mode,reference,actual,failures);
                var physical=0L;
                if(mode==2){physical=3;}
                if((long)actual[4]!=3L||(long)actual[5]!=physical){failures.Add($"generic-cast/{Modes[mode]} expected3/{physical}, actual {actual[4]}/{actual[5]}");}
                observations.Add(new{Mode=Modes[mode],ExpectedLogical=3,ExpectedPhysical=physical,Actual=Snapshot(actual),Reference=Snapshot(reference)});
            }
            return true;
        });
        Save("generic-cast",source,generated,observations,failures);
        Assert.Empty(failures);
    }

    private static long TraceEntryCount(string trace,bool average){
        var count=0L;
        foreach(var token in trace.Split(',',StringSplitOptions.RemoveEmptyEntries)){
            if(average){
                if(token.StartsWith("S",StringComparison.Ordinal)){count++;}
            }else if(token.StartsWith("O",StringComparison.Ordinal)||token.StartsWith("I",StringComparison.Ordinal)){
                count++;
            }
        }
        return count;
    }

    private static string BuildMatrix(bool traced,bool anchored=false){
        if(!traced){return BuildProbe(Queries,Types,false,false);}
        string[] queries=[
            "this.Values().All(p=>this.Hit(\"O\",p)>0&&this.Values().All(q=>this.Hit(\"I\",q)>0))",
            "this.Values().Count(p=>this.Hit(\"O\",p)>0&&this.Values().Count(q=>this.Hit(\"I\",q)==p)>0)",
            "this.Values().First(p=>this.Hit(\"O\",p)>0&&this.Values().First(q=>this.Hit(\"I\",q)==p)==p)",
            "this.Values().FirstOrDefault(p=>this.Hit(\"O\",p)>0&&this.Values().FirstOrDefault(q=>this.Hit(\"I\",q)==p)==p)",
            "this.Values().Any(p=>this.Hit(\"O\",p)>0&&this.Values().Any(q=>this.Hit(\"I\",q)==p))",
            "this.Values().Last(p=>this.Hit(\"O\",p)>0&&this.Values().Last(q=>this.Hit(\"I\",q)==p)==p)",
            "this.Values().Average(p=>this.Values().Average(q=>this.Hit(\"I\",q)+this.Hit(\"O\",p)))"
        ];
        if(anchored){
            for(var index=0;index<queries.Length;index++){
                queries[index]=queries[index].Replace("this.Hit(\"O\",p)","this.Hit(\"O\",p).Anchor",StringComparison.Ordinal)
                    .Replace("this.Hit(\"I\",q)","this.Hit(\"I\",q).Anchor",StringComparison.Ordinal);
            }
        }
        return BuildProbe(queries,Types,true,true);
    }

    private static string BuildProbe(string[] queries,string[] types,bool enumerable,bool trace,bool counterOnly=false,int? onlyMode=null){
        var source=new StringBuilder("""
            using System;
            using System.Collections.Generic;
            using System.Globalization;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            using PredicateAlias=System.Func<int,bool>;
            namespace Sample;
            public sealed class Probe{
                public readonly List<string> Trace=new();
                public int Failure;
                public int Hit(string name,int value){
                    this.Trace.Add(name+value);
                    if((this.Failure==1&&name=="O"&&value==2)||(this.Failure==2&&name=="I"&&value==2)){
                        throw new InvalidOperationException(name+value);
                    }
                    return value;
                }
                public int Fail(int value)=>throw new InvalidOperationException("unselected");
                public bool Invoke(Func<bool> callback)=>callback();
                public bool Skip(Func<bool> callback)=>true;
                public bool Twice(Func<bool> callback)=>callback()&&callback();
                public Func<int,bool> Opaque=>p=>this.Values().All(q=>q>0);
            """);
        if(enumerable){source.AppendLine("public IEnumerable<int> Values(){for(var i=1;i<=4;i++){this.Trace.Add(\"S\"+i);yield return i;}}");}
        else if(trace){source.AppendLine("public int[] Values(){this.Trace.Add(\"S\");return new[]{1,2,3,4};}");}
        else{source.AppendLine("public int[] Values()=>new[]{1,2,3,4};");}
        source.AppendLine("public static object[] Run(int mode,int id,int failure){var probe=new Probe();probe.Failure=failure;using var scope=LinqCounterScope.Start();string value=\"\",error=\"\",message=\"\";try{object result=0;");
        var firstMode=0;
        if(counterOnly){firstMode=2;}
        var modes=new List<int>();
        for(var mode=firstMode;mode<4;mode++){
            if(onlyMode is null||mode==onlyMode||mode==3){modes.Add(mode);}
        }
        foreach(var mode in modes){
            source.Append("if(mode==").Append(mode).AppendLine("){switch(id){");
            for(var id=0;id<queries.Length;id++){
                source.Append("case ").Append(id).Append(":result=probe.Query").Append(mode).Append('_').Append(id).AppendLine("();break;");
            }
            source.AppendLine("}}");
        }
        source.AppendLine("value=Convert.ToString(result,CultureInfo.InvariantCulture)!;}catch(Exception exception){error=exception.GetType().FullName!;message=exception.Message;}return new object[]{value,error,message,string.Join(\",\",probe.Trace),scope.Counter.LogicalDelegate,scope.Counter.PhysicalDelegate,scope.Counter.EnumerableFallback};}");
        foreach(var mode in modes){
            for(var id=0;id<queries.Length;id++){
                if(mode==0){source.AppendLine("[LinqIntercept(true,LinqInstrumentation.Counter)]");}
                else if(mode==1){source.AppendLine("[LinqIntercept(true,LinqInstrumentation.Counter,Optimizations=LinqOptimizations.None)]");}
                else if(mode==2){source.AppendLine("[LinqIntercept(false,LinqInstrumentation.Counter)]");}
                else{source.AppendLine("[LinqIntercept(false)]");}
                source.Append("private ").Append(types[id]).Append(" Query").Append(mode).Append('_').Append(id).Append("()=>").Append(queries[id]).AppendLine(";");
            }
        }
        return source.AppendLine("}").ToString();
    }

    private static void CheckMeaning(string name,int mode,object[] expected,object[] actual,List<string> failures,bool exactTrace=true){
        for(var column=0;column<3;column++){
            if(!Equals(expected[column],actual[column])){
                failures.Add($"{name}/{mode} column{column}: expected {expected[column]}, actual {actual[column]}");
            }
        }
        if(exactTrace){
            if(!Equals(expected[3],actual[3])){failures.Add($"{name}/{mode} trace: expected {expected[3]}, actual {actual[3]}");}
            return;
        }
        // Anchor無しAllは減少を許す。値/例外は上で厳密比較し、各callback/eventの上限は維持する。
        var remaining=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var token in ((string)expected[3]).Split(',',StringSplitOptions.RemoveEmptyEntries)){
            remaining.TryGetValue(token,out var count);remaining[token]=count+1;
        }
        foreach(var token in ((string)actual[3]).Split(',',StringSplitOptions.RemoveEmptyEntries)){
            if(!remaining.TryGetValue(token,out var count)||count==0){failures.Add($"{name}/{mode} EvaluationUpperBound: {token}; expected {expected[3]}, actual {actual[3]}");}
            else{remaining[token]=count-1;}
        }
    }

    private static object Snapshot(object[] value)=>new{Value=value[0],ExceptionType=value[1],ExceptionMessage=value[2],Trace=value[3],LogicalDelegate=value[4],PhysicalDelegate=value[5],EnumerableFallback=value[6]};

    private static string TracePolicy(bool exact){
        if(exact){return "Exact";}
        return "EvaluationUpperBound";
    }

    private static (CSharpCompilation Compilation,string Generated) GenerateObserved(string source,string assemblyName,int minimumInterceptions){
        var options=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([
            new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")
        ]);
        var compilation=GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]).WithAssemblyName(assemblyName);
        compilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),CSharpSyntaxTree.ParseText(source,options,path:"Container.cs"));
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new global::Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:options);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var generatorDiagnostics);
        var diagnostics=generatorDiagnostics.Concat(output.GetDiagnostics()).ToArray();
        var errors=diagnostics.Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(diagnostic=>diagnostic.ToString()).ToList();
        var generated=string.Concat(output.SyntaxTrees.Skip(1).Select(tree=>tree.ToString()));
        var records=diagnostics.Select(diagnostic=>(object)new{diagnostic.Id,Severity=diagnostic.Severity.ToString(),Message=diagnostic.GetMessage(),Location=diagnostic.Location.ToString(),Full=diagnostic.ToString()}).ToList();
        Save("generation-"+assemblyName,source,generated,records,errors);
        Assert.Empty(errors);
        Assert.True(generated.Split("InterceptsLocationAttribute(1,").Length-1>=minimumInterceptions,"LINQ の呼び出しが差し替えられていません。");
        return ((CSharpCompilation)output,generated);
    }

    private static void Save(string stage,string source,string generated,List<object> observations,List<string> failures){
        var target=Environment.GetEnvironmentVariable("SEAMLESS_NESTED_COUNTER_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(target)){return;}
        var sourceHash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        var generatedHash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(generated)));
        var directory=Path.Combine(Path.GetFullPath(target),stage,sourceHash);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"input.cs"),source);
        File.WriteAllText(Path.Combine(directory,"generated.cs"),generated);
        File.WriteAllText(Path.Combine(directory,"observations.json"),JsonSerializer.Serialize(new{Contract,OnExceptionTiming,PreviousCounterDefinition,PreviousOnExceptionTiming,CounterDefinition=Contract,Stage=stage,Observations=observations,Failures=failures},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(directory,"hashes.json"),JsonSerializer.Serialize(new{Contract,OnExceptionTiming,PreviousCounterDefinition,PreviousOnExceptionTiming,CounterDefinition=Contract,SourceSHA256=sourceHash,GeneratedSHA256=generatedHash,LoadedRuntimeSHA256=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(global::Seamless.Linq.LinqCounter).Assembly.Location))),LoadedGeneratorSHA256=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location)))}));
    }
}
