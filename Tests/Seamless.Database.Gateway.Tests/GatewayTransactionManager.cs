using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Transactions;
using Seamless.Database.Client;
using Seamless.Database.Gateway.Native;
using MemoryPack;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text.Json;
using Seamless.Database.Gateway.Http;
using Seamless.Database.Gateway.Tds;
using Microsoft.Data.SqlClient;
using Manager=Seamless.Database.Gateway.Hosting.GatewayTransactionManager;

namespace Seamless.Database.Gateway.Tests;

public sealed class GatewayTransactionManager{
    private static string NewPath()=>Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??throw new InvalidOperationException("Missing isolated test root."),"gateway-common-"+Guid.NewGuid().ToString("N"));
    private static DatabaseContainerOptions Options(string path,GatewayDatabaseProfile profile)=>DatabaseContainerOptions.OnStorage(path) with{RecordSerialization=DatabaseRecordSerialization.CanonicalV1,GatewayProfile=profile,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(path,"temporary"),TransactionMemoryLimit=1024};
    private static void Execute(SqlExecutionEngine engine,SessionContext session,string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
    private static int Count(SqlExecutionEngine engine,SessionContext session,string database)=>Convert.ToInt32(Assert.Single(engine.Execute("SELECT COUNT(*) FROM ["+database+"].dbo.TypedOrders",session).Rows)[0]);

    [Fact]
    public void NestedDmlCommitsAndRollsBackAcrossDatabases(){
        var path=NewPath();using var manager=new Manager(Path.Combine(path,"gateway"));using var first=new RuntimeSqlGeneratedRoot(Options(Path.Combine(path,"a"),manager.Profile(1)));using var second=new RuntimeSqlGeneratedRoot(Options(Path.Combine(path,"b"),manager.Profile(2)));
        var registry=new DatabaseRegistry(transactions:manager);registry.Register("a",new InProcessDatabaseAdapter(first));registry.Register("b",new InProcessDatabaseAdapter(second));manager.Recover();var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(100,"a");
        Execute(engine,session,"BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10); USE b; BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20); COMMIT TRANSACTION");
        Assert.Equal(1,session.TransactionCount);Assert.Equal(1,Count(engine,session,"a"));Assert.Equal(1,Count(engine,session,"b"));Execute(engine,session,"ROLLBACK TRANSACTION");Assert.Equal(0,Count(engine,session,"a"));Assert.Equal(0,Count(engine,session,"b"));
        Execute(engine,session,"USE a; BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10); USE b; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20)");var id=session.Transaction!.Id;Execute(engine,session,"COMMIT TRANSACTION");Assert.Equal(TransactionOutcomeKind.Committed,manager.GetOutcome(id).Kind);Assert.Equal(1,Count(engine,session,"a"));Assert.Equal(1,Count(engine,session,"b"));
    }

    [Fact]
    public void SavepointRestoresParticipantsAddedLaterAndQualifiedDml(){
        using var fixture=new Fixture(NewPath());var engine=fixture.Engine;var session=fixture.Session;
        Execute(engine,session,"BEGIN TRANSACTION; INSERT a.dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10); SAVE TRANSACTION point; INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20); ROLLBACK TRANSACTION point");
        Assert.Equal(1,Count(engine,session,"a"));Assert.Equal(0,Count(engine,session,"b"));
        Execute(engine,session,"INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20); SAVE TRANSACTION both; UPDATE a.dbo.TypedOrders SET Version=11; UPDATE b.dbo.TypedOrders SET Version=21; ROLLBACK TRANSACTION both; COMMIT TRANSACTION");
        Assert.Equal(10,Assert.Single(engine.Execute("SELECT Version FROM a.dbo.TypedOrders",session).Rows)[0]);Assert.Equal(20,Assert.Single(engine.Execute("SELECT Version FROM b.dbo.TypedOrders",session).Rows)[0]);
    }

    [Theory]
    [InlineData(GatewayCommitPhase.BeforeWal,false)]
    [InlineData(GatewayCommitPhase.WalDurable,true)]
    [InlineData(GatewayCommitPhase.DatabaseApplied,true)]
    public void RecoveryUsesOneCommonCommitWhenApplicationFailsBetweenDatabases(GatewayCommitPhase phase,bool committed){
        var path=NewPath();Guid transaction;
        using(var fixture=new Fixture(path,(current,id)=>{if(current==phase){throw new IOException("injected Gateway failure");}})){
            Execute(fixture.Engine,fixture.Session,"BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10); USE b; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20)");transaction=fixture.Session.Transaction!.Id;
            var error=Assert.Throws<TransactionCommitException>(()=>Execute(fixture.Engine,fixture.Session,"COMMIT TRANSACTION"));Assert.Equal(committed?TransactionOutcomeKind.Committed:TransactionOutcomeKind.Aborted,error.Outcome);Assert.False(fixture.Session.IsInTransaction);
            if(committed){Assert.Throws<DatabaseFaultedException>(()=>Count(fixture.Engine,fixture.Session,"a"));Assert.Throws<DatabaseFaultedException>(()=>Count(fixture.Engine,fixture.Session,"b"));}
        }
        using(var fixture=new Fixture(path)){
            Assert.Equal(committed?1:0,Count(fixture.Engine,fixture.Session,"a"));Assert.Equal(committed?1:0,Count(fixture.Engine,fixture.Session,"b"));Assert.Equal(committed?TransactionOutcomeKind.Committed:TransactionOutcomeKind.Unknown,fixture.Manager.GetOutcome(transaction).Kind);
        }
    }

    [Fact]
    public void CommonCheckpointAndWalPruningPreserveBothDatabasesAndOutcomes(){
        var path=NewPath();Guid transaction;
        using(var fixture=new Fixture(path)){
            Execute(fixture.Engine,fixture.Session,"BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10); USE b; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20)");transaction=fixture.Session.Transaction!.Id;Execute(fixture.Engine,fixture.Session,"COMMIT TRANSACTION");
            for(var epoch=0;epoch<3;epoch++){fixture.Manager.Checkpoint();}
        }
        using(var fixture=new Fixture(path)){Assert.Equal(1,Count(fixture.Engine,fixture.Session,"a"));Assert.Equal(1,Count(fixture.Engine,fixture.Session,"b"));Assert.Equal(TransactionOutcomeKind.Committed,fixture.Manager.GetOutcome(transaction).Kind);fixture.Manager.Checkpoint();}
    }

    [Fact]
    public void InterruptedCheckpointReplaysOnlyDatabasesThatStillNeedTheCommittedRows(){
        var path=NewPath();
        using(var fixture=new Fixture(path,(phase,id)=>{if(phase==GatewayCommitPhase.DatabaseCheckpointed&&id==1){throw new IOException("checkpoint interrupted after the first database");}})){
            Execute(fixture.Engine,fixture.Session,"BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10); USE b; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20); COMMIT TRANSACTION");Assert.Throws<IOException>(fixture.Manager.Checkpoint);
        }
        using(var fixture=new Fixture(path)){Assert.Equal(1,Count(fixture.Engine,fixture.Session,"a"));Assert.Equal(1,Count(fixture.Engine,fixture.Session,"b"));fixture.Manager.Checkpoint();}
    }

    [Fact]
    public async Task DeadlockVictimRollsBackAndUnrelatedDatabaseRemainsAvailable(){
        using var fixture=new Fixture(NewPath());using var other=new SessionContext(102,"b");using var unrelated=new SessionContext(103,"c");using var cancellation=new CancellationTokenSource();fixture.Session.CancellationToken=cancellation.Token;
        Execute(fixture.Engine,fixture.Session,"BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10)");Execute(fixture.Engine,other,"BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20)");
        var waiting=Task.Run(()=>Execute(fixture.Engine,fixture.Session,"INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(3,N'b-from-a',30)"));
        try{
            Assert.True(SpinWait.SpinUntil(()=>fixture.Manager.PendingLockRequests==1,TimeSpan.FromSeconds(5)));Assert.Equal(0,Count(fixture.Engine,unrelated,"c"));
            var error=Assert.Throws<Seamless.Database.Query.SqlQueryException>(()=>Execute(fixture.Engine,other,"INSERT a.dbo.TypedOrders(ObjectId,Name,Version) VALUES(4,N'a-from-b',40)"));Assert.Equal(1205,error.Number);Assert.False(other.IsInTransaction);
            await waiting.WaitAsync(TimeSpan.FromSeconds(10));Execute(fixture.Engine,fixture.Session,"COMMIT TRANSACTION");Assert.Equal(1,Count(fixture.Engine,fixture.Session,"a"));Assert.Equal(1,Count(fixture.Engine,fixture.Session,"b"));Assert.Equal(3,Assert.Single(fixture.Engine.Execute("SELECT ObjectId FROM b.dbo.TypedOrders",fixture.Session).Rows)[0]);
        }finally{cancellation.Cancel();try{await waiting.WaitAsync(TimeSpan.FromSeconds(10));}catch(OperationCanceledException){}}
    }

    [Fact]
    public async Task OtherConnectionsWaitUntilCommitAndLockTimeoutLeavesNoWaiter(){
        using var fixture=new Fixture(NewPath());using var other=new SessionContext(104,"a");using var cancellation=new CancellationTokenSource();other.CancellationToken=cancellation.Token;
        Execute(fixture.Engine,fixture.Session,"BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10)");
        Execute(fixture.Engine,other,"SET LOCK_TIMEOUT 20");Assert.Equal(1222,Assert.Throws<Seamless.Database.Query.SqlQueryException>(()=>Count(fixture.Engine,other,"a")).Number);Assert.Equal(0,fixture.Manager.PendingLockRequests);Execute(fixture.Engine,other,"SET LOCK_TIMEOUT -1");
        var reading=Task.Run(()=>Count(fixture.Engine,other,"a"));
        try{Assert.True(SpinWait.SpinUntil(()=>fixture.Manager.PendingLockRequests==1,TimeSpan.FromSeconds(5)));Execute(fixture.Engine,fixture.Session,"COMMIT TRANSACTION");Assert.Equal(1,await reading.WaitAsync(TimeSpan.FromSeconds(10)));}
        finally{cancellation.Cancel();try{await reading.WaitAsync(TimeSpan.FromSeconds(10));}catch(OperationCanceledException){}}
    }

    [Fact]
    public async Task NativeQueriesUseCommonTransactions(){
        using var fixture=new Fixture(NewPath());var version=typeof(INativeQueryModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        var definitions=new[]{"a","b"}.SelectMany(name=>new[]{new NativeQueryDefinition("insert",name,"","int","int",false),new NativeQueryDefinition("read",name,"","int","int"),new NativeQueryDefinition("readonly-write",name,"","int","int")}).ToArray();using var server=new NativeQueryServer(fixture.Engine.Databases,0);server.Register(new("common-test","","","",version,definitions),new NativeModule(),version);server.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));await using var client=new NativeQueryClient("127.0.0.1",server.Port);
        var result=new List<int>();await foreach(var row in client.QueryAsync<int,int>("common-test","insert","a","",7,timeout.Token)){result.Add(row);}Assert.Equal(1,Assert.Single(result));Assert.Equal(1,Count(fixture.Engine,fixture.Session,"a"));
        await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){await foreach(var row in transaction.QueryAsync<int,int>("common-test","insert","",8,timeout.Token)){}await transaction.RollbackAsync(timeout.Token);}
        Assert.Equal(1,Count(fixture.Engine,fixture.Session,"a"));
        async Task<int> Query(NativeTransaction transaction,string query,string database,int id){var values=new List<int>();await foreach(var row in transaction.QueryAsync<int,int>("common-test",query,database,"",id,timeout.Token)){values.Add(row);}return Assert.Single(values);}
        await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){
            Assert.Equal(1,await Query(transaction,"insert","a",8));await transaction.SavepointAsync("point",timeout.Token);await transaction.BeginNestedAsync(timeout.Token);Assert.Equal(2,transaction.TransactionCount);
            Assert.Equal(1,await Query(transaction,"insert","b",9));Assert.Equal(1,await Query(transaction,"read","b",9));await transaction.CommitAsync(timeout.Token);Assert.Equal(1,transaction.TransactionCount);Assert.Equal(NativeTransactionOutcomeKind.Unknown,(await transaction.GetOutcomeAsync(timeout.Token)).Kind);
            await transaction.RollbackToSavepointAsync("point",timeout.Token);Assert.Equal(0,await Query(transaction,"read","b",9));await transaction.CommitAsync(timeout.Token);Assert.Equal(NativeTransactionOutcomeKind.Committed,(await transaction.GetOutcomeAsync(timeout.Token)).Kind);
        }
        Assert.Equal(2,Count(fixture.Engine,fixture.Session,"a"));Assert.Equal(0,Count(fixture.Engine,fixture.Session,"b"));
        await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){
            Assert.Equal(1,await Query(transaction,"insert","a",12));var duplicate=await Assert.ThrowsAsync<NativeQueryException>(()=>Query(transaction,"insert","a",12));Assert.True(duplicate.IsRecoverable);Assert.False(transaction.IsCompleted);
            Assert.Equal(1,await Query(transaction,"insert","b",13));await transaction.CommitAsync(timeout.Token);
        }
        await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){
            Assert.Equal(1,await Query(transaction,"insert","a",15));Assert.True((await Assert.ThrowsAsync<NativeQueryException>(()=>Query(transaction,"readonly-write","a",14))).IsRecoverable);Assert.Equal(0,await Query(transaction,"read","a",14));Assert.Equal(1,await Query(transaction,"read","a",15));await transaction.RollbackAsync(timeout.Token);
        }
        Assert.Equal(3,Count(fixture.Engine,fixture.Session,"a"));Assert.Equal(1,Count(fixture.Engine,fixture.Session,"b"));
        await using(var transaction=await client.BeginTransactionAsync("a",timeout.Token)){
            Assert.Equal(1,await Query(transaction,"insert","a",16));var reader=Task.Run(()=>Count(fixture.Engine,fixture.Session,"a"));Assert.True(SpinWait.SpinUntil(()=>fixture.Manager.PendingLockRequests==1,TimeSpan.FromSeconds(5)));
            using var canceled=new CancellationTokenSource();canceled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>{await foreach(var row in transaction.QueryAsync<int,int>("common-test","insert","b","",17,canceled.Token)){}});Assert.True(transaction.IsCompleted);Assert.Equal(3,await reader.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    private sealed class NativeModule:INativeQueryModule{
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            var database=(RuntimeSqlGeneratedRoot)context.Database;var id=MemoryPackSerializer.Deserialize<int>(arguments.Span);
            if(queryId=="read"){var found=0;if(database.TypedOrders.ContainsKey(new(id))){found=1;}yield return MemoryPackSerializer.Serialize(found);yield break;}
            if(queryId is not ("insert" or "readonly-write")){throw new InvalidOperationException("Unexpected query.");}
            if(!database.TypedOrders.Add(new(id,"native",1))){throw new InvalidOperationException("Duplicate native row.");}yield return MemoryPackSerializer.Serialize(1);
        }
    }

    [Fact]
    public async Task HttpBatchUsesOneLogicalTransactionAndRollsBackUnfinishedRequests(){
        using var fixture=new Fixture(NewPath());var reservation=new TcpListener(IPAddress.Loopback,0);reservation.Start();var port=((IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
        using var server=new HttpServer(port,fixture.Engine.Databases.Resolve("a").Adapter,fixture.Engine);server.Start();using var client=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(20)};
        using(var response=await client.PostAsJsonAsync("/api/sql?database=a",new{query="BEGIN TRANSACTION; INSERT a.dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10); SAVE TRANSACTION point; INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20); ROLLBACK TRANSACTION point; BEGIN TRANSACTION; INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(3,N'b',30); COMMIT TRANSACTION; COMMIT TRANSACTION; SELECT ObjectId FROM b.dbo.TypedOrders"})){
            response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());Assert.Equal(3,json.RootElement.GetProperty("results").EnumerateArray().Last().GetProperty("rows")[0].GetProperty("ObjectId").GetInt32());
        }
        Assert.Equal(1,Count(fixture.Engine,fixture.Session,"a"));Assert.Equal(1,Count(fixture.Engine,fixture.Session,"b"));
        using(var response=await client.PostAsJsonAsync("/api/sql?database=a",new{query="BEGIN TRANSACTION; INSERT a.dbo.TypedOrders(ObjectId,Name,Version) VALUES(4,N'cancel-on-request-end',40)"})){response.EnsureSuccessStatusCode();_ =await response.Content.ReadAsStringAsync();}
        Assert.Equal(1,Count(fixture.Engine,fixture.Session,"a"));
    }

    [Fact]
    public async Task TdsConnectionPreservesNestedStateAcrossDatabasesAndRollsBackOnDisconnect(){
        using var fixture=new Fixture(NewPath());using var server=new TdsServer(0,fixture.Engine.Databases.Resolve("a").Adapter,fixture.Engine);server.Start();
        var connectionString=new SqlConnectionStringBuilder{DataSource="127.0.0.1,"+server.Port,InitialCatalog="a",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10}.ConnectionString;
        using(var connection=new SqlConnection(connectionString)){
            await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandTimeout=20;command.CommandText="BEGIN TRANSACTION; INSERT a.dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',10); SAVE TRANSACTION point; BEGIN TRANSACTION; INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',20); COMMIT TRANSACTION; ROLLBACK TRANSACTION point; INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(3,N'b',30); COMMIT TRANSACTION";await command.ExecuteNonQueryAsync();
        }
        Assert.Equal(1,Count(fixture.Engine,fixture.Session,"a"));Assert.Equal(1,Count(fixture.Engine,fixture.Session,"b"));
        var disconnected=new SqlConnection(connectionString);await disconnected.OpenAsync();using(var command=disconnected.CreateCommand()){command.CommandText="BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(9,N'disconnected',90)";await command.ExecuteNonQueryAsync();}
        var reading=Task.Run(()=>Count(fixture.Engine,fixture.Session,"a"));
        try{Assert.True(SpinWait.SpinUntil(()=>fixture.Manager.PendingLockRequests==1,TimeSpan.FromSeconds(5)));disconnected.Dispose();Assert.Equal(1,await reading.WaitAsync(TimeSpan.FromSeconds(10)));}
        finally{disconnected.Dispose();}
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public async Task TdsLockTimeoutAndAttentionMatchSqlServerTransactionState(bool abort,bool cancel){
        using var fixture=new Fixture(NewPath());Execute(fixture.Engine,fixture.Session,"BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(99,N'holding',1)");
        using var server=new TdsServer(0,fixture.Engine.Databases.Resolve("a").Adapter,fixture.Engine);server.Start();
        using var connection=new SqlConnection(new SqlConnectionStringBuilder{DataSource="127.0.0.1,"+server.Port,InitialCatalog="a",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10}.ConnectionString);await connection.OpenAsync();
        using var command=connection.CreateCommand();command.CommandTimeout=20;command.CommandText="SET XACT_ABORT "+(abort?"ON":"OFF")+"; SET LOCK_TIMEOUT "+(cancel?"-1":"20")+"; BEGIN TRANSACTION; INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'pending',1)";await command.ExecuteNonQueryAsync();
        command.CommandText="UPDATE a.dbo.TypedOrders SET Version=2 WHERE ObjectId=99";var waiting=command.ExecuteNonQueryAsync();if(cancel){Assert.True(SpinWait.SpinUntil(()=>fixture.Manager.PendingLockRequests==1,TimeSpan.FromSeconds(5)));command.Cancel();}
        var failure=await Assert.ThrowsAsync<SqlException>(async()=>await waiting.WaitAsync(TimeSpan.FromSeconds(10)));if(!cancel){Assert.Equal(1222,failure.Number);}
        command.CommandText="SELECT XACT_STATE(),@@TRANCOUNT";using(var reader=await command.ExecuteReaderAsync()){Assert.True(await reader.ReadAsync());Assert.Equal(abort?0:1,Convert.ToInt32(reader.GetValue(0)));Assert.Equal(abort?0:1,reader.GetInt32(1));}
        if(!abort){command.CommandText="ROLLBACK TRANSACTION";await command.ExecuteNonQueryAsync();}
        Execute(fixture.Engine,fixture.Session,"ROLLBACK TRANSACTION");Assert.Equal(0,Count(fixture.Engine,fixture.Session,"b"));Assert.Equal(0,fixture.Manager.PendingLockRequests);
    }

    [Fact]
    public async Task HttpSessionOwnsTransactionAcrossRequestsAndRejectsStaleTransactionIds(){
        using var fixture=new Fixture(NewPath());var reservation=new TcpListener(IPAddress.Loopback,0);reservation.Start();var port=((IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
        using var server=new HttpServer(port,fixture.Engine.Databases.Resolve("a").Adapter,fixture.Engine,new(){MaximumSessions=1});server.Start();using var client=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(20)};
        using var created=await client.PostAsync("/api/session",null);Assert.Equal(HttpStatusCode.Created,created.StatusCode);using var identity=JsonDocument.Parse(await created.Content.ReadAsStringAsync());var token=identity.RootElement.GetProperty("id").GetString()!;
        using(var limit=await client.PostAsync("/api/session",null)){Assert.Equal(HttpStatusCode.TooManyRequests,limit.StatusCode);}
        async Task<HttpResponseMessage> Send(string sql,string? transaction=null){using var request=new HttpRequestMessage(HttpMethod.Post,"/api/sql"){Content=JsonContent.Create(new{query=sql})};request.Headers.Add("X-Seamless-Session",token);if(transaction is not null){request.Headers.Add("X-Seamless-Transaction",transaction);}return await client.SendAsync(request);}
        using var begun=await Send("BEGIN TRANSACTION; INSERT a.dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'a',1); SAVE TRANSACTION point");begun.EnsureSuccessStatusCode();using var beginJson=JsonDocument.Parse(await begun.Content.ReadAsStringAsync());var transaction=beginJson.RootElement.GetProperty("session").GetProperty("transactionId").GetString()!;
        using(var missing=await Send("COMMIT TRANSACTION")){Assert.Equal(HttpStatusCode.Conflict,missing.StatusCode);}using(var stale=await Send("ROLLBACK TRANSACTION",Guid.NewGuid().ToString())){Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);}
        using(var nested=await Send("BEGIN TRANSACTION; INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(2,N'b',2); COMMIT TRANSACTION; ROLLBACK TRANSACTION point; SELECT ObjectId FROM a.dbo.TypedOrders",transaction)){nested.EnsureSuccessStatusCode();using var result=JsonDocument.Parse(await nested.Content.ReadAsStringAsync());Assert.Equal(1,result.RootElement.GetProperty("session").GetProperty("transactionCount").GetInt32());Assert.Equal(1,result.RootElement.GetProperty("results").EnumerateArray().Last().GetProperty("rows")[0].GetProperty("ObjectId").GetInt32());}
        Assert.Equal(0,Count(fixture.Engine,fixture.Session,"b"));
        var waiting=client.PostAsJsonAsync("/api/sql",new{query="SELECT COUNT(*) AS n FROM a.dbo.TypedOrders"});Assert.True(SpinWait.SpinUntil(()=>fixture.Manager.PendingLockRequests==1,TimeSpan.FromSeconds(5)));
        using(var unrelated=await client.PostAsJsonAsync("/api/sql",new{query="SELECT COUNT(*) AS n FROM c.dbo.TypedOrders"})){unrelated.EnsureSuccessStatusCode();}
        using(var request=new HttpRequestMessage(HttpMethod.Delete,"/api/session")){request.Headers.Add("X-Seamless-Session",token);request.Headers.Add("X-Seamless-Transaction",transaction);using var closed=await client.SendAsync(request);closed.EnsureSuccessStatusCode();}
        using(var visible=await waiting.WaitAsync(TimeSpan.FromSeconds(10))){visible.EnsureSuccessStatusCode();using var result=JsonDocument.Parse(await visible.Content.ReadAsStringAsync());Assert.Equal(0,result.RootElement.GetProperty("rows")[0].GetProperty("n").GetInt32());}
        using(var newSession=await client.PostAsync("/api/session",null)){Assert.Equal(HttpStatusCode.Created,newSession.StatusCode);}
        using(var failed=await client.PostAsJsonAsync("/api/sql",new{query="SELECT 1 AS first; SELECT Missing FROM dbo.TypedOrders"})){Assert.Equal(HttpStatusCode.InternalServerError,failed.StatusCode);using var error=JsonDocument.Parse(await failed.Content.ReadAsStringAsync());Assert.True(error.RootElement.TryGetProperty("error",out _));}
        Assert.Null(server.LastSessionCleanupError);
    }

    [Fact]
    public async Task ExpiredHttpSessionRollsBackAndReleasesWaitingReaders(){
        using var fixture=new Fixture(NewPath());var reservation=new TcpListener(IPAddress.Loopback,0);reservation.Start();var port=((IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
        using var server=new HttpServer(port,fixture.Engine.Databases.Resolve("a").Adapter,fixture.Engine,new(){SessionIdleTimeout=TimeSpan.FromSeconds(1)});server.Start();using var client=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(10)};
        using var created=await client.PostAsync("/api/session",null);created.EnsureSuccessStatusCode();using var identity=JsonDocument.Parse(await created.Content.ReadAsStringAsync());var token=identity.RootElement.GetProperty("id").GetString()!;
        using(var request=new HttpRequestMessage(HttpMethod.Post,"/api/sql"){Content=JsonContent.Create(new{query="BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'expire',1)"})}){request.Headers.Add("X-Seamless-Session",token);using var begun=await client.SendAsync(request);begun.EnsureSuccessStatusCode();}
        var waiting=Task.Run(()=>Count(fixture.Engine,fixture.Session,"a"));Assert.True(SpinWait.SpinUntil(()=>fixture.Manager.PendingLockRequests==1,TimeSpan.FromSeconds(1)));Assert.Equal(0,await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
        using(var request=new HttpRequestMessage(HttpMethod.Get,"/api/session")){request.Headers.Add("X-Seamless-Session",token);using var expired=await client.SendAsync(request);Assert.Equal(HttpStatusCode.NotFound,expired.StatusCode);}
        Assert.Null(server.LastSessionCleanupError);
    }

    [Fact]
    public void CapturedContextCannotWriteAfterItsLogicalTransactionEnded(){
        using var fixture=new Fixture(NewPath());Execute(fixture.Engine,fixture.Session,"BEGIN TRANSACTION; INSERT dbo.TypedOrders(ObjectId,Name,Version) VALUES(1,N'pending',1)");var database=(RuntimeSqlGeneratedRoot)((InProcessDatabaseAdapter)fixture.Engine.Databases.Resolve("a").Adapter).Database;var context=fixture.Session.Run(ExecutionContext.Capture)!;Execute(fixture.Engine,fixture.Session,"ROLLBACK TRANSACTION");
        Assert.Throws<InvalidOperationException>(()=>ExecutionContext.Run(context,_=>database.TypedOrders.Add(new(2,"orphan",1)),null));Assert.Equal(0,Count(fixture.Engine,fixture.Session,"a"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpilledMultiDatabaseDmlUsesPreparationChunksAndIgnoresUncommittedChunks(bool fail){
        var path=NewPath();
        using(var fixture=new Fixture(path,fail?(phase,id)=>{if(phase==GatewayCommitPhase.PreparationDurable){throw new IOException("stopped before the final commit");}}:null)){
            Execute(fixture.Engine,fixture.Session,"BEGIN TRANSACTION; INSERT a.dbo.TypedOrders(ObjectId,Name,Version) VALUES(0,N'a',0); INSERT b.dbo.TypedOrders(ObjectId,Name,Version) VALUES(0,N'b',0)");
            var first=(RuntimeSqlGeneratedRoot)((InProcessDatabaseAdapter)fixture.Engine.Databases.Resolve("a").Adapter).Database;var second=(RuntimeSqlGeneratedRoot)((InProcessDatabaseAdapter)fixture.Engine.Databases.Resolve("b").Adapter).Database;
            fixture.Session.Run(()=>{for(var index=1;index<2000;index++){Assert.True(first.TypedOrders.Add(new(index,new string('a',96),index)));Assert.True(second.TypedOrders.Add(new(index,new string('b',96),index)));}return 0;});
            Assert.NotEmpty(Directory.EnumerateFiles(first.Options.TemporaryDirectory!,"*.tmp",SearchOption.AllDirectories));Assert.NotEmpty(Directory.EnumerateFiles(second.Options.TemporaryDirectory!,"*.tmp",SearchOption.AllDirectories));Execute(fixture.Engine,fixture.Session,"SAVE TRANSACTION spilled; INSERT a.dbo.TypedOrders(ObjectId,Name,Version) VALUES(3000,N'cancel',1); ROLLBACK TRANSACTION spilled");
            if(fail){var failure=Assert.Throws<TransactionCommitException>(()=>Execute(fixture.Engine,fixture.Session,"COMMIT TRANSACTION"));Assert.Equal(TransactionOutcomeKind.Aborted,failure.Outcome);}else{Execute(fixture.Engine,fixture.Session,"COMMIT TRANSACTION");}
            Assert.Empty(Directory.EnumerateFiles(first.Options.TemporaryDirectory!,"*.tmp",SearchOption.AllDirectories));Assert.Empty(Directory.EnumerateFiles(second.Options.TemporaryDirectory!,"*.tmp",SearchOption.AllDirectories));
        }
        using(var fixture=new Fixture(path)){Assert.Equal(fail?0:2000,Count(fixture.Engine,fixture.Session,"a"));Assert.Equal(fail?0:2000,Count(fixture.Engine,fixture.Session,"b"));}
    }

    private sealed class Fixture:IDisposable{
        private readonly RuntimeSqlGeneratedRoot first;
        private readonly RuntimeSqlGeneratedRoot second;
        private readonly RuntimeSqlGeneratedRoot third;
        internal Fixture(string path,Action<GatewayCommitPhase,int>? failure=null){
            this.Manager=new(Path.Combine(path,"gateway"),new(){MaxFrameBytes=4096,QueueBytes=16384,SegmentBytes=32768},failureInjection:failure);this.first=new(Options(Path.Combine(path,"a"),this.Manager.Profile(1)));this.second=new(Options(Path.Combine(path,"b"),this.Manager.Profile(2)));this.third=new(Options(Path.Combine(path,"c"),this.Manager.Profile(3)));
            var registry=new DatabaseRegistry(transactions:this.Manager);registry.Register("a",new InProcessDatabaseAdapter(this.first));registry.Register("b",new InProcessDatabaseAdapter(this.second));registry.Register("c",new InProcessDatabaseAdapter(this.third));this.Manager.Recover();this.Engine=new(registry);this.Session=new(101,"a");
        }
        internal Manager Manager{get;}
        internal SqlExecutionEngine Engine{get;}
        internal SessionContext Session{get;}
        public void Dispose(){this.Session.Dispose();this.first.Dispose();this.second.Dispose();this.third.Dispose();this.Manager.Dispose();}
    }
}
