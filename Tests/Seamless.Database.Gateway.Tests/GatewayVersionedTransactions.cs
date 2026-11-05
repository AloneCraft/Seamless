using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Rdbms;
using Seamless.Transactions;
namespace Seamless.Database.Gateway.Tests;

public sealed partial class GatewayVersionedTransactions{
    [Theory,Trait("Category","Integration")]
    [InlineData(TransactionConcurrencyMode.Optimistic,TransactionConcurrencyMode.Optimistic)]
    [InlineData(TransactionConcurrencyMode.Optimistic,TransactionConcurrencyMode.Exclusive)]
    [InlineData(TransactionConcurrencyMode.Exclusive,TransactionConcurrencyMode.Optimistic)]
    public async Task IdentityAndRowVersionReservationsSurviveBusinessRollbackAndRecovery(TransactionConcurrencyMode initialMode,TransactionConcurrencyMode recoveredMode){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var path=Path.Combine(data,"mvcc-values-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options() with{TransactionConcurrency=initialMode},[Definition("a")])){
            var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(50,"a");Execute(engine,session,"CREATE TABLE dbo.Generated(Id int IDENTITY(10,2) PRIMARY KEY,N int NOT NULL,Stamp rowversion); BEGIN TRANSACTION; INSERT dbo.Generated(N)VALUES(1)");var aborted=Assert.Single(engine.Execute("SELECT Id,Stamp FROM dbo.Generated",session).Rows);Assert.Equal(10,aborted[0]);Assert.Equal(1UL,global::System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian((byte[])aborted[1]!));Execute(engine,session,"ROLLBACK TRANSACTION; INSERT dbo.Generated(N)VALUES(2)");var committed=Assert.Single(engine.Execute("SELECT Id,Stamp FROM dbo.Generated",session).Rows);Assert.Equal(12,committed[0]);Assert.Equal(2UL,global::System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian((byte[])committed[1]!));host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options() with{TransactionConcurrency=recoveredMode})){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(51,"a");Execute(engine,session,"INSERT dbo.Generated(N)VALUES(3)");var row=engine.Execute("SELECT Id,Stamp FROM dbo.Generated ORDER BY Id",session).Rows.Last();Assert.Equal(14,row[0]);Assert.Equal(3UL,global::System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian((byte[])row[1]!));}
    }
    [Fact,Trait("Category","Integration")]
    public async Task SqlWritersOnDifferentIntegerPrimaryKeysCommitWithoutATableWideConflict(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;using var host=await GatewayDatabaseHost.OpenAsync(Path.Combine(data,"mvcc-writers-"+Guid.NewGuid().ToString("N")),GeneratedAssemblyBuilderTests.Compiler(data,cache),Options(),[Definition("a")]);var engine=new SqlExecutionEngine(host.Databases);using var first=new SessionContext(30,"a");using var second=new SessionContext(31,"a");Execute(engine,first,"INSERT dbo.Items VALUES(1,100),(2,200); BEGIN TRANSACTION");Execute(engine,second,"BEGIN TRANSACTION");Execute(engine,first,"UPDATE dbo.Items SET N=N+1 WHERE Id=1");Execute(engine,second,"UPDATE dbo.Items SET N=N+2 WHERE Id=2");Execute(engine,first,"COMMIT TRANSACTION");Execute(engine,second,"COMMIT TRANSACTION");Assert.Equal(new object?[]{101,202},engine.Execute("SELECT N FROM dbo.Items ORDER BY Id",first).Rows.Select(static row=>row[0]).ToArray());
        // 同じキーは片方だけが確定し、呼び出し側へ再試行判断を返す。
        Execute(engine,first,"BEGIN TRANSACTION; UPDATE dbo.Items SET N=111 WHERE Id=1");Execute(engine,second,"BEGIN TRANSACTION; UPDATE dbo.Items SET N=999 WHERE Id=1");Execute(engine,first,"COMMIT TRANSACTION");var conflict=Assert.Throws<TransactionCommitException>(()=>Execute(engine,second,"COMMIT TRANSACTION"));Assert.Equal(TransactionOutcomeKind.Aborted,conflict.Outcome);Assert.Equal(111,Assert.Single(engine.Execute("SELECT N FROM dbo.Items WHERE Id=1",second).Rows)[0]);
        Execute(engine,second,"UPDATE dbo.Items SET N=0 WHERE Id=NULL; UPDATE dbo.Items SET N=0 WHERE Id=2147483648");Assert.Equal(111,Assert.Single(engine.Execute("SELECT N FROM dbo.Items WHERE Id=1",second).Rows)[0]);
        Assert.ThrowsAny<Exception>(()=>Execute(engine,second,"UPDATE dbo.Items SET Id=2 WHERE Id=1"));Assert.Equal(new object?[]{111,202},engine.Execute("SELECT N FROM dbo.Items ORDER BY Id",first).Rows.Select(static row=>row[0]).ToArray());
    }
    [Fact,Trait("Category","Integration")]
    public async Task DdlKeepsLateReadsOnTheOriginalGenerationAndRestoresAnEarlierGenerationAtASavepoint(){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var path=Path.Combine(data,"mvcc-ddl-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options(),[Definition("a"),Definition("b")])){
            var engine=new SqlExecutionEngine(host.Databases);using var old=new SessionContext(10,"a");using var ddl=new SessionContext(11,"b");Execute(engine,ddl,"INSERT a.dbo.Items VALUES(1,100); INSERT b.dbo.Items VALUES(1,200)");Execute(engine,old,"BEGIN TRANSACTION");Assert.Equal(100,Assert.Single(engine.Execute("SELECT N FROM a.dbo.Items",old).Rows)[0]);
            Execute(engine,ddl,"ALTER TABLE dbo.Items ADD Extra int NOT NULL DEFAULT 7");Assert.Equal(200,Assert.Single(engine.Execute("SELECT N FROM b.dbo.Items",old).Rows)[0]);Assert.Throws<global::Seamless.Database.Query.SqlQueryException>(()=>engine.Execute("SELECT Extra FROM b.dbo.Items",old).Rows.ToArray());Execute(engine,old,"ROLLBACK TRANSACTION");Assert.Equal(7,Assert.Single(engine.Execute("SELECT Extra FROM dbo.Items",ddl).Rows)[0]);
            Execute(engine,ddl,"BEGIN TRANSACTION; SAVE TRANSACTION original; ALTER TABLE dbo.Items ADD Other int NULL; UPDATE dbo.Items SET Other=9 WHERE Id=1; ROLLBACK TRANSACTION original; UPDATE dbo.Items SET N=220 WHERE Id=1; COMMIT TRANSACTION");Assert.Equal(220,Assert.Single(engine.Execute("SELECT N FROM dbo.Items",ddl).Rows)[0]);Assert.Throws<global::Seamless.Database.Query.SqlQueryException>(()=>engine.Execute("SELECT Other FROM dbo.Items",ddl).Rows.ToArray());host.Transactions.Checkpoint();
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options())){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(12,"b");Assert.Equal(new object?[]{220,7},Assert.Single(engine.Execute("SELECT N,Extra FROM dbo.Items",session).Rows));Assert.Throws<global::Seamless.Database.Query.SqlQueryException>(()=>engine.Execute("SELECT Other FROM dbo.Items",session).Rows.ToArray());}
    }
    internal static DatabaseContainerOptions Options()=>new(){Transactions=DatabaseTransactionMode.SharedV2,TransactionConcurrency=TransactionConcurrencyMode.Optimistic,TransactionHistory=new(){MaxRetainedMemoryBytes=1<<20,MaxRetainedStorageBytes=8<<20,MaxMetadataBytes=2<<20,MaintenanceReserveMemoryBytes=64<<10,MaintenanceReserveStorageBytes=1<<20,MaintenanceWaitTimeout=TimeSpan.FromSeconds(5)},IndexSize=16,PageSize=4096,SegmentSize=65536,MemorySize=65536,ManagedMemoryLimitBytes=16<<20,TemporaryStorageLimitBytes=32<<20,TransactionMemoryLimit=1024};
    private static DatabaseCatalog Definition(string name)=>new(name,[new SchemaCatalog("dbo",[new TableCatalog("Items",[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new(1,"N",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],new("pk",[new("Id",false)]))])],"Japanese_CI_AS");
    private static void Execute(SqlExecutionEngine engine,SessionContext session,string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
    [Theory,Trait("Category","Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqlUsesOneSnapshotAcrossDatabasesAndReplaysTheCommonDecision(bool checkpoint){
        var data=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!;var cache=Environment.GetEnvironmentVariable("SEAMLESS_TEST_CACHE_ROOT")!;var path=Path.Combine(data,"mvcc-sql-"+Guid.NewGuid().ToString("N"));var compiler=GeneratedAssemblyBuilderTests.Compiler(data,cache);Guid id;
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options(),[Definition("a"),Definition("b")])){
            Assert.True(host.Transactions.IsVersioned);var engine=new SqlExecutionEngine(host.Databases);using var first=new SessionContext(1,"a");using var second=new SessionContext(2,"a");Execute(engine,first,"INSERT a.dbo.Items VALUES(1,100); INSERT b.dbo.Items VALUES(1,200)");
            Execute(engine,first,"BEGIN TRANSACTION");Assert.Equal(100,Assert.Single(engine.Execute("SELECT N FROM a.dbo.Items",first).Rows)[0]);Execute(engine,second,"UPDATE b.dbo.Items SET N=250 WHERE Id=1");Assert.Equal(200,Assert.Single(engine.Execute("SELECT N FROM b.dbo.Items",first).Rows)[0]);Execute(engine,first,"ROLLBACK TRANSACTION");
            Execute(engine,first,"BEGIN TRANSACTION; UPDATE a.dbo.Items SET N=90 WHERE Id=1; SAVE TRANSACTION before_b; UPDATE b.dbo.Items SET N=10 WHERE Id=1; ROLLBACK TRANSACTION before_b; UPDATE b.dbo.Items SET N=180 WHERE Id=1");id=first.Transaction!.Id;Assert.Equal(id,first.Transaction.Scope!.Id);Execute(engine,first,"COMMIT TRANSACTION");Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(id).Kind);if(checkpoint){host.Transactions.Checkpoint();}
        }
        using(var host=await GatewayDatabaseHost.OpenAsync(path,compiler,Options())){var engine=new SqlExecutionEngine(host.Databases);using var session=new SessionContext(3,"a");Assert.Equal(90,Assert.Single(engine.Execute("SELECT N FROM a.dbo.Items",session).Rows)[0]);Assert.Equal(180,Assert.Single(engine.Execute("SELECT N FROM b.dbo.Items",session).Rows)[0]);Assert.Equal(0,session.TransactionCount);Assert.Equal(TransactionOutcomeKind.Committed,host.Transactions.GetOutcome(id).Kind);}
    }
}
