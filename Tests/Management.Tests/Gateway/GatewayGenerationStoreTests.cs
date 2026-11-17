using System.Text.Json;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Rdbms;
using Seamless.Transactions;
using Seamless.Database.Query;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Microsoft.Data.SqlClient;
using System.Data;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Native;
using MemoryPack;
using System.Reflection;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayGenerationStoreTests{
    [Theory,Trait("Category","Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeSourceContextEnlistsEveryDeclaredDatabaseInTheSameTransaction(bool optimistic){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};if(optimistic){options=GatewayVersionedTransactions.Options();}using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"native-context-"+Guid.NewGuid().ToString("N")),compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS"),new("b",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);
        var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(130,"a");foreach(var result in engine.ExecuteBatch("CREATE TABLE a.dbo.T(Id int PRIMARY KEY); CREATE TABLE b.dbo.T(Id int PRIMARY KEY); INSERT a.dbo.T VALUES(1); INSERT b.dbo.T VALUES(1)",session)){foreach(var row in result.Rows){}}
        var first=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database;var second=((InProcessDatabaseAdapter)host.Databases.Resolve("b").Adapter).Database;
        var firstKey=first.GetType().GetProperty("dbo")!.GetValue(first)!.GetType().GetProperty("T")!.PropertyType.GetGenericArguments()[0].FullName!.Replace('+','.');var secondKey=second.GetType().GetProperty("dbo")!.GetValue(second)!.GetType().GetProperty("T")!.PropertyType.GetGenericArguments()[0].FullName!.Replace('+','.');
        var code="using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var a=(global::"+first.GetType().FullName+")context.Database;var b=(global::"+second.GetType().FullName+")context.GetDatabase(\"b\");if(id==\"delete\"){a.dbo.T.Remove(new global::"+firstKey+"(1));b.dbo.T.Remove(new global::"+secondKey+"(1));}yield return MemoryPackSerializer.Serialize(checked((int)(a.dbo.T.LongCount+b.dbo.T.LongCount)));}}";
        await host.Transactions.InstallNativeQueryAsync(new("cross","Query",[new("Query.cs",code)],[new("count","a","","System.Int32","System.Int32"),new("delete","a","","System.Int32","System.Int32",false)],["a","b"]));
        using var server=new NativeQueryServer(host.Databases,0);server.Start();await using var client=new NativeQueryClient("127.0.0.1",server.Port);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));var fingerprint=host.Databases.Resolve("a").SchemaFingerprint!;
        async Task<int> Query(NativeTransaction transaction,string id){var rows=new List<int>();await foreach(var row in transaction.QueryAsync<int,int>("cross",id,fingerprint,0,timeout.Token)){rows.Add(row);}return Assert.Single(rows);}
        await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){Assert.Equal(2,await Query(transaction,"count"));await transaction.SavepointAsync("before",timeout.Token);Assert.Equal(0,await Query(transaction,"delete"));await transaction.RollbackToSavepointAsync("before",timeout.Token);Assert.Equal(2,await Query(transaction,"count"));await transaction.RollbackAsync(timeout.Token);}
        Assert.Equal(1,Assert.Single(engine.Execute("SELECT COUNT(*) FROM a.dbo.T",session).Rows)[0]);Assert.Equal(1,Assert.Single(engine.Execute("SELECT COUNT(*) FROM b.dbo.T",session).Rows)[0]);
        await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){Assert.Equal(0,await Query(transaction,"delete"));await transaction.CommitAsync(timeout.Token);}
        Assert.Equal(0,Assert.Single(engine.Execute("SELECT COUNT(*) FROM a.dbo.T",session).Rows)[0]);Assert.Equal(0,Assert.Single(engine.Execute("SELECT COUNT(*) FROM b.dbo.T",session).Rows)[0]);await server.StopAsync();
    }

    [Fact,Trait("Category","Integration")]
    public async Task CollectibleNativeDtoFormattersAreReleasedWithoutRemovingClientContracts(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache,true);
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"native-dto-"+Guid.NewGuid().ToString("N")),compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);
        var registration=host.Databases.Resolve("a");var database=((InProcessDatabaseAdapter)registration.Adapter).Database;
        var code="using MemoryPack;using Seamless.Database.Client;namespace Seamless.Database.Gateway.Tests;[MemoryPackable]public sealed partial record NativeCompilerResult(int N,string Name);public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){yield return MemoryPackSerializer.Serialize(new NativeCompilerResult(8,\"value\"));}}";
        var source=new NativeQuerySourceProject("dto","Seamless.Database.Gateway.Tests.Query",[new("Query.cs",code)],[new("n","a","","System.Int32",typeof(NativeCompilerResult).FullName!)],["a"]);
        var artifact=await compiler.BuildNativeAsync(source,[new("a",registration.SchemaFingerprint!,database.GetType().Assembly.Location)],"test");var weak=ExecuteNativeDtoAndRelease(artifact,database);
        for(var attempt=0;attempt<50&&weak.IsAlive;attempt++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(10);}Assert.False(weak.IsAlive);
        Assert.Equal(new NativeCompilerResult(9,"client"),MemoryPackSerializer.Deserialize<NativeCompilerResult>(MemoryPackSerializer.Serialize(new NativeCompilerResult(9,"client"))));
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ExecuteNativeDtoAndRelease(NativeQueryBuildArtifact artifact,DatabaseContainer database){
        using var loaded=new LoadedNativeQueryModule(artifact,new Dictionary<string,Assembly>{{"a",database.GetType().Assembly}});
        var bytes=Assert.Single(loaded.Module.Execute("n",ReadOnlyMemory<byte>.Empty,new CompilerNativeContext(database)));Assert.Equal(new NativeCompilerResult(8,"value"),MemoryPackSerializer.Deserialize<NativeCompilerResult>(bytes));
        Assert.Equal(NativeWireContract.For<NativeCompilerResult>(),loaded.AddContracts(artifact.Manifest).Queries.Single().ResultContract);
        var type=loaded.Module.GetType().Assembly.GetType(typeof(NativeCompilerResult).FullName!,true)!;
        var cache=(global::System.Collections.IDictionary)typeof(MemoryPackFormatterProvider).GetField("formatters",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        Assert.True(cache.Contains(type));loaded.Dispose();Assert.False(cache.Contains(type));Assert.True(cache.Contains(typeof(NativeCompilerResult)));return loaded.UnloadReference;
    }

    [Fact,Trait("Category","Integration")]
    public async Task RegisteredNativeSourcesFollowDdlSavepointsCommonCommitAndRecovery(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");
        var root=Path.Combine(data,"native-publication-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS"),new("b",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(129,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            Execute("CREATE TABLE dbo.Items(Id int PRIMARY KEY,N int NOT NULL); INSERT dbo.Items VALUES(1,8)");
            var rootType=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database.GetType().FullName!;
            var code="using Seamless.Database.Client;using MemoryPack;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+rootType+")context.Database;foreach(var row in db.dbo.Items){yield return MemoryPackSerializer.Serialize(row.N);}}}";
            await host.Transactions.InstallNativeQueryAsync(new("registered","Query",[new("Query.cs",code)],[new("n","a","","System.Int32","System.Int32")],["a","b"]));
            using var server=new NativeQueryServer(host.Databases,0);server.Start();await using var client=new NativeQueryClient("127.0.0.1",server.Port);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));
            async Task<int> Query(){var definition=Assert.Single(host.Transactions.NativeQueries).Manifest.Queries.Single();var rows=new List<int>();await foreach(var row in client.QueryAsync<int,int>("registered","n","a",definition.SchemaFingerprint,0,timeout.Token)){rows.Add(row);}return Assert.Single(rows);}
            Assert.Equal(8,await Query());var registered=Assert.Single(host.Transactions.NativeQueries).Manifest.Queries.Single();Assert.Equal(NativeWireContract.For<int>(),registered.ResultContract);Assert.Equal(NativeWireContract.For<int>(),registered.ArgumentsContract);
            var mismatch=await Assert.ThrowsAsync<NativeQueryException>(async()=>{await foreach(var row in client.QueryAsync<int,string>("registered","n","a",registered.SchemaFingerprint,0,timeout.Token)){} });Assert.Contains("result contract",mismatch.Message);Assert.True(mismatch.IsRecoverable);Assert.Equal(8,await Query());var first=Assert.Single(host.Transactions.NativeQueries).Manifest.AssemblySha256;
            var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"a","b"};
            Assert.Empty(host.Transactions.ListNativePublications(new HashSet<string>{"a"},default,null,null,1).Items);
            var beforeDdl=host.Transactions.ListNativePublications(allowed,default,null,null,1);
            var oldGeneration=Assert.Single(beforeDdl.Items).GenerationId;
            Execute("ALTER TABLE dbo.Items ADD Extra bigint NULL");Assert.NotEqual(first,Assert.Single(host.Transactions.NativeQueries).Manifest.AssemblySha256);Assert.Equal(8,await Query());
            var stale=host.Transactions.ListNativePublications(allowed,beforeDdl.Version,"registered","n",1);
            Assert.NotEqual(beforeDdl.Version,stale.Version);
            Assert.Empty(stale.Items);
            Assert.NotEqual(oldGeneration,Assert.Single(host.Transactions.ListNativePublications(allowed,default,null,null,1).Items).GenerationId);
            first=Assert.Single(host.Transactions.NativeQueries).Manifest.AssemblySha256;Execute("CREATE TABLE b.dbo.Dependency(Id int PRIMARY KEY)");Assert.NotEqual(first,Assert.Single(host.Transactions.NativeQueries).Manifest.AssemblySha256);Assert.Equal(8,await Query());
            first=Assert.Single(host.Transactions.NativeQueries).Manifest.AssemblySha256;Execute("BEGIN TRANSACTION; SAVE TRANSACTION point; ALTER TABLE dbo.Items DROP COLUMN N");Assert.Null(Assert.Single(host.Transactions.NativeQueries).Diagnostic);Execute("ROLLBACK TRANSACTION point; COMMIT TRANSACTION");Assert.Equal(first,Assert.Single(host.Transactions.NativeQueries).Manifest.AssemblySha256);Assert.Equal(8,await Query());
            Execute("BEGIN TRANSACTION; ALTER TABLE dbo.Items DROP COLUMN N; COMMIT TRANSACTION");Assert.Contains("CS1061",Assert.Single(host.Transactions.NativeQueries).Diagnostic);Assert.Contains("disabled",(await Assert.ThrowsAsync<NativeQueryException>(Query)).Message);
            Assert.Empty(host.Transactions.ListNativePublications(allowed,default,null,null,1).Items);
            Execute("ALTER TABLE dbo.Items ADD N int NOT NULL DEFAULT(9)");Assert.Null(Assert.Single(host.Transactions.NativeQueries).Diagnostic);Assert.Equal(9,await Query());await server.StopAsync();host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){
            using var server=new NativeQueryServer(host.Databases,0);server.Start();await using var client=new NativeQueryClient("127.0.0.1",server.Port);var definition=Assert.Single(host.Transactions.NativeQueries).Manifest.Queries.Single();var rows=new List<int>();
            await foreach(var row in client.QueryAsync<int,int>("registered","n","a",definition.SchemaFingerprint,0)){rows.Add(row);}Assert.Equal(9,Assert.Single(rows));await server.StopAsync();
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task NativeSourceProjectRebuildsAgainstNewTypedAssembliesAndReportsRemovedMembers(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");
        var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"native-rebuild-"+Guid.NewGuid().ToString("N")),compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);
        var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(128,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
        Execute("CREATE TABLE dbo.Items(Id int PRIMARY KEY,N int NOT NULL); INSERT dbo.Items VALUES(1,8)");
        var rootType=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database.GetType().FullName!;
        var code="using Seamless.Database.Client;using MemoryPack;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+rootType+")context.Database;foreach(var row in db.dbo.Items){yield return MemoryPackSerializer.Serialize(row.N);}}}";
        var source=new NativeQuerySourceProject("source-query","Query",[new("Query.cs",code)],[new("n","a","","System.Int32","System.Int32")],["a"]);
        NativeDatabaseAssembly Input(){var registration=host.Databases.Resolve("a");return new("a",registration.SchemaFingerprint!,((InProcessDatabaseAdapter)registration.Adapter).Database.GetType().Assembly.Location);}
        var first=await compiler.BuildNativeAsync(source,[Input()],"test");Assert.True(File.Exists(first.SourceProjectPath));var weak=ExecuteCompiledNative(first,host,8);
        var store=new GatewayNativeQueryStore(Path.Combine(data,"native-artifacts-"+Guid.NewGuid().ToString("N")),compiler,new global::Seamless.Execution.ResourceBudget(64L<<20,256L<<20));
        GatewayNativeQueryReference reference;
        using(var version=await store.PrepareAsync(source,[Input()],"test")){reference=version.Reference;ExecuteStoredNative(version,host,8);}
        using(var version=store.Read(reference)){
            ExecuteStoredNative(version,host,8);var path=version.Artifact!.SourceProjectPath;var bytes=await File.ReadAllBytesAsync(path);
            try{await File.WriteAllTextAsync(path,"{}");Assert.Throws<InvalidDataException>(()=>store.Read(reference));}finally{await File.WriteAllBytesAsync(path,bytes);}
        }
        for(var attempt=0;attempt<50&&weak.IsAlive;attempt++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(10);}Assert.False(weak.IsAlive);
        Execute("ALTER TABLE dbo.Items ADD Extra bigint NULL");var second=await compiler.BuildNativeAsync(source,[Input()],"test");Assert.NotEqual(first.Dependencies[0].AssemblyName,second.Dependencies[0].AssemblyName);Assert.NotEqual(first.Manifest.Queries[0].SchemaFingerprint,second.Manifest.Queries[0].SchemaFingerprint);ExecuteCompiledNative(second,host,8);
        Assert.Throws<InvalidDataException>(()=>new LoadedNativeQueryModule(first,new Dictionary<string,Assembly>{{"a",((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database.GetType().Assembly}}));
        Execute("ALTER TABLE dbo.Items DROP COLUMN N");var error=await Assert.ThrowsAsync<InvalidDataException>(()=>compiler.BuildNativeAsync(source,[Input()],"test"));Assert.Contains("did not compile",error.Message);
        var logDirectory=error.Message[(error.Message.IndexOf("Logs: ",StringComparison.Ordinal)+6)..];Assert.Contains("CS1061",await File.ReadAllTextAsync(Path.Combine(logDirectory,"build.stdout.log")));
        using var disabled=await store.PrepareAsync(source,[Input()],"test",true);Assert.False(disabled.IsEnabled);Assert.Contains("CS1061",disabled.Diagnostic);using var restored=store.Read(disabled.Reference);Assert.False(restored.IsEnabled);Assert.Equal(disabled.Diagnostic,restored.Diagnostic);
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ExecuteStoredNative(GatewayNativeQueryVersion version,GatewayDatabaseHost host,int expected){
        var database=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database;version.Bind(new Dictionary<string,Assembly>{{"a",database.GetType().Assembly}});
        Assert.Equal(expected,MemoryPackSerializer.Deserialize<int>(Assert.Single(version.Module.Execute("n",ReadOnlyMemory<byte>.Empty,new CompilerNativeContext(database)))));
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ExecuteCompiledNative(NativeQueryBuildArtifact artifact,GatewayDatabaseHost host,int expected){
        var database=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database;
        using var loaded=new LoadedNativeQueryModule(artifact,new Dictionary<string,Assembly>{{"a",database.GetType().Assembly}});
        Assert.Equal(expected,MemoryPackSerializer.Deserialize<int>(Assert.Single(loaded.Module.Execute("n",ReadOnlyMemory<byte>.Empty,new CompilerNativeContext(database)))));return loaded.UnloadReference;
    }
    private sealed class CompilerNativeContext(object database):INativeQueryContext{public object Database=>database;public string DatabaseName=>"a";public CancellationToken CancellationToken=>CancellationToken.None;}

    [Fact,Trait("Category","Integration")]
    public async Task CheckpointCollectsUnreferencedGenerationsAndKeepsThePreviousPublication(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");
        var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");
        var root=Path.Combine(data,"generation-collection-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS"),new("b",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(125,"a");
            void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            Execute("CREATE TABLE dbo.T(Id int PRIMARY KEY); INSERT dbo.T VALUES(10); ALTER TABLE dbo.T ADD N int NULL");
            var discarded=RollbackColumnDrop(host,root);
            var generations=Path.Combine(root,"generations","1");Assert.True(Directory.GetDirectories(generations).Length>=4);
            // The intermediate candidate remains available through rollback; no collection occurs before quiescence.
            Assert.NotNull(host.Databases.Resolve("a").Adapter.FindTable("dbo.T")!.Columns.SingleOrDefault(static column=>column.Name=="N"));
            for(var attempt=0;attempt<50&&discarded.IsAlive;attempt++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(10);}Assert.False(discarded.IsAlive);host.Transactions.Checkpoint();
            var report=Assert.IsType<GatewayGenerationCollectionResult>(host.Transactions.LastGenerationCollection);
            Assert.Empty(report.Deferred);Assert.True(report.Removed.Count>=2);Assert.Equal(2,Directory.GetDirectories(generations).Length);
            Assert.Single(Directory.GetDirectories(Path.Combine(root,"generations","2")));
            Execute("ALTER TABLE dbo.T ADD M bigint NULL");GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();host.Transactions.Checkpoint();
            Assert.Empty(host.Transactions.LastGenerationCollection!.Deferred);Assert.Equal(2,Directory.GetDirectories(generations).Length);
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(126,"a");
            Assert.Equal(new object?[]{10,null,null},Assert.Single(engine.Execute("SELECT Id,N,M FROM dbo.T",session).Rows));
            Assert.Equal(0,Assert.Single(engine.Execute("SELECT COUNT(*) FROM b.sys.tables",session).Rows)[0]);
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference RollbackColumnDrop(GatewayDatabaseHost host,string root){
        var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(127,"a");var directory=Path.Combine(root,"generations","1");var previous=Directory.GetDirectories(directory);
        foreach(var result in engine.ExecuteBatch("BEGIN TRANSACTION; SAVE TRANSACTION point; ALTER TABLE dbo.T DROP COLUMN N",session)){foreach(var row in result.Rows){}}
        var id=Path.GetFileName(Directory.GetDirectories(directory).Except(previous).Single());
        var target=new WeakReference(global::System.Runtime.Loader.AssemblyLoadContext.All.Single(context=>context.Assemblies.Any(assembly=>assembly.GetName().Name=="Seamless.Generated.Generation_"+id)));
        foreach(var result in engine.ExecuteBatch("ROLLBACK TRANSACTION point; ROLLBACK TRANSACTION",session)){foreach(var row in result.Rows){}}
        return target;
    }

    [Fact,Trait("Category","Integration")]
    public async Task ReferencedKeyChangesUseStoredValuesInsteadOfArrayReferencesOrCollationEquality(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");
        var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");
        var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"stored-keys-"+Guid.NewGuid().ToString("N")),GeneratedAssemblyBuilderTests.Compiler(data,cache),options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);
        var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(124,"a");
        void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
        object? Scalar(string sql)=>Assert.Single(engine.Execute(sql,session).Rows)[0];
        Execute("CREATE TABLE dbo.BinaryParent(K varbinary(8) NOT NULL PRIMARY KEY,N int); CREATE TABLE dbo.BinaryChild(Id int PRIMARY KEY,K varbinary(8) NULL REFERENCES dbo.BinaryParent(K) ON UPDATE SET NULL); INSERT dbo.BinaryParent VALUES(0x0102,1); INSERT dbo.BinaryChild VALUES(1,0x0102); UPDATE dbo.BinaryParent SET N=2");
        Assert.Equal(new byte[]{1,2},Assert.IsType<byte[]>(Scalar("SELECT K FROM dbo.BinaryChild")));
        Execute("UPDATE dbo.BinaryParent SET K=0x010200");Assert.Null(Scalar("SELECT K FROM dbo.BinaryChild"));
        Execute("CREATE TABLE dbo.StringParent(K nvarchar(8) COLLATE Japanese_CI_AS NOT NULL PRIMARY KEY,N int); CREATE TABLE dbo.StringChild(Id int PRIMARY KEY,K nvarchar(8) COLLATE Japanese_CI_AS NULL REFERENCES dbo.StringParent(K) ON UPDATE SET NULL); INSERT dbo.StringParent VALUES(N'A',1); INSERT dbo.StringChild VALUES(1,N'A'); UPDATE dbo.StringParent SET N=2");
        Assert.Equal("A",Scalar("SELECT K FROM dbo.StringChild"));Execute("UPDATE dbo.StringParent SET K=N'a'");Assert.Null(Scalar("SELECT K FROM dbo.StringChild"));
    }

    [Fact,Trait("Category","Integration")]
    public async Task BinaryKeysAndNativeRowArraysHaveValueOwnershipInsideTransactions(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-binary-ownership-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};using var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(124,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
        Execute("CREATE TABLE dbo.BinaryKey(K varbinary(8) NOT NULL PRIMARY KEY,N int NOT NULL); BEGIN TRANSACTION; INSERT dbo.BinaryKey VALUES(0x0102,1); UPDATE dbo.BinaryKey SET N=2 WHERE K=0x0102");Assert.Equal(2,Assert.Single(engine.Execute("SELECT N FROM dbo.BinaryKey",session).Rows)[0]);Execute("SAVE TRANSACTION point; DELETE dbo.BinaryKey WHERE K=0x0102; ROLLBACK TRANSACTION point; COMMIT TRANSACTION");Assert.Equal(2,Assert.Single(engine.Execute("SELECT N FROM dbo.BinaryKey",session).Rows)[0]);
        Execute("CREATE TABLE dbo.Payload(Id int NOT NULL PRIMARY KEY,B varbinary(8) NOT NULL)");var fingerprint=host.Databases.Resolve("a").SchemaFingerprint;var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];using var native=new NativeQueryServer(host.Databases,0);native.Register(new("ownership","","","",version,[new("insert","a",fingerprint,"int","int",false),new("mutate","a",fingerprint,"int","int"),new("read","a",fingerprint,"int","int")]),new ArrayOwnershipModule(),version);native.Start();await using var client=new NativeQueryClient("127.0.0.1",native.Port);await using(var transaction=await client.BeginTransactionAsync("a")){async Task<int> Query(string name){var values=new List<int>();await foreach(var value in transaction.QueryAsync<int,int>("ownership",name,fingerprint,1)){values.Add(value);}return Assert.Single(values);}Assert.Equal(1,await Query("insert"));Assert.Equal(1,await Query("read"));Assert.Equal(9,await Query("mutate"));Assert.Equal(1,await Query("read"));await transaction.CommitAsync();}Assert.Equal(new byte[]{1},Assert.Single(engine.Execute("SELECT B FROM dbo.Payload",session).Rows)[0]);await native.StopAsync();
    }
    private sealed class ArrayOwnershipModule:INativeQueryModule{
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){var schema=context.Database.GetType().GetProperty("dbo")!.GetValue(context.Database)!;var table=schema.GetType().GetProperty("Payload")!.GetValue(schema)!;var types=table.GetType().GetGenericArguments();var key=Activator.CreateInstance(types[0],[1])!;if(queryId=="insert"){var bytes=new byte[]{1};var row=Activator.CreateInstance(types[1],[key,bytes])!;table.GetType().GetMethod("Add",[types[1]])!.Invoke(table,[row]);bytes[0]=7;yield return MemoryPackSerializer.Serialize(1);yield break;}var record=table.GetType().GetProperty("Item",[types[0]])!.GetValue(table,[key])!;var value=(byte[])types[1].GetProperty("B")!.GetValue(record)!;if(queryId=="mutate"){value[0]=9;}yield return MemoryPackSerializer.Serialize((int)value[0]);}
    }

    [Fact,Trait("Category","Integration")]
    public async Task CorruptLiveCheckpointStopsAdmissionInsteadOfRevertingToAnOlderGeneration(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-corrupt-checkpoint-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};string live;
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(123,"a");foreach(var result in engine.ExecuteBatch("CREATE TABLE dbo.Items(Id int NOT NULL PRIMARY KEY,N int NOT NULL); INSERT dbo.Items VALUES(1,5); ALTER TABLE dbo.Items ADD Extra int NULL; UPDATE dbo.Items SET N=6",session)){foreach(var row in result.Rows){}}host.Transactions.Checkpoint();live=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database.Options.StorageDirectory!;}
        var path=Path.Combine(live,"store-v2","store.json");var manifest=global::System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;var checkpoints=manifest["Checkpoints"]!.AsArray();var last=checkpoints[checkpoints.Count-1]!;var state=global::System.Text.Json.Nodes.JsonNode.Parse(last["Components"]!["~transaction-checkpoint-v2"]!.GetValue<string>())!;Assert.Equal(5,state["Version"]!.GetValue<int>());state["DataSha256"]=new string('0',64);last["Components"]!["~transaction-checkpoint-v2"]=state.ToJsonString();File.WriteAllText(path,manifest.ToJsonString());
        var failure=await Assert.ThrowsAsync<InvalidDataException>(async()=>{using var rejected=await GatewayDatabaseHost.OpenAsync(root,compiler,options);});Assert.Contains("checkpoint data hash",failure.Message,StringComparison.OrdinalIgnoreCase);
    }

    [Fact,Trait("Category","Integration")]
    public async Task BootstrapImportsTypedRowsPreservesDuplicatesAndIdentityWatermarks(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-import-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};var id=new ColumnCatalog(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),true){IdentitySeed=10,IdentityIncrement=2,InitialIdentityValue=40};var number=new ColumnCatalog(1,"N",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false);var primary=new PrimaryKeyCatalog("PK_Imported",[new("Id",false)]);var definition=new DatabaseCatalog("a",[new SchemaCatalog("dbo",[new TableCatalog("Imported",[id,number],primary),new TableCatalog("Duplicates",[number with{Ordinal=0}],null)])],"Japanese_CI_AS");var disposed=0;
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[definition],initialDataSource:(_,generated)=>new BootstrapImportSource(generated,()=>disposed++))){Assert.Equal(1,disposed);var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(121,"a");Assert.Equal(2,engine.Execute("SELECT N FROM dbo.Duplicates",session).Rows.Count());Assert.Equal(new object?[]{30,32},engine.Execute("SELECT Id FROM dbo.Imported ORDER BY Id",session).Rows.Select(static row=>row[0]));foreach(var result in engine.ExecuteBatch("INSERT dbo.Imported(N)VALUES(9)",session)){foreach(var row in result.Rows){}}Assert.Equal(42,Assert.Single(engine.Execute("SELECT Id FROM dbo.Imported WHERE N=9",session).Rows)[0]);host.Transactions.Checkpoint();}
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(122,"a");Assert.Equal(2,engine.Execute("SELECT N FROM dbo.Duplicates",session).Rows.Count());Assert.Equal(new object?[]{30,32,42},engine.Execute("SELECT Id FROM dbo.Imported ORDER BY Id",session).Rows.Select(static row=>row[0]));}
    }
    private sealed class BootstrapImportSource(GeneratedDatabase generated,Action disposed):IDatabaseImportSource,IDisposable{
        public IEnumerable<TRow> ReadTable<TRow>(global::Seamless.Database.System.Keys.DatabaseObject key){var table=generated.Tables.Single(table=>table.ObjectName==key.Name);var shape=GeneratedRecordShape.TryCreate(typeof(TRow))!;for(var row=0;row<2;row++){var values=table.Columns.ToDictionary(static column=>column.PropertyName,column=>column.IsSynthetic?(object)(long)(row+1):column.SourceName=="Id"?30+row*2:7);yield return(TRow)shape.Create(name=>values[name]);}}
        public void Dispose()=>disposed();
    }

    [Fact,Trait("Category","Integration")]
    public async Task HeldSqlPlanAndServerReleaseTheReplacedAssembly(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-plan-unload-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[new TableCatalog("Items",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new PrimaryKeyCatalog("PK_Items",[new("Id",false)]))])],"Japanese_CI_AS")]);using var server=new GatewayServer(host.Databases,new(){TdsPort=0,HttpPort=0,NativePort=0});var state=ReplaceAndKeepPlan(host);Assert.Equal(3961,Assert.Throws<SqlQueryException>(()=>state.Plan.Execute().Rows.ToArray()).Number);
        for(var attempt=0;attempt<50&&state.Assembly.IsAlive;attempt++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(10);}Assert.False(state.Assembly.IsAlive);GC.KeepAlive(state.Plan);GC.KeepAlive(server);
    }
    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WeakReference Assembly,SqlQueryPlan Plan) ReplaceAndKeepPlan(GatewayDatabaseHost host){
        var adapter=(InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter;var assembly=new WeakReference(global::System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(adapter.Database.GetType().Assembly)!);var queries=new SqlQueryEngine(host.Databases);var plan=queries.Prepare("SELECT Id FROM dbo.Items","a");var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(120,"a");foreach(var result in engine.ExecuteBatch("ALTER TABLE dbo.Items ADD N int NULL",session)){foreach(var row in result.Rows){}}return(assembly,plan);
    }

    [Fact,Trait("Category","Integration")]
    public async Task ComputedColumnsUseTypedGettersAndPersistTheCalculatedValues(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-computed-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(118,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}object?[] Row()=>Assert.Single(engine.Execute("SELECT N,Doubled,Stored FROM dbo.Computed",session).Rows);
            Execute("CREATE TABLE dbo.Computed(Id int NOT NULL PRIMARY KEY,N int NOT NULL,Doubled AS N*2,Stored AS N*3 PERSISTED); INSERT dbo.Computed(Id,N)VALUES(1,4)");Assert.Equal(new object?[]{4,8,12},Row());Execute("UPDATE dbo.Computed SET N=5");Assert.Equal(new object?[]{5,10,15},Row());Assert.Equal(271,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.Computed(Id,N,Doubled)VALUES(2,1,2)")).Number);
            var metadata=engine.Execute("SELECT name,is_nullable,is_persisted FROM sys.computed_columns WHERE object_id=OBJECT_ID('dbo.Computed') ORDER BY column_id",session).Rows.ToArray();Assert.Equal(new object?[]{"Doubled",true,false},metadata[0]);Assert.Equal(new object?[]{"Stored",true,true},metadata[1]);
            var adapter=(InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter;using(var read=adapter.AcquireReadSession(null)){var schema=adapter.Database.GetType().GetProperty("dbo")!.GetValue(adapter.Database)!;var table=schema.GetType().GetProperty("Computed")!.GetValue(schema)!;var stored=Assert.Single(((global::System.Collections.IEnumerable)table).Cast<object>());var row=global::System.Linq.Expressions.Expression.Parameter(typeof(object),"row");var storedValue=global::System.Linq.Expressions.Expression.Lambda<Func<object,object?>>(global::System.Linq.Expressions.Expression.Convert(GeneratedRecordShape.TryCreate(stored.GetType())!.Stored(global::System.Linq.Expressions.Expression.Convert(row,stored.GetType()),"Stored"),typeof(object)),row).Compile();Assert.Equal(15,storedValue(stored));}
            Execute("BEGIN TRANSACTION; SAVE TRANSACTION point; ALTER TABLE dbo.Computed ADD Extra AS N+10 PERSISTED; UPDATE dbo.Computed SET N=7");Assert.Equal(17,Assert.Single(engine.Execute("SELECT Extra FROM dbo.Computed",session).Rows)[0]);Execute("ROLLBACK TRANSACTION point; COMMIT TRANSACTION");Assert.Equal(new object?[]{5,10,15},Row());Assert.Equal(5074,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.Computed ALTER COLUMN N bigint NOT NULL")).Number);Assert.Equal(4936,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.Computed ADD Bad AS GETDATE() PERSISTED")).Number);
            Execute("CREATE TABLE dbo.ComputedKey(N int NOT NULL,K AS N+1 PRIMARY KEY); INSERT dbo.ComputedKey(N)VALUES(1),(2); ALTER TABLE dbo.ComputedKey ADD Extra int NULL");Assert.Equal(new object?[]{2,3},engine.Execute("SELECT K FROM dbo.ComputedKey ORDER BY K",session).Rows.Select(static row=>row[0]));
            Execute("ALTER TABLE dbo.Computed ADD Clock AS SYSUTCDATETIME()");var first=(DateTime)Assert.Single(engine.Execute("SELECT Clock FROM dbo.Computed",session).Rows)[0]!;await Task.Delay(20);var second=(DateTime)Assert.Single(engine.Execute("SELECT Clock FROM dbo.Computed",session).Rows)[0]!;Assert.True(second>first);
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(119,"a");Assert.Equal(new object?[]{5,10,15},Assert.Single(engine.Execute("SELECT N,Doubled,Stored FROM dbo.Computed",session).Rows));}
    }

    [Fact,Trait("Category","Integration")]
    public async Task ReferentialActionsCascadeAndRestoreEveryAffectedTable(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-cascade-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(117,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}object? Scalar(string sql)=>Assert.Single(engine.Execute(sql,session).Rows)[0];
        Execute("CREATE TABLE dbo.Parent(Id int NOT NULL PRIMARY KEY); CREATE TABLE dbo.Child(Id int NOT NULL PRIMARY KEY,Pid int NOT NULL CONSTRAINT FK_Child FOREIGN KEY REFERENCES dbo.Parent(Id) ON UPDATE CASCADE ON DELETE CASCADE); CREATE TABLE dbo.Nullable(Id int NOT NULL PRIMARY KEY,Pid int NULL CONSTRAINT FK_Nullable FOREIGN KEY REFERENCES dbo.Parent(Id) ON DELETE SET NULL ON UPDATE CASCADE); CREATE TABLE dbo.Defaulted(Id int NOT NULL PRIMARY KEY,Pid int NOT NULL DEFAULT(0) CONSTRAINT FK_Defaulted FOREIGN KEY REFERENCES dbo.Parent(Id) ON DELETE SET DEFAULT ON UPDATE CASCADE); INSERT dbo.Parent VALUES(0),(1); INSERT dbo.Child VALUES(1,1); INSERT dbo.Nullable VALUES(1,1); INSERT dbo.Defaulted VALUES(1,1)");
        Execute("BEGIN TRANSACTION; SAVE TRANSACTION point; UPDATE dbo.Parent SET Id=2 WHERE Id=1");Assert.Equal(2,Scalar("SELECT Pid FROM dbo.Child"));Assert.Equal(2,Scalar("SELECT Pid FROM dbo.Nullable"));Assert.Equal(2,Scalar("SELECT Pid FROM dbo.Defaulted"));Assert.Equal(0,Scalar("SELECT COUNT(*) FROM dbo.Parent WHERE Id=1"));Execute("ROLLBACK TRANSACTION point");Assert.Equal(1,Scalar("SELECT Pid FROM dbo.Child"));
        Execute("DELETE dbo.Parent WHERE Id=1");Assert.Equal(0,Scalar("SELECT COUNT(*) FROM dbo.Child"));Assert.Null(Scalar("SELECT Pid FROM dbo.Nullable"));Assert.Equal(0,Scalar("SELECT Pid FROM dbo.Defaulted"));Execute("ROLLBACK TRANSACTION");Assert.Equal(1,Scalar("SELECT Pid FROM dbo.Child"));Assert.Equal(1,Scalar("SELECT Pid FROM dbo.Nullable"));Assert.Equal(1,Scalar("SELECT Pid FROM dbo.Defaulted"));
        Assert.Equal(1785,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.Parent ADD ParentId int NULL CONSTRAINT FK_Cycle FOREIGN KEY REFERENCES dbo.Parent(Id) ON DELETE CASCADE")).Number);Assert.Equal(1778,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.Child ADD Wrong bigint NULL CONSTRAINT FK_Type FOREIGN KEY REFERENCES dbo.Parent(Id)")).Number);
        var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<global::System.Reflection.AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];var fingerprint=host.Databases.Resolve("a").SchemaFingerprint;using(var native=new NativeQueryServer(host.Databases,0)){native.Register(new("cascade","","","",version,[new("delete","a",fingerprint,"int","int",false)]),new DirectDeleteModule(),version);native.Start();await using var client=new NativeQueryClient("127.0.0.1",native.Port);await using(var transaction=await client.BeginTransactionAsync("a")){var counts=new List<int>();await foreach(var count in transaction.QueryAsync<int,int>("cascade","delete",fingerprint,1)){counts.Add(count);}Assert.Equal(0,Assert.Single(counts));await transaction.RollbackAsync();}Assert.Equal(1,Scalar("SELECT Pid FROM dbo.Child"));await native.StopAsync();}
        Execute("DELETE dbo.Parent WHERE Id=0");Assert.Equal(547,Assert.Throws<SqlQueryException>(()=>Execute("DELETE dbo.Parent WHERE Id=1")).Number);Assert.Equal(1,Scalar("SELECT Pid FROM dbo.Child"));Assert.Equal(1,Scalar("SELECT Pid FROM dbo.Nullable"));Assert.Equal(1,Scalar("SELECT Pid FROM dbo.Defaulted"));
    }
    private sealed class DirectDeleteModule:INativeQueryModule{
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){var id=MemoryPackSerializer.Deserialize<int>(arguments.Span);var schema=context.Database.GetType().GetProperty("dbo")!.GetValue(context.Database)!;var table=schema.GetType().GetProperty("Parent")!.GetValue(schema)!;var key=Activator.CreateInstance(table.GetType().GetGenericArguments()[0],[id])!;table.GetType().GetMethod("Remove",[key.GetType()])!.Invoke(table,[key]);var child=schema.GetType().GetProperty("Child")!.GetValue(schema)!;yield return MemoryPackSerializer.Serialize((int)child.GetType().GetProperty("Count")!.GetValue(child)!);}
    }

    [Fact,Trait("Category","Integration")]
    public async Task IdentityAndSchemaIdentifiersAreNeverReusedAfterRollbackOrRecovery(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-identity-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};int? discardedId;
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(115,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}object?[][] Rows(string sql)=>engine.Execute(sql,session).Rows.ToArray();
            Execute("CREATE TABLE dbo.Sequence(Id int IDENTITY(10,2) PRIMARY KEY,N int NOT NULL); INSERT dbo.Sequence(N)VALUES(1),(2)");Assert.Equal(new object?[]{10,12},Rows("SELECT Id FROM dbo.Sequence ORDER BY Id").Select(static row=>row[0]));
            Execute("BEGIN TRANSACTION; SAVE TRANSACTION point; INSERT dbo.Sequence(N)VALUES(3); ROLLBACK TRANSACTION point; INSERT dbo.Sequence(N)VALUES(4); COMMIT TRANSACTION");Assert.Equal(16,Assert.Single(Rows("SELECT Id FROM dbo.Sequence WHERE N=4"))[0]);
            Assert.Equal(544,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.Sequence(Id,N)VALUES(90,9)")).Number);Execute("SET IDENTITY_INSERT dbo.Sequence ON; INSERT dbo.Sequence(Id,N)VALUES(90,9); SET IDENTITY_INSERT dbo.Sequence OFF; INSERT dbo.Sequence(N)VALUES(10)");Assert.Equal(92,Assert.Single(Rows("SELECT Id FROM dbo.Sequence WHERE N=10"))[0]);
            Execute("BEGIN TRANSACTION; INSERT dbo.Sequence(N)VALUES(11); ROLLBACK TRANSACTION; BEGIN TRANSACTION; CREATE TABLE dbo.Discarded(N int)");discardedId=Convert.ToInt32(Assert.Single(Rows("SELECT object_id FROM sys.tables WHERE name=N'Discarded'"))[0]);Execute("ROLLBACK TRANSACTION; CREATE TABLE dbo.Retained(N int)");Assert.True(host.Databases.Resolve("a").Adapter.FindTable("dbo.Retained")!.StableObjectId>discardedId);host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(116,"a");foreach(var result in engine.ExecuteBatch("INSERT dbo.Sequence(N)VALUES(12)",session)){foreach(var row in result.Rows){}}Assert.Equal(96,Assert.Single(engine.Execute("SELECT Id FROM dbo.Sequence WHERE N=12",session).Rows)[0]);}
    }

    [Fact,Trait("Category","Integration")]
    public async Task ScalarFunctionsSupportLoopsTableReadsCallsRecursionAndTransactionalReplacement(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-udf-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(113,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}object? Scalar(string sql)=>Assert.Single(engine.Execute(sql,session).Rows)[0];
            Execute("CREATE TABLE dbo.Source(Id int NOT NULL PRIMARY KEY,N int NOT NULL); INSERT dbo.Source VALUES(1,40)");
            Execute("CREATE FUNCTION dbo.Calculate(@n int) RETURNS int AS BEGIN DECLARE @i int=0,@sum int=0,@base int; SELECT @base=N FROM dbo.Source WHERE Id=1; WHILE @i<@n BEGIN SET @i=@i+1; IF @i=2 CONTINUE; IF @i=5 BREAK; SET @sum=@sum+@i; END; RETURN @sum+@base; END");Assert.Equal(48,Scalar("SELECT dbo.Calculate(10)"));
            Execute("CREATE FUNCTION dbo.Recursive(@n int) RETURNS int AS BEGIN IF @n=0 RETURN 0; RETURN @n+dbo.Recursive(@n-1); END");Assert.Equal(10,Scalar("SELECT dbo.Recursive(4)"));Assert.Equal(217,Assert.Throws<SqlQueryException>(()=>Scalar("SELECT dbo.Recursive(33)")).Number);
            Execute("CREATE FUNCTION dbo.Fill() RETURNS int AS BEGIN RETURN dbo.Calculate(3); END");Execute("CREATE TABLE dbo.Target(Id int NOT NULL PRIMARY KEY,N int NOT NULL DEFAULT(dbo.Fill())); INSERT dbo.Target(Id) VALUES(1)");Assert.Equal(44,Scalar("SELECT N FROM dbo.Target WHERE Id=1"));
            Execute("UPDATE dbo.Target SET N=100; UPDATE dbo.Target SET N=DEFAULT");Assert.Equal(44,Scalar("SELECT N FROM dbo.Target WHERE Id=1"));
            Assert.Equal(3729,Assert.Throws<SqlQueryException>(()=>Execute("ALTER FUNCTION dbo.Fill() RETURNS int AS BEGIN RETURN 99; END")).Number);Execute("BEGIN TRANSACTION; SAVE TRANSACTION point; ALTER TABLE dbo.Target DROP CONSTRAINT DF_Target_N");Execute("ALTER FUNCTION dbo.Fill() RETURNS int AS BEGIN RETURN 99; END");Execute("ALTER TABLE dbo.Target ADD CONSTRAINT DF_Target_N2 DEFAULT(dbo.Fill()) FOR N; INSERT dbo.Target(Id) VALUES(2)");Assert.Equal(99,Scalar("SELECT N FROM dbo.Target WHERE Id=2"));Execute("ROLLBACK TRANSACTION point; COMMIT TRANSACTION");Assert.Equal(44,Scalar("SELECT dbo.Fill()"));
            Assert.Equal(443,Assert.Throws<SqlQueryException>(()=>Execute("CREATE FUNCTION dbo.Bad() RETURNS uniqueidentifier AS BEGIN RETURN NEWID(); END")).Number);Assert.Equal(3729,Assert.Throws<SqlQueryException>(()=>Execute("DROP FUNCTION dbo.Fill")).Number);
            Execute("CREATE FUNCTION dbo.Standalone() RETURNS int AS BEGIN RETURN 3; END");Execute("DROP FUNCTION dbo.Standalone");
            Execute("CREATE FUNCTION dbo.Bound() RETURNS int WITH SCHEMABINDING AS BEGIN DECLARE @n int; SELECT @n=N FROM dbo.Source WHERE Id=1; RETURN @n; END");Assert.Equal(40,Scalar("SELECT dbo.Bound()"));Assert.Equal(5074,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.Source ALTER COLUMN N bigint NOT NULL")).Number);Assert.Equal(3729,Assert.Throws<SqlQueryException>(()=>Execute("DROP TABLE dbo.Source")).Number);Execute("DROP FUNCTION dbo.Bound");
            Execute("CREATE FUNCTION dbo.NullInput(@n int) RETURNS int WITH RETURNS NULL ON NULL INPUT AS BEGIN RETURN ISNULL(@n,123); END");Assert.Null(Scalar("SELECT dbo.NullInput(NULL)"));Assert.Equal(3,Scalar("SELECT dbo.NullInput(3)"));
            Execute("BEGIN TRANSACTION; SAVE TRANSACTION ddlerror");Assert.Equal(3729,Assert.Throws<SqlQueryException>(()=>Execute("ALTER FUNCTION dbo.Fill() RETURNS int AS BEGIN RETURN 1; END")).Number);Assert.Equal(0,session.TransactionCount);Assert.Equal(0,session.TransactionState);
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(114,"a");Assert.Equal(44,Assert.Single(engine.Execute("SELECT dbo.Fill()",session).Rows)[0]);}
    }

    [Fact,Trait("Category","Integration")]
    public async Task DefaultsBackfillOnceAndPersistWithoutReevaluation(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-defaults-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20};object?[][] expected;
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(111,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            Execute("CREATE TABLE dbo.Defaults(Id int NOT NULL PRIMARY KEY,N int NOT NULL CONSTRAINT DF_N DEFAULT(7),G uniqueidentifier NOT NULL DEFAULT(NEWID()),D datetime2(7) NOT NULL DEFAULT(SYSUTCDATETIME())); INSERT dbo.Defaults(Id) VALUES(1),(2)");var initial=engine.Execute("SELECT N,G,D FROM dbo.Defaults ORDER BY Id",session).Rows.ToArray();Assert.All(initial,row=>Assert.Equal(7,row[0]));Assert.NotEqual(initial[0][1],initial[1][1]);Assert.Equal(initial[0][2],initial[1][2]);
            Execute("ALTER TABLE dbo.Defaults ADD Filled int NOT NULL DEFAULT(9); ALTER TABLE dbo.Defaults ADD Empty int NULL DEFAULT(10); ALTER TABLE dbo.Defaults ADD WithValues int NULL DEFAULT(11) WITH VALUES");var migrated=engine.Execute("SELECT Filled,Empty,WithValues FROM dbo.Defaults",session).Rows.ToArray();Assert.All(migrated,row=>Assert.Equal(new object?[]{9,null,11},row));
            Execute("BEGIN TRANSACTION; SAVE TRANSACTION point; ALTER TABLE dbo.Defaults DROP CONSTRAINT DF_N; ALTER TABLE dbo.Defaults ADD CONSTRAINT DF_N2 DEFAULT(21) FOR N; INSERT dbo.Defaults(Id) VALUES(3)");Assert.Equal(21,Assert.Single(engine.Execute("SELECT N FROM dbo.Defaults WHERE Id=3",session).Rows)[0]);Execute("ROLLBACK TRANSACTION point; COMMIT TRANSACTION; INSERT dbo.Defaults(Id,N) VALUES(3,DEFAULT)");Assert.Equal(7,Assert.Single(engine.Execute("SELECT N FROM dbo.Defaults WHERE Id=3",session).Rows)[0]);expected=engine.Execute("SELECT Id,N,G,D,Filled,Empty,WithValues FROM dbo.Defaults ORDER BY Id",session).Rows.ToArray();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(112,"a");var actual=engine.Execute("SELECT Id,N,G,D,Filled,Empty,WithValues FROM dbo.Defaults ORDER BY Id",session).Rows.ToArray();Assert.Equal(expected.Length,actual.Length);for(var index=0;index<expected.Length;index++){Assert.Equal(expected[index],actual[index]);}}
    }

    [Fact,Trait("Category","Integration")]
    public async Task ConstraintChangesValidateDataAndRestoreKeysAndForeignKeysOnRollback(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"typed-constraints-"+Guid.NewGuid().ToString("N"));using var host=await GatewayDatabaseHost.OpenAsync(root,GeneratedAssemblyBuilderTests.Compiler(data,cache),new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(101,"a");
        void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
        Execute("CREATE TABLE dbo.Parent(Id int NOT NULL CONSTRAINT PK_Parent PRIMARY KEY,Code nvarchar(12) NULL,CONSTRAINT UQ_Code UNIQUE(Code),CONSTRAINT CK_Positive CHECK(Id>0)); CREATE TABLE dbo.Child(Id int NOT NULL PRIMARY KEY,ParentId int NULL,CONSTRAINT FK_Parent FOREIGN KEY(ParentId) REFERENCES dbo.Parent(Id)); INSERT dbo.Parent(Id,Code) VALUES(1,N'abc'); INSERT dbo.Child(Id,ParentId) VALUES(1,1)");
        Assert.Equal(2627,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.Parent(Id,Code) VALUES(2,N'ABC')")).Number);Assert.Equal(547,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.Child(Id,ParentId) VALUES(2,99)")).Number);Assert.Equal(547,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.Parent(Id,Code) VALUES(-1,N'bad')")).Number);
        Execute("BEGIN TRANSACTION; SAVE TRANSACTION point");Assert.Equal(547,Assert.Throws<SqlQueryException>(()=>Execute("DELETE dbo.Parent WHERE Id=1")).Number);Assert.Equal(1,session.TransactionState);Assert.Equal(547,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.Parent ADD CONSTRAINT CK_TooLarge CHECK(Id>10)")).Number);Assert.Equal(0,session.TransactionCount);Assert.Equal(0,session.TransactionState);
        Execute("BEGIN TRANSACTION; SAVE TRANSACTION point; ALTER TABLE dbo.Child DROP CONSTRAINT FK_Parent; ALTER TABLE dbo.Parent DROP CONSTRAINT PK_Parent; INSERT dbo.Parent(Id,Code) VALUES(1,N'new')");Assert.Equal(2,engine.Execute("SELECT Id FROM dbo.Parent",session).Rows.Count());
        Execute("ROLLBACK TRANSACTION point");Assert.Single(engine.Execute("SELECT Id FROM dbo.Parent",session).Rows);Assert.Equal(547,Assert.Throws<SqlQueryException>(()=>Execute("DELETE dbo.Parent WHERE Id=1")).Number);Execute("COMMIT TRANSACTION");
        Execute("BEGIN TRANSACTION; ALTER TABLE dbo.Child DROP CONSTRAINT FK_Parent; ALTER TABLE dbo.Parent DROP CONSTRAINT PK_Parent; INSERT dbo.Parent(Id,Code) VALUES(1,N'new')");Assert.Equal(2627,Assert.Throws<SqlQueryException>(()=>Execute("ALTER TABLE dbo.Parent ADD CONSTRAINT PK_New PRIMARY KEY(Id)")).Number);if(session.IsInTransaction){Execute("ROLLBACK TRANSACTION");}Assert.Single(engine.Execute("SELECT Id FROM dbo.Parent",session).Rows);
    }

    [Fact,Trait("Category","Integration")]
    public async Task BroadTypedColumnsRoundTripThroughTdsAndAnUnchangedTableCopy(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"wide-typed-ddl-"+Guid.NewGuid().ToString("N"));
        using var host=await GatewayDatabaseHost.OpenAsync(root,GeneratedAssemblyBuilderTests.Compiler(data,cache),new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);var engine=new SqlExecutionEngine(host.Databases);using var server=new TdsServer(0,host.Databases.Resolve("a").Adapter,engine);server.Start();
        using var connection=new SqlConnection(new SqlConnectionStringBuilder{DataSource="127.0.0.1,"+server.Port,InitialCatalog="a",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10}.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandTimeout=120;
        command.CommandText="CREATE TABLE dbo.Wide(Id int NOT NULL PRIMARY KEY,B bit NULL,T tinyint NULL,S smallint NULL,L bigint NULL,N decimal(18,4) NULL,R real NULL,F float NULL,G uniqueidentifier NULL,P varbinary(max) NULL,X xml NULL,D date NULL,C time(7) NULL,A datetime2(7) NULL,O datetimeoffset(7) NULL,V varchar(12) NULL,U nvarchar(20) NULL)";await command.ExecuteNonQueryAsync();
        var guid=Guid.Parse("12345678-1234-4321-abcd-123456789012");var stamp=new DateTime(2020,2,3,4,5,6).AddTicks(1234567);var offset=new DateTimeOffset(stamp,TimeSpan.FromHours(9));var clock=new TimeSpan(0,4,5,6).Add(TimeSpan.FromTicks(1234567));
        command.CommandText="INSERT dbo.Wide(Id,B,T,S,L,N,R,F,G,P,X,D,C,A,O,V,U) VALUES(1,@b,@t,@s,@l,@n,@r,@f,@g,@p,@x,@d,@c,@a,@o,@v,@u)";
        void Parameter(string name,SqlDbType type,object value){command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter(name,type){Value=value});}
        Parameter("@b",SqlDbType.Bit,false);Parameter("@t",SqlDbType.TinyInt,byte.MaxValue);Parameter("@s",SqlDbType.SmallInt,short.MinValue);Parameter("@l",SqlDbType.BigInt,long.MaxValue);Parameter("@n",SqlDbType.Decimal,-1.2345m);command.Parameters["@n"].Precision=18;command.Parameters["@n"].Scale=4;Parameter("@r",SqlDbType.Real,0.1f);Parameter("@f",SqlDbType.Float,Math.BitIncrement(1d));Parameter("@g",SqlDbType.UniqueIdentifier,guid);Parameter("@p",SqlDbType.VarBinary,new byte[]{0,1,255});Parameter("@x",SqlDbType.Xml,"<r>日本語</r>");Parameter("@d",SqlDbType.Date,stamp.Date);Parameter("@c",SqlDbType.Time,clock);command.Parameters["@c"].Scale=7;Parameter("@a",SqlDbType.DateTime2,stamp);command.Parameters["@a"].Scale=7;Parameter("@o",SqlDbType.DateTimeOffset,offset);command.Parameters["@o"].Scale=7;Parameter("@v",SqlDbType.VarChar,"abc");Parameter("@u",SqlDbType.NVarChar,"日本語");await command.ExecuteNonQueryAsync();command.Parameters.Clear();
        command.CommandText="INSERT dbo.Wide(Id) VALUES(2); CREATE TABLE dbo.Unchanged(Id int NOT NULL PRIMARY KEY)";await command.ExecuteNonQueryAsync();
        command.CommandText="SELECT B,T,S,L,N,R,F,G,P,X,D,C,A,O,V,U FROM dbo.Wide ORDER BY Id";using var reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.False(reader.GetBoolean(0));Assert.Equal(byte.MaxValue,reader.GetByte(1));Assert.Equal(short.MinValue,reader.GetInt16(2));Assert.Equal(long.MaxValue,reader.GetInt64(3));Assert.Equal(-1.2345m,reader.GetDecimal(4));Assert.Equal(0.1f,reader.GetFloat(5));Assert.Equal(Math.BitIncrement(1d),reader.GetDouble(6));Assert.Equal(guid,reader.GetGuid(7));Assert.Equal(new byte[]{0,1,255},reader.GetFieldValue<byte[]>(8));Assert.Contains("日本語",reader.GetString(9));Assert.Equal(stamp.Date,reader.GetDateTime(10));Assert.Equal(clock,reader.GetTimeSpan(11));Assert.Equal(stamp,reader.GetDateTime(12));Assert.Equal(offset,reader.GetFieldValue<DateTimeOffset>(13));Assert.Equal("abc",reader.GetString(14));Assert.Equal("日本語",reader.GetString(15));Assert.True(await reader.ReadAsync());for(var index=0;index<reader.FieldCount;index++){Assert.True(reader.IsDBNull(index));}Assert.False(await reader.ReadAsync());
    }

    [Fact,Trait("Category","Integration")]
    public async Task TypedSqlAssignmentsTruncateIntegersRoundScaleAndRejectOverflow(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var root=Path.Combine(data,"sql-assignments-"+Guid.NewGuid().ToString("N"));
        using var host=await GatewayDatabaseHost.OpenAsync(root,compiler,new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20},[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")]);var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(81,"a");
        void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
        Execute("CREATE TABLE dbo.Numbers(Id int NOT NULL PRIMARY KEY,Qty int NOT NULL,Amount decimal(6,2) NOT NULL,Code char(4) NOT NULL); INSERT dbo.Numbers(Id,Qty,Amount,Code) VALUES(1,1.9,1.235,'a')");Assert.Equal(new object?[]{1,1.24m,"a   "},Assert.Single(engine.Execute("SELECT Qty,Amount,Code FROM dbo.Numbers",session).Rows));
        Execute("BEGIN TRANSACTION");Assert.Equal(8115,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.Numbers(Id,Qty,Amount,Code) VALUES(2,1,9999.999,'a')")).Number);Assert.Equal(1,session.TransactionState);Assert.Equal(8152,Assert.Throws<SqlQueryException>(()=>Execute("INSERT dbo.Numbers(Id,Qty,Amount,Code) VALUES(3,1,1,'longer')")).Number);Execute("COMMIT TRANSACTION");Assert.Single(engine.Execute("SELECT Id FROM dbo.Numbers",session).Rows);
    }

    [Fact,Trait("Category","Integration")]
    public async Task SqlDdlCreatesAltersDropsAndRestoresSchemaAtSavepoint(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"sql-typed-ddl-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TransactionMemoryLimit=1024};
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[new("a",[new SchemaCatalog("dbo",[])],"Japanese_CI_AS")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(71,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            Execute("CREATE TABLE dbo.Items(Id int NOT NULL PRIMARY KEY,Qty int NOT NULL,Removed nvarchar(40) NULL); CREATE TABLE dbo.Unchanged(Id int NOT NULL PRIMARY KEY); INSERT dbo.Unchanged(Id) VALUES(42); INSERT dbo.Items(Id,Qty,Removed) VALUES(1,7,N'keep')");
            Execute("BEGIN TRANSACTION; SAVE TRANSACTION point; ALTER TABLE dbo.Items ALTER COLUMN Qty bigint NOT NULL; ALTER TABLE dbo.Items ADD Extra uniqueidentifier NULL; ALTER TABLE dbo.Items DROP COLUMN Removed; INSERT dbo.Items(Id,Qty,Extra) VALUES(2,8,NULL)");Assert.All(engine.Execute("SELECT Qty FROM dbo.Items",session).Rows,row=>Assert.IsType<long>(row[0]));Execute("ROLLBACK TRANSACTION point");Assert.Equal(new object?[]{7,"keep"},Assert.Single(engine.Execute("SELECT Qty,Removed FROM dbo.Items",session).Rows));Execute("COMMIT TRANSACTION");
            Execute("BEGIN TRANSACTION; ALTER TABLE dbo.Items ALTER COLUMN Qty bigint NOT NULL; ALTER TABLE dbo.Items DROP COLUMN Removed; COMMIT TRANSACTION");Assert.Equal(7L,Assert.Single(engine.Execute("SELECT Qty FROM dbo.Items",session).Rows)[0]);Execute("DROP TABLE dbo.Items");Assert.Equal(42,Assert.Single(engine.Execute("SELECT Id FROM dbo.Unchanged",session).Rows)[0]);
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(72,"a");Assert.Equal(42,Assert.Single(engine.Execute("SELECT Id FROM dbo.Unchanged",session).Rows)[0]);Assert.Throws<SqlQueryException>(()=>engine.Execute("SELECT Id FROM dbo.Items",session).Rows.ToArray());}
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(GatewayCommitPhase.BeforeWal,false)]
    [InlineData(GatewayCommitPhase.WalDurable,true)]
    [InlineData(GatewayCommitPhase.DatabaseApplied,true)]
    public async Task GenerationReferencesAndOtherDatabaseDmlRecoverAsOneCommit(GatewayCommitPhase failurePhase,bool committed){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"atomic-generation-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TransactionMemoryLimit=1024};
        DatabaseCatalog Definition(string name)=>new(name,[new SchemaCatalog("dbo",[new TableCatalog("Items",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new("pk",[new("Id",false)]))])],"Japanese_CI_AS");var armed=false;
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[Definition("a"),Definition("b")],failureInjection:(phase,id)=>{if(armed&&phase==failurePhase){throw new IOException("atomic publication fault");}})){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(61,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            Execute("INSERT a.dbo.Items(Id) VALUES(1); INSERT b.dbo.Items(Id) VALUES(1); BEGIN TRANSACTION; INSERT a.dbo.Items(Id) VALUES(2); INSERT b.dbo.Items(Id) VALUES(2)");
            session.Run(()=>{using var binding=DatabaseRegistry.EnterSqlSession(session);var current=host.Transactions.TransactionSchema(session,"a");var table=current.Definition.Schemas[0].Tables[0];var identity=current.Tables[0];var schema=new GatewaySchemaCatalog(new("a",[new SchemaCatalog("dbo",[new TableCatalog("Items",[table.Columns[0] with{Type=new(DatabaseTypeKind.Int64,false,SqlTypeName:"bigint")},new(1,"Extra",new(DatabaseTypeKind.Int32,true,SqlTypeName:"int"),false)],table.PrimaryKey)])],"Japanese_CI_AS"),[identity with{Columns=identity.Columns.Append(new(current.NextColumnId,identity.NextSqlColumnId,"Extra")).ToArray(),NextSqlColumnId=identity.NextSqlColumnId+1}],current.NextTableId,current.NextColumnId+1);host.Transactions.StageSchema(session,"a",schema,new(_=>7,session.CancellationToken));return 0;});
            Execute("INSERT a.dbo.Items(Id,Extra) VALUES(3,8)");armed=true;var failure=Assert.Throws<TransactionCommitException>(()=>Execute("COMMIT TRANSACTION"));Assert.Equal(committed?TransactionOutcomeKind.Committed:TransactionOutcomeKind.Aborted,failure.Outcome);Assert.False(session.IsInTransaction);
            if(committed){Assert.ThrowsAny<Exception>(()=>engine.Execute("SELECT Id FROM a.dbo.Items",session).Rows.ToArray());Assert.Throws<DatabaseFaultedException>(()=>engine.Execute("SELECT Id FROM b.dbo.Items",session).Rows.ToArray());}
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(62,"a");Assert.Equal(committed?3:1,engine.Execute("SELECT Id FROM a.dbo.Items",session).Rows.Count());Assert.Equal(committed?2:1,engine.Execute("SELECT Id FROM b.dbo.Items",session).Rows.Count());
            if(committed){Assert.All(engine.Execute("SELECT Id,Extra FROM a.dbo.Items",session).Rows,row=>{Assert.IsType<long>(row[0]);Assert.NotNull(row[1]);});}else{Assert.IsType<int>(Assert.Single(engine.Execute("SELECT Id FROM a.dbo.Items",session).Rows)[0]);Assert.Throws<SqlQueryException>(()=>engine.Execute("SELECT Extra FROM a.dbo.Items",session).Rows.ToArray());}
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task RetainedGenerationsRestoreLossyChangesAndPublishOnlyTheOuterCommit(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"retained-generations-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TransactionMemoryLimit=1024};
        var definition=new DatabaseCatalog("a",[new SchemaCatalog("dbo",[new TableCatalog("Items",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new(1,"Name",new(DatabaseTypeKind.String,false,MaximumLength:40,SqlTypeName:"nvarchar"),false),new(2,"Removed",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new("pk",[new("Id",false)]))])],"Japanese_CI_AS");
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,[definition])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(51,"a");void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            GatewaySchemaCatalog Current()=>session.Run(()=>{using var binding=DatabaseRegistry.EnterSqlSession(session);return host.Transactions.TransactionSchema(session,"a");});
            void Stage(GatewaySchemaCatalog schema)=>session.Run(()=>{using var binding=DatabaseRegistry.EnterSqlSession(session);host.Transactions.StageSchema(session,"a",schema,new(id=>9,session.CancellationToken));return 0;});
            Execute("INSERT dbo.Items(Id,Name,Removed) VALUES(1,N'one',123); BEGIN TRANSACTION; INSERT dbo.Items(Id,Name,Removed) VALUES(2,N'two',456); SAVE TRANSACTION beforeChange");var original=Current();
            GatewaySchemaCatalog Wider(GatewaySchemaCatalog source){var table=source.Tables.Single();var old=source.Definition.Schemas.Single().Tables.Single();var columns=new ColumnCatalog[]{new(0,"Id",new(DatabaseTypeKind.Int64,false,SqlTypeName:"bigint"),false),old.Columns[1] with{Ordinal=1},new(2,"Extra",new(DatabaseTypeKind.Int32,true,SqlTypeName:"int"),false,"9")};var identities=table.Columns.Take(2).Append(new(source.NextColumnId,table.NextSqlColumnId,"Extra")).ToArray();return new(new("a",[new SchemaCatalog("dbo",[new TableCatalog("Items",columns,old.PrimaryKey)])],source.Definition.DefaultCollation),[table with{Columns=identities,NextSqlColumnId=table.NextSqlColumnId+1}],source.NextTableId,source.NextColumnId+1);}
            var wider=Wider(original);Stage(wider);Assert.All(engine.Execute("SELECT Id,Extra FROM dbo.Items",session).Rows,row=>{Assert.IsType<long>(row[0]);Assert.Equal(9,row[1]);});Execute("INSERT dbo.Items(Id,Name,Extra) VALUES(3,N'three',33); SAVE TRANSACTION afterChange");
            var withoutName=new GatewaySchemaCatalog(new("a",[new SchemaCatalog("dbo",[new TableCatalog("Items",[wider.Definition.Schemas[0].Tables[0].Columns[0],wider.Definition.Schemas[0].Tables[0].Columns[2] with{Ordinal=1}],wider.Definition.Schemas[0].Tables[0].PrimaryKey)])],definition.DefaultCollation),[wider.Tables[0] with{Columns=wider.Tables[0].Columns.Where(static column=>column.Name!="Name").ToArray()}],wider.NextTableId,wider.NextColumnId);
            Stage(withoutName);Assert.Throws<SqlQueryException>(()=>engine.Execute("SELECT Name FROM dbo.Items",session).Rows.ToArray());Execute("ROLLBACK TRANSACTION afterChange");Assert.Equal("three",Assert.Single(engine.Execute("SELECT Name FROM dbo.Items WHERE Id=3",session).Rows)[0]);Execute("ROLLBACK TRANSACTION beforeChange");Assert.Equal(456,Assert.Single(engine.Execute("SELECT Removed FROM dbo.Items WHERE Id=2",session).Rows)[0]);Assert.Equal(2,engine.Execute("SELECT Id FROM dbo.Items",session).Rows.Count());Execute("COMMIT TRANSACTION");
            var oldPlan=new SqlQueryEngine(host.Databases).Prepare("SELECT Removed FROM dbo.Items","a");Execute("BEGIN TRANSACTION");Stage(Wider(Current()));Execute("INSERT dbo.Items(Id,Name,Extra) VALUES(3,N'committed',33)");var transaction=session.Transaction!.Id;Execute("COMMIT TRANSACTION");Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(transaction).Kind);Assert.Equal(3961,Assert.Throws<SqlQueryException>(()=>oldPlan.Execute()).Number);Assert.Equal(3,engine.Execute("SELECT Id FROM dbo.Items",session).Rows.Count());
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(52,"a");var rows=engine.Execute("SELECT Id,Extra FROM dbo.Items ORDER BY Id",session).Rows.ToArray();Assert.Equal(new long[]{1,2,3},rows.Select(row=>(long)row[0]!).ToArray());Assert.Equal(new[]{9,9,33},rows.Select(row=>(int)row[1]!).ToArray());}
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewHostPublishesInitialDatabasesAsOneDecisionAndRestoresBoth(bool afterDurable){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"generation-host-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);var options=new DatabaseContainerOptions{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TransactionMemoryLimit=1024};
        DatabaseCatalog Definition(string name)=>new(name,[new SchemaCatalog("dbo",[new TableCatalog("Items",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new("pk",[new("Id",false)]))])],"Japanese_CI_AS");var initial=new[]{Definition("a"),Definition("b")};
        await Assert.ThrowsAsync<IOException>(async()=>{using var failed=await GatewayDatabaseHost.OpenAsync(root,compiler,options,initial,failureInjection:(phase,id)=>{if(phase==(afterDurable?GatewayCommitPhase.WalDurable:GatewayCommitPhase.BeforeWal)){throw new IOException("bootstrap failure");}});});
        Guid transaction;
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options,afterDurable?null:initial)){
            Assert.Equal(new[]{"a","b"},host.Databases.Databases.Select(static item=>item.Name).Order().ToArray());var engine=new Sql.SqlExecutionEngine(host.Databases);using var session=new SessionContext(11,"a");
            void Execute(string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
            Execute("BEGIN TRANSACTION; INSERT a.dbo.Items(Id) VALUES(1); INSERT b.dbo.Items(Id) VALUES(2)");transaction=session.Transaction!.Id;Execute("COMMIT TRANSACTION");Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(transaction).Kind);host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(root,compiler,options)){
            var engine=new Sql.SqlExecutionEngine(host.Databases);using var session=new SessionContext(12,"a");Assert.Equal(1,Assert.Single(engine.Execute("SELECT Id FROM a.dbo.Items",session).Rows)[0]);Assert.Equal(2,Assert.Single(engine.Execute("SELECT Id FROM b.dbo.Items",session).Rows)[0]);Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(transaction).Kind);
        }
    }

    [Fact,Trait("Category","Integration")]
    public async Task SealedGenerationIsNotACommitAndLiveUpdatesDoNotChangeItsSeed(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root.");var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing isolated cache root.");var root=Path.Combine(data,"generation-store-"+Guid.NewGuid().ToString("N"));
        var schema=GatewaySchemaCatalog.Create(new("typed",[new SchemaCatalog("dbo",[new TableCatalog("Items",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new(1,"Name",new(DatabaseTypeKind.String,false,MaximumLength:40,SqlTypeName:"nvarchar"),false)],new("pk",[new("Id",false)]))])],"Japanese_CI_AS"));
        await using var journal=new PreparedTransactionJournal(Path.Combine(root,"wal"));journal.Replay((id,lsn,metadata,entries)=>{foreach(var entry in entries){}});
        var store=new GatewayGenerationStore(Path.Combine(root,"generations"),GeneratedAssemblyBuilderTests.Compiler(data,cache),new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TransactionMemoryLimit=1024});
        var owner=new GatewayTransactionOwner(Guid.NewGuid(),1);using var candidate=await store.BuildAsync(schema,new(journal.WalId,1),owner);
        Assert.Throws<InvalidOperationException>(()=>candidate.Loaded.Database.BeginGatewayTransaction(new(Guid.NewGuid(),2)));
        using(var snapshot=candidate.Loaded.Database.BeginUnpublishedSnapshot()){snapshot.Complete();}
        using var session=new SessionContext(1,"typed");using var transaction=candidate.Loaded.Database.BeginGatewayTransaction(owner);
        candidate.Loaded.Adapter.ExecuteInsert("dbo.Items",new Dictionary<string,object?>{{"Id",1},{"Name","seed"}},session);
        using var prepared=candidate.Seal(transaction);Assert.Equal(TransactionOutcomeKind.Unknown,journal.GetOutcome(owner.TransactionId).Kind);Assert.Throws<InvalidOperationException>(()=>prepared.Loaded.Database.BeginGatewayTransaction(owner));
        var committed=await journal.CommitAsync(owner.TransactionId,[],JsonSerializer.SerializeToUtf8Bytes(prepared.Reference));prepared.Loaded.Database.CompleteGatewayRecovery(committed.Lsn);
        Assert.Throws<InvalidOperationException>(()=>prepared.Loaded.Database.ConfigureUnpublishedGatewayGeneration(owner));
        var updatedOwner=new GatewayTransactionOwner(Guid.NewGuid(),1);using(var update=prepared.Loaded.Database.BeginGatewayTransaction(updatedOwner)){
            prepared.Loaded.Adapter.ExecuteInsert("dbo.Items",new Dictionary<string,object?>{{"Id",2},{"Name","live"}},session);var updated=await journal.CommitAsync(updatedOwner.TransactionId,update.PrepareGatewayCommit(updatedOwner));update.ApplyGatewayCommit(updatedOwner,updated.Lsn);update.Commit();
        }
        var changedHash=prepared.Loaded.Database.GetGatewayDataHash();Assert.NotEqual(prepared.Snapshot.DataSha256,changedHash);var outcome=journal.GetOutcome(updatedOwner.TransactionId);prepared.Loaded.Database.CheckpointGateway(outcome.Lsn);var reference=prepared.Reference;prepared.Dispose();
        using(var reopened=store.Open(reference,journal.WalId,committed.Lsn)){
            Assert.Equal(changedHash,reopened.Loaded.Database.GetGatewayDataHash());reopened.Loaded.Database.ReplayGatewayEntry(new(1,int.MaxValue,WalMutation.Delete,"obsolete-schema",[],null),committed.Lsn);reopened.Loaded.Database.CompleteGatewayRecovery(committed.Lsn);
            var rows=reopened.Loaded.Adapter.ExecuteSelect("dbo.Items",null,null,session).Rows.ToArray();Assert.Equal(2,rows.Length);Assert.Contains(rows,row=>Equals(row[1],"live"));
        }
        var manifest=Path.Combine(root,"generations",reference.Manifest);var original=File.ReadAllBytes(manifest);File.WriteAllBytes(manifest+".backup",original);File.WriteAllText(manifest,"{}");Assert.Throws<InvalidDataException>(()=>store.Open(reference,journal.WalId,committed.Lsn));File.WriteAllBytes(manifest,original);
        using var json=JsonDocument.Parse(original);var seedFile=Path.Combine(Path.GetDirectoryName(manifest)!,"seed",json.RootElement.GetProperty("Files")[0].GetProperty("Path").GetString()!);var content=File.ReadAllBytes(seedFile);File.WriteAllBytes(seedFile+".backup",content);content[0]^=1;File.WriteAllBytes(seedFile,content);Assert.Throws<InvalidDataException>(()=>store.Open(reference,journal.WalId,committed.Lsn));
    }
}
