using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Transactions;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Tests;

internal static class GatewayCrashEntryPoint{
    public static async Task<int> Main(string[] arguments){
        if(arguments.Length>0&&arguments[0]=="--transport-probe"){return await TransportProbe.RunAsync(arguments);}
        if(arguments.Length>0&&arguments[0] is "--record-owned-tls-client" or "--record-owned-ipc-client"){return await RecordRpc.NativeRecordTransportTests.RunTransportClientAsync(arguments);}
        if(arguments.Length!=4||arguments[0]!="gateway-process-kill"){return 2;}
        try{
            var root=Path.GetFullPath(arguments[1]);var allowed=Path.GetFullPath(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated root."))+Path.DirectorySeparatorChar;
            if(!root.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!Guid.TryParse(arguments[3],out var transaction)){return 3;}var stamp=JsonSerializer.Deserialize<GatewayWorkerOwner>(File.ReadAllText(Path.Combine(root,"worker-owner.json")));if(stamp?.TransactionId!=transaction){return 3;}
            var phase=Enum.Parse<GatewayCommitPhase>(arguments[2]);using var host=await GatewayDatabaseHost.OpenAsync(root,GatewayProcessTermination.Compiler(),GatewayProcessTermination.Options(stamp.Versioned),failureInjection:(current,id)=>{
                if(current==phase){using var process=Process.GetCurrentProcess();using(var output=new FileStream(Path.Combine(root,"fault-hit.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read)){JsonSerializer.Serialize(output,new GatewayFaultHit(transaction,process.Id,process.StartTime.ToUniversalTime(),phase,stamp.Versioned));output.Flush(true);}process.Kill();}
            });
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(91,"a");session.Run(()=>{host.Transactions.Begin(session,transaction);return 0;});
            foreach(var result in engine.ExecuteBatch("INSERT a.dbo.Items(Id,Qty) VALUES(2,8); INSERT b.dbo.Items(Id,Qty) VALUES(2,8); ALTER TABLE a.dbo.Items ALTER COLUMN Qty bigint NOT NULL; INSERT a.dbo.Items(Id,Qty) VALUES(3,9); COMMIT TRANSACTION",session)){foreach(var row in result.Rows){}}
            if(phase==GatewayCommitPhase.DatabaseCheckpointed||GatewayProcessTermination.IsCommonCheckpointBoundary(phase)){host.Transactions.Checkpoint();}return 4;
        }catch(Exception error){Console.Error.WriteLine(error);return 3;}
    }
}
internal sealed record GatewayWorkerOwner(Guid TransactionId,int ParentPid,DateTime ParentCreatedUtc,bool Versioned=false);
internal sealed record GatewayFaultHit(Guid TransactionId,int Pid,DateTime CreatedUtc,GatewayCommitPhase Phase,bool Versioned);

public sealed class GatewayProcessTermination{
    internal static bool IsCommonCheckpointBoundary(GatewayCommitPhase phase)=>phase is GatewayCommitPhase.BeforeCommonCheckpointPublication or GatewayCommitPhase.AfterCommonCheckpointPublication;
    internal static GeneratedAssemblyBuilder Compiler()=>GeneratedAssemblyBuilderTests.Compiler(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root."),Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing cache root."));
    internal static DatabaseContainerOptions Options(bool versioned=false){if(versioned){return GatewayVersionedTransactions.Options();}return new(){IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TransactionMemoryLimit=1024};}
    [Theory,Trait("Category","Integration")]
    [InlineData(GatewayCommitPhase.BeforeCandidateBuild,false)]
    [InlineData(GatewayCommitPhase.CandidateBuilt,false)]
    [InlineData(GatewayCommitPhase.CandidateCopied,false)]
    [InlineData(GatewayCommitPhase.GenerationSealed,false)]
    [InlineData(GatewayCommitPhase.BeforeWal,false)]
    [InlineData(GatewayCommitPhase.PreparationDurable,false)]
    [InlineData(GatewayCommitPhase.WalDurable,true)]
    [InlineData(GatewayCommitPhase.DatabaseApplied,true)]
    [InlineData(GatewayCommitPhase.BeforePublication,true)]
    [InlineData(GatewayCommitPhase.AfterPublication,true)]
    [InlineData(GatewayCommitPhase.AllDatabasesApplied,true)]
    [InlineData(GatewayCommitPhase.DatabaseCheckpointed,true)]
    public Task ProcessKillNeverPublishesOnlyOneDatabaseOrSchema(GatewayCommitPhase phase,bool committed)=>VerifyProcessKill(phase,committed,false);
    internal static async Task VerifyProcessKill(GatewayCommitPhase phase,bool committed,bool versioned){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated data root."),"gateway-kill-"+Guid.NewGuid().ToString("N"));
        var checkpointPath=Path.Combine(root,"transactions","common-checkpoint.json");
        DatabaseCatalog Definition(string name)=>new(name,[new SchemaCatalog("dbo",[new TableCatalog("Items",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new(1,"Qty",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new(2,"Rv",new(DatabaseTypeKind.Binary,false,MaximumLength:8,SqlTypeName:"timestamp"),false)],new("pk",[new("Id",false)]))])],"Japanese_CI_AS");
        using(var host=await GatewayDatabaseHost.OpenAsync(root,Compiler(),Options(versioned),[Definition("a"),Definition("b")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(90,"a");foreach(var result in engine.ExecuteBatch("INSERT a.dbo.Items(Id,Qty) VALUES(1,7); INSERT b.dbo.Items(Id,Qty) VALUES(1,7)",session)){foreach(var row in result.Rows){}}
            var type=((InProcessDatabaseAdapter)host.Databases.Resolve("a").Adapter).Database.GetType().FullName!;
            var source="using MemoryPack;using Seamless.Database.Client;public sealed class Query:INativeQueryModule{public IEnumerable<byte[]> Execute(string id,ReadOnlyMemory<byte> args,INativeQueryContext context){var db=(global::"+type+")context.Database;foreach(var row in db.dbo.Items){yield return MemoryPackSerializer.Serialize(checked((long)row.Qty));}}}";
            await host.Transactions.InstallNativeQueryAsync(new("crash-query","Query",[new("Query.cs",source)],[new("qty","a","","System.Int32","System.Int64")],["a","b"]));
            if(IsCommonCheckpointBoundary(phase)){host.Transactions.Checkpoint();}
        }
        var previousCheckpoint=IsCommonCheckpointBoundary(phase)?File.ReadAllText(checkpointPath):null;
        string? oldGeneration=null,oldStoreManifest=null,oldStoreContents=null;
        if(previousCheckpoint is not null){
            oldGeneration=Assert.Single(Directory.EnumerateDirectories(Path.Combine(root,"generations","1")));
            oldStoreManifest=Path.Combine(oldGeneration,"live","store-mvcc-v1","store.json");
            oldStoreContents=File.ReadAllText(oldStoreManifest);
        }
        var transaction=Guid.NewGuid();using(var parent=Process.GetCurrentProcess()){File.WriteAllText(Path.Combine(root,"worker-owner.json"),JsonSerializer.Serialize(new GatewayWorkerOwner(transaction,parent.Id,parent.StartTime.ToUniversalTime(),versioned)));}
        var logs=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")??throw new InvalidOperationException("Missing cache root."),"gateway-kill",transaction.ToString("N"));Directory.CreateDirectory(logs);
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var argument in new[]{typeof(GatewayCrashEntryPoint).Assembly.Location,"gateway-process-kill",root,phase.ToString(),transaction.ToString("D")}){start.ArgumentList.Add(argument);}
        using var child=Process.Start(start)??throw new InvalidOperationException("The Gateway worker did not start.");var created=child.StartTime.ToUniversalTime();var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();File.WriteAllText(Path.Combine(logs,"owner.json"),JsonSerializer.Serialize(new{Pid=child.Id,CreatedUtc=created,Phase=phase.ToString(),Root=root}));
        try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));}catch{if(!child.HasExited){using var live=Process.GetProcessById(child.Id);if(live.StartTime.ToUniversalTime()==created){child.Kill(entireProcessTree:true);}}throw;}
        var output=await stdout;var errors=await stderr;await File.WriteAllTextAsync(Path.Combine(logs,"stdout.log"),output);await File.WriteAllTextAsync(Path.Combine(logs,"stderr.log"),errors);Assert.True(child.ExitCode is not (0 or 2 or 3 or 4),output+errors);
        var hit=JsonSerializer.Deserialize<GatewayFaultHit>(File.ReadAllText(Path.Combine(root,"fault-hit.json")));Assert.NotNull(hit);Assert.Equal(transaction,hit.TransactionId);Assert.Equal(child.Id,hit.Pid);Assert.Equal(created,hit.CreatedUtc);Assert.Equal(phase,hit.Phase);Assert.Equal(versioned,hit.Versioned);
        if(previousCheckpoint is not null){
            Assert.Equal(oldStoreContents,File.ReadAllText(oldStoreManifest!));
            var currentCheckpoint=File.ReadAllText(checkpointPath);
            if(phase==GatewayCommitPhase.BeforeCommonCheckpointPublication){Assert.Equal(previousCheckpoint,currentCheckpoint);}
            else{
                Assert.NotEqual(previousCheckpoint,currentCheckpoint);
                using var envelope=JsonDocument.Parse(currentCheckpoint);
                using var checkpoint=JsonDocument.Parse(envelope.RootElement.GetProperty("Payload").GetBytesFromBase64());
                var retained=checkpoint.RootElement.GetProperty("RetainedGenerations").EnumerateArray();
                Assert.DoesNotContain(retained,item=>item.GetProperty("GenerationId").GetString()==Path.GetFileName(oldGeneration));
            }
        }
        var timeline=new ConcurrentQueue<object>();
        var timer=Stopwatch.StartNew();
        var trace=versioned&&(phase==GatewayCommitPhase.AfterPublication||Environment.GetEnvironmentVariable("SEAMLESS_TEST_RECOVERY_TRACE")=="1");
        using var checkpointTiming=versioned?CheckpointTiming.Capture(root,Path.Combine(logs,"checkpoint-timing.json")):null;
        void Mark(string operation,TransactionMaintenanceDiagnostics? maintenance=null,int? databaseId=null){
            if(trace){timeline.Enqueue(new{ElapsedMilliseconds=timer.Elapsed.TotalMilliseconds,Utc=DateTime.UtcNow,Operation=operation,Maintenance=maintenance,DatabaseId=databaseId});}
        }
        try{
            var startupCheckpointPublications=0;
            using var host=await GatewayDatabaseHost.OpenAsync(root,Compiler(),Options(versioned),failureInjection:(step,databaseId)=>{
                if(step==GatewayCommitPhase.AfterCommonCheckpointPublication){Interlocked.Increment(ref startupCheckpointPublications);}
                if(step is GatewayCommitPhase.TransactionStarted or GatewayCommitPhase.DatabaseCheckpointed or GatewayCommitPhase.BeforeCommonCheckpointPublication or GatewayCommitPhase.AfterCommonCheckpointPublication){Mark(step.ToString(),databaseId:databaseId);}
            });
            if(versioned&&phase==GatewayCommitPhase.AfterPublication){Assert.Equal(1,Volatile.Read(ref startupCheckpointPublications));}
            if(versioned&&phase==GatewayCommitPhase.AfterCommonCheckpointPublication){Assert.Equal(0,Volatile.Read(ref startupCheckpointPublications));}
            T Observe<T>(string sql,Func<T> read){
                Mark(sql+" start",host.Transactions.VersionedDiagnostics?.Maintenance);
                try{return read();}
                catch(TimeoutException){Mark(sql+" timeout",host.Transactions.VersionedDiagnostics?.Maintenance);throw;}
                finally{Mark(sql+" end",host.Transactions.VersionedDiagnostics?.Maintenance);}
            }
            var engine=new SqlExecutionEngine(host.Databases);
            using var session=new SessionContext(92,"a");
            const string readA="SELECT Id,Qty FROM a.dbo.Items ORDER BY Id";
            const string readB="SELECT Id,Qty FROM b.dbo.Items ORDER BY Id";
            var first=Observe(readA,()=>engine.Execute(readA,session).Rows.ToArray());
            var second=Observe(readB,()=>engine.Execute(readB,session).Rows.ToArray());
            Assert.Equal(committed?3:1,first.Length);
            Assert.Equal(committed?2:1,second.Length);
            Assert.Equal(committed?TransactionOutcomeKind.Committed:TransactionOutcomeKind.Unknown,host.Transactions.GetOutcome(transaction).Kind);
            Assert.All(first,row=>Assert.Equal(committed?typeof(long):typeof(int),row[1]!.GetType()));Assert.All(second,row=>Assert.IsType<int>(row[1]));
            const string readRowVersion="SELECT Rv FROM a.dbo.Items";
            const string readWatermark="SELECT @@DBTS";
            var versions=Observe(readRowVersion,()=>engine.Execute(readRowVersion,session).Rows.Select(row=>global::System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(Assert.IsType<byte[]>(row[0]))).ToArray());
            var watermark=Observe(readWatermark,()=>global::System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(Assert.IsType<byte[]>(Assert.Single(engine.Execute(readWatermark,session).Rows)[0])));
            Assert.All(versions,value=>Assert.InRange(value,1UL,watermark));
            foreach(var row in engine.Execute("UPDATE a.dbo.Items SET Qty=Qty WHERE Id=1",session).Rows){}var updated=global::System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(Assert.IsType<byte[]>(Assert.Single(engine.Execute("SELECT Rv FROM a.dbo.Items WHERE Id=1",session).Rows)[0]));Assert.True(updated>watermark);
            var definition=Assert.Single(host.Transactions.NativeQueries).Manifest.Queries.Single();Assert.Equal(host.Databases.Resolve("a").SchemaFingerprint,definition.SchemaFingerprint);Assert.Equal(NativeWireContract.For<long>(),definition.ResultContract);
            using var native=new global::Seamless.Database.Gateway.Native.NativeQueryServer(host.Databases,0);native.Start();await using var client=new NativeQueryClient("127.0.0.1",native.Port);var rows=new List<long>();
            await foreach(var row in client.QueryAsync<int,long>("crash-query","qty","a",definition.SchemaFingerprint,0)){rows.Add(row);}Assert.Equal(committed?new long[]{7,8,9}:new long[]{7},rows.Order().ToArray());await native.StopAsync();
        }finally{
            if(trace){await File.WriteAllTextAsync(Path.Combine(logs,"recovery-timeline.json"),JsonSerializer.Serialize(timeline.ToArray()));}
        }
    }
}
