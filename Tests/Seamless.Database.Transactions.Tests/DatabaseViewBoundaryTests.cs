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

                public Table<int,Row> Alias=>this.Table<int,Row>(ReadValue,nameof(Inner));
                public static int ReadId(Row row)=>row.Id;public static int ReadValue(Row row)=>row.Value;
            }
            // ビューは生成される入口(Database.IReader)への拡張プロパティ。
            public static class Queries{extension(Database.IReader q){public Seamless.Collections.ISet<Row> Identity=>q.Inner;}}
            public sealed class Entry{
                private int calls;private int Key(Row row){this.calls++;return row.Value;}
                [LinqIntercept(true)] private int[] Query(Database.TypedTransaction tx,int limit)=>tx.Outer.Where(row=>row.Id<limit).Join(tx.Inner,row=>this.Key(row),static row=>row.Id,static(a,b)=>a.Id*100+b.Value).ToArray();
                [LinqIntercept(true)] private int[] ThroughView(Database.TypedTransaction tx,int limit)=>tx.Outer.Where(row=>row.Id<limit).Join(tx.Identity,row=>this.Key(row),static row=>row.Id,static(a,b)=>a.Id*100+b.Value).ToArray();
                public static async Task<string> Run(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,TransactionHistory=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=0,MaxMetadataBytes=1<<20,MaintenanceReserveMemoryBytes=1<<20,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}});
                    _=db.Inner;await using(var seed=await db.BeginTransactionAsync()){for(var i=1;i<=3;i++)seed.Inner.Add(new(i,i*10));for(var i=0;i<80;i++)seed.Outer.Add(new(i,i%3+1));await seed.CommitAsync();}
                    await using var read=await db.BeginReadOnlyTransactionAsync();
                    var keyType=typeof(Database).Assembly.GetTypes().Single(type=>type.Name.StartsWith("DatabaseJoinKey_",StringComparison.Ordinal));var key=keyType.GetMethod("Key",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.CreateDelegate<Func<Row,int>>();var available=((global::Seamless.Transactions.Collections.TransactionCollection<int,Row>)read.Inner).Rows.TryGetJoinIndex(key,out _);
                    var entry=new Entry();var values=entry.Query(read,80);var calls=entry.calls;entry.calls=0;var viewValues=entry.ThroughView(read,80);var viewCalls=entry.calls;await read.CommitAsync();return JsonSerializer.Serialize(new{count=values.Length,calls,indexAvailable=available,values,viewValues,viewCalls});
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
        // 本体に LINQ の無いビュー(q.Inner をそのまま返す)は取り込まず、呼び出しのまま残る。
        Assert.Contains("tx.Identity",code,StringComparison.Ordinal);
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
             [LinqIntercept(true)]private int[] Query(Database.TypedTransaction tx,int limit)=>tx.Outer.Where(row=>this.Filter(row,limit)).Join(tx.Inner,row=>this.Key(row),static row=>row.Id,(a,b)=>this.Result(a,b)).ToArray();
             [LinqIntercept(false)]private int[] Reference(Database.TypedTransaction tx,int limit)=>tx.Outer.Where(row=>this.Filter(row,limit)).Join(tx.Inner,row=>this.Key(row),static row=>row.Id,(a,b)=>this.Result(a,b)).ToArray();
             [LinqIntercept(true)]private int[] QueryEmpty(Database.TypedTransaction tx)=>tx.Outer.Where(row=>this.Filter(row,70)).Join(tx.Empty,row=>this.Key(row),static row=>row.Id,(a,b)=>this.Result(a,b)).ToArray();
             [LinqIntercept(false)]private int[] ReferenceEmpty(Database.TypedTransaction tx)=>tx.Outer.Where(row=>this.Filter(row,70)).Join(tx.Empty,row=>this.Key(row),static row=>row.Id,(a,b)=>this.Result(a,b)).ToArray();
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

    [Fact]
    public async Task SchemaViewReadsOtherSchemasFromTheRootAndExpandsInCallerCode(){
        // 設計：Document/refactoring-designs/20261114-宣言と手続きと索引の統一.md の手順A。
        // スキーマのビューの本体は、受け手によらずルートからたどって表を読む(q.Database.<スキーマ>.<表>)。読む側の r.<スキーマ>.<ビュー> は呼び出し側へ取り込んで動く。
        // 書く側(tx)から LINQ を含むビューを呼ぶ形は対象外：書く側の表は集合ではないので Set の演算が Enumerable の演算へ結び付き直り、FLINQ007 になる(Database の有無によらない以前からの制約)。
        const string source="""
            using System;using System.Linq;using System.Text.Json;using Seamless.Database;using Seamless.FLinq;using Seamless.Linq;
            namespace RootReach{
             internal static partial class DatabaseGenerator;
             public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options);
             public static class Queries{
              extension(Database.IPersonReader q){
               public Seamless.Collections.ISet<Views.Person.Located> Located=>
                q.Database.Person.Address.Join(q.Database.Employee.Job,static address=>address.JobId,static job=>job.Id,static(address,job)=>new Views.Person.Located(address.Id,job.Title));
              }
             }
             public sealed class Entry{
              [LinqIntercept(true)]private int[] ByReader(Database.Reader r)=>r.Person.Located.Select(static row=>row.Id).Order().ToArray();
              [LinqIntercept(false)]private int[] Reference(Database.Reader r)=>r.Person.Located.Select(static row=>row.Id).Order().ToArray();
              [LinqIntercept(true)]private int[] ByLocal(Database.Reader r){var person=r.Person;return person.Located.Select(static row=>row.Id).Order().ToArray();}
              public static string Run(){
               using var db=new Database(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
               using(var tx=db.BeginTransaction()){
                tx.Database.Employee.Job.Add(new(new(10),"clerk"));tx.Database.Employee.Job.Add(new(new(20),"lead"));
                tx.Person.Address.Add(new(new(1),10));tx.Person.Address.Add(new(new(2),20));tx.Person.Address.Add(new(new(3),30));
                tx.Commit();
               }
               var entry=new Entry();int[] expected;int[] actual;int[] local;bool same;bool sameTransaction;bool ownWrite;
               using(var r=db.BeginRead()){expected=entry.Reference(r);actual=entry.ByReader(r);local=entry.ByLocal(r);same=ReferenceEquals(r.Person.Database,r);}
               using(var tx=db.BeginTransaction()){tx.Person.Database.Employee.Job.Add(new(new(30),"new"));sameTransaction=ReferenceEquals(tx.Person.Database,tx);ownWrite=tx.Employee.Job.ContainsKey(new(30));}
               return JsonSerializer.Serialize(new{expected,actual,local,same,sameTransaction,ownWrite});
              }
             }
            }
            namespace RootReach.Tables.Person{public partial struct Address{[Key]public partial int Id{get;init;}public partial int JobId{get;init;}}}
            namespace RootReach.Tables.Employee{public partial struct Job{[Key]public partial int Id{get;init;}public partial string Title{get;init;}}}
            namespace RootReach.Views.Person{public readonly record struct Located(int Id,string Title);}
            """;
        var(result,generated)=await RunBoundarySourceAsync(source,"RootReach");
        using var document=JsonDocument.Parse(result);
        var value=document.RootElement;
        // 他のスキーマ(Employee)の表と結合できる。展開しない実行と同じ結果になる。
        Assert.Equal([1,2],value.GetProperty("actual").EnumerateArray().Select(static item=>item.GetInt32()));
        Assert.Equal(value.GetProperty("expected").EnumerateArray().Select(static item=>item.GetInt32()),value.GetProperty("actual").EnumerateArray().Select(static item=>item.GetInt32()));
        // スキーマの入口をいったん変数に入れてから呼んでも同じ。
        Assert.Equal(value.GetProperty("actual").EnumerateArray().Select(static item=>item.GetInt32()),value.GetProperty("local").EnumerateArray().Select(static item=>item.GetInt32()));
        // スキーマの入口の Database は、元の入口そのもの。書く側では取引そのもので、書ける表に届く。
        Assert.True(value.GetProperty("same").GetBoolean());
        Assert.True(value.GetProperty("sameTransaction").GetBoolean());
        Assert.True(value.GetProperty("ownWrite").GetBoolean());
        // ビューの本体は呼び出し側へ取り込んで展開する。取り込んだ本体の式(受け手 r.Person・person からルートへたどる式)が生成コードに現れる。
        // 生成コードのデバッグ用の実行計画には元の式(r.Person.Located)も記録されるので、呼び出しの文字列の有無では確かめない。
        var code=string.Join(Environment.NewLine,generated);
        Assert.Contains("r.Person.Database.Employee.Job",code,StringComparison.Ordinal);
        Assert.Contains("person.Database.Employee.Job",code,StringComparison.Ordinal);
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
            using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;using Seamless.Database;using Seamless.FLinq;using Seamless.Linq;
            namespace DeclarationBoundary{
             internal static partial class DatabaseGenerator;
             public readonly record struct Row(int Id,int Value);
             public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){public Table<int,Row> Items=>this.Table<int,Row>(static row=>row.Id);}
             // ビューは生成される入口(Database.IReader)への拡張プロパティ。
             public static class Queries{extension(Database.IReader q){public Seamless.Collections.ISet<Views.Positive> Positive=>q.Items.Where(global::DeclarationBoundary.Probe.Include).Select(static row=>new Views.Positive(row.Value));}}
             public static class Probe{
              public static readonly List<string> Events=new();
              [LinqIntercept(false)]public static bool Include(Row row){Events.Add("inner:"+row.Id);return row.Value>0;}
             }
            public sealed class Entry{
              [LinqIntercept(true)]private IEnumerable<int> Query(Database.Reader r)=>r.Positive.Where(static row=>row.Value>1).Select(static row=>row.Value);
              [LinqIntercept(false)]private IEnumerable<int> Reference(Database.Reader r)=>r.Positive.Where(static row=>row.Value>1).Select(static row=>row.Value);
              public static string Run(){
               using var db=new Database(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});using(var tx=db.BeginTransaction()){tx.Items.Add(new(1,1));tx.Items.Add(new(2,1));tx.Items.Add(new(3,3));tx.Commit();}
               using var r=db.BeginRead();
               Probe.Events.Clear();var entry=new Entry();int[] expected;try{expected=entry.Reference(r).Order().ToArray();}catch(Exception error){throw new InvalidOperationException("reference: "+error,error);}var expectedEvents=Probe.Events.Order().ToArray();Probe.Events.Clear();
               var pending=entry.Query(r);var before=Probe.Events.Count;int[] actual;try{actual=pending.Order().ToArray();}catch(Exception error){throw new InvalidOperationException("expanded: "+error,error);}var events=Probe.Events.Order().ToArray();Probe.Events.Clear();int[] again;try{again=pending.Order().ToArray();}catch(Exception error){throw new InvalidOperationException("again: "+error,error);}var againEvents=Probe.Events.Order().ToArray();
               return JsonSerializer.Serialize(new{expected,actual,again,expectedEvents,events,againEvents,before});
              }
             }
            }
            namespace DeclarationBoundary.Views{
             public readonly record struct Positive(int Value);
            }
            """;
        var(result,generated)=await RunBoundarySourceAsync(source,"DeclarationBoundary");
        using var document=JsonDocument.Parse(result);
        var value=document.RootElement;
        // 呼び出した時点では評価しない(列挙するたびに評価する)。
        Assert.Equal(0,value.GetProperty("before").GetInt32());
        // ビューは集合: Select の結果の重複(Value=1 が2行)を除く。傍受しない実行(Seamless.Linq.Set)と同じ結果になる。
        Assert.Equal([3],value.GetProperty("actual").EnumerateArray().Select(static item=>item.GetInt32()));
        Assert.Equal(value.GetProperty("expected").EnumerateArray().Select(static item=>item.GetInt32()),value.GetProperty("actual").EnumerateArray().Select(static item=>item.GetInt32()));
        Assert.Equal(value.GetProperty("actual").EnumerateArray().Select(static item=>item.GetInt32()),value.GetProperty("again").EnumerateArray().Select(static item=>item.GetInt32()));
        Assert.Equal(value.GetProperty("expectedEvents").EnumerateArray().Select(static item=>item.GetString()),value.GetProperty("events").EnumerateArray().Select(static item=>item.GetString()));
        Assert.Equal(value.GetProperty("events").EnumerateArray().Select(static item=>item.GetString()),value.GetProperty("againEvents").EnumerateArray().Select(static item=>item.GetString()));
        // ビューの本体は呼び出し側へ取り込んで展開する(ビューの呼び出しは生成コードに残らない)。
        var code=string.Join(Environment.NewLine,generated);
        Assert.DoesNotMatch(@"\br\.Positive",code);
    }
}
