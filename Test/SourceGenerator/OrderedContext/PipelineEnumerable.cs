using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Seamless.FLinq;
using Seamless.FLinq.Generator;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Test.SourceGenerator.OrderedContext;

/// <summary>連続Orderのprefixが持つgeneric/capture/scalar subqueryの描画と実計測を独立に検証する。</summary>
[LinqIntercept(false)]
public sealed class PipelineEnumerable{
    private const string PrivateHeader="""
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Text.Json;
        using Seamless.FLinq;
        using Seamless.Linq;
        namespace Sample;
        [LinqIntercept(false)]public sealed class State{
            public int Failure;
            public int Reads;
            public int PrimaryCalls;
            public int WhereCalls;
            public int SelectCalls;
            public int SecondaryCalls;
            public readonly List<string> Trace=new();
        }
        [LinqIntercept(false)]public sealed class Entry{
            public static string Run(bool optimized,int consumer,int[] values,int failure,string captured,string marker,int bias){
                var state=new State{Failure=failure};
                var probe=new Probe<string>(state,marker,bias);
                string result=null;
                string exceptionType=null;
                string exceptionMessage=null;
                using var scope=LinqCounterScope.Start();
                try{
                    if(optimized){result=probe.Optimized(consumer,values,captured);}
                    else{result=probe.Reference(consumer,values,captured);}
                }catch(Exception exception){exceptionType=exception.GetType().Name;exceptionMessage=exception.Message;}
                return JsonSerializer.Serialize(new{
                    Result=result,ExceptionType=exceptionType,ExceptionMessage=exceptionMessage,
                    state.Reads,state.PrimaryCalls,state.WhereCalls,state.SelectCalls,state.SecondaryCalls,
                    Trace=state.Trace.ToArray(),scope.Counter.LogicalDelegate,scope.Counter.PhysicalDelegate,
                    scope.Counter.LogicalWhere,scope.Counter.LogicalSelect,scope.Counter.LogicalOperations,
                    scope.Counter.EnumerableFallback
                });
            }
        }
        [LinqIntercept(false)]public sealed class Probe<T>{
            private readonly State state;
            private readonly T marker;
            private readonly int bias;
            public Probe(State state,T marker,int bias){this.state=state;this.marker=marker;this.bias=bias;}
            private int[] Read(int[] values){this.state.Reads++;this.state.Trace.Add("R");return values;}
            private T[] Tokens(int value,T captured){
                this.state.PrimaryCalls++;this.state.Trace.Add("K:"+value);
                return new[]{captured,this.marker};
            }
            private bool Matches(T token,T captured){
                this.state.WhereCalls++;this.state.Trace.Add("W:"+token);
                return EqualityComparer<T>.Default.Equals(token,captured);
            }
            private int Score(int value,T token,T captured){
                this.state.SelectCalls++;this.state.Trace.Add("V:"+value+":"+token+":"+captured);
                if(this.state.Failure==1&&value==2){throw new InvalidOperationException("nested");}
                return value*this.bias;
            }
            private int Secondary(int value,T captured){
                this.state.SecondaryCalls++;this.state.Trace.Add("S:"+value+":"+captured);
                if(this.state.Failure==2&&value==2){throw new InvalidOperationException("outer");}
                return value&1;
            }
        """;

    // 旧Headerは負対照に保持し、正のケースはfixture内のgenericメンバーの可視性のみを変える。
    private static readonly string PublicHeader=PrivateHeader
        .Replace("private readonly State state;","public readonly State state;",StringComparison.Ordinal)
        .Replace("private readonly T marker;","public readonly T marker;",StringComparison.Ordinal)
        .Replace("private readonly int bias;","public readonly int bias;",StringComparison.Ordinal)
        .Replace("private int[] Read(","public int[] Read(",StringComparison.Ordinal)
        .Replace("private T[] Tokens(","public T[] Tokens(",StringComparison.Ordinal)
        .Replace("private bool Matches(","public bool Matches(",StringComparison.Ordinal)
        .Replace("private int Score(","public int Score(",StringComparison.Ordinal)
        .Replace("private int Secondary(","public int Secondary(",StringComparison.Ordinal);

