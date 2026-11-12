using System.Text.Json;

namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Fact]
    public async Task InitializerTargetsStaySeparateFromInstanceAndParameterValues(){
        const string source="""
            using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;using Seamless.FLinq;
            namespace InitializerBindingCase{
             public sealed record WithResult(int Value);
             public sealed class Child{public int Field;public int Value{get;set;}}
             public sealed class Result{
              public int Field;public int Value{get;set;}public int Offset{get;set;}
              public Child Child{get;}=new();public List<int> Values{get;}=new();public Dictionary<int,int> Map{get;}=new();
             }
             public sealed class Entry{
              public int Field=7;public int Value=>13;
              [LinqIntercept(true)]private Result[] Query(int[] source,int Offset)=>source.Select(row=>new Result{Field=Field+row,Value=Value+row,Offset=Offset,Child={Field=Field,Value=Value+Offset},Values={Field,Value,Offset,row},Map={[Field]=Value}}).ToArray();
              [LinqIntercept(false)]private Result[] Reference(int[] source,int Offset)=>source.Select(row=>new Result{Field=Field+row,Value=Value+row,Offset=Offset,Child={Field=Field,Value=Value+Offset},Values={Field,Value,Offset,row},Map={[Field]=Value}}).ToArray();
              [LinqIntercept(true)]private int[] Assignments(int[] source)=>source.Select(row=>Field=Value+row).ToArray();
              [LinqIntercept(false)]private int[] AssignmentsReference(int[] source)=>source.Select(row=>Field=Value+row).ToArray();
              [LinqIntercept(true)]private WithResult[] WithValues(int[] source)=>source.Select(row=>new WithResult(row) with{Value=Value}).ToArray();
              [LinqIntercept(false)]private WithResult[] WithValuesReference(int[] source)=>source.Select(row=>new WithResult(row) with{Value=Value}).ToArray();
              private static object Describe(Result value)=>new{value.Field,value.Value,value.Offset,childField=value.Child.Field,childValue=value.Child.Value,value.Values,value.Map};
              public static string Run(){
               var expectedEntry=new Entry();var actualEntry=new Entry();int[] input=[1,2];
               var expected=expectedEntry.Reference(input,5).Select(Describe).ToArray();var actual=actualEntry.Query(input,5).Select(Describe).ToArray();
               var expectedAssigned=expectedEntry.AssignmentsReference(input);var actualAssigned=actualEntry.Assignments(input);
               var expectedWith=expectedEntry.WithValuesReference(input);var actualWith=actualEntry.WithValues(input);
               return JsonSerializer.Serialize(new{expected,actual,expectedAssigned,actualAssigned,expectedWith,actualWith,expectedField=expectedEntry.Field,actualField=actualEntry.Field});
              }
             }
            }
            """;
        var evidence=Environment.GetEnvironmentVariable("VIEW_BOUNDARY_PROOF_ROOT");
        var(result,_)=await RunBoundarySourceAsync(source,"InitializerBindingCase",evidence is null?null:Path.Combine(evidence,"binding-generated.cs.txt"));
        using var observation=JsonDocument.Parse(result);
        var value=observation.RootElement;
        Assert.Equal(value.GetProperty("expected").GetRawText(),value.GetProperty("actual").GetRawText());
        Assert.Equal(value.GetProperty("expectedAssigned").GetRawText(),value.GetProperty("actualAssigned").GetRawText());
        Assert.Equal(value.GetProperty("expectedWith").GetRawText(),value.GetProperty("actualWith").GetRawText());
        Assert.Equal(new[]{13,13},value.GetProperty("actualWith").EnumerateArray().Select(static item=>item.GetProperty("Value").GetInt32()));
        Assert.Equal(15,value.GetProperty("actualField").GetInt32());
        Assert.Equal(value.GetProperty("expectedField").GetInt32(),value.GetProperty("actualField").GetInt32());
        var first=value.GetProperty("actual")[0];
        Assert.Equal(8,first.GetProperty("Field").GetInt32());
        Assert.Equal(14,first.GetProperty("Value").GetInt32());
        Assert.Equal(5,first.GetProperty("Offset").GetInt32());
        Assert.Equal(7,first.GetProperty("childField").GetInt32());
        Assert.Equal(18,first.GetProperty("childValue").GetInt32());
        Assert.Equal(new[]{7,13,5,1},first.GetProperty("Values").EnumerateArray().Select(static item=>item.GetInt32()));
        Assert.Equal(13,first.GetProperty("Map").GetProperty("7").GetInt32());
    }

    [Fact]
    public async Task ViewProjectionInitializerPreservesAssignedColumns(){
        const string source="""
            using System;using System.Linq;using System.Text.Json;using Seamless.Database;using Seamless.FLinq;
            namespace ViewInitializerCase{
             internal static partial class DatabaseGenerator;
             public readonly record struct Input(int Id,int Value);
             public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){public Table<int,Input> Inputs=>this.Table<int,Input>(static row=>row.Id);}
             public sealed class Entry{
              [LinqIntercept(true)]private int[] Query(Database db)=>db.InitializedRows.Where(static result=>result.Id==1).Select(static result=>result.Value).ToArray();
              [LinqIntercept(false)]private int[] Reference(Database db)=>db.InitializedRows.Where(static result=>result.Id==1).Select(static result=>result.Value).ToArray();
              public static string Run(){
               using var db=new Database(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
               using(var tx=db.BeginTransaction()){tx.Inputs.Add(new(1,10));tx.Inputs.Add(new(2,20));tx.Commit();}
               var entry=new Entry();return JsonSerializer.Serialize(new{expected=entry.Reference(db),actual=entry.Query(db)});
              }
             }
            }
            namespace ViewInitializerCase.Views{
             public readonly partial record struct InitializedRows(int Id,int Value){
              public static global::System.Collections.Generic.IEnumerable<InitializedRows> Query(global::ViewInitializerCase.Database container)=>container.Inputs.Select(static row=>new InitializedRows(row.Id,row.Value){Id=2});
             }
            }
            """;
        var(result,_)=await RunBoundarySourceAsync(source,"ViewInitializerCase");
        using var observation=JsonDocument.Parse(result);
        Assert.Empty(observation.RootElement.GetProperty("expected").EnumerateArray());
        Assert.Empty(observation.RootElement.GetProperty("actual").EnumerateArray());
    }
}
