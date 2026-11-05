using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.Loader;
using Seamless.Database.Generator;
using Seamless.FLinq.Generator;
namespace Seamless.Database.Transactions.Tests;

// Roslynを呼ぶハーネス自体を展開せず、文字列内の利用者コードだけをdriverで生成・実行する。
[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class DatabaseTransactionExpansionTests{
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
            }
            [LinqIntercept(true)]
            public sealed class Report{
                public decimal Total(Database.TypedTransaction tx){
                    using var probe=new ScopeProbe();
                    if(tx.Accounts.Rows.Count()!=1||!tx.Accounts.Rows.Any()||!tx.Accounts.Rows.Contains(tx.Accounts[1])){throw new InvalidOperationException("Snapshot membership is inconsistent.");}
                    return tx.Accounts.Rows.Sum(static row=>row.Balance);
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
                [LinqIntercept(true)]
                public async Task<decimal> Run(bool fail){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{
                        Transactions=DatabaseTransactionMode.SharedV2,
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
                    if(ScopeProbe.Active!=0){throw new InvalidOperationException("Generated query lost using lifetime.");}
                    return result;
                }
                private static void PlaceOrder(Database.TypedTransaction tx){
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
        if(versioned){source=source.Replace("Transactions=DatabaseTransactionMode.SharedV2,","Transactions=DatabaseTransactionMode.SharedV2,TransactionConcurrency=TransactionConcurrencyMode.Optimistic,TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=262144,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)},",StringComparison.Ordinal);}
        var(output,generated)=Generate(source,"ExpansionCase");
        Assert.Contains(generated,static text=>text.Contains("InterceptsLocation",StringComparison.Ordinal)
            &&text.Contains("ExpansionCase",StringComparison.Ordinal)
            &&text.Contains("Total",StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,"database-flinq-"+versioned+".g.cs"),string.Join(Environment.NewLine,generated));
        Assert.Contains(generated,static text=>text.Contains(".Iterate(ref",StringComparison.Ordinal));
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
        }finally{
            load.Unload();
        }
    }
    private static (Compilation Output,string[] Sources) Generate(string source,string name,bool debug=true){
        var symbols=Array.Empty<string>();if(debug){symbols=["DEBUG"];}
        var parse=new CSharpParseOptions(LanguageVersion.Preview).WithPreprocessorSymbols(symbols).WithFeatures(
            [new KeyValuePair<string,string>("InterceptorsPreviewNamespaces","Intercept"),new KeyValuePair<string,string>("InterceptorsNamespaces","Intercept")]);
        var paths=((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")??string.Empty).Split(Path.PathSeparator).Where(File.Exists)
            .Append(typeof(Account).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase);
        var references=paths.Select(static path=>(MetadataReference)MetadataReference.CreateFromFile(path));
        var compilation=CSharpCompilation.Create(name,[CSharpSyntaxTree.ParseText(source,parse,path:name+".cs")],references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable));
        GeneratorDriver driver=CSharpGeneratorDriver.Create(
            [new DatabaseSchemaGenerator().AsSourceGenerator(),new DatabaseContainerGenerator().AsSourceGenerator(),new FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:parse);
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);
        Assert.Empty(diagnostics.Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error));
        Assert.Empty(output.GetDiagnostics().Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error));
        var generated=driver.GetRunResult().Results.SelectMany(static result=>result.GeneratedSources)
            .Where(static source=>source.HintName is not "InterceptsLocationAttribute.g.cs" and not "LinqRewriteAttributes.g.cs").Select(static source=>source.SourceText.ToString()).ToArray();
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
                    using var db=new Database(DatabaseContainerOptions.InMemory with{Transactions=DatabaseTransactionMode.SharedV2,TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                        IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,MaximumQueryWorkers=2,
                        TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=262144,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
                    await using(var seed=await db.BeginTransactionAsync()){
                        for(var key=0;key<128;key++){seed.Accounts.Add(new Account(key,100));}await seed.CommitAsync();
                    }
                    var result=await new Reader().Count(db);
                    await using var write=await db.BeginTransactionAsync();write.Accounts[1]=new Account(1,200);await write.CommitAsync();
                    return result;
                }
            }
            """;
        var(output,generated)=Generate(source,"AsyncOwnerCase");
        Assert.Contains(generated,static text=>text.Contains(".IterateParallel(ref",StringComparison.Ordinal));
        using var image=new MemoryStream();var emitted=output.Emit(image);Assert.True(emitted.Success,string.Join(Environment.NewLine,emitted.Diagnostics));image.Position=0;
        var load=new AssemblyLoadContext("async-owner-"+Guid.NewGuid(),isCollectible:true);load.Resolving+=static(_,name)=>AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        try{
            var assembly=load.LoadFromStream(image);Assert.Equal(128,await(Task<long>)assembly.GetType("AsyncOwnerCase.Entry",true)!.GetMethod("Run")!.Invoke(null,null)!);
            var strategies=assembly.GetTypes().Where(static type=>type.GetInterfaces().Any(static contract=>contract.IsGenericType&&contract.GetGenericTypeDefinition()==typeof(global::Seamless.Collections.Iteration.IParallelStrategy<,>))).ToArray();
            Assert.Equal(2,strategies.Length);
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
            public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){public Table<int,Account> Accounts=>this.Table<int,Account>(static row=>row.Id);}
            [LinqIntercept(true)]
            public sealed class Reader{
                private static int CountRows(Database.TypedTransaction tx)=>checked((int)tx.Accounts.Rows.LongCount());
                public async Task<int> ReadOnly(Database db){await using var tx=await db.BeginTransactionAsync();var count=CountRows(tx);await tx.CommitAsync();return count;}
                public async Task<int> Alias(Database db){await using var tx=await db.BeginTransactionAsync();var rows=tx.Accounts;var count=rows.Rows.Count();await tx.CommitAsync();return count;}
                public async Task<int> Block(Database db){await using(var tx=await db.BeginTransactionAsync()){var count=CountRows(tx);await tx.CommitAsync();return count;}}
                public async Task<int> Nested(Database db){await using var tx=await db.BeginTransactionAsync();int count;await using(var child=await tx.BeginTransactionAsync()){count=CountRows(child);await child.CommitAsync();}await tx.CommitAsync();return count;}
                public async Task SlowRead(Database db){await using var tx=await db.BeginTransactionAsync();var count=CountRows(tx);Entry.ReadReady=1;await Task.Delay(1000);await tx.CommitAsync();}
                private static void Set(Database.TypedTransaction tx){var rows=tx.Accounts;rows[1]=new Account(1,90);}
                public async Task Write(Database db){await using var tx=await db.BeginTransactionAsync();Set(tx);await tx.CommitAsync();}
                public async Task Unknown(Database db,Action<Database.TypedTransaction> action){await using var tx=await db.BeginTransactionAsync();action(tx);await tx.CommitAsync();}
                public int ExternalReader(Database.TypedTransaction tx)=>CountRows(tx);
            }
            public static class Entry{
                public static volatile int ReadReady;
                public static async Task<int[]> Run(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{Transactions=DatabaseTransactionMode.SharedV2,TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
                        IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,TransactionMemoryLimit=1024,
                        TransactionHistory=new(){MaxRetainedMemoryBytes=65536,MaxRetainedStorageBytes=0,MaxMetadataBytes=262144,MaintenanceReserveMemoryBytes=65536,MaintenanceReserveStorageBytes=0,MaintenanceWaitTimeout=TimeSpan.FromSeconds(3)}});
                    await using(var seed=await db.BeginTransactionAsync()){seed.Accounts.Add(new Account(1,100));await seed.CommitAsync();}
                    var reader=new Reader();var counts=new[]{await reader.ReadOnly(db),await reader.Alias(db),await reader.Block(db),await reader.Nested(db)};
                    await reader.Write(db);await reader.Unknown(db,static tx=>tx.Accounts[1]=new Account(1,80));
                    await using(var normal=await db.BeginTransactionAsync()){if(reader.ExternalReader(normal)!=1){throw new Exception();}normal.Accounts[1]=new Account(1,70);await normal.CommitAsync();}
                    return counts;
                }
                public static async Task<TransactionAbortReason> Pressure(){
                    using var db=new Database(DatabaseContainerOptions.InMemory with{Transactions=DatabaseTransactionMode.SharedV2,TransactionConcurrency=TransactionConcurrencyMode.Optimistic,
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
        }
        finally{load.Unload();}
    }
}
