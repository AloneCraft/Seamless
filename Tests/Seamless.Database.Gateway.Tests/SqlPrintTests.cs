using System.Data;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Query;
using SqlParameter=Seamless.Database.Query.SqlParameter;

namespace Seamless.Database.Gateway.Tests;

public sealed class SqlPrintTests{
    [Fact,Trait("Category","Unit")]
    public void PrintUsesQueryEvaluationAndBounds(){
        var engine=new SqlExecutionEngine(new EmptyAdapter());using var session=new SessionContext(1,"fixture");
        Assert.Throws<ArgumentNullException>(()=>QueryResult.Message(null!));
        Assert.Equal(" ",engine.Execute("PRINT NULL",session).Information);
        Assert.Equal(" ",engine.Execute("PRINT N''",session).Information);
        Assert.Equal("ab",engine.Execute("PRINT N'a'+N'b'",session).Information);
        var unicode=new string('n',4100);var ascii=new string('v',8100);
        var parameters=new[]{new SqlParameter("@p",new SqlType("nvarchar",typeof(string),-1))};
        Assert.Equal(new string('n',4000),engine.Execute("PRINT @p",session,parameters,new Dictionary<string,object?>{{"@p",unicode}}).Information);
        var varchar=new[]{new SqlParameter("@p",new SqlType("varchar",typeof(string),-1,Collation:"Latin1_General_100_CI_AS"))};
        Assert.Equal(new string('v',8000),engine.Execute("PRINT @p",session,varchar,new Dictionary<string,object?>{{"@p",ascii}}).Information);
        Assert.Equal(" ",engine.Execute("PRINT @p",session,parameters,new Dictionary<string,object?>{{"@p",null}}).Information);
        Assert.Equal("123",engine.Execute("PRINT 123",session).Information);
        Assert.Equal("12.34",engine.Execute("PRINT CAST(12.34 AS decimal(5,2))",session).Information);
        Assert.Throws<SqlQueryException>(()=>engine.Execute("PRINT CAST(1 AS bit)",session));
        Assert.Throws<SqlQueryException>(()=>engine.Execute("PRINT @p",session,parameters,new Dictionary<string,object?>()));
        var batch=engine.ExecuteBatch("PRINT N'before'; SELECT 7 AS x; PRINT N'after'",session);
        Assert.Equal(3,batch.Count);Assert.Equal("before",batch[0].Information);Assert.Equal(7,Assert.Single(batch[1].Rows)[0]);Assert.Equal("after",batch[2].Information);
    }
    [Fact,Trait("Category","Integration")]
    public async Task PrintEmitsSqlClientInfoMessagesInOrder(){
        var adapter=new EmptyAdapter();var engine=new SqlExecutionEngine(adapter);using var server=new TdsServer(0,adapter,engine);server.Start();
        var builder=new SqlConnectionStringBuilder{DataSource="127.0.0.1,"+server.Port,InitialCatalog="fixture",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};
        using var connection=new SqlConnection(builder.ConnectionString);var messages=new List<string>();var events=new List<string>();connection.InfoMessage+=(_,args)=>{messages.Add(args.Message);events.Add("info:"+args.Message);};
        await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="PRINT @p; SELECT 7 AS x; PRINT N'after'; PRINT NULL; PRINT N'';";
        command.Parameters.Add("@p",SqlDbType.NVarChar,50).Value="before";
        using(var reader=await command.ExecuteReaderAsync()){
            var values=new List<int>();do{while(await reader.ReadAsync()){values.Add(reader.GetInt32(0));events.Add("row");}}while(await reader.NextResultAsync());
            Assert.Equal(new[]{7},values);
        }
        Assert.Equal(new[]{"before","after"," "," "},messages);
        Assert.Equal(new[]{"info:before","row","info:after","info: ","info: "},events);
    }
    [SqlServerPrintFact,Trait("Category","Integration")]
    public async Task PrintMatchesSqlServerInfoMessage(){
        var adapter=new EmptyAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        var source=new SqlConnectionStringBuilder{DataSource="localhost",InitialCatalog="AdventureWorks2016",IntegratedSecurity=true,Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};
        var target=new SqlConnectionStringBuilder{DataSource="127.0.0.1,"+server.Port,InitialCatalog="fixture",UserID="sa",Password="local-test",Encrypt=false,TrustServerCertificate=true,Pooling=false,ConnectTimeout=10};
        var expected=await ReadMessages(source.ConnectionString);var actual=await ReadMessages(target.ConnectionString);
        Assert.Equal(5,expected.Count);Assert.Equal(expected,actual);
        Assert.Equal(await ReadAffected(source.ConnectionString),await ReadAffected(target.ConnectionString));
    }
    private static async Task<List<(string Message,int Number,byte Severity,byte State)>>ReadMessages(string connectionString){
        using var connection=new SqlConnection(connectionString);var messages=new List<(string,int,byte,byte)>();
        connection.InfoMessage+=(_,args)=>{foreach(SqlError error in args.Errors){messages.Add((error.Message,error.Number,error.Class,error.State));}};
        await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="PRINT NULL; PRINT N''; PRINT N'hello'; PRINT 123; PRINT CAST(12.34 AS decimal(5,2));";
        await command.ExecuteNonQueryAsync();return messages;
    }
    private static async Task<int>ReadAffected(string connectionString){
        using var connection=new SqlConnection(connectionString);await connection.OpenAsync();using var command=connection.CreateCommand();
        command.CommandText="PRINT N'hello';";return await command.ExecuteNonQueryAsync();
    }
    private sealed class EmptyAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata>GetTables()=>[];public TableMetadata?FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}

public sealed class SqlServerPrintFactAttribute:FactAttribute{
    public SqlServerPrintFactAttribute(){if(Environment.GetEnvironmentVariable("SEAMLESS_PRINT_SOURCE_SQL")!="1"){this.Skip="Set SEAMLESS_PRINT_SOURCE_SQL=1 when a local AdventureWorks2016 SQL Server is available.";}}
}
