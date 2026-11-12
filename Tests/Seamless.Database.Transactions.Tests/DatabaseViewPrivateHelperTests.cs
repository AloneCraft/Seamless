using System.Text.Json;

namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Theory]
    // QueryはpartialのView行型(struct)に置くため、helperの宣言型は常にstructになる。classに置く旧形式は書けない。
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredViewKeepsPrivateStructHelperCallsAndTheirExceptions(bool methodGroup){
        var source="""
            using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;using Seamless.Database;using Seamless.FLinq;
            namespace PrivateViewHelperCase{
             internal static partial class DatabaseGenerator;
             public readonly record struct Input(int Id,int Value);
             public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){public Table<int,Input> Inputs=>this.Table<int,Input>(static row=>row.Id);}
             public static class Probe{public static bool Fail;public static readonly List<string> Events=new();}
             public sealed class Entry{
              [LinqIntercept(true)]private int[] Query(Database db)=>db.PrivateRows.Select(static row=>row.Value).ToArray();
              [LinqIntercept(false)]private int[] Reference(Database db)=>db.PrivateRows.Select(static row=>row.Value).ToArray();
              public static string Run(){
               using var db=new Database(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
               using(var tx=db.BeginTransaction()){for(var i=1;i<=3;i++)tx.Inputs.Add(new(i,i*10));tx.Commit();}
               var entry=new Entry();var observations=new List<object>();
               foreach(var fail in new[]{false,true}){
                Probe.Fail=fail;
                object Observe(bool expanded){
                 Probe.Events.Clear();int[] values=[];string? error=null;
                 try{values=expanded?entry.Query(db):entry.Reference(db);}catch(Exception exception){error=exception.GetType().FullName+":"+exception.Message;}
                 return new{values,error,events=Probe.Events.ToArray()};
                }
                var expected=Observe(false);var actual=Observe(true);
                observations.Add(new{fail,expected,actual,equal=JsonSerializer.Serialize(expected)==JsonSerializer.Serialize(actual)});
               }
               return JsonSerializer.Serialize(observations);
              }
             }
            }
            namespace PrivateViewHelperCase.Views{
             public readonly partial record struct PrivateRows(int Value){
              [LinqIntercept(false)]private static int Project(global::PrivateViewHelperCase.Input row){
               global::PrivateViewHelperCase.Probe.Events.Add("project:"+row.Id);
               if(global::PrivateViewHelperCase.Probe.Fail&&row.Id==3)throw new InvalidOperationException("helper3");
               return row.Value;
              }
              public static global::System.Collections.Generic.IEnumerable<PrivateRows> Query(global::PrivateViewHelperCase.Database container)=>container.Inputs.Select(static row=>new PrivateRows(Project(row)));
             }
            }
            """;
        if(methodGroup){source=source.Replace(".Select(static row=>new PrivateRows(Project(row)))",".Select(Project).Select(static value=>new PrivateRows(value))",StringComparison.Ordinal);}
        var evidence=Environment.GetEnvironmentVariable("VIEW_BOUNDARY_PROOF_ROOT");
        var caseName=$"private-helper-{methodGroup}";
        var(result,generated)=await RunBoundarySourceAsync(source,"PrivateViewHelperCase",evidence is null?null:Path.Combine(evidence,caseName+"-generated.cs.txt"));
        if(evidence is not null){File.WriteAllText(Path.Combine(evidence,caseName+"-observation.json"),result);}
        using var observation=JsonDocument.Parse(result);
        Assert.Equal(2,observation.RootElement.GetArrayLength());
        foreach(var item in observation.RootElement.EnumerateArray()){
            Assert.True(item.GetProperty("equal").GetBoolean(),item.GetRawText());
            var actual=item.GetProperty("actual");
            if(item.GetProperty("fail").GetBoolean()){
                Assert.Equal("System.InvalidOperationException:helper3",actual.GetProperty("error").GetString());
                Assert.Equal("project:3",actual.GetProperty("events").EnumerateArray().Last().GetString());
            }else{
                Assert.Equal(3,actual.GetProperty("events").GetArrayLength());
                Assert.Equal(new[]{10,20,30},actual.GetProperty("values").EnumerateArray().Select(static value=>value.GetInt32()).Order());
                Assert.Equal(JsonValueKind.Null,actual.GetProperty("error").ValueKind);
            }
        }
        var code=string.Join(Environment.NewLine,generated);
        Assert.Contains("Name = \"Project\"",code,StringComparison.Ordinal);
        // 定義は呼出元へ融合し、View境界(ExpandDefinition)を通らない。private helperの転送は融合後も維持する。
        Assert.Contains("InlinedIntoCaller",code,StringComparison.Ordinal);
        Assert.DoesNotContain(".ExpandDefinition<",code,StringComparison.Ordinal);
    }
}
