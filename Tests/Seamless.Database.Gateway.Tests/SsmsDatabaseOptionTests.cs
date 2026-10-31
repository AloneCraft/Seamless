using System.Data;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class SsmsDatabaseOptionTests{
    private static string Resource(string name){using var stream=typeof(SqlExecutionEngine).Assembly.GetManifestResourceStream("Seamless.Database.Gateway.Sql.Compatibility."+name)??throw new InvalidOperationException(name);using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    [Fact,Trait("Category","Unit")]
    public void EmptyRetentionProfileIsScopedAndDoesNotBypassReadOnly(){
        var registry=new DatabaseRegistry();registry.Register("fixture",new Adapter(),true);var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"fixture");
        var sql=Resource("SsmsDatabaseOptions.sql");var parser=new TSql160Parser(true);var script=(TSqlScript)parser.Parse(new StringReader(sql),out var errors);Assert.Empty(errors);
        var statements=script.Batches.Single().Statements;string Text(TSqlStatement statement)=>sql.Substring(statement.StartOffset,statement.FragmentLength);
        var prefix=Text(statements[0])+";"+Text(statements[1]);var select=Text(statements[2]);var cleanup=Text(statements[3]);
        var parameters=new[]{new Seamless.Database.Query.SqlParameter("@_msparam_0",Seamless.Database.Query.SqlType.Bit)};var values=new Dictionary<string,object?>{{"@_msparam_0",false}};
        void ExecuteBatch(string text){foreach(var result in engine.ExecuteBatch(text,session,parameters,values)){_=result.Columns;_=result.Rows.ToArray();}}
        Assert.ThrowsAny<Exception>(()=>ExecuteBatch(select));
        Assert.Equal(3906,Assert.Throws<Seamless.Database.Query.SqlQueryException>(()=>ExecuteBatch(cleanup)).Number);
        ExecuteBatch(prefix);
        using(var other=new SessionContext(2,"fixture")){Assert.ThrowsAny<Exception>(()=>engine.Execute(select,other,parameters,values).Rows.ToArray());}
        ExecuteBatch(select);ExecuteBatch(cleanup);
        Assert.ThrowsAny<Exception>(()=>ExecuteBatch(select));
        ExecuteBatch(prefix);session.Reset();Assert.ThrowsAny<Exception>(()=>ExecuteBatch(select));
        Assert.Equal(3906,Assert.Throws<Seamless.Database.Query.SqlQueryException>(()=>ExecuteBatch(sql.Replace("= 9","= 3",StringComparison.Ordinal))).Number);
        Assert.Equal(3906,Assert.Throws<Seamless.Database.Query.SqlQueryException>(()=>ExecuteBatch(sql+"; DELETE FROM dbo.Protected;")).Number);
        Assert.Equal(new byte[]{0,64},Assert.IsType<byte[]>(engine.Execute("SELECT 0x0040",session).Rows.Single()[0]));
    }
    [Theory,Trait("Category","Integration")]
    [InlineData("SsmsDatabaseOptions.sql","fixture")]
    [InlineData("SsmsDatabaseList.sql","fixture")]
    [InlineData("SsmsDatabaseOptions.sql","master")]
    [InlineData("SsmsDatabaseList.sql","master")]
    public async Task ActualSfcDatabaseQueriesExecuteThroughNtextStatement(string resource,string initialCatalog){
        var adapter=new Adapter();var registry=new DatabaseRegistry();registry.Register("fixture",adapter,true);registry.Register("scratch",new Adapter());using var server=new TdsServer(0,adapter,new SqlExecutionEngine(registry));server.Start();
        try{
            await using var connection=new SqlConnection(new SqlConnectionStringBuilder{DataSource=$"127.0.0.1,{server.Port}",InitialCatalog=initialCatalog,UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5}.ConnectionString);await connection.OpenAsync();
            using var command=connection.CreateCommand();command.CommandType=CommandType.StoredProcedure;command.CommandText="sp_executesql";command.Parameters.Add("@stmt",SqlDbType.NText).Value=Resource(resource);command.Parameters.Add("@params",SqlDbType.NVarChar,-1).Value="@_msparam_0 bit";command.Parameters.Add("@_msparam_0",SqlDbType.Bit).Value=false;
            await using var reader=await command.ExecuteReaderAsync();var readonlyValues=new List<bool>();var names=new List<string>();
            while(await reader.ReadAsync()){
                readonlyValues.Add(reader.GetBoolean(reader.GetOrdinal("ReadOnly")));
                if(resource=="SsmsDatabaseOptions.sql"){
                    Assert.Equal(30,reader.FieldCount);Assert.False(reader.GetBoolean(reader.GetOrdinal("DataRetentionEnabled")));Assert.False(reader.GetBoolean(reader.GetOrdinal("AutoShrink")));Assert.False(reader.GetBoolean(reader.GetOrdinal("BrokerEnabled")));Assert.True(reader.GetBoolean(reader.GetOrdinal("AnsiNullsEnabled")));Assert.Equal(0,reader.GetInt32(reader.GetOrdinal("MirroringTimeout")));
                }else{names.Add(reader.GetString(reader.GetOrdinal("Name")));Assert.Equal(1,reader.GetInt32(reader.GetOrdinal("Status")));Assert.True(reader.GetBoolean(reader.GetOrdinal("IsAccessible")));Assert.False(reader.GetBoolean(reader.GetOrdinal("IsSystemObject")));Assert.False(reader.GetBoolean(reader.GetOrdinal("IsDatabaseSnapshot")));}
            }
            Assert.Equal(new[]{false,true},readonlyValues.Order());if(names.Count>0){Assert.Equal(new[]{"fixture","scratch"},names.Order());}Assert.False(await reader.NextResultAsync());
        }finally{await server.StopAsync();}
    }
    private sealed class Adapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata> GetTables()=>[];public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string t,IReadOnlyList<string>?c,Func<object,bool>?p,SessionContext s)=>throw new InvalidOperationException();
        public int ExecuteInsert(string t,IReadOnlyDictionary<string,object?>v,SessionContext s)=>throw new InvalidOperationException();
        public int ExecuteUpdate(string t,IReadOnlyDictionary<string,object?>v,Func<object,bool>?p,SessionContext s)=>throw new InvalidOperationException();
        public int ExecuteDelete(string t,Func<object,bool>?p,SessionContext s)=>throw new InvalidOperationException();
        public void BeginTransaction(SessionContext s)=>throw new InvalidOperationException();public void CommitTransaction(SessionContext s)=>throw new InvalidOperationException();public void RollbackTransaction(SessionContext s)=>throw new InvalidOperationException();
    }
}
