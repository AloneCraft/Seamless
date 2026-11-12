using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Fact]
    public async Task SnapshotBoundViewKeepsItsSetBoundaryWhileDirectRowsUseThePrimaryKeyIndex(){
        const string source="""
            using System;using System.Linq;using System.Threading.Tasks;using System.Text.Json;using Seamless.Database;using Seamless.FLinq;using Seamless.Transactions;using Seamless.Linq;
            namespace TypedViewBoundary;
            internal static partial class DatabaseGenerator;
            public readonly record struct Row(int Id,int Value);
            public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){
                public Table<int,Row> Outer=>this.Table<int,Row>(ReadId);
                public Table<int,Row> Inner=>this.Table<int,Row>(static row=>row.Id);
                public View<Row> Identity(Database.TypedTransaction tx)=>this.GeneratedView(()=>tx.Inner.Rows,nameof(Identity));
                public Table<int,Row> Alias=>this.Table<int,Row>(ReadValue,nameof(Inner));
                public static int ReadId(Row row)=>row.Id;public static int ReadValue(Row row)=>row.Value;
            }
            public sealed class Entry{
                private int calls;private int Key(Row row){this.calls++;return row.Value;}
                [LinqIntercept(true)] private int[] Query(Database.TypedTransaction tx,int limit)=>tx.Outer.Rows.Where(row=>row.Id<limit).Join(tx.Inner.Rows,row=>this.Key(row),static row=>row.Id,static(a,b)=>a.Id*100+b.Value).ToArray();
                [LinqIntercept(true)] private int[] ThroughView(Database db,Database.TypedTransaction tx,int limit)=>tx.Outer.Rows.Where(row=>row.Id<limit).Join(db.Identity(tx),row=>this.Key(row),static row=>row.Id,static(a,b)=>a.Id*100+b.Value).ToArray();
                public static async Task<string> Run(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,TransactionHistory=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=0,MaxMetadataBytes=1<<20,MaintenanceReserveMemoryBytes=1<<20,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}});
                    _=db.Inner;await using(var seed=await db.BeginTransactionAsync()){for(var i=1;i<=3;i++)seed.Inner.Add(new(i,i*10));for(var i=0;i<80;i++)seed.Outer.Add(new(i,i%3+1));await seed.CommitAsync();}
                    await using var read=await db.BeginReadOnlyTransactionAsync();
                    var keyType=typeof(Database).Assembly.GetTypes().Single(type=>type.Name.StartsWith("DatabaseJoinKey_",StringComparison.Ordinal));var key=keyType.GetMethod("Key",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<Func<Row,int>>();var available=read.Inner.Rows.TryGetJoinIndex(key,out _);
                    var entry=new Entry();var values=entry.Query(read,80);var calls=entry.calls;entry.calls=0;var viewValues=entry.ThroughView(db,read,80);var viewCalls=entry.calls;await read.CommitAsync();return JsonSerializer.Serialize(new{count=values.Length,calls,indexAvailable=available,values,viewValues,viewCalls});
                }
            }
            """;
        var(result,generated)=await RunBoundarySourceAsync(source,"TypedViewBoundary");
        using var document=JsonDocument.Parse(result);
        var value=document.RootElement;
        Assert.True(value.GetProperty("indexAvailable").GetBoolean());
        Assert.Equal(80,value.GetProperty("calls").GetInt32());
        Assert.Equal(80,value.GetProperty("viewCalls").GetInt32());
        Assert.Equal(value.GetProperty("values").EnumerateArray().Select(static item=>item.GetInt32()),
            value.GetProperty("viewValues").EnumerateArray().Select(static item=>item.GetInt32()));
        var code=string.Join(Environment.NewLine,generated);
        Assert.Single(Regex.Matches(code,@"(?m)^\s*\w+\s*=\s*\w+\.TryGetJoinIndex\("));
        Assert.Contains("db.Identity(tx)",code,StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransactionJoinKeepsReferenceEvaluationOrderAndExceptionsAcrossIndexFallback(){
        const string source="""
            using System;using System.Linq;using System.Threading.Tasks;using System.Collections.Generic;using System.Text.Json;using Seamless.Database;using Seamless.FLinq;using Seamless.Transactions;
            namespace JoinEvaluationBoundary;
            internal static partial class DatabaseGenerator;
            public readonly record struct Row(int Id,int Value);
            public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){
             public Table<int,Row> Outer=>this.Table<int,Row>(ReadId);
             public Table<int,Row> Inner=>this.Table<int,Row>(static row=>row.Id);
             public Table<int,Row> Empty=>this.Table<int,Row>(static row=>row.Id);
             public static int ReadId(Row row)=>row.Id;
            }
            public sealed class Entry{
             private readonly string mode;private readonly List<string> events=new();private int filters,keys,results;
             private Entry(string mode){this.mode=mode;}
             private bool Filter(Row row,int limit){this.events.Add("F"+row.Id);this.filters++;if(this.mode=="filter"&&this.filters==65)throw new InvalidOperationException("filter65");return row.Id<limit;}
             private int Key(Row row){this.events.Add("K"+row.Id);this.keys++;if(this.mode=="key"&&this.keys==65||this.mode=="emptyinner")throw new InvalidOperationException("key65");return row.Value;}
             private int Result(Row outer,Row inner){this.events.Add("R"+outer.Id);this.results++;if(this.mode=="result"&&this.results==65)throw new InvalidOperationException("result65");return outer.Id*100+inner.Value;}
             [LinqIntercept(true)]private int[] Query(Database.TypedTransaction tx,int limit)=>tx.Outer.Rows.Where(row=>this.Filter(row,limit)).Join(tx.Inner.Rows,row=>this.Key(row),static row=>row.Id,(a,b)=>this.Result(a,b)).ToArray();
             [LinqIntercept(false)]private int[] Reference(Database.TypedTransaction tx,int limit)=>tx.Outer.Rows.Where(row=>this.Filter(row,limit)).Join(tx.Inner.Rows,row=>this.Key(row),static row=>row.Id,(a,b)=>this.Result(a,b)).ToArray();
             [LinqIntercept(true)]private int[] QueryEmpty(Database.TypedTransaction tx)=>tx.Outer.Rows.Where(row=>this.Filter(row,70)).Join(tx.Empty.Rows,row=>this.Key(row),static row=>row.Id,(a,b)=>this.Result(a,b)).ToArray();
             [LinqIntercept(false)]private int[] ReferenceEmpty(Database.TypedTransaction tx)=>tx.Outer.Rows.Where(row=>this.Filter(row,70)).Join(tx.Empty.Rows,row=>this.Key(row),static row=>row.Id,(a,b)=>this.Result(a,b)).ToArray();
             private object Observe(Database.TypedTransaction tx,bool expanded){int[] values=[];string? error=null;try{if(this.mode=="emptyinner"){values=expanded?this.QueryEmpty(tx):this.ReferenceEmpty(tx);}else{var limit=this.mode=="emptyouter"?0:70;values=expanded?this.Query(tx,limit):this.Reference(tx,limit);}}catch(InvalidOperationException exception){error=exception.Message;}return new{values,error,events=this.events.ToArray(),this.filters,this.keys,this.results};}
             public static async Task<string> Run(){
              using var db=new Database(DatabaseContainerOptions.InMemory with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,TransactionHistory=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=0,MaxMetadataBytes=1<<20,MaintenanceReserveMemoryBytes=1<<20,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}});
              _=db.Inner;_=db.Empty;await using(var seed=await db.BeginTransactionAsync()){seed.Inner.Add(new(7,31));for(var i=0;i<70;i++)seed.Outer.Add(new(i,7));await seed.CommitAsync();}
              await using var read=await db.BeginReadOnlyTransactionAsync();var observations=new List<object>();foreach(var mode in new[]{"normal","filter","key","result","emptyinner","emptyouter"}){var expected=new Entry(mode).Observe(read,false);var actual=new Entry(mode).Observe(read,true);observations.Add(new{mode,expected,actual,equal=JsonSerializer.Serialize(expected)==JsonSerializer.Serialize(actual)});}await read.CommitAsync();return JsonSerializer.Serialize(observations);
             }
            }
            """;
        var(result,generated)=await RunBoundarySourceAsync(source,"JoinEvaluationBoundary");
        using var document=JsonDocument.Parse(result);
        Assert.Equal(6,document.RootElement.GetArrayLength());
        foreach(var observation in document.RootElement.EnumerateArray()){
            Assert.True(observation.GetProperty("equal").GetBoolean(),observation.GetRawText());
        }
        Assert.Contains(generated,static code=>Regex.IsMatch(code,@"(?m)^\s*\w+\s*=\s*\w+\.TryGetJoinIndex\("));
    }

    private static async Task<(string Result,string[] Generated)> RunBoundarySourceAsync(string source,string name,string? generatedEvidencePath=null){
        var(output,generated)=Generate(source,name,generatedEvidencePath:generatedEvidencePath);
        using var image=new MemoryStream();
        var emitted=output.Emit(image);
        Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));
        image.Position=0;
        var context=new AssemblyLoadContext(name+Guid.NewGuid(),isCollectible:true);
        context.Resolving+=static(_,requested)=>AssemblyLoadContext.Default.LoadFromAssemblyName(requested);
        try{
            var assembly=context.LoadFromStream(image);
            var run=assembly.GetType(name+".Entry",throwOnError:true)!.GetMethod("Run",BindingFlags.Public|BindingFlags.Static)!;
            var value=run.Invoke(null,null)!;
            string result;
            if(value is Task<string> pending){result=await pending;}
            else{result=Assert.IsType<string>(value);}
            return(result,generated);
        }finally{
            context.Unload();
        }
    }

    [Fact]
    public async Task DeclaredViewBodyRunsInCallerCodeAndKeepsSetSemantics(){
        const string source="""
            using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;using Seamless.Database;using Seamless.FLinq;
            namespace DeclarationBoundary{
             internal static partial class DatabaseGenerator;
             public readonly record struct Row(int Id,int Value);
             public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){public Table<int,Row> Items=>this.Table<int,Row>(static row=>row.Id);}
             public static class Probe{
              public static readonly List<string> Events=new();public static int Receivers;
              [LinqIntercept(false)]public static Database Receiver(Database db){Receivers++;return db;}
              [LinqIntercept(false)]public static bool Include(Row row){Events.Add("inner:"+row.Id);return row.Value>0;}
             }
             public sealed class Entry{
              [LinqIntercept(true)]private IEnumerable<int> Query(Database db)=>Probe.Receiver(db).Positive.Where(static row=>row.Value>1).Select(static row=>row.Value);
              [LinqIntercept(false)]private IEnumerable<int> Reference(Database db)=>Probe.Receiver(db).Positive.Where(static row=>row.Value>1).Select(static row=>row.Value);
              public static string Run(){
               using var db=new Database(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});using(var tx=db.BeginTransaction()){tx.Items.Add(new(1,1));tx.Items.Add(new(2,1));tx.Items.Add(new(3,3));tx.Commit();}
               Probe.Events.Clear();Probe.Receivers=0;var entry=new Entry();var expected=entry.Reference(db).ToArray();var expectedEvents=Probe.Events.ToArray();Probe.Events.Clear();Probe.Receivers=0;
               var pending=entry.Query(db);var before=Probe.Events.Count;var receiversBefore=Probe.Receivers;var actual=pending.ToArray();var events=Probe.Events.ToArray();var receivers=Probe.Receivers;Probe.Events.Clear();var again=pending.ToArray();var againEvents=Probe.Events.ToArray();
               return JsonSerializer.Serialize(new{expected,actual,again,expectedEvents,events,againEvents,before,receiversBefore,receivers});
              }
             }
            }
            namespace DeclarationBoundary.Views{
             public readonly partial record struct Positive(int Value){public static global::System.Collections.Generic.IEnumerable<Positive> Query(global::DeclarationBoundary.Database container)=>container.Items.Where(global::DeclarationBoundary.Probe.Include).Select(static row=>new Positive(row.Value));}
            }
            """;
        var(result,generated)=await RunBoundarySourceAsync(source,"DeclarationBoundary");
        using var document=JsonDocument.Parse(result);
        var value=document.RootElement;
        Assert.Equal(0,value.GetProperty("before").GetInt32());
        Assert.Equal(1,value.GetProperty("receiversBefore").GetInt32());
        Assert.Equal(1,value.GetProperty("receivers").GetInt32());
        Assert.Equal(value.GetProperty("expected").EnumerateArray().Select(static item=>item.GetInt32()),value.GetProperty("actual").EnumerateArray().Select(static item=>item.GetInt32()));
        Assert.Equal(value.GetProperty("actual").EnumerateArray().Select(static item=>item.GetInt32()),value.GetProperty("again").EnumerateArray().Select(static item=>item.GetInt32()));
        Assert.Equal(value.GetProperty("expectedEvents").EnumerateArray().Select(static item=>item.GetString()),value.GetProperty("events").EnumerateArray().Select(static item=>item.GetString()));
        Assert.Equal(value.GetProperty("events").EnumerateArray().Select(static item=>item.GetString()),value.GetProperty("againEvents").EnumerateArray().Select(static item=>item.GetString()));
        var code=string.Join(Environment.NewLine,generated);
        Assert.Contains(".ExpandDefinition<",code,StringComparison.Ordinal);
        Assert.Matches(@"(?m)^\s*private static global::System.Collections.Generic.IEnumerable<[^\r\n]+> \w+_ViewDefinition\(",code);
    }

    private sealed class ViewDefinitionOwner():DatabaseContainer(DatabaseContainerOptions.InMemory);

    [Fact]
    public void GeneratedViewsWithTheSameNameKeepSeparateDefinitions(){
        using var owner=new ViewDefinitionOwner();
        var inner=owner.GeneratedView<int>(()=>new[]{2,2},"Shared");
        var outer=owner.GeneratedView<int>(()=>inner.Select(value=>value+1),"Shared");
        Assert.NotSame(inner,outer);
        Assert.Equal(new[]{3},outer.ToArray());
        Assert.Equal(new[]{3},outer.ExpandDefinition<ViewDefinitionOwner>(_=>inner.ExpandDefinition<ViewDefinitionOwner>(_=>new[]{2,2}).Select(value=>value+1)).ToArray());
        Assert.Equal(0,owner.ResourceUsage.MemoryBytes);
        Assert.Equal(0,owner.ResourceUsage.TemporaryStorageBytes);
    }

    [Fact]
    public void DefinitionCycleGuardUnwindsAfterFailureAndBeforeYield(){
        using var owner=new ViewDefinitionOwner();
        var cycle=true;
        View<int> loop=null!;
        loop=owner.GeneratedView<int>(()=>cycle?loop:new[]{9},"Loop");
        Assert.Throws<InvalidOperationException>(()=>loop.ToArray());
        cycle=false;
        Assert.Equal(new[]{9},loop.ToArray());
        using(var first=loop.GetEnumerator()){
            Assert.True(first.MoveNext());
            using var second=loop.GetEnumerator();
            Assert.True(second.MoveNext());
            Assert.Equal(9,first.Current);
            Assert.Equal(9,second.Current);
            Assert.False(first.MoveNext());
            Assert.False(second.MoveNext());
        }
        Assert.Equal(0,owner.ResourceUsage.MemoryBytes);
        Assert.Equal(0,owner.ResourceUsage.TemporaryStorageBytes);
    }
}
