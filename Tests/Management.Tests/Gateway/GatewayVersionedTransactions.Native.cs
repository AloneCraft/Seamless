using MemoryPack;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
namespace Seamless.Database.Gateway.Tests;

public sealed partial class GatewayVersionedTransactions{
    [Theory,Trait("Category","Integration")]
    [InlineData(GatewayCommitPhase.BeforeWal,false)]
    [InlineData(GatewayCommitPhase.DatabaseApplied,true)]
    [InlineData(GatewayCommitPhase.AfterPublication,true)]
    public async Task TwoSchemaChangesAndTheirNativeRebuildRecoverAsOneDecision(GatewayCommitPhase stop,bool committed){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var root=Path.Combine(data,"mvcc-two-ddl-native-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var armed=false;
        void Inject(GatewayCommitPhase phase,int database){if(armed&&phase==stop&&(phase!=GatewayCommitPhase.DatabaseApplied||database==1)){throw new IOException("Injected compound DDL publication failure.");}}
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options(),[Definition("a"),Definition("b")],failureInjection:Inject)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(47,"a");Execute(engine,session,"INSERT a.dbo.Items VALUES(1,100); INSERT b.dbo.Items VALUES(1,200)");var type=((InProcessDatabaseAdapter)host.Databases.Resolve("b").Adapter).Database.GetType().FullName!;
            await host.Transactions.InstallNativeQueryAsync(new("snapshot-module","Query",[new("Query.cs","using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+type+")context.Database;var sum=0;foreach(var row in global::Seamless.Database.StoredTableExtensions.GetGatewayTransactionRows(db.dbo.Items)){sum+=row.N;}yield return MemoryPackSerializer.Serialize(sum);}}")],[new("n","b","","System.Int32","System.Int32")],["b"]));host.Transactions.Checkpoint();
            Execute(engine,session,"BEGIN TRANSACTION; ALTER TABLE a.dbo.Items ADD ExtraA int NOT NULL DEFAULT 7; ALTER TABLE b.dbo.Items ADD ExtraB int NOT NULL DEFAULT 9; UPDATE a.dbo.Items SET N=110 WHERE Id=1; UPDATE b.dbo.Items SET N=220 WHERE Id=1");armed=true;
            var failure=Assert.Throws<global::Seamless.Transactions.TransactionCommitException>(()=>Execute(engine,session,"COMMIT TRANSACTION"));var expected=global::Seamless.Transactions.TransactionOutcomeKind.Aborted;if(committed){expected=global::Seamless.Transactions.TransactionOutcomeKind.Committed;}Assert.Equal(expected,failure.Outcome);armed=false;
        }
        var savedDatabases=new global::System.Collections.Concurrent.ConcurrentQueue<int>();
        using var checkpointTiming=CheckpointTiming.Capture(root,Path.Combine(cache,Path.GetFileName(root),"checkpoint-timing.json"));
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,Options(),failureInjection:(phase,database)=>{
            if(phase==GatewayCommitPhase.DatabaseCheckpointed){savedDatabases.Enqueue(database);}
        })){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(48,"a");
            var checkpointPath=Path.Combine(root,"transactions","common-checkpoint.json");
            Assert.True(File.Exists(checkpointPath),"復旧前に作成した共通チェックポイントが見つかりません。");
            if(committed){
                Assert.Equal(new object?[]{110,7},ReadRecovered("aのSQL読み取り",()=>Assert.Single(engine.Execute("SELECT N,ExtraA FROM a.dbo.Items",session).Rows)));
                Assert.Equal(new object?[]{220,9},ReadRecovered("bのSQL読み取り",()=>Assert.Single(engine.Execute("SELECT N,ExtraB FROM b.dbo.Items",session).Rows)));
                Assert.Equal(220,ReadRecovered("bのネイティブ読み取り",()=>ReadNative(host,session)));
            }
            else{Assert.Equal(100,Assert.Single(engine.Execute("SELECT N FROM a.dbo.Items",session).Rows)[0]);Assert.Equal(200,Assert.Single(engine.Execute("SELECT N FROM b.dbo.Items",session).Rows)[0]);Assert.Throws<global::Seamless.Database.Query.SqlQueryException>(()=>engine.Execute("SELECT ExtraA FROM a.dbo.Items",session).Rows.ToArray());Assert.Throws<global::Seamless.Database.Query.SqlQueryException>(()=>engine.Execute("SELECT ExtraB FROM b.dbo.Items",session).Rows.ToArray());Assert.Equal(200,ReadNative(host,session));}

            T ReadRecovered<T>(string stage,Func<T> read){
                var checkpointBefore=File.GetLastWriteTimeUtc(checkpointPath);
                var before=host.Transactions.VersionedDiagnostics;
                try{return read();}
                catch(TimeoutException error){
                    var after=host.Transactions.VersionedDiagnostics;
                    var checkpointAfter=File.GetLastWriteTimeUtc(checkpointPath);
                    var checkpointStaged=File.Exists(checkpointPath+".new");
                    throw new TimeoutException($"復旧後の{stage}が時間切れになりました。開始前={before}、失敗時={after}、保存完了を観測したDB ID=[{string.Join(",",savedDatabases)}]、共通チェックポイント更新日時={checkpointBefore:O} → {checkpointAfter:O}、一時ファイルあり={checkpointStaged}。",error);
                }
            }
        }
    }
    [Fact,Trait("Category","Integration")]
    public async Task CheckpointDrainsAnExecutingNativeOperationAndWaitsNewTransactions(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"mvcc-native-drain-"+Guid.NewGuid().ToString("N")),GeneratedAssemblyBuilderTests.Compiler(data,cache),Options(),[Definition("b")]);
        var engine=new SqlExecutionEngine(host.Databases);using var seed=new SessionContext(44,"b");Execute(engine,seed,"INSERT dbo.Items VALUES(1,200)");var type=((InProcessDatabaseAdapter)host.Databases.Resolve("b").Adapter).Database.GetType().FullName!;
        var source=new NativeQuerySourceProject("snapshot-module","Query",[new("Query.cs","using MemoryPack;using Seamless.Database.Client;using System.Threading;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+type+")context.Database;var sum=0;foreach(var row in global::Seamless.Database.StoredTableExtensions.GetGatewayTransactionRows(db.dbo.Items)){sum+=row.N;}((ManualResetEventSlim)context.GetDatabase(\"entered\")).Set();((ManualResetEventSlim)context.GetDatabase(\"release\")).Wait(context.CancellationToken);yield return MemoryPackSerializer.Serialize(sum);}}")],[new("n","b","","System.Int32","System.Int32")],["b"]);
        await host.Transactions.InstallNativeQueryAsync(source);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();using var attempted=new ManualResetEventSlim();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));using var reader=new SessionContext(45,"b");
        var native=Task.Run(()=>reader.Run(()=>{
            using var binding=DatabaseRegistry.EnterSqlSession(reader);using var reads=host.Transactions.AcquireNativeQueryReads(reader,"snapshot-module",true);Assert.True(host.Transactions.TryResolveNative(reader,"snapshot-module",out _,out var module));
            var database=host.Transactions.NativeDatabase(reader,"snapshot-module","b",true);using var iterator=module.Execute("n",ReadOnlyMemory<byte>.Empty,new BlockingNativeContext(database,entered,release,timeout.Token)).GetEnumerator();
            Assert.True(host.Transactions.ExecuteNativeOperation(reader,iterator.MoveNext));return MemoryPackSerializer.Deserialize<int>(iterator.Current);
        }));
        Task? checkpoint=null;Task? admission=null;
        try{
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));Assert.Equal(1,host.Transactions.VersionedDiagnostics!.Value.ActiveOperations);checkpoint=Task.Run(host.Transactions.Checkpoint);
            Assert.True(SpinWait.SpinUntil(()=>host.Transactions.VersionedDiagnostics!.Value.Reclaiming,TimeSpan.FromSeconds(2)));
            admission=Task.Run(()=>{using var session=new SessionContext(46,"b");attempted.Set();Execute(engine,session,"BEGIN TRANSACTION; ROLLBACK TRANSACTION");});
            Assert.True(attempted.Wait(TimeSpan.FromSeconds(1)));Assert.False(checkpoint.IsCompleted);Assert.False(admission.IsCompleted);Assert.False(native.IsCompleted);
        }finally{release.Set();await native.WaitAsync(TimeSpan.FromSeconds(5));if(checkpoint is not null){await checkpoint.WaitAsync(TimeSpan.FromSeconds(5));}if(admission is not null){await admission.WaitAsync(TimeSpan.FromSeconds(5));}}
        Assert.Equal(200,await native);Assert.Equal(0,host.Transactions.VersionedDiagnostics!.Value.ActiveOperations);Assert.Equal(0,host.Transactions.VersionedDiagnostics.Value.ActiveTransactions);
    }
    private sealed class BlockingNativeContext(object database,ManualResetEventSlim entered,ManualResetEventSlim release,CancellationToken token):INativeQueryContext{
        public object Database=>database;public string DatabaseName=>"b";public CancellationToken CancellationToken=>token;
        public object GetDatabase(string name)=>name switch{"entered"=>entered,"release"=>release,"b"=>database,_=>throw new InvalidOperationException(name)};
    }
    [Fact,Trait("Category","Integration")]
    public async Task NativePublicationAndDdlUseTheSameCatalogueSnapshotAndRecoverTogether(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var path=Path.Combine(data,"mvcc-native-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options(),[Definition("a"),Definition("b")])){
            var engine=new SqlExecutionEngine(host.Databases);using var seed=new SessionContext(40,"b");Execute(engine,seed,"INSERT a.dbo.Items VALUES(1,100); INSERT b.dbo.Items VALUES(1,200)");var type=((InProcessDatabaseAdapter)host.Databases.Resolve("b").Adapter).Database.GetType().FullName!;
            NativeQuerySourceProject Source(int offset)=>new("snapshot-module","Query",[new("Query.cs","using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+type+")context.Database;var sum="+offset+";foreach(var row in global::Seamless.Database.StoredTableExtensions.GetGatewayTransactionRows(db.dbo.Items)){sum=checked(sum+row.N);}yield return MemoryPackSerializer.Serialize(sum);}}")],[new("n","b","","System.Int32","System.Int32")],["b"]);
            await host.Transactions.InstallNativeQueryAsync(Source(0));using var old=new SessionContext(41,"a");Execute(engine,old,"BEGIN TRANSACTION");Assert.Equal(100,Assert.Single(engine.Execute("SELECT N FROM dbo.Items",old).Rows)[0]);await host.Transactions.InstallNativeQueryAsync(Source(10));Assert.Equal(200,ReadNative(host,old));using var current=new SessionContext(42,"b");Assert.Equal(210,ReadNative(host,current));Execute(engine,old,"ROLLBACK TRANSACTION");Execute(engine,current,"ALTER TABLE dbo.Items ADD Extra int NULL");Assert.Equal(210,ReadNative(host,current));host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options())){using var session=new SessionContext(43,"b");Assert.Equal(210,ReadNative(host,session));}
    }
    private static int ReadNative(GatewayDatabaseHost host,SessionContext session)=>session.Run(()=>{
        using var binding=DatabaseRegistry.EnterSqlSession(session);using var reads=host.Transactions.AcquireNativeQueryReads(session,"snapshot-module",true);Assert.True(host.Transactions.TryResolveNative(session,"snapshot-module",out _,out var module));var database=host.Transactions.NativeDatabase(session,"snapshot-module","b",true);using var iterator=module.Execute("n",ReadOnlyMemory<byte>.Empty,new NativeContext(database)).GetEnumerator();Assert.True(host.Transactions.ExecuteNativeOperation(session,iterator.MoveNext));var value=MemoryPackSerializer.Deserialize<int>(iterator.Current);Assert.False(host.Transactions.ExecuteNativeOperation(session,iterator.MoveNext));return value;
    });
    private sealed class NativeContext(object database):INativeQueryContext{public object Database=>database;public string DatabaseName=>"b";public CancellationToken CancellationToken=>default;}
}