    private const string Query="""
            [LinqIntercept(__OPTIMIZED__,__INSTRUMENTATION__)]
            public string __NAME__(int consumer,int[] values,T captured){
                var query=this.Read(values).OrderBy(value=>this.Tokens(value,captured)/*ANCHOR*/
                    .Where(token=>this.Matches(token,captured)/*ANCHOR*/)
                    .Select(token=>this.Score(value,token,captured)/*ANCHOR*/).Sum()/*ANCHOR*/)
                    .OrderBy(value=>this.Secondary(value,captured)/*ANCHOR*/);
                values=Array.Empty<int>();
                this.state.Trace.Add("D");
                if(consumer==0){return string.Join(",",query.ToArray());}
                if(consumer==1){return string.Join(",",query.ToList());}
                if(consumer==2){return string.Join(",",query);}
                if(consumer==3){return query.FirstOrDefault().ToString();}
                var first=query.FirstOrDefault();
                var materialized=query.ToArray();
                var seen=new List<int>();
                foreach(var value in query){seen.Add(value);}
                return first+"/"+string.Join(",",materialized)+"/"+string.Join(",",seen);
            }
        """;

    private static readonly int[][] Shapes=[[],[1],[2],[2,1],[1,1],[3,1,2]];
    private static readonly (string Captured,string Marker,int Bias)[] Captures=[("same","same",5),("capture","other",-3)];

    private sealed record Case(int Consumer,int[] Values,int Failure,string Captured,string Marker,int Bias);
    private sealed record Bundle(CSharpCompilation Compilation,string Source,string Generated,byte[] Assembly);
    private sealed record Expected(string? Result,string? ExceptionType,string? ExceptionMessage,int Primary,int Where,int Select,int Secondary){
        public long LogicalDelegate=>(long)this.Primary+this.Where+this.Select+this.Secondary;
        public long LogicalOperations=>(long)this.Where+this.Select;
    }
    private sealed class Observation{
        public Observation(){}
        public string? Result{get;set;}
        public string? ExceptionType{get;set;}
        public string? ExceptionMessage{get;set;}
        public int Reads{get;set;}
        public int PrimaryCalls{get;set;}
        public int WhereCalls{get;set;}
        public int SelectCalls{get;set;}
        public int SecondaryCalls{get;set;}
        public string[] Trace{get;set;}=[];
        public long LogicalDelegate{get;set;}
        public long PhysicalDelegate{get;set;}
        public long LogicalWhere{get;set;}
        public long LogicalSelect{get;set;}
        public long LogicalOperations{get;set;}
        public long EnumerableFallback{get;set;}
    }

