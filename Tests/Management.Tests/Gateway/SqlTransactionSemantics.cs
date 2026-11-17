using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;

namespace Seamless.Database.Gateway.Tests;

public sealed class SqlTransactionSemantics{
    private static RuntimeSqlGeneratedRoot Database()=>new(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20});
    private static void Execute(SqlExecutionEngine engine,SessionContext session,string sql){foreach(var result in engine.ExecuteBatch(sql,session)){foreach(var row in result.Rows){}}}
    private static int Scalar(SqlExecutionEngine engine,SessionContext session,string sql)=>Convert.ToInt32(Assert.Single(engine.Execute(sql,session).Rows)[0]);

    [Fact]
    public void NestedCommitDoesNotPublishDmlOrDdl(){
        using var database=Database();var registry=new DatabaseRegistry();registry.Register("fixture",new InProcessDatabaseAdapter(database));var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"fixture");
        Execute(engine,session,"CREATE TABLE dbo.t(id int NOT NULL PRIMARY KEY,n int NULL)");
        Execute(engine,session,"BEGIN TRANSACTION; INSERT dbo.t(id,n) VALUES(1,10); BEGIN TRANSACTION; ALTER TABLE dbo.t ADD note nvarchar(20) NULL; COMMIT TRANSACTION;");
        Assert.True(session.IsInTransaction);Assert.Equal(1,Scalar(engine,session,"SELECT @@TRANCOUNT"));Assert.Equal(1,Scalar(engine,session,"SELECT XACT_STATE()"));
        Execute(engine,session,"ROLLBACK TRANSACTION");
        Assert.False(session.IsInTransaction);Assert.Equal(0,Scalar(engine,session,"SELECT @@TRANCOUNT"));Assert.Equal(0,Scalar(engine,session,"SELECT COUNT(*) FROM dbo.t"));Assert.Equal(2,database.RuntimeSql.FindTable("dbo","t")!.Columns.Count);
    }

    [Fact]
    public void SavepointRestoresDmlAndSchemaWithoutEndingTransaction(){
        using var database=Database();var registry=new DatabaseRegistry();registry.Register("fixture",new InProcessDatabaseAdapter(database));var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(2,"fixture");
        Execute(engine,session,"CREATE TABLE dbo.t(id int NOT NULL PRIMARY KEY,n int NULL)");
        Execute(engine,session,"BEGIN TRANSACTION; INSERT dbo.t(id,n) VALUES(1,10); SAVE TRANSACTION before_ddl; ALTER TABLE dbo.t ADD extra int NULL; UPDATE dbo.t SET n=20,extra=7 WHERE id=1;");
        Assert.Equal(3,session.Run(()=>database.RuntimeSql.FindTable("dbo","t")!.Columns.Count));
        Execute(engine,session,"ROLLBACK TRANSACTION before_ddl");
        Assert.True(session.IsInTransaction);Assert.Equal(1,Scalar(engine,session,"SELECT @@TRANCOUNT"));Assert.Equal(2,session.Run(()=>database.RuntimeSql.FindTable("dbo","t")!.Columns.Count));Assert.Equal(10,Scalar(engine,session,"SELECT n FROM dbo.t WHERE id=1"));
        Execute(engine,session,"UPDATE dbo.t SET n=30 WHERE id=1; COMMIT TRANSACTION;");
        Assert.Equal(30,Scalar(engine,session,"SELECT n FROM dbo.t WHERE id=1"));Assert.Equal(0,Scalar(engine,session,"SELECT XACT_STATE()"));
    }

    [Fact]
    public void SavepointNamesUseMostRecentCaseSensitiveMatch(){
        using var database=Database();var registry=new DatabaseRegistry();registry.Register("fixture",new InProcessDatabaseAdapter(database));var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(3,"fixture");
        Execute(engine,session,"CREATE TABLE dbo.t(id int NOT NULL PRIMARY KEY,n int NULL)");
        Execute(engine,session,"BEGIN TRANSACTION; INSERT dbo.t(id,n) VALUES(1,1); SAVE TRANSACTION point; UPDATE dbo.t SET n=2; SAVE TRANSACTION point; UPDATE dbo.t SET n=3;");
        Execute(engine,session,"ROLLBACK TRANSACTION point");Assert.Equal(2,Scalar(engine,session,"SELECT n FROM dbo.t"));
        var error=Assert.Throws<Seamless.Database.Query.SqlQueryException>(()=>Execute(engine,session,"ROLLBACK TRANSACTION Point"));Assert.Equal(6401,error.Number);Assert.True(session.IsInTransaction);
        Execute(engine,session,"ROLLBACK TRANSACTION point; UPDATE dbo.t SET n=4; COMMIT TRANSACTION;");Assert.Equal(4,Scalar(engine,session,"SELECT n FROM dbo.t"));
    }

    [Fact]
    public void FailedStatementRestoresItsRowsWithoutPoisoningOuterTransaction(){
        using var database=Database();var registry=new DatabaseRegistry();registry.Register("fixture",new InProcessDatabaseAdapter(database));var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(4,"fixture");
        Execute(engine,session,"CREATE TABLE dbo.t(id int NOT NULL PRIMARY KEY,n int NULL); BEGIN TRANSACTION; INSERT dbo.t(id,n) VALUES(1,10);");
        Assert.ThrowsAny<Exception>(()=>Execute(engine,session,"INSERT dbo.t(id,n) VALUES(2,20),(1,30)"));
        Assert.Equal(1,Scalar(engine,session,"SELECT XACT_STATE()"));Assert.Equal(1,Scalar(engine,session,"SELECT COUNT(*) FROM dbo.t"));
        Execute(engine,session,"INSERT dbo.t(id,n) VALUES(3,30); COMMIT TRANSACTION;");Assert.Equal(2,Scalar(engine,session,"SELECT COUNT(*) FROM dbo.t"));
    }

    [Fact]
    public void XactAbortMakesCaughtTransactionUncommittableAndAbortsUncaughtOne(){
        using var database=Database();var registry=new DatabaseRegistry();registry.Register("fixture",new InProcessDatabaseAdapter(database));var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(5,"fixture");
        Execute(engine,session,"CREATE TABLE dbo.t(id int NOT NULL PRIMARY KEY,n int NULL); INSERT dbo.t(id,n) VALUES(1,1); SET XACT_ABORT ON; BEGIN TRANSACTION;");
        var results=engine.ExecuteBatch("BEGIN TRY INSERT dbo.t(id,n) VALUES(2,2),(1,3); END TRY BEGIN CATCH SELECT XACT_STATE() AS state; BEGIN TRY COMMIT TRANSACTION; END TRY BEGIN CATCH SELECT ERROR_NUMBER() AS error; END CATCH; ROLLBACK TRANSACTION; END CATCH",session);
        Assert.Equal(2,results.Count(static result=>result.Columns.Count>0));Assert.Equal((short)-1,Assert.Single(results[0].Rows)[0]);Assert.Equal(3930,Assert.Single(results[1].Rows)[0]);Assert.False(session.IsInTransaction);
        Execute(engine,session,"BEGIN TRANSACTION");Assert.ThrowsAny<Exception>(()=>Execute(engine,session,"INSERT dbo.t(id,n) VALUES(2,2),(1,3)"));Assert.False(session.IsInTransaction);Assert.Equal(1,Scalar(engine,session,"SELECT COUNT(*) FROM dbo.t"));
    }

    [Fact]
    public void UncommittableTransactionRollsBackAtEndOfBatch(){
        using var database=Database();var engine=new SqlExecutionEngine(new InProcessDatabaseAdapter(database));using var session=new SessionContext(7,database.DatabaseName);
        Execute(engine,session,"CREATE TABLE dbo.t(id int NOT NULL PRIMARY KEY); INSERT dbo.t(id) VALUES(1); BEGIN TRANSACTION; INSERT dbo.t(id) VALUES(2); SET XACT_ABORT ON");
        var results=engine.ExecuteBatch("BEGIN TRY INSERT dbo.t(id) VALUES(3),(1); END TRY BEGIN CATCH SELECT XACT_STATE() AS state; END CATCH",session);
        Assert.Equal(2,results.Count);Assert.Equal((short)-1,Assert.Single(results[0].Rows)[0]);Assert.Equal(3998,Assert.Throws<Seamless.Database.Query.SqlQueryException>(()=>results[1].Rows.ToArray()).Number);Assert.False(session.IsInTransaction);Assert.Equal(1,Scalar(engine,session,"SELECT COUNT(*) FROM dbo.t"));
    }

    [Fact]
    public void TransactionFunctionsReturnSqlServerMetadataTypes(){
        using var database=Database();var engine=new SqlExecutionEngine(new InProcessDatabaseAdapter(database));using var session=new SessionContext(6,database.DatabaseName);
        var state=engine.Execute("SELECT XACT_STATE() AS state",session);Assert.Equal(typeof(short),Assert.Single(state.Columns).ClrType);Assert.Equal((short)0,Assert.Single(state.Rows)[0]);
        var count=engine.Execute("SELECT @@TRANCOUNT AS depth",session);Assert.Equal(typeof(int),Assert.Single(count.Columns).ClrType);Assert.Equal(0,Assert.Single(count.Rows)[0]);
    }
}
