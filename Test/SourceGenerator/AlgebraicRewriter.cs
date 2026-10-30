using System.Text;
using System.Text.Json;
using Seamless.FLinq;

namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class AlgebraicRewriter{
    [Fact]
    public void CountComparisonPreservesExceptionsAndAnchor(){
        string[] queries=[
            "this.Values().Count(p=>this.Hit(p))>0",
            "0==this.Values().Count(p=>this.Hit(p))",
            "this.Values().LongCount(p=>this.Hit(p))>=1",
            "this.Values().Where(p=>this.Hit(p)).Count()>0",
            "this.ThrowingValues().Count()>0",
            "this.Values().Count(p=>checked(p+2147483646)>0)>0",
            "this.Values().Count(p=>10/(2-p)>0)>0",
            "this.Values().Count(p=>this.Hit(p).Anchor)>0",
            "this.Values().Count(p=>p>0)>0",
            "this.Values().Where(p=>p>0).Count()>0",
            "this.Values().Count()>0",
            "global::System.Linq.Enumerable.Range(0,4).Count(p=>p>0)>0",
            "this.ThrowingValues().Count(p=>p>0)>0"
        ];
        var builder=new StringBuilder("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Probe{
                private readonly List<string> trace=new();
                private int fault;
                private bool empty;
                public int[] Values(){this.trace.Add("S");if(this.empty){return [];}return [1,2,3,4];}
                public bool Hit(int value){this.trace.Add("P"+value);if(value==this.fault){throw new InvalidOperationException("P"+value);}return value>0;}
                public IEnumerable<int> ThrowingValues(){this.trace.Add("S1");if(this.empty){yield break;}yield return 1;this.trace.Add("S2");throw new InvalidOperationException("source2");}
                public static object[] Run(int mode,int id,int fault,bool empty){
                    var probe=new Probe();probe.fault=fault;probe.empty=empty;
                    using var scope=LinqCounterScope.Start();string value="",type="",message="";
                    try{bool result=false;switch(mode*100+id){
            """);
        for(var mode=0;mode<4;mode++){
            for(var id=0;id<queries.Length;id++){
                builder.Append("case ").Append(mode*100+id).Append(":result=probe.Query").Append(mode).Append('_').Append(id).AppendLine("();break;");
            }
        }
        builder.AppendLine("}value=result.ToString();}catch(Exception exception){type=exception.GetType().FullName!;message=exception.Message;}return new object[]{value,type,message,string.Join(\",\",probe.trace),scope.Counter.LogicalDelegate};}");
        for(var mode=0;mode<4;mode++){
            for(var id=0;id<queries.Length;id++){
                if(mode==0){builder.AppendLine("[LinqIntercept(true,LinqInstrumentation.Counter)]");}
                else if(mode==1){builder.AppendLine("[LinqIntercept(true,LinqInstrumentation.Counter,Optimizations=LinqOptimizations.None)]");}
                else if(mode==2){builder.AppendLine("[LinqIntercept(false,LinqInstrumentation.Counter)]");}
                else{builder.AppendLine("[LinqIntercept(false)]");}
                builder.Append("private bool Query").Append(mode).Append('_').Append(id).Append("()=>").Append(queries[id]).AppendLine(";");
            }
        }
        var source=builder.AppendLine("}").ToString();
        Save("input.cs",source);
        var (compilation,generated)=ExecutableGeneration.Generate(source,"CountComparisonExceptions",queries.Length*3);
        Save("generated.cs",generated);
        var observations=new List<object>();
        var failures=new List<string>();
        ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>{
            var run=probe.GetMethod("Run")!;
            for(var id=0;id<queries.Length;id++){
                foreach(var scenario in new[]{(Fault:0,Empty:false),(Fault:1,Empty:false),(Fault:2,Empty:false),(Fault:0,Empty:true)}){
                    var reference=(object[])run.Invoke(null,[3,id,scenario.Fault,scenario.Empty])!;
                    for(var mode=0;mode<3;mode++){
                        var actual=(object[])run.Invoke(null,[mode,id,scenario.Fault,scenario.Empty])!;
                        for(var column=0;column<4;column++){
                            if(!Equals(reference[column],actual[column])){failures.Add($"case{id}/mode{mode}/fault{scenario.Fault}/empty{scenario.Empty}/column{column}: expected {reference[column]}, actual {actual[column]}");}
                        }
                        // 安全なarray/Whereは最適化を保持する。参照からコピーせず実行量を独立算出する。
                        if(id is 8 or 9){
                            var expected=4L;
                            if(scenario.Empty){expected=0;}
                            else if(mode==0){expected=1;}
                            if((long)actual[4]!=expected){failures.Add($"pure{id}/mode{mode}: expected logical {expected}, actual {actual[4]}");}
                        }
                        observations.Add(new{Case=id,Query=queries[id],Mode=mode,scenario.Fault,scenario.Empty,Actual=actual,Reference=reference});
                    }
                }
            }
            return true;
        });
        Save("observations.json",JsonSerializer.Serialize(new{Observations=observations,Failures=failures},new JsonSerializerOptions{WriteIndented=true}));
        Save("hashes.json",JsonSerializer.Serialize(new{
            SourceSHA256=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(source))),
            GeneratedSHA256=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(generated))),
            LoadedGeneratorSHA256=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(global::Seamless.FLinq.Generator.FLinqIncrementalGenerator).Assembly.Location)))
        }));
        Assert.Empty(failures);
    }
    private static void Save(string name,string content){
        var path=Environment.GetEnvironmentVariable("SEAMLESS_COUNT_COMPARISON_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(path)){return;}
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path,name),content);
    }
}
