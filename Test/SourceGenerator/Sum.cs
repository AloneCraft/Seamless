using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Seamless.FLinq;

namespace Test.SourceGenerator;

/// <summary>累積の例外、選択子の算術文脈、列挙順と解除を実生成した System/None/All で比較する。</summary>
[LinqIntercept(false)]
public sealed class Sum{
    private static readonly (string Name,string Result)[] Cases=[
        ("LongOverflow","!OverflowException"),
        ("IntOverflow","!OverflowException"),
        ("NegativeOverflow","!OverflowException"),
        ("PrefixStop","!OverflowException"),
        ("ImplicitUncheckedSelector","0"),
        ("ExplicitCheckedSelector","!OverflowException"),
        ("ExplicitUncheckedSelector","-2147483648"),
        ("DelegateSelector","0"),
        ("EmptyEnumerable","0"),
        ("RejectedEnumerable","0"),
        ("SingleBoundary","9223372036854775807"),
        ("OrderAscending","9223372036854775807"),
        ("OrderDescending","!OverflowException"),
        ("Reverse","9223372036854775807"),
        ("Append","9223372036854775807"),
        ("MultiplePrepend","9223372036854775807"),
        ("Concat","!OverflowException"),
        ("Intersect","!OverflowException"),
        ("Except","!OverflowException"),
        ("SelectMany","!OverflowException"),
        ("GroupResult","!OverflowException"),
        ("DecimalReverse","79228162514264337593543950335"),
    ];

    private const string Header="""
        using System;
        using System.Collections.Generic;
        using System.Globalization;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;

        public sealed class State{
            public readonly List<string> Trace=new();
            public int Disposes;
            public long Record(string stage,long value){
                this.Trace.Add(stage+":"+value.ToString(CultureInfo.InvariantCulture));
                return value;
            }
            public IEnumerable<long> Sequence(params long[] values){
                try{
                    for(var i=0;i<values.Length;i++){
                        this.Trace.Add("N:"+i.ToString(CultureInfo.InvariantCulture));
                        yield return values[i];
                    }
                }finally{
                    this.Disposes++;
                    this.Trace.Add("D");
                }
            }
        }

        [LinqIntercept(false)]
        public sealed class Probe{
            public static string[] RunSystem()=>SystemQueries.Run();
            public static string[] RunNone()=>NoneQueries.Run();
            public static string[] RunAll()=>AllQueries.Run();
            public static string Observe<T>(string name,Func<State,T> evaluate){
                var state=new State();
                string result;
                try{
                    result=Convert.ToString(evaluate(state),CultureInfo.InvariantCulture)!;
                }catch(Exception exception){
                    result="!"+exception.GetType().Name;
                }
                return name+"|"+result+"|dispose="+state.Disposes+"|trace="+string.Join(",",state.Trace);
            }
        }
        """;

