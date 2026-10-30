using System.Data.Common;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class SystemCatalogWireTests{
    [Fact,Trait("Category","Integration")]
    public async Task ColumnsReturnsNullAndNamedCollationsWithoutCorruptingRows(){
        var adapter=new CatalogAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
            await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();
            using var command=connection.CreateCommand();command.CommandText="SELECT * FROM sys.columns ORDER BY column_id";
            await using var reader=await command.ExecuteReaderAsync();
            var schema=reader.GetColumnSchema();
            Assert.True(await reader.ReadAsync());Assert.Equal("Id",reader.GetString(1));Assert.True(await reader.IsDBNullAsync(9));
            Assert.True(await reader.ReadAsync());Assert.Equal("Title",reader.GetString(1));Assert.Equal("Japanese_CI_AS",reader.GetString(9));
            Assert.False(await reader.ReadAsync());Assert.False(await reader.NextResultAsync());Assert.True(schema[9].AllowDBNull);
        }finally{await server.StopAsync();}
    }
    private sealed class CatalogAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata>GetTables()=>[new("dbo","Items",[new("Id",typeof(int),"int",false,true,0),new("Title",typeof(string),"nvarchar",true,false,1,50,collation:"Japanese_CI_AS")])];
        public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
