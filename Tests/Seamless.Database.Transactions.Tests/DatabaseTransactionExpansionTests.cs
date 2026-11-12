using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.Loader;
using Seamless.Database.Generator;
using Seamless.FLinq.Generator;
namespace Seamless.Database.Transactions.Tests;

// Roslynを呼ぶハーネス自体を展開せず、文字列内の利用者コードだけをdriverで生成・実行する。
[global::Seamless.FLinq.LinqIntercept(false)]
public sealed partial class DatabaseTransactionExpansionTests{
    [Fact]
    public async Task AsyncQueryKeepsAwaitAndQualifiesThrownTypes(){
        const string source="""
            using System;
            using System.Linq;
            using System.Threading.Tasks;
            using Seamless.FLinq;
            namespace AsyncThrowCase;
            public sealed class Entry{
                public static Task<long> Run(int[] values)=>Query(values);
                [LinqIntercept(true)]
                public static async Task<long> Query(int[] values){
                    await Task.Yield();
                    if(values.Length==0){throw new InvalidOperationException("empty input");}
                    return values.LongCount(static value=>value>0);
                }
            }
            """;
        var(output,generated)=Generate(source,"AsyncThrowCase");
        Assert.Contains(generated,static text=>text.Contains("public static async global::System.Threading.Tasks.Task<long>",StringComparison.Ordinal));
        using var image=new MemoryStream();
        var emitted=output.Emit(image);
        Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));
        image.Position=0;
        var load=new AssemblyLoadContext("async-throw-"+Guid.NewGuid(),isCollectible:true);
        load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var run=load.LoadFromStream(image).GetType("AsyncThrowCase.Entry",true)!.GetMethod("Run")!;
            Assert.Equal(2,await(Task<long>)run.Invoke(null,[new[]{-1,1,2}])!);
            var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>(Task<long>)run.Invoke(null,[Array.Empty<int>()])!);
            Assert.Equal("empty input",error.Message);
        }finally{load.Unload();}
    }

    [Fact]
    public async Task PrimaryKeyJoinPreservesOrderAndEvaluatesOuterKeyOnceWhenSwitchingToLookup(){
        const string source="""
            using System;
            using System.Linq;
            using System.Threading.Tasks;
            using Seamless.Database;
            using Seamless.FLinq;
            using Seamless.Linq;
            using Seamless.Transactions;
            namespace PrimaryKeyJoinCase;
            internal static partial class DatabaseGenerator;
            public readonly record struct Row(int Id,int Value);
            public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){
                public Table<int,Row> Outer=>this.Table<int,Row>(ReadId);
                public Table<int,Row> Inner=>this.Table<int,Row>(static row=>row.Id);
                public Table<int,Row> Alias=>this.Table<int,Row>(ReadValue,nameof(Inner));
                public Table<int,Row> Empty=>this.Table<int,Row>(ReadId);
                public static int ReadId(Row row)=>row.Id;
                public static int ReadValue(Row row)=>row.Value;
            }
            public sealed class Entry{
                private int calls;
                private int OuterKey(Row row){this.calls++;return row.Value;}
                [LinqIntercept(true)]
                private (int,int)[] Query(Database.TypedTransaction tx,int limit)=>
                    tx.Outer.Rows.Where(row=>row.Id<limit).Join(tx.Inner.Rows,row=>this.OuterKey(row),static row=>row.Id,
                        static (outer,inner)=>(outer.Id,inner.Value)).ToArray();
                [LinqIntercept(true)]
                private (int,int)[] QueryEmpty(Database.TypedTransaction tx)=>
                    tx.Outer.Rows.Join(tx.Empty.Rows,row=>this.OuterKey(row),Database.ReadId,
                        static (outer,inner)=>(outer.Id,inner.Value)).ToArray();
                [LinqIntercept(true)]
                private (int,int)[] QueryAnchored(Database.TypedTransaction tx)=>
                    tx.Outer.Rows.Join(tx.Inner.Rows.Anchor,row=>this.OuterKey(row),Database.ReadId,
                        static (outer,inner)=>(outer.Id,inner.Value)).ToArray();
                public static async Task<bool> Run(){
                    var generatedKey=typeof(Database).Assembly.GetTypes().Single(type=>type.Name.StartsWith("DatabaseJoinKey_",StringComparison.Ordinal));
                    var keyMethod=generatedKey.GetMethod("Key",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
                    var key=keyMethod.CreateDelegate<Func<Row,int>>();
                    foreach(var aliasFirst in new[]{false,true}){
                        using var db=new Database(DatabaseContainerOptions.InMemory with{
                            TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                            RecordSerialization=DatabaseRecordSerialization.CanonicalV1,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,
                            TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,
                            TransactionHistory=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=0,MaxMetadataBytes=1<<20,
                                MaintenanceReserveMemoryBytes=1<<20,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)}
                        });
                        _=aliasFirst?db.Alias:db.Inner;
                        await using(var seed=await db.BeginTransactionAsync()){
                            seed.Inner.Add(new(7,31));
                            for(var i=0;i<65;i++)seed.Outer.Add(new(i,i%3==0?-1:7));
                            await seed.CommitAsync();
                        }
                        await using var read=await db.BeginReadOnlyTransactionAsync();
                        if(read.Inner.Rows.TryGetJoinIndex(key,out _)==aliasFirst)return false;
                        var entry=new Entry();
                        if(entry.QueryEmpty(read).Length!=0||entry.calls!=0)return false;
                        foreach(var limit in new[]{0,64,65}){
                            var expected=read.Outer.Rows.Where(row=>row.Id<limit&&row.Value==7).Select(row=>(row.Id,31)).ToArray();
                            entry.calls=0;
                            if(!entry.Query(read,limit).SequenceEqual(expected)||entry.calls!=limit)return false;
                        }
                        entry.calls=0;
                        var anchored=read.Outer.Rows.Where(row=>row.Value==7).Select(row=>(row.Id,31)).ToArray();
                        if(!entry.QueryAnchored(read).SequenceEqual(anchored)||entry.calls!=65)return false;
                        await read.CommitAsync();
                    }
                    return true;
                }
            }
            """;
        var(output,generated)=Generate(source,"PrimaryKeyJoinCase",debug:false);
        Assert.Equal(2,generated.Sum(static text=>global::System.Text.RegularExpressions.Regex.Matches(text,@"(?m)^\s*\w+\s*=\s*\w+\.TryGetJoinIndex\(").Count));
        Assert.Equal(2,generated.Sum(static text=>global::System.Text.RegularExpressions.Regex.Matches(text,@"(?m)^\s*if\(\w+&&\w+==64\)").Count));
        using var image=new MemoryStream();
        var emit=output.Emit(image);
        Assert.True(emit.Success,string.Join(Environment.NewLine,emit.Diagnostics));
        image.Position=0;
        var load=new AssemblyLoadContext("primary-key-join-"+Guid.NewGuid(),isCollectible:true);
        load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var entry=load.LoadFromStream(image).GetType("PrimaryKeyJoinCase.Entry",throwOnError:true)!;
            Assert.True(await (Task<bool>)entry.GetMethod("Run")!.Invoke(null,null)!);
        }finally{load.Unload();}
    }

    [Fact]
    public void OrdinaryDatabaseTablesUseTheDatabaseRowsProviderAndKeepResults(){
        const string source="""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Database;
            using Seamless.Database.Transactions.Tests;
            namespace OrdinaryRowsCase;
            [LinqIntercept(true)]public sealed class Entry{
                public static decimal Query(BankDatabase db){
                    var array=db.Accounts.Where(static row=>row.Id>0).ToArray();
                    var list=db.Accounts.Select(static row=>row.Balance).ToList();
                    var map=db.Accounts.ToDictionary(static row=>row.Id);
                    if(array.Length!=2||list.Count!=2||map.Count!=2||db.Accounts.Count()!=2){throw new Exception("Materialization mismatch");}
                    return db.Accounts.Where(static row=>row.Id>0).Sum(static row=>row.Balance);
                }
                public static decimal Run(){
                    using var db=new BankDatabase(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536});
                    using(var seed=db.BeginTransaction()){seed.Accounts.Add(new Account(1,100));seed.Accounts.Add(new Account(2,200));seed.Commit();}
                    // 取引の未確定の変更は db.X(取引の外の読み取り)からは見えない。
                    using(var tx=db.BeginTransaction()){
                        tx.Accounts[1]=new Account(1,110);tx.Accounts.Remove(2);tx.Accounts.Add(new Account(3,400));tx.Rollback();
                    }
                    if(Count(db)!=2){throw new Exception("Rolled back rows are visible");}
                    return Query(db);
                }
                private static long Count(BankDatabase db)=>db.Accounts.LongCount();
            }
            """;
        var(output,generated)=Generate(source,"OrdinaryRowsCase");
        Assert.Contains(generated,static text=>text.Contains("provider=DatabaseRows",StringComparison.Ordinal)&&text.Contains(".Iterate(ref",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("GetQueryRows()",StringComparison.Ordinal)&&text.Contains("CreateResultBuffer",StringComparison.Ordinal));
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));image.Position=0;
        var context=new AssemblyLoadContext("OrdinaryRowsCase",isCollectible:true);try{var assembly=context.LoadFromStream(image);Assert.Equal(300m,assembly.GetType("OrdinaryRowsCase.Entry")!.GetMethod("Run")!.Invoke(null,null));}finally{context.Unload();}
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedTransactionQueryIsExpandedInOneCompilation(bool versioned){
        var source="""
            using System.Linq;
            using System;
            using System.Threading.Tasks;
            using Seamless.FLinq;
            using Seamless.Database;
            using Seamless.Transactions;
            using Account=Seamless.Database.Transactions.Tests.Account;
            namespace ExpansionCase;
            internal static partial class DatabaseGenerator;
            public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){
                public Table<int,Account> Accounts=>this.Table<int,Account>(static row=>row.Id);
                public Table<int,Account> Orders=>this.Table<int,Account>(static row=>row.Id);
            }
            [LinqIntercept(true)]
            public sealed class Report{
                public decimal Total(Database.TypedTransaction tx){
                    using var probe=new ScopeProbe();
                    var array=tx.Accounts.Rows.ToArray();
                    var list=tx.Accounts.Rows.Select(static row=>row.Balance).ToList();
                    var dictionary=tx.Accounts.Rows.ToDictionary(static row=>row.Id);
                    if(array.Length!=1||list.Count!=1||dictionary[1].Balance!=array[0].Balance){throw new InvalidOperationException("Materialized results disagree.");}
                    if(tx.Accounts.Rows.Count()!=1||!tx.Accounts.Rows.Any()||!tx.Accounts.Rows.Contains(tx.Accounts[1])){throw new InvalidOperationException("Snapshot membership is inconsistent.");}
                    return tx.Accounts.Sum(static row=>row.Balance);
                }
            }
            public sealed class ScopeProbe:IDisposable{
                public static int Active;
                public ScopeProbe(){Active++;}
                public void Dispose(){Active--;}
            }
            public sealed class Entry{
                public decimal Execute(Database.TypedTransaction tx)=>new Report().Total(tx);
                public Task<decimal> Call(bool fail)=>this.Run(fail);
                public Task<decimal> CallConflict()=>this.Conflict();
                [LinqIntercept(true)]
                public async Task<decimal> Conflict(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{
                        IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20
                    });
                    await using(var seed=await db.BeginTransactionAsync()){seed.Accounts.Add(new Account(1,100));await seed.CommitAsync();}
                    await using(var tx=await db.BeginTransactionAsync()){
                        PlaceOrder(tx);UsePoints(tx,false);
                        Task competing;
                        using(System.Threading.ExecutionContext.SuppressFlow()){
                            competing=Task.Run(async()=>{await using var writer=await db.BeginTransactionAsync();writer.Accounts[1]=new Account(1,80);await writer.CommitAsync();});
                        }
                        await competing;
                        try{await tx.CommitAsync();throw new Exception("The composed transaction did not conflict.");}
                        catch(TransactionAbortedException error)when(error.Reason==TransactionAbortReason.Conflict){}
                    }
                    await using var read=await db.BeginReadOnlyTransactionAsync();
                    if(read.Orders.ContainsKey(1)){throw new Exception("PlaceOrder survived the conflict of the composed transaction.");}
                    return read.Accounts[1].Balance;
                }
                [LinqIntercept(true)]
                public async Task<decimal> Run(bool fail){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{
                        IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20
                    });
                    await using(var seed=await db.BeginTransactionAsync()){
                        seed.Accounts.Add(new Account(1,100));
                        await seed.CommitAsync();
                    }
                    try{
                        await using var tx=await db.BeginTransactionAsync();
                        PlaceOrder(tx);
                        UsePoints(tx,fail);
                        if(tx.Accounts.Sum(static row=>row.Balance)!=70){throw new InvalidOperationException("Own changes are missing.");}
                        await tx.CommitAsync();
                    }catch(InvalidOperationException)when(fail){ }
                    await using var read=await db.BeginTransactionAsync();
                    var result=this.Execute(read);
                    var expectedOrders=1;if(fail){expectedOrders=0;}
                    if(read.Orders.Count()!=expectedOrders){throw new InvalidOperationException("Composed order and points did not share the outcome.");}
                    if(ScopeProbe.Active!=0){throw new InvalidOperationException("Generated query lost using lifetime.");}
                    return result;
                }
                private static void PlaceOrder(Database.TypedTransaction tx){
                    tx.Orders.Add(new Account(1,25));
                    var row=tx.Accounts[1];
                    tx.Accounts[1]=row with{Balance=row.Balance-25};
                }
                private static void UsePoints(Database.TypedTransaction tx,bool fail){
                    var row=tx.Accounts[1];
                    tx.Accounts[1]=row with{Balance=row.Balance-5};
                    if(fail){throw new InvalidOperationException("Cancel both business operations.");}
                }
            }
            """;
        if(versioned){source=source.Replace("DatabaseContainerOptions.InMemory with{","DatabaseContainerOptions.InMemory with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=262144,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)},",StringComparison.Ordinal);}
        var(output,generated)=Generate(source,"ExpansionCase");
        Assert.Contains(generated,static text=>text.Contains("InterceptsLocation",StringComparison.Ordinal)
            &&text.Contains("ExpansionCase",StringComparison.Ordinal)
            &&text.Contains("Total",StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"database-flinq-"+versioned+".g.cs"),string.Join(Environment.NewLine,generated));
        Assert.Contains(generated,static text=>text.Contains(".Iterate(ref",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains(".CreateResultBuffer<",StringComparison.Ordinal)&&text.Contains(".CreateDictionaryBuffer<",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("provider=TransactionRows; terminal=Sum",StringComparison.Ordinal));
        using var image=new MemoryStream();
        var emitted=output.Emit(image);
        Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));
        image.Position=0;
        var load=new AssemblyLoadContext("database-expansion-"+Guid.NewGuid(),isCollectible:true);
        load.Resolving+=static (_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);
            var type=assembly.GetType("ExpansionCase.Entry",throwOnError:true)!;
            var entry=Activator.CreateInstance(type)!;
            var run=type.GetMethod("Call")!;
            Assert.Equal(70,await (Task<decimal>)run.Invoke(entry,[false])!);
            Assert.Equal(100,await (Task<decimal>)run.Invoke(entry,[true])!);
            if(versioned){Assert.Equal(80,await(Task<decimal>)type.GetMethod("CallConflict")!.Invoke(entry,null)!);}
        }finally{
            load.Unload();
        }
    }
    private static (Compilation Output,string[] Sources) Generate(string source,string name,bool debug=true,string? generatedEvidencePath=null,Action<IReadOnlyList<Diagnostic>>? observeDiagnostics=null){
        var symbols=Array.Empty<string>();if(debug){symbols=["DEBUG"];}
        var parse=new CSharpParseOptions(LanguageVersion.Preview).WithPreprocessorSymbols(symbols).WithFeatures(
            [new KeyValuePair<string,string>("InterceptorsPreviewNamespaces","Intercept"),new KeyValuePair<string,string>("InterceptorsNamespaces","Intercept")]);
        var paths=((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")??string.Empty).Split(Path.PathSeparator).Where(File.Exists)
            .Append(typeof(Account).Assembly.Location).Append(typeof(FactAttribute).Assembly.Location).Append(typeof(Assert).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase);
        var references=paths.Select(static path=>(MetadataReference)MetadataReference.CreateFromFile(path));
        var compilation=CSharpCompilation.Create(name,[CSharpSyntaxTree.ParseText(source,parse,path:name+".cs")],references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable));
        GeneratorDriver driver=CSharpGeneratorDriver.Create(
            [new DatabaseSchemaGenerator().AsSourceGenerator(),new DatabaseContainerGenerator().AsSourceGenerator(),new FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:parse);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);
        Assert.Empty(diagnostics.Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error));
        var generated=driver.GetRunResult().Results.SelectMany(static result=>result.GeneratedSources)
            .Where(static source=>source.HintName is not "InterceptsLocationAttribute.g.cs" and not "LinqRewriteAttributes.g.cs").Select(static source=>source.SourceText.ToString()).ToArray();
        if(generatedEvidencePath is not null){File.WriteAllText(generatedEvidencePath,string.Join(Environment.NewLine,generated));}
        observeDiagnostics?.Invoke(diagnostics.Concat(output.GetDiagnostics()).ToArray());
        Assert.Empty(output.GetDiagnostics().Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error));
        return(output,generated);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutionCommentsKeepArrayAndListBehaviorAndDebugMetadataIsConditional(bool debug){
        const string source="""
            using System;
            using System.Linq;
            using System.Collections.Generic;
            using Seamless.FLinq;
            namespace AnnotationCase;
            [LinqIntercept(true)]
            public sealed class Queries{
                public static int Defaults(System.Threading.CancellationToken token=default,DayOfWeek day=DayOfWeek.Wednesday){token.ThrowIfCancellationRequested();return (int)day;}
                public int[] ArrayQuery(int[] rows){if(Defaults()!=3){throw new Exception("Default arguments changed.");}return rows.Where(static value=>value!=1).Select(static value=>value*10).ToArray();}
                public int[] ListQuery(List<int> rows)=>rows.Where(static value=>value>1).ToArray();
                public int Early(int[] rows)=>rows.Where(static value=>10/value>0).First();
            }
            public static class Entry{
                public static int[] Run(){
                    var query=new Queries();
                    if(query.Early([2,0])!=2){throw new Exception("Early termination changed.");}
                    return query.ArrayQuery([3,1,2]).Concat(query.ListQuery([0,1,2,3])).ToArray();
                }
            }
            """;
        var(output,generated)=Generate(source,"AnnotationCase",debug);
        var annotated=string.Join(Environment.NewLine,generated);
        foreach(var category in new[]{"[元ソース]","[入力識別]","generator-mvid=","[構文設定]","[コンパイル設定]","[最適化設定]","[手順 ","[並列判断]","[例外/取消]","[資源契約]","GeneratedCodeAttribute"}){Assert.Contains(category,annotated,StringComparison.Ordinal);}
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));image.Position=0;
        var load=new AssemblyLoadContext("annotation-case-"+Guid.NewGuid(),isCollectible:true);load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);
            Assert.Equal(new[]{30,20,2,3},(int[])assembly.GetType("AnnotationCase.Entry",true)!.GetMethod("Run")!.Invoke(null,null)!);
            var debugFields=assembly.GetTypes().SelectMany(static type=>type.GetFields(global::System.Reflection.BindingFlags.Static|global::System.Reflection.BindingFlags.NonPublic|global::System.Reflection.BindingFlags.Public))
                .Where(static field=>field.Name.Contains("DebugPlan",StringComparison.Ordinal)||field.Name.Contains("DebugConstant",StringComparison.Ordinal)).ToArray();
            if(debug){Assert.NotEmpty(debugFields);Assert.All(debugFields,static field=>Assert.True(field.IsLiteral));}
            else{Assert.Empty(debugFields);}
        }finally{load.Unload();}
    }
    [Fact]
    public async Task AsyncTransactionOwnerExpandsItsInlineQueryAndKeepsReadonlyAndAsyncDisposal(){
        const string source="""
            using System;
            using System.Linq;
            using System.Threading.Tasks;
            using Seamless.FLinq;
            using Seamless.Database;
            using Seamless.Transactions;
            using Account=Seamless.Database.Transactions.Tests.Account;
            namespace AsyncOwnerCase;
            internal static partial class DatabaseGenerator;
            public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){public Table<int,Account> Accounts=>this.Table<int,Account>(static row=>row.Id);}
            [LinqIntercept(true)]
            public sealed class Reader{
                public decimal[] Group(Database.TypedTransaction tx)=>tx.Accounts.GroupBy(static row=>row.Id%3,static row=>row.Balance).Select(static group=>group.Sum()).ToArray();
                public (int Key,int Count,decimal Total)[] GroupRows(Database.TypedTransaction tx)=>tx.Accounts.GroupBy(static row=>row.Id%3).Select(static group=>(group.Key,group.Count(),group.Sum(static row=>row.Balance))).ToArray();
                public (int Outer,int Inner,decimal Total)[] Joined(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<6).Join(tx.Accounts.Where(static row=>row.Id<12),static row=>row.Id%3,static row=>row.Id%3,static(outer,inner)=>(outer.Id,inner.Id,outer.Balance+inner.Balance)).ToArray();
                private static int Note(global::System.Collections.Generic.List<int> trace,int value,int phase){trace.Add(phase*1000+value);return value;}
                public (int First,int Second)[] GroupTrace(Database.TypedTransaction tx,global::System.Collections.Generic.List<int> trace)=>tx.Accounts.GroupBy(static row=>0).Select(group=>(group.Sum(row=>Note(trace,row.Id,1)),group.Sum(row=>Note(trace,row.Id,2)))).ToArray();
                public int[] SkipAggregate(Database.TypedTransaction tx,global::System.Collections.Generic.List<int> trace,bool skip)=>tx.Accounts.GroupBy(static row=>0).Select(group=>skip?0:group.Sum(row=>Note(trace,row.Id,3))).ToArray();
                public int[] TakeGroups(Database.TypedTransaction tx,global::System.Collections.Generic.List<int> trace,int count)=>tx.Accounts.GroupBy(row=>Note(trace,row.Id%3,4)).Select(group=>group.Sum(row=>Note(trace,row.Id,5))).Take(count).ToArray();
                public int[] NullJoin(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<4).Join(tx.Accounts.Where(static row=>row.Id<6),static row=>row.Id==0?null:"key",static row=>row.Id==0?null:"key",static(outer,inner)=>outer.Id*100+inner.Id).ToArray();
                public int[] EmptyJoin(Database.TypedTransaction tx)=>tx.Accounts.Where(static row=>row.Id<0).Join(tx.Accounts,static row=>row.Id,static row=>ThrowInner(),static(outer,inner)=>outer.Id).ToArray();
                private static int ThrowInner()=>throw new InvalidOperationException("Empty outer evaluated the inner selector.");
                public async Task<int> Condition(Database db,bool skip){
                    await using var tx=await db.BeginTransactionAsync();var hits=0;
                    if(skip||tx.Accounts.Rows.Any(row=>++hits==1)){}
                    if(!skip&&tx.Accounts.Rows.Count()!=128){throw new InvalidOperationException("Condition count changed.");}
                    var steps=0;
                    while(steps<2&&tx.Accounts.Rows.Any()){steps++;}
                    do{steps--;}while(steps>0&&tx.Accounts.Rows.Any());
                    if(steps!=0){throw new InvalidOperationException("Loop condition changed.");}
                    await tx.CommitAsync();return hits;
                }
                public async Task<long> Count(Database db){
                    await using var tx=await db.BeginTransactionAsync();
                    var count=tx.Accounts.Rows.LongCount();
                    var count32=tx.Accounts.Rows.Count();
                    if(count!=count32){throw new InvalidOperationException("Count and LongCount disagree.");}
                    var readOnly=tx.IsReadOnly;
                    await Task.Yield();
                    await tx.CommitAsync();
                    if(!readOnly){throw new InvalidOperationException("The rewritten begin lost its readonly decision.");}
                    return count;
                }
            }
            public static class Entry{
                public static async Task<long> Run(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                        IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,MaximumQueryWorkers=2,
                        TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=262144,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
                    await using(var seed=await db.BeginTransactionAsync()){
                        for(var key=0;key<128;key++){seed.Accounts.Add(new Account(key,100));}await seed.CommitAsync();
                    }
                    var result=await new Reader().Count(db);
                    var skipped=await new Reader().Condition(db,true);var evaluated=await new Reader().Condition(db,false);
                    if(skipped!=0||evaluated!=1){throw new InvalidOperationException($"Short-circuit or callback effects changed: {skipped},{evaluated}.");}
                    await using(var read=await db.BeginReadOnlyTransactionAsync()){
                        var expected=read.Accounts.Rows.GroupBy(static row=>row.Id%3,static row=>row.Balance).Select(static group=>group.Sum()).ToArray();
                        var actual=new Reader().Group(read);if(!expected.SequenceEqual(actual)){throw new InvalidOperationException("Partitioned grouping changed order or sums.");}
                        var expectedTrace=new global::System.Collections.Generic.List<int>();var actualTrace=new global::System.Collections.Generic.List<int>();
                        var traced=read.Accounts.Rows.GroupBy(static row=>0).Select(group=>(group.Sum(row=>{expectedTrace.Add(1000+row.Id);return row.Id;}),group.Sum(row=>{expectedTrace.Add(2000+row.Id);return row.Id;}))).ToArray();
                        if(!traced.SequenceEqual(new Reader().GroupTrace(read,actualTrace))||!expectedTrace.SequenceEqual(actualTrace)){throw new InvalidOperationException("Grouping reordered aggregate callbacks.");}
                        actualTrace.Clear();if(new Reader().SkipAggregate(read,actualTrace,true).Single()!=0||actualTrace.Count!=0){throw new InvalidOperationException("Grouping evaluated a short-circuited aggregate.");}
                        foreach(var take in new[]{-1,0,1,2,5}){
                            expectedTrace.Clear();actualTrace.Clear();
                            var limited=read.Accounts.Rows.GroupBy(row=>{expectedTrace.Add(4000+row.Id%3);return row.Id%3;}).Select(group=>group.Sum(row=>{expectedTrace.Add(5000+row.Id);return row.Id;})).Take(take).ToArray();
                            if(!limited.SequenceEqual(new Reader().TakeGroups(read,actualTrace,take))||!expectedTrace.SequenceEqual(actualTrace)){throw new InvalidOperationException("Take changed grouping evaluation or callback order.");}
                        }
                        var nullJoin=read.Accounts.Rows.Where(static row=>row.Id<4).Join(read.Accounts.Rows.Where(static row=>row.Id<6),static row=>row.Id==0?null:"key",static row=>row.Id==0?null:"key",static(outer,inner)=>outer.Id*100+inner.Id).ToArray();
                        if(!nullJoin.SequenceEqual(new Reader().NullJoin(read))){throw new InvalidOperationException("Join matched null keys or reordered matches.");}
                        if(new Reader().EmptyJoin(read).Length!=0){throw new InvalidOperationException("Empty Join returned rows.");}await read.CommitAsync();
                    }
                    await using(var own=await db.BeginTransactionAsync()){
                        own.Accounts[1]=new Account(1,123);own.Accounts.Remove(2);own.Accounts.Add(new Account(1000,7));
                        var expected=own.Accounts.Rows.GroupBy(static row=>row.Id%3).Select(static group=>(group.Key,group.Count(),group.Sum(static row=>row.Balance))).ToArray();
                        if(!expected.SequenceEqual(new Reader().GroupRows(own))){throw new InvalidOperationException("Grouped own changes disagree.");}
                        var joined=own.Accounts.Rows.Where(static row=>row.Id<6).Join(own.Accounts.Rows.Where(static row=>row.Id<12),static row=>row.Id%3,static row=>row.Id%3,static(outer,inner)=>(outer.Id,inner.Id,outer.Balance+inner.Balance)).ToArray();
                        if(!joined.SequenceEqual(new Reader().Joined(own))){throw new InvalidOperationException("Joined own changes disagree.");}await own.RollbackAsync();
                    }
                    await using var write=await db.BeginTransactionAsync();write.Accounts[1]=new Account(1,200);await write.CommitAsync();
                    return result;
                }
            }
            """;
        var(output,generated)=Generate(source,"AsyncOwnerCase");
        Assert.Contains(generated,static text=>text.Contains(".IterateParallel(ref",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("[条件/評価位置]",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("PartitionedLookupBuilder<",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("[Join/取引]",StringComparison.Ordinal));
        Assert.Contains(generated,static text=>text.Contains("[GroupBy/要求上限]",StringComparison.Ordinal));
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));image.Position=0;
        var load=new AssemblyLoadContext("async-owner-"+Guid.NewGuid(),isCollectible:true);load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);Assert.Equal(128,await(Task<long>)assembly.GetType("AsyncOwnerCase.Entry",true)!.GetMethod("Run")!.Invoke(null,null)!);
            var strategies=assembly.GetTypes().Where(static type=>type.GetInterfaces().Any(static contract=>contract.IsGenericType&&contract.GetGenericTypeDefinition()==typeof(global::Seamless.Collections.Iteration.IParallelStrategy<,>))).ToArray();
            Assert.Contains(strategies,static strategy=>strategy.GetField("Count")!.FieldType==typeof(long));
            Assert.Contains(strategies,static strategy=>strategy.GetField("Count")!.FieldType==typeof(int));
            // 数十億行を確保せず、生成された部分件数の境界とchecked結合を検証する。
            foreach(var strategy in strategies){
                var count=strategy.GetField("Count")!;var merge=strategy.GetMethod("Merge")!;var left=Activator.CreateInstance(strategy)!;var right=Activator.CreateInstance(strategy)!;
                if(count.FieldType==typeof(long)){
                    count.SetValue(left,(long)int.MaxValue+1);count.SetValue(right,1L);object?[] arguments=[left,right];merge.Invoke(null,arguments);
                    Assert.Equal((long)int.MaxValue+2,count.GetValue(arguments[0]));
                }else{
                    Assert.Equal(typeof(int),count.FieldType);count.SetValue(left,int.MaxValue);count.SetValue(right,1);
                    Assert.IsType<OverflowException>(Assert.Throws<global::System.Reflection.TargetInvocationException>(()=>merge.Invoke(null,[left,right])).InnerException);
                }
            }
        }
        finally{load.Unload();}
    }
    [Fact]
    public async Task BoundaryAnalysisCombinesKnownHelpersAndKeepsWritersAndUnknownDelegatesWritable(){
        const string source="""
            using System;
            using System.Linq;
            using System.Threading.Tasks;
            using Seamless.FLinq;
            using Seamless.Database;
            using Seamless.Transactions;
            using Account=Seamless.Database.Transactions.Tests.Account;
            namespace AccessCase;
            internal static partial class DatabaseGenerator;
            public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){public Table<int,Account> Accounts=>this.Table<int,Account>(static row=>row.Id);public Table<int,Account> Orders=>this.Table<int,Account>(static row=>row.Id);}
            [LinqIntercept(true)]
            public sealed class Reader{
                private static int CountRows(Database.TypedTransaction tx)=>checked((int)tx.Accounts.Rows.LongCount());
                public async Task<int> ReadOnly(Database db){await using var tx=await db.BeginTransactionAsync();var count=CountRows(tx);await tx.CommitAsync();return count;}
                public async Task<int> Alias(Database db){await using var tx=await db.BeginTransactionAsync();var rows=tx.Accounts;var count=rows.Rows.Count();await tx.CommitAsync();return count;}
                public async Task<int> Block(Database db){await using(var tx=await db.BeginTransactionAsync()){var count=CountRows(tx);await tx.CommitAsync();return count;}}
                public async Task<int> Nested(Database db){await using var tx=await db.BeginTransactionAsync();int count;await using(var child=await tx.BeginTransactionAsync()){count=CountRows(child);await child.CommitAsync();}await tx.CommitAsync();return count;}
                public async Task SlowRead(Database db){await using var tx=await db.BeginTransactionAsync();var count=CountRows(tx);Entry.ReadReady=1;await Task.Delay(1000);await tx.CommitAsync();}
                public async Task ReadOneTableAndWriteAnother(Database db){await using var tx=await db.BeginTransactionAsync();var count=CountRows(tx);tx.Orders[1]=new Account(1,count);Entry.WriterReady=1;await Task.Delay(1000);if(tx.Accounts[1].Balance!=100){throw new Exception("Writer lost its original snapshot.");}await tx.CommitAsync();}
                private static void Set(Database.TypedTransaction tx){if(tx.Accounts.Rows.Any()){var rows=tx.Accounts;rows[1]=new Account(1,90);}}
                public async Task Write(Database db){await using var tx=await db.BeginTransactionAsync();Set(tx);await tx.CommitAsync();}
                public async Task Unknown(Database db,Action<Database.TypedTransaction> action){await using var tx=await db.BeginTransactionAsync();action(tx);await tx.CommitAsync();}
                public int ExternalReader(Database.TypedTransaction tx)=>CountRows(tx);
            }
            public static class Entry{
                public static volatile int ReadReady;
                public static volatile int WriterReady;
                public static async Task<int[]> Run(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                        IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,TransactionMemoryLimit=1024,
                        TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=262144,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
                    await using(var seed=await db.BeginTransactionAsync()){seed.Accounts.Add(new Account(1,100));await seed.CommitAsync();}
                    var reader=new Reader();var counts=new[]{await reader.ReadOnly(db),await reader.Alias(db),await reader.Block(db),await reader.Nested(db)};
                    await reader.Write(db);await reader.Unknown(db,static tx=>tx.Accounts[1]=new Account(1,80));
                    try{await reader.Unknown(db,static tx=>{tx.Accounts[1]=new Account(1,55);throw new ApplicationException("external callback failed");});throw new Exception("The callback failure was lost.");}catch(ApplicationException error)when(error.Message=="external callback failed"){}
                    await using(var normal=await db.BeginTransactionAsync()){if(reader.ExternalReader(normal)!=1||normal.Accounts[1].Balance!=80){throw new Exception("The external callback left a partial change.");}normal.Accounts[1]=new Account(1,70);await normal.CommitAsync();}
                    return counts;
                }
                public static async Task<TransactionAbortReason> Pressure(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                        IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,TransactionMemoryLimit=1024,
                        TransactionHistory=new(){MaxRetainedMemoryBytes=512,MaxRetainedStorageBytes=0,MaxMetadataBytes=262144,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
                    await using(var seed=await db.BeginTransactionAsync()){seed.Accounts.Add(new Account(1,100));await seed.CommitAsync();}
                    ReadReady=0;var reader=Task.Run(()=>new Reader().SlowRead(db));var attempts=0;
                    while(ReadReady==0){if(reader.IsCompleted){await reader;throw new Exception("The reader finished before registering its snapshot.");}if(attempts++>5000){throw new TimeoutException();}await Task.Delay(1);}
                    await using(var first=await db.BeginTransactionAsync()){first.Accounts[1]=new Account(1,90);await first.CommitAsync();}
                    try{await using var second=await db.BeginTransactionAsync();second.Accounts[1]=new Account(1,80);await second.CommitAsync();throw new Exception("History pressure was not observed.");}
                    catch(TransactionAbortedException error)when(error.Reason==TransactionAbortReason.HistoryPressure){}
                    try{await reader;throw new Exception("The generated read-only transaction was not aborted.");}
                    catch(TransactionAbortedException error){return error.Reason;}
                }
                public static async Task<TransactionAbortReason> WriterPressure(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                        IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,TransactionMemoryLimit=1024,
                        TransactionHistory=new(){MaxRetainedMemoryBytes=512,MaxRetainedStorageBytes=0,MaxMetadataBytes=262144,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
                    await using(var seed=await db.BeginTransactionAsync()){seed.Accounts.Add(new Account(1,100));await seed.CommitAsync();}
                    WriterReady=0;var writer=Task.Run(()=>new Reader().ReadOneTableAndWriteAnother(db));var attempts=0;
                    while(WriterReady==0){if(writer.IsCompleted){await writer;throw new Exception("Writer ended before staging the other table.");}if(attempts++>5000){throw new TimeoutException();}await Task.Delay(1);}
                    await using(var first=await db.BeginTransactionAsync()){first.Accounts[1]=new Account(1,90);await first.CommitAsync();}
                    try{await using var second=await db.BeginTransactionAsync();second.Accounts[1]=new Account(1,80);await second.CommitAsync();throw new Exception("History pressure was not observed.");}
                    catch(TransactionAbortedException error)when(error.Reason==TransactionAbortReason.HistoryPressure){}
                    TransactionAbortReason reason;try{await writer;throw new Exception("The writer did not validate its original read.");}catch(TransactionAbortedException error){reason=error.Reason;}
                    if(reason!=TransactionAbortReason.Conflict){throw new Exception("A read/write scope was treated as a cancellable read-only scope: "+reason);}
                    await using(var verify=await db.BeginReadOnlyTransactionAsync()){if(verify.Accounts[1].Balance!=90||verify.Orders.Count()!=0){throw new Exception("The conflicting operation left a partial change.");}}
                    return reason;
                }
            }
            """;
        var(output,generated)=Generate(source,"AccessCase");
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"database-access.g.cs"),string.Join(Environment.NewLine,generated));
        Assert.Equal(5,generated.Count(static text=>text.Contains("__DatabaseReadOnly_",StringComparison.Ordinal)));
        Assert.Contains(generated,static text=>text.Contains(".IterateParallel(ref",StringComparison.Ordinal)&&text.Contains("public long Count;",StringComparison.Ordinal));
        var annotated=string.Join(Environment.NewLine,generated);
        foreach(var category in new[]{"[アクセス判定]","[並列]","[変形]","[キュー","[資源","[不変条件]","[Debug]"}){Assert.Contains(category,annotated,StringComparison.Ordinal);}
        Assert.Contains("GeneratedCodeAttribute",annotated,StringComparison.Ordinal);Assert.Contains("DebuggerDisplayAttribute",annotated,StringComparison.Ordinal);
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));image.Position=0;
        var load=new AssemblyLoadContext("database-access-"+Guid.NewGuid(),isCollectible:true);load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);var type=assembly.GetType("AccessCase.Entry",true)!;
            Assert.Contains(assembly.GetTypes(),static generatedType=>generatedType.GetField("DebugPlan",global::System.Reflection.BindingFlags.NonPublic|global::System.Reflection.BindingFlags.Static)?.IsLiteral==true);
            Assert.Equal(new[]{1,1,1,1},await(Task<int[]>)type.GetMethod("Run")!.Invoke(null,null)!);
            Assert.Equal(Seamless.Transactions.TransactionAbortReason.HistoryPressure,await(Task<Seamless.Transactions.TransactionAbortReason>)type.GetMethod("Pressure")!.Invoke(null,null)!);
            Assert.Equal(Seamless.Transactions.TransactionAbortReason.Conflict,await(Task<Seamless.Transactions.TransactionAbortReason>)type.GetMethod("WriterPressure")!.Invoke(null,null)!);
        }
        finally{load.Unload();}
    }
}