    // 同じ式を三つの設定で生成する。RunにLinqIntercept(false)を付けると呼出し先も差し替わらない。
    // method groupで呼ぶ形も避け、属性付きqueryへの呼出し式を残す。
    private const string QuerySet="""
            public static string[] Run()=>[
                Probe.Observe("LongOverflow",state=>__PREFIX__LongOverflow(state)),
                Probe.Observe("IntOverflow",state=>__PREFIX__IntOverflow(state)),
                Probe.Observe("NegativeOverflow",state=>__PREFIX__NegativeOverflow(state)),
                Probe.Observe("PrefixStop",state=>__PREFIX__PrefixStop(state)),
                Probe.Observe("ImplicitUncheckedSelector",state=>__PREFIX__ImplicitUncheckedSelector(state)),
                Probe.Observe("ExplicitCheckedSelector",state=>__PREFIX__ExplicitCheckedSelector(state)),
                Probe.Observe("ExplicitUncheckedSelector",state=>__PREFIX__ExplicitUncheckedSelector(state)),
                Probe.Observe("DelegateSelector",state=>__PREFIX__DelegateSelector(state)),
                Probe.Observe("EmptyEnumerable",state=>__PREFIX__EmptyEnumerable(state)),
                Probe.Observe("RejectedEnumerable",state=>__PREFIX__RejectedEnumerable(state)),
                Probe.Observe("SingleBoundary",state=>__PREFIX__SingleBoundary(state)),
                Probe.Observe("OrderAscending",state=>__PREFIX__OrderAscending(state)),
                Probe.Observe("OrderDescending",state=>__PREFIX__OrderDescending(state)),
                Probe.Observe("Reverse",state=>__PREFIX__Reverse(state)),
                Probe.Observe("Append",state=>__PREFIX__Append(state)),
                Probe.Observe("MultiplePrepend",state=>__PREFIX__MultiplePrepend(state)),
                Probe.Observe("Concat",state=>__PREFIX__Concat(state)),
                Probe.Observe("Intersect",state=>__PREFIX__Intersect(state)),
                Probe.Observe("Except",state=>__PREFIX__Except(state)),
                Probe.Observe("SelectMany",state=>__PREFIX__SelectMany(state)),
                Probe.Observe("GroupResult",state=>__PREFIX__GroupResult(state)),
                Probe.Observe("DecimalReverse",state=>__PREFIX__DecimalReverse(state)),
            ];

            [Query] public static long __PREFIX__LongOverflow(State state)=>
                new[]{long.MaxValue,1L}.Where(x=>x>0).Sum();
            [Query] public static int __PREFIX__IntOverflow(State state)=>
                new[]{int.MaxValue,1}.Where(x=>x>0).Sum();
            [Query] public static long __PREFIX__NegativeOverflow(State state)=>
                new[]{long.MinValue,-1L}.Where(x=>x<0).Sum();

            // 3件目を読む前に止まり、取得した非span列挙子のfinallyを一度だけ通る。
            [Query] public static long __PREFIX__PrefixStop(State state)=>
                state.Sequence(long.MaxValue,1,-1)
                    .Where(x=>state.Record("W",x)!=0)
                    .Sum(x=>state.Record("S",x));

            [Query] public static int __PREFIX__ImplicitUncheckedSelector(State state)=>
                new[]{256}.Where(x=>x>=0).Sum(x=>(byte)state.Record("S",x));
            [Query] public static int __PREFIX__ExplicitCheckedSelector(State state)=>
                new[]{256}.Where(x=>x>=0).Sum(x=>checked((byte)state.Record("S",x)));
            [Query] public static int __PREFIX__ExplicitUncheckedSelector(State state){
                checked{
                    return new[]{int.MaxValue}.Where(x=>x>=0).Sum(x=>unchecked((int)state.Record("S",x)+1));
                }
            }
            [Query] public static int __PREFIX__DelegateSelector(State state){
                Func<int,int> selector=x=>(byte)state.Record("S",x);
                return new[]{256}.Where(x=>x>=0).Sum(selector);
            }

            [Query] public static long __PREFIX__EmptyEnumerable(State state)=>
                state.Sequence().Where(x=>state.Record("W",x)>0).Sum(x=>state.Record("S",x));
            [Query] public static long __PREFIX__RejectedEnumerable(State state)=>
                state.Sequence(-1,-2).Where(x=>state.Record("W",x)>0).Sum(x=>state.Record("S",x));
            [Query] public static long __PREFIX__SingleBoundary(State state)=>
                new[]{long.MaxValue}.Where(x=>x>0).Sum();

            [Query] public static long __PREFIX__OrderAscending(State state)=>
                new[]{long.MaxValue,1L,-1L}.OrderBy(x=>state.Record("K",x)).Sum(x=>state.Record("S",x));
            [Query] public static long __PREFIX__OrderDescending(State state)=>
                new[]{-1L,1L,long.MaxValue}.OrderByDescending(x=>state.Record("K",x)).Sum();
            [Query] public static long __PREFIX__Reverse(State state)=>
                new[]{long.MaxValue,1L,-1L}.Reverse().Sum(x=>state.Record("S",x));

            // 最終和は範囲内。checkedだけ付けてAppend/Prependを前倒しすると誤った例外になる。
            [Query] public static long __PREFIX__Append(State state)=>
                new[]{long.MaxValue,-1L}.Append(1).Sum();
            [Query] public static long __PREFIX__MultiplePrepend(State state)=>
                new[]{0L}.Prepend(long.MaxValue).Prepend(1).Prepend(-1).Sum();

            [Query] public static long __PREFIX__Concat(State state)=>
                new[]{long.MaxValue}.Concat(new[]{1L}).Where(x=>x>0).Sum();
            [Query] public static long __PREFIX__Intersect(State state)=>
                new[]{long.MaxValue,1L,-1L}.Intersect(new[]{long.MaxValue,1L}).Sum();
            [Query] public static long __PREFIX__Except(State state)=>
                new[]{long.MaxValue,1L,-1L}.Except(new[]{-1L}).Sum();
            [Query] public static long __PREFIX__SelectMany(State state){
                long[] second=[long.MaxValue,1L];
                return new[]{0}.SelectMany(x=>second).Sum();
            }
            [Query] public static long __PREFIX__GroupResult(State state)=>
                new[]{long.MaxValue,1L}.GroupBy(x=>x,x=>x,(key,g)=>g.Sum()).Sum();
            [Query] public static decimal __PREFIX__DecimalReverse(State state)=>
                new[]{decimal.MaxValue,1m,-1m}.Reverse().Sum();
        """;