    [Fact]
    public void PrivateGenericUnsupportedControl(){
        // 生成・emitと既知の拒否だけを確認し、runtime比較には含めない。
        _=Generate(false,"private-generic-control",privateControl:true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedCompilation(bool anchored){
        // Generate自身が診断とemit成否を保存してからassertする。実行結果との混同を避ける。
        _=Generate(anchored,"compilation");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Values(bool anchored){
        var bundle=Generate(anchored,"values");
        ObserveAll(bundle,anchored,"values",[0],(item,expected,reference,actual,failures)=>{
            Check(reference.Result==expected.Result,item,"System result versus independent model",failures);
            Check(actual.Result==reference.Result,item,"optimized result versus System",failures);
            Check(reference.ExceptionType is null&&actual.ExceptionType is null,item,"successful result without exception",failures);
            if(anchored){Check(reference.Trace.SequenceEqual(actual.Trace),item,"anchored callback order",failures);}
        });
    }

    [Fact]
    public void AnchoredExceptions(){
        var bundle=Generate(true,"exceptions");
        ObserveAll(bundle,true,"exceptions",[1,2],(item,expected,reference,actual,failures)=>{
            Check(reference.Result==expected.Result,item,"System partial outcome versus independent model",failures);
            Check(reference.ExceptionType==expected.ExceptionType&&reference.ExceptionMessage==expected.ExceptionMessage,item,"System exception versus independent model",failures);
            Check(actual.Result==reference.Result,item,"optimized partial outcome versus System",failures);
            Check(actual.ExceptionType==reference.ExceptionType&&actual.ExceptionMessage==reference.ExceptionMessage,item,"optimized exception versus System",failures);
            Check(reference.Trace.SequenceEqual(actual.Trace),item,"anchored callback order before exception",failures);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EvaluationAndInstrumentation(bool anchored){
        var bundle=Generate(anchored,"evaluation-counter");
        int[] injectedFailures=[0];
        if(anchored){injectedFailures=[0,1,2];}
        ObserveAll(bundle,anchored,"evaluation-counter",injectedFailures,(item,expected,reference,actual,failures)=>{
            Check(reference.Reads==1&&actual.Reads==1,item,"captured source evaluated once",failures);
            Check(reference.PrimaryCalls==expected.Primary&&reference.WhereCalls==expected.Where
                &&reference.SelectCalls==expected.Select&&reference.SecondaryCalls==expected.Secondary,item,"System callback quantities versus independent model",failures);
            if(anchored){
                Check(actual.PrimaryCalls==expected.Primary&&actual.WhereCalls==expected.Where
                    &&actual.SelectCalls==expected.Select&&actual.SecondaryCalls==expected.Secondary,item,"anchored callback quantities",failures);
                Check(actual.LogicalDelegate==expected.LogicalDelegate&&actual.LogicalWhere==expected.Where
                    &&actual.LogicalSelect==expected.Select&&actual.LogicalOperations==expected.LogicalOperations,item,"anchored logical quantities including nested scalar",failures);
            }else{
                // Plain式では合法な評価削減を許す。独立modelは上限、実callbackと計測の一致は別に検証する。
                Check(actual.PrimaryCalls<=expected.Primary&&actual.WhereCalls<=expected.Where
                    &&actual.SelectCalls<=expected.Select&&actual.SecondaryCalls<=expected.Secondary,item,"plain callback upper bounds",failures);
            }
            var observedDelegates=(long)actual.PrimaryCalls+actual.WhereCalls+actual.SelectCalls+actual.SecondaryCalls;
            Check(actual.LogicalDelegate==observedDelegates,item,"logical delegates versus observed physical callbacks",failures);
            Check(actual.LogicalWhere==actual.WhereCalls&&actual.LogicalSelect==actual.SelectCalls
                &&actual.LogicalOperations==(long)actual.WhereCalls+actual.SelectCalls,item,"nested operator instrumentation versus callbacks",failures);
            Check(actual.PhysicalDelegate==0&&actual.EnumerableFallback==0,item,"inline path without fallback",failures);
        });
    }

    private static IEnumerable<Case> Cases(int[] failures){
        foreach(var capture in Captures){
            foreach(var values in Shapes){
                for(var consumer=0;consumer<5;consumer++){
                    foreach(var failure in failures){yield return new(consumer,values,failure,capture.Captured,capture.Marker,capture.Bias);}
                }
            }
        }
    }

    // LINQ/生成コード/観測したCounterを使わず、consumerの需要から実行すべきcallback数と結果を算出する。
    private static Expected Predict(Case item){
        var primary=0;
        var where=0;
        var select=0;
        var secondary=0;
        string? exceptionType=null;
        string? exceptionMessage=null;
        var sorted=(int[])item.Values.Clone();
        Array.Sort(sorted);
        if(item.Bias<0){Array.Reverse(sorted);}
        var ordered=new List<int>();
        foreach(var value in sorted){if((value&1)==0){ordered.Add(value);}}
        foreach(var value in sorted){if((value&1)!=0){ordered.Add(value);}}
        bool Phase(int consumer){
            var evaluatePrimary=consumer==3||item.Values.Length>1;
            if(evaluatePrimary){
                foreach(var value in item.Values){
                    primary++;
                    // Tokensはcapturedとmarkerの2要素。先頭は必ず通過する。
                    for(var token=0;token<2;token++){
                        where++;
                        if(token==1&&item.Captured!=item.Marker){continue;}
                        select++;
                        if(item.Failure==1&&value==2){exceptionType="InvalidOperationException";exceptionMessage="nested";return false;}
                    }
                }
            }
            var evaluateSecondary=consumer is 2 or 3||item.Values.Length>1;
            if(evaluateSecondary){
                foreach(var value in sorted){
                    secondary++;
                    if(item.Failure==2&&value==2){exceptionType="InvalidOperationException";exceptionMessage="outer";return false;}
                }
            }
            return true;
        }
        string First(){if(ordered.Count==0){return "0";}return ordered[0].ToString(global::System.Globalization.CultureInfo.InvariantCulture);}
        string? result=null;
        if(item.Consumer==4){
            if(Phase(3)&&Phase(0)&&Phase(2)){result=First()+"/"+string.Join(",",ordered)+"/"+string.Join(",",ordered);}
        }else if(Phase(item.Consumer)){
            if(item.Consumer==3){result=First();}
            else{result=string.Join(",",ordered);}
        }
        return new(result,exceptionType,exceptionMessage,primary,where,select,secondary);
    }

    private static void ObserveAll(Bundle bundle,bool anchored,string gate,int[] injectedFailures,Action<Case,Expected,Observation,Observation,List<string>> validate){
        var context=new global::System.Runtime.Loader.AssemblyLoadContext("OrderedContext_"+gate,isCollectible:true);
        var records=new List<object>();
        var failures=new List<string>();
        try{
            using var stream=new MemoryStream(bundle.Assembly,writable:false);
            var entry=context.LoadFromStream(stream).GetType("Sample.Entry")!.GetMethod("Run")!;
            foreach(var item in Cases(injectedFailures)){
                var expected=Predict(item);
                var reference=Read(entry,item,false);
                var actual=Read(entry,item,true);
                var start=failures.Count;
                validate(item,expected,reference,actual,failures);
                records.Add(new{item,expected,reference,actual,failedChecks=failures.Skip(start).ToArray()});
            }
        }finally{context.Unload();}
        var expectedCases=Captures.Length*Shapes.Length*5*injectedFailures.Length;
        Save(anchored,gate,"observations.json",JsonSerializer.Serialize(new{gate,anchored,expectedCases,actualCases=records.Count,records,failures}));
        Assert.Equal(expectedCases,records.Count);
        Assert.True(failures.Count==0,string.Join("\n",failures));
    }

    private static Observation Read(global::System.Reflection.MethodInfo entry,Case item,bool optimized){
        var text=(string)entry.Invoke(null,[optimized,item.Consumer,item.Values,item.Failure,item.Captured,item.Marker,item.Bias])!;
        return JsonSerializer.Deserialize<Observation>(text)!;
    }

    private static void Check(bool condition,Case item,string check,List<string> failures){
        if(!condition){failures.Add($"consumer={item.Consumer};values={string.Join(",",item.Values)};failure={item.Failure};capture={item.Captured}/{item.Marker};bias={item.Bias};{check}");}
    }

    private static Bundle Generate(bool anchored,string gate,bool privateControl=false){
        var optimized=Query.Replace("__OPTIMIZED__","true",StringComparison.Ordinal)
            .Replace("__INSTRUMENTATION__","LinqInstrumentation.Counter",StringComparison.Ordinal)
            .Replace("__NAME__","Optimized",StringComparison.Ordinal);
        var reference=Query.Replace("__OPTIMIZED__","false",StringComparison.Ordinal)
            .Replace("__INSTRUMENTATION__","LinqInstrumentation.None",StringComparison.Ordinal)
            .Replace("__NAME__","Reference",StringComparison.Ordinal);
        var anchor=string.Empty;
        if(anchored){anchor=".Anchor";}
        var header=PublicHeader;
        if(privateControl){header=PrivateHeader;}
        var source=(header+"\n"+optimized+"\n"+reference+"\n}").Replace("/*ANCHOR*/",anchor,StringComparison.Ordinal);
        Save(anchored,gate,"input.cs",source);
        var parseOptions=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([
            new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")]);
        var compilation=global::Test.SourceGenerator.GeneratorTestHelpers.CreateCompilation([("Container.cs",source)])
            .WithAssemblyName("OrderedRenderContext_"+gate.Replace("-","",StringComparison.Ordinal)+"_"+anchored);
        compilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(),CSharpSyntaxTree.ParseText(source,parseOptions,path:"Container.cs"));
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:parseOptions,
            driverOptions:new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None,trackIncrementalGeneratorSteps:false));
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var generatorDiagnostics);
        var generated=string.Concat(output.SyntaxTrees.Skip(1).Select(static tree=>tree.ToString()));
        Save(anchored,gate,"generated.cs",generated);
        using var pe=new MemoryStream();
        var emit=output.Emit(pe);
        var diagnostics=generatorDiagnostics.Concat(output.GetDiagnostics()).Concat(emit.Diagnostics).Select(static diagnostic=>new{
            diagnostic.Id,Severity=diagnostic.Severity.ToString(),Text=diagnostic.ToString(),Source=diagnostic.Location.SourceTree?.FilePath
        }).ToArray();
        var errors=diagnostics.Where(static diagnostic=>diagnostic.Severity=="Error").ToArray();
        var intercepts=generated.Split("InterceptsLocationAttribute(1,",StringSplitOptions.None).Length-1;
        var fallbackMarkerPresent=generated.Contains("UnsupportedLinqExpansion(",StringComparison.Ordinal);
        Save(anchored,gate,"compilation.json",JsonSerializer.Serialize(new{anchored,gate,privateControl,runtimeComparisons=0,emitSucceeded=emit.Success,errorCount=errors.Length,intercepts,fallbackMarkerPresent,diagnostics,
            sourceSHA256=Hash(Encoding.UTF8.GetBytes(source)),generatedSHA256=Hash(Encoding.UTF8.GetBytes(generated)),assemblySHA256=Hash(pe.ToArray()),
            generator=AssemblyHash(typeof(FLinqIncrementalGenerator).Assembly),runtime=AssemblyHash(typeof(global::Seamless.Linq.GeneratedCodeHelpers).Assembly),
            tests=AssemblyHash(typeof(PipelineEnumerable).Assembly)}));
        Assert.True(errors.Length==0,string.Join("\n",errors.Select(static error=>error.Text)));
        Assert.True(emit.Success,"生成コードのemitに失敗しました。");
        Assert.True(intercepts>0,"Optimizedの呼び出しが差し替えられていません。");
        if(privateControl){
            Assert.Contains("UnsupportedLinqExpansion(\"Optimized\",\"Direct interception could not inline 'Optimized' inside 'Run'. UnsafeAccessor cannot resolve non-public members of a generic type on this runtime; skip interception and fall back to the original method.\")",generated,StringComparison.Ordinal);
        }else{
            Assert.DoesNotContain("UnsupportedLinqExpansion(",generated,StringComparison.Ordinal);
        }
        return new((CSharpCompilation)output,source,generated,pe.ToArray());
    }

    private static object AssemblyHash(global::System.Reflection.Assembly assembly)=>new{assembly.FullName,assembly.Location,SHA256=Hash(File.ReadAllBytes(assembly.Location))};
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static void Save(bool anchored,string gate,string name,string text){
        var target=Environment.GetEnvironmentVariable("SEAMLESS_ORDERED_CONTEXT_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(target)){return;}
        var mode="plain";
        if(anchored){mode="anchor";}
        var directory=Path.Combine(Path.GetFullPath(target),mode,gate);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,name),text);
    }
}
