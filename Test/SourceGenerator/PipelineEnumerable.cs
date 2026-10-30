using Seamless.FLinq;

namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class PipelineEnumerable{
    private const string Source="""
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Probe{
            private readonly List<string> trace=new();
            private int mode;
            private int failure;
            [LinqIntercept(false)]private int Key(int value){
                this.trace.Add("K"+value);
                if(this.failure==1&&value==61){throw new InvalidOperationException("key");}
                if(this.mode==1){return -(value&31);}
                if(this.mode==2){if((value&1)==0){return int.MinValue;}return int.MaxValue;}
                if(this.mode==3){return value*257;}
                if(this.mode==4){return int.MaxValue-(value&31);}
                if(this.mode==5){return value&255;}
                if(this.mode==6){return value&256;}
                return value&31;
            }
            [LinqIntercept(false)]private bool Predicate(int value){
                this.trace.Add("W"+value);
                if(this.failure==2){throw new InvalidOperationException("where");}
                if(this.failure==4){return false;}
                return (value&3)==1;
            }
            [LinqIntercept(false)]private int Select(int value){
                this.trace.Add("S"+value);
                if(this.failure==3){throw new InvalidOperationException("select");}
                return value*3+1;
            }
            [LinqIntercept(false)]private int[] ReadSource(int[] values){this.trace.Add("R");return values;}
            public static string Run(bool optimized,bool first,int[] values,int mode,int failure){
                var probe=new Probe();
                probe.mode=mode;
                probe.failure=failure;
                string result;
                try{
                    if(first){
                        if(optimized){result=probe.First(values).ToString();}
                        else{result=probe.ReferenceFirst(values).ToString();}
                    }else{
                        if(optimized){result=string.Join(",",probe.All(values));}
                        else{result=string.Join(",",probe.ReferenceAll(values));}
                    }
                }catch(Exception exception){result="!"+exception.GetType().Name+":"+exception.Message;}
                return result+"|"+string.Join(",",probe.trace);
            }
            public static string RunTerminal(bool optimized,int terminal,int[] values,int failure){
                var probe=new Probe();
                probe.failure=failure;
                string result;
                try{
                    if(optimized){result=probe.Terminal(terminal,values);}
                    else{result=probe.ReferenceTerminal(terminal,values);}
                }catch(Exception exception){result="!"+exception.GetType().Name+":"+exception.Message;}
                return result+"|"+string.Join(",",probe.trace);
            }
            [LinqIntercept(true)]private string Terminal(int terminal,int[] values){
                if(terminal==10){return values.OrderBy(value=>this.Key(value)).OrderBy(value=>this.Select(value)).FirstOrDefault().ToString();}
                if(terminal==11){
                    var chainedSource=this.ReadSource(values).OrderBy(value=>this.Key(value)).OrderBy(value=>this.Select(value));
                    values=Array.Empty<int>();
                    this.trace.Add("D");
                    var chainedFirst=chainedSource.FirstOrDefault();
                    return chainedFirst+"/"+string.Join(",",chainedSource.ToArray())+"/"+string.Join(",",chainedSource);
                }
                if(terminal==8){return string.Join(",",values.OrderBy(value=>this.Key(value)).OrderBy(value=>this.Select(value)).ToArray());}
                if(terminal==9){
                    var twiceOrdered=values.OrderBy(value=>this.Key(value)).OrderBy(value=>this.Select(value));
                    var twiceArray=twiceOrdered.ToArray();
                    return string.Join(",",twiceArray)+"/"+string.Join(",",twiceOrdered);
                }
                if(terminal==6){return string.Join(",",values.OrderBy(value=>this.Key(value)).Where(value=>this.Predicate(value)).OrderBy(value=>this.Select(value)).ToArray());}
                if(terminal==7){return string.Join(",",values.OrderBy(value=>this.Key(value)).Where(value=>this.Predicate(value)).OrderBy(value=>this.Select(value)).ToList());}
                if(terminal==0){return values.OrderBy(value=>this.Key(value)).FirstOrDefault().ToString();}
                if(terminal==1){return string.Join(",",values.OrderBy(value=>this.Key(value)).ToArray());}
                if(terminal==2){return string.Join(",",values.OrderBy(value=>this.Key(value)).ToList());}
                if(terminal==3){return values.OrderBy(value=>10/value).FirstOrDefault().ToString();}
                if(terminal==5){
                    var capturedOrdered=this.ReadSource(values).OrderBy(value=>this.Key(value));
                    values=Array.Empty<int>();
                    return string.Join(",",capturedOrdered.ToArray())+"/"+capturedOrdered.FirstOrDefault();
                }
                var ordered=values.OrderBy(value=>this.Key(value));
                var array=ordered.ToArray();
                var list=ordered.ToList();
                var seen=new List<int>();
                foreach(var value in ordered){seen.Add(value);}
                var first=ordered.FirstOrDefault();
                return string.Join(",",array)+"/"+string.Join(",",list)+"/"+string.Join(",",seen)+"/"+first;
            }
            [LinqIntercept(false)]private string ReferenceTerminal(int terminal,int[] values){
                if(terminal==10){return values.OrderBy(value=>this.Key(value)).OrderBy(value=>this.Select(value)).FirstOrDefault().ToString();}
                if(terminal==11){
                    var chainedSource=this.ReadSource(values).OrderBy(value=>this.Key(value)).OrderBy(value=>this.Select(value));
                    values=Array.Empty<int>();
                    this.trace.Add("D");
                    var chainedFirst=chainedSource.FirstOrDefault();
                    return chainedFirst+"/"+string.Join(",",chainedSource.ToArray())+"/"+string.Join(",",chainedSource);
                }
                if(terminal==8){return string.Join(",",values.OrderBy(value=>this.Key(value)).OrderBy(value=>this.Select(value)).ToArray());}
                if(terminal==9){
                    var twiceOrdered=values.OrderBy(value=>this.Key(value)).OrderBy(value=>this.Select(value));
                    var twiceArray=twiceOrdered.ToArray();
                    return string.Join(",",twiceArray)+"/"+string.Join(",",twiceOrdered);
                }
                if(terminal==6){return string.Join(",",values.OrderBy(value=>this.Key(value)).Where(value=>this.Predicate(value)).OrderBy(value=>this.Select(value)).ToArray());}
                if(terminal==7){return string.Join(",",values.OrderBy(value=>this.Key(value)).Where(value=>this.Predicate(value)).OrderBy(value=>this.Select(value)).ToList());}
                if(terminal==0){return values.OrderBy(value=>this.Key(value)).FirstOrDefault().ToString();}
                if(terminal==1){return string.Join(",",values.OrderBy(value=>this.Key(value)).ToArray());}
                if(terminal==2){return string.Join(",",values.OrderBy(value=>this.Key(value)).ToList());}
                if(terminal==3){return values.OrderBy(value=>10/value).FirstOrDefault().ToString();}
                if(terminal==5){
                    var capturedOrdered=this.ReadSource(values).OrderBy(value=>this.Key(value));
                    values=Array.Empty<int>();
                    return string.Join(",",capturedOrdered.ToArray())+"/"+capturedOrdered.FirstOrDefault();
                }
                var ordered=values.OrderBy(value=>this.Key(value));
                var array=ordered.ToArray();
                var list=ordered.ToList();
                var seen=new List<int>();
                foreach(var value in ordered){seen.Add(value);}
                var first=ordered.FirstOrDefault();
                return string.Join(",",array)+"/"+string.Join(",",list)+"/"+string.Join(",",seen)+"/"+first;
            }
            [LinqIntercept(true)]private int First(int[] values)=>values.OrderBy(value=>this.Key(value)).Where(value=>this.Predicate(value)).Select(value=>this.Select(value)).FirstOrDefault();
            [LinqIntercept(true)]private IEnumerable<int> All(int[] values)=>values.OrderBy(value=>this.Key(value)).Where(value=>this.Predicate(value)).Select(value=>this.Select(value));
            [LinqIntercept(false)]private int ReferenceFirst(int[] values)=>values.OrderBy(value=>this.Key(value)).Where(value=>this.Predicate(value)).Select(value=>this.Select(value)).FirstOrDefault();
            [LinqIntercept(false)]private IEnumerable<int> ReferenceAll(int[] values)=>values.OrderBy(value=>this.Key(value)).Where(value=>this.Predicate(value)).Select(value=>this.Select(value));
        }
        """;

    [Fact]
    public void OrderBy(){
        var (compilation,generated)=ExecutableGeneration.Generate(Source,"SmallRangeIntOrderProbe",3);
        SaveGenerated(Source,generated);
        // 生成名の正規化後にも、snapshot後の分岐へ到達したことを確認する。
        Assert.Contains("orderCountingSorted",generated);
        ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>{
            var run=probe.GetMethod("Run")!;
            // 件数境界、キー幅境界、負数、両極値、最大値近傍を同じ外部契約で比較する。
            foreach(var count in new[]{0,1,63,64,128,257}){
                var values=global::System.Linq.Enumerable.Range(0,count).Reverse().ToArray();
                foreach(var mode in new[]{0,1,2,3,4,5,6}){
                    foreach(var first in new[]{false,true}){
                        var expected=(string)run.Invoke(null,[false,first,values,mode,0])!;
                        var actual=(string)run.Invoke(null,[true,first,values,mode,0])!;
                        Assert.Equal(expected,actual);
                    }
                }
            }
            // キー途中の例外、後段の例外、全件不合格とFirst短絡のtraceを比較する。
            var failures=global::System.Linq.Enumerable.Range(0,128).Reverse().ToArray();
            foreach(var failure in new[]{1,2,3,4}){
                foreach(var first in new[]{false,true}){
                    var expected=(string)run.Invoke(null,[false,first,failures,0,failure])!;
                    var actual=(string)run.Invoke(null,[true,first,failures,0,failure])!;
                    Assert.Equal(expected,actual);
                }
            }
            // 通常列挙とWhere後Firstは、空/1件でもKey→Where→合格時Selectの順を守る。
            foreach(var values in new[]{Array.Empty<int>(),new[]{0},new[]{1},new[]{61}}){
                foreach(var failure in new[]{0,1,2,3,4}){
                    foreach(var first in new[]{false,true}){
                        var expected=(string)run.Invoke(null,[false,first,values,0,failure])!;
                        var actual=(string)run.Invoke(null,[true,first,values,0,failure])!;
                        Assert.Equal(expected,actual);
                    }
                }
            }
            // plain Firstの鍵評価とToArray/ToListの1件shortcutを区別する。
            var terminalRun=probe.GetMethod("RunTerminal")!;
            var terminalRecords=new List<object>();
            var terminalFailures=new List<string>();
            foreach(var terminal in new[]{0,1,2,3,4,5,6,7,8,9,10,11}){
                foreach(var values in new[]{Array.Empty<int>(),new[]{0},new[]{1},new[]{61}}){
                    foreach(var failure in new[]{0,1}){
                        var expected=(string)terminalRun.Invoke(null,[false,terminal,values,failure])!;
                        var actual=(string)terminalRun.Invoke(null,[true,terminal,values,failure])!;
                        terminalRecords.Add(new{terminal,values,failure,expected,actual});
                        if(expected!=actual){terminalFailures.Add($"terminal={terminal};values={string.Join(",",values)};failure={failure};System={expected};FLinq={actual}");}
                    }
                }
            }
            SaveTerminalObservations(terminalRecords);
            Assert.True(terminalFailures.Count==0,string.Join("\n",terminalFailures));
            return true;
        });
        // 再代入のあるlocalには専用束縛を残さない。複数件の外部結果/traceも維持する。
        var reassignedSource=Source.Replace("var array=ordered.ToArray();","var savedOrdered=ordered; ordered=savedOrdered; var array=ordered.ToArray();",StringComparison.Ordinal)
            .Replace("var twiceArray=twiceOrdered.ToArray();","var savedTwice=twiceOrdered; twiceOrdered=savedTwice; var twiceArray=twiceOrdered.ToArray();",StringComparison.Ordinal)
            .Replace("var chainedFirst=chainedSource.FirstOrDefault();","var savedChained=chainedSource; chainedSource=savedChained; var chainedFirst=chainedSource.FirstOrDefault();",StringComparison.Ordinal)
            .Replace("var capturedOrdered=this.ReadSource(values).OrderBy(value=>this.Key(value));","var capturedOrdered=this.ReadSource(values).OrderBy(value=>this.Key(value)); var savedCaptured=capturedOrdered; capturedOrdered=savedCaptured;",StringComparison.Ordinal);
        var (reassignedCompilation,reassignedGenerated)=ExecutableGeneration.Generate(reassignedSource,"ReassignedOrderedProbe",3);
        SaveGenerated(reassignedSource,reassignedGenerated);
        Assert.DoesNotContain("orderedMaterializationEnumerator",reassignedGenerated);
        ExecutableGeneration.Run(reassignedCompilation,"Sample.Probe",probe=>{
            var run=probe.GetMethod("RunTerminal")!;
            foreach(var terminal in new[]{4,5}){
                var values=new[]{0,1};
                Assert.Equal((string)run.Invoke(null,[false,terminal,values,0])!,(string)run.Invoke(null,[true,terminal,values,0])!);
            }
            return true;
        });
    }

    [Theory]
    [InlineData("values.OrderByDescending(value=>this.Key(value))")]
    [InlineData("values.OrderBy(value=>this.Key(value)).ThenBy(value=>value)")]
    [InlineData("values.OrderBy(value=>this.Key(value),Comparer<int>.Create((left,right)=>left.CompareTo(right)))")]
    [InlineData("values.OrderBy(value=>this.Key(value)).Skip(1).Take(5)")]
    public void OrderBy対象外の比較契約を維持する(string order){
        var source=Source.Replace("values.OrderBy(value=>this.Key(value))",order,StringComparison.Ordinal)
            .Replace(".OrderBy(value=>this.Select(value))",".OrderByDescending(value=>this.Select(value))",StringComparison.Ordinal)
            .Replace("this.ReadSource(values).OrderBy(value=>this.Key(value))","this.ReadSource(values).OrderByDescending(value=>this.Key(value))",StringComparison.Ordinal)
            .Replace("values.OrderBy(value=>10/value)","values.OrderByDescending(value=>10/value)",StringComparison.Ordinal);
        var (compilation,generated)=ExecutableGeneration.Generate(source,"SmallRangeIntOrderExcludedProbe",2);
        SaveGenerated(source,generated);
        Assert.DoesNotContain("orderCountingSorted",generated);
        ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>{
            var run=probe.GetMethod("Run")!;
            var values=global::System.Linq.Enumerable.Range(0,128).Reverse().ToArray();
            foreach(var first in new[]{false,true}){
                var expected=(string)run.Invoke(null,[false,first,values,0,0])!;
                var actual=(string)run.Invoke(null,[true,first,values,0,0])!;
                Assert.Equal(expected,actual);
            }
            return true;
        });
    }

    [Theory]
    [InlineData("values.OrderBy(value=>this.Key(value)).ThenBy(value=>this.Select(value))",false)]
    [InlineData("values.OrderByDescending(value=>this.Key(value),this.CreateComparer())",true)]
    [InlineData("values.OrderBy(value=>this.Key(value),this.CreateComparer()).ThenByDescending(value=>this.Select(value),this.CreateComparer())",true)]
    public void OrderBy各終端の評価契約を維持する(string order,bool outerComparer){
        var source=Source.Replace("values.OrderBy(value=>this.Key(value))",order,StringComparison.Ordinal)
            .Replace("this.ReadSource(values).OrderBy(value=>this.Key(value))",order.Replace("values","this.ReadSource(values)",StringComparison.Ordinal),StringComparison.Ordinal)
            .Replace("[LinqIntercept(false)]private int[] ReadSource", """
                [LinqIntercept(false)]private IComparer<int> CreateComparer(){
                    this.trace.Add("F");
                    return Comparer<int>.Create((left,right)=>{
                        this.trace.Add("C"+left+":"+right);
                        if(this.failure==5){throw new InvalidOperationException("compare");}
                        return left.CompareTo(right);
                    });
                }
                [LinqIntercept(false)]private int[] ReadSource
                """,StringComparison.Ordinal);
        if(outerComparer){
            source=source.Replace(".OrderBy(value=>this.Select(value))",".OrderByDescending(value=>this.Select(value),this.CreateComparer())",StringComparison.Ordinal);
        }
        var (compilation,generated)=ExecutableGeneration.Generate(source,"OrderedConsumerContractProbe",3);
        SaveGenerated(source,generated);
        ExecutableGeneration.Run(compilation,"Sample.Probe",probe=>{
            var run=probe.GetMethod("RunTerminal")!;
            var failures=new List<string>();
            var records=new List<object>();
            foreach(var terminal in new[]{0,1,2,4,5,6,7,8,9,10,11}){
                foreach(var values in new[]{Array.Empty<int>(),new[]{0},new[]{1},new[]{61},new[]{1,0},new[]{1,1},new[]{33,1,2,0}}){
                    foreach(var failure in new[]{0,1,2,3,4,5}){
                        var expected=(string)run.Invoke(null,[false,terminal,values,failure])!;
                        var actual=(string)run.Invoke(null,[true,terminal,values,failure])!;
                        records.Add(new{terminal,values,failure,expected,actual});
                        if(expected!=actual){failures.Add($"terminal={terminal};values={string.Join(",",values)};failure={failure};System={expected};FLinq={actual}");}
                    }
                }
            }
            SaveOrderedConsumerObservations(source,records);
            Assert.True(failures.Count==0,string.Join("\n",failures));
            return true;
        });
    }

    private static void SaveOrderedConsumerObservations(string source,List<object> records){
        var target=Environment.GetEnvironmentVariable("SEAMLESS_ORDER_SORT_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(target)){return;}
        var hash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(source)));
        var directory=Path.Combine(Path.GetFullPath(target),hash);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"ordered-consumer-observations.json"),global::System.Text.Json.JsonSerializer.Serialize(records));
    }

    private static void SaveGenerated(string source,string generated){
        var target=Environment.GetEnvironmentVariable("SEAMLESS_ORDER_SORT_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(target)){return;}
        var sourceHash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(source)));
        var generatedHash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(generated)));
        var directory=Path.Combine(Path.GetFullPath(target),sourceHash);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"input.cs"),source);
        File.WriteAllText(Path.Combine(directory,"generated.cs"),generated);
        File.WriteAllText(Path.Combine(directory,"hashes.json"),global::System.Text.Json.JsonSerializer.Serialize(new{SourceSHA256=sourceHash,GeneratedSHA256=generatedHash}));
    }

    private static void SaveTerminalObservations(List<object> records){
        var target=Environment.GetEnvironmentVariable("SEAMLESS_ORDER_SORT_ARTIFACT_DIR");
        if(string.IsNullOrWhiteSpace(target)){return;}
        File.WriteAllText(Path.Combine(Path.GetFullPath(target),"terminal-observations.json"),global::System.Text.Json.JsonSerializer.Serialize(records));
    }
}