    [Fact,Trait("Category","Integration")]
    public void 累積例外と選択子と列挙順と解除をSystemに合わせる(){
        var source=CreateSource();
        var directory=Environment.GetEnvironmentVariable("SEAMLESS_SUM_CONTRACT_ARTIFACT_DIR");
        SaveArtifact(directory,"input.cs",source);
        var(compilation,generated)=ExecutableGeneration.Generate(source,"PipelineSumContractProbe",Cases.Length*2);
        SaveArtifact(directory,"generated.cs",generated);
        // 各ケースのNone/All両方が差し替わることを確認し、nested Sumの生成数だけで空振りを隠さない。
        foreach(var mode in new[]{"None","All"}){
            foreach(var testCase in Cases){
                Assert.Contains("OptimizedLinq_"+mode+"_"+testCase.Name+"_",generated);
            }
        }
        var observations=ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>(
            System:(string[])probe.GetMethod("RunSystem")!.Invoke(null,null)!,
            None:(string[])probe.GetMethod("RunNone")!.Invoke(null,null)!,
            All:(string[])probe.GetMethod("RunAll")!.Invoke(null,null)!));
        // RunのfinallyでALCを解放してから保存する。artifact書込み失敗が解放を飛ばさない。
        var options=new JsonSerializerOptions{WriteIndented=true};
        SaveArtifact(directory,"observations.json",JsonSerializer.Serialize(new{
            System=observations.System,
            None=observations.None,
            All=observations.All,
        },options));
        SaveArtifact(directory,"hashes.json",JsonSerializer.Serialize(new{
            InputSHA256=Hash(source),
            GeneratedSHA256=Hash(generated),
            GeneratorAssembly=typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location,
            GeneratorSHA256=HashFile(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location),
        },options));

        Assert.Equal(Cases.Length,observations.System.Length);
        Assert.Equal(Cases.Length,observations.None.Length);
        Assert.Equal(Cases.Length,observations.All.Length);
        var differences=new List<string>();
        for(var index=0;index<Cases.Length;index++){
            var testCase=Cases[index];
            Assert.StartsWith(testCase.Name+"|"+testCase.Result+"|",observations.System[index]);
            foreach(var(mode,actual) in new[]{("None",observations.None[index]),("All",observations.All[index])}){
                if(actual!=observations.System[index]){
                    differences.Add(mode+" "+testCase.Name+Environment.NewLine+"System: "+observations.System[index]+Environment.NewLine+"Actual: "+actual);
                }
            }
        }
        // System側の停止・finally・空入力も明示し、同じ誤りを比較するだけの試験にしない。
        Assert.Equal("PrefixStop|!OverflowException|dispose=1|trace=N:0,W:9223372036854775807,S:9223372036854775807,N:1,W:1,S:1,D",observations.System[3]);
        Assert.Equal("EmptyEnumerable|0|dispose=1|trace=D",observations.System[8]);
        Assert.Equal("RejectedEnumerable|0|dispose=1|trace=N:0,W:-1,N:1,W:-2,D",observations.System[9]);
        Assert.True(differences.Count==0,string.Join(Environment.NewLine+Environment.NewLine,differences));
    }

    private static string CreateSource(){
        var builder=new StringBuilder(Header);
        foreach(var mode in new[]{"System","None","All"}){
            string attribute;
            if(mode=="System"){
                attribute="[LinqIntercept(false)]";
            }else if(mode=="None"){
                attribute="[LinqIntercept(true,Optimizations=LinqOptimizations.None)]";
            }else{
                attribute="[LinqIntercept(true,Optimizations=LinqOptimizations.All,ConvergenceMode=LinqConvergenceMode.GlobalFixedPoint)]";
            }
            builder.AppendLine();
            builder.AppendLine("public sealed class "+mode+"Queries{");
            builder.AppendLine(QuerySet.Replace("[Query]",attribute).Replace("__PREFIX__",mode+"_"));
            builder.AppendLine("}");
        }
        return builder.ToString();
    }

    private static void SaveArtifact(string? directory,string name,string text){
        if(string.IsNullOrWhiteSpace(directory)){
            return;
        }
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,name),text,new UTF8Encoding(false));
    }

    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string HashFile(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
