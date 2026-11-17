using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class TdsFixedValueTests{
    [Theory,InlineData(false),InlineData(true),Trait("Category","Integration")]
    public async Task DepartmentNormalAndSchemaOnlyReaders(bool schemaOnly){
        var adapter=new FixedAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            await using var connection=await Open(server.Port);
            using var command=connection.CreateCommand();command.CommandText="SELECT * FROM dbo.Department";
            var behavior=CommandBehavior.Default;if(schemaOnly){behavior=CommandBehavior.SchemaOnly;}
            await using(var reader=await command.ExecuteReaderAsync(behavior)){
                var schema=reader.GetColumnSchema();Assert.Equal("datetime",schema[3].DataTypeName);Assert.False(schema[3].AllowDBNull);
                if(!schemaOnly){for(var i=0;i<3;i++){Assert.True(await reader.ReadAsync());Assert.Equal((short)(i+1),reader.GetInt16(0));Assert.Equal("name"+i,reader.GetString(1));Assert.Equal("group"+i,reader.GetString(2));Assert.Equal(new DateTime(2020,1,1).AddDays(i),reader.GetDateTime(3));}Assert.False(await reader.ReadAsync());}
            }
            command.CommandText="SELECT DB_NAME()";Assert.Equal("fixture",await command.ExecuteScalarAsync());
        }finally{await server.StopAsync();}
    }
    [Theory,InlineData(false),InlineData(true),Trait("Category","Integration")]
    public async Task DateAndMoneyValuesAndSchemaOnlyRemainSynchronized(bool schemaOnly){
        var adapter=new FixedAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            await using var connection=await Open(server.Port);using var command=connection.CreateCommand();
            if(!schemaOnly){
                command.CommandText="SELECT * FROM dbo.FixedValues";
                await using(var reader=await command.ExecuteReaderAsync()){
                    Assert.True(await reader.ReadAsync());Assert.Equal(new DateTime(2020,1,1),reader.GetDateTime(0));Assert.True(await reader.IsDBNullAsync(1));Assert.Equal(new DateTime(2021,2,3,4,5,0),reader.GetDateTime(2));Assert.True(await reader.IsDBNullAsync(3));
                    Assert.Equal(123.4567m,reader.GetDecimal(4));Assert.True(await reader.IsDBNullAsync(5));Assert.Equal(-12.3456m,reader.GetDecimal(6));Assert.True(await reader.IsDBNullAsync(7));Assert.Equal(42,reader.GetInt32(8));
                    Assert.True(await reader.ReadAsync());Assert.Equal(new DateTime(2022,1,2),reader.GetDateTime(1));Assert.Equal(new DateTime(2022,2,3,4,5,0),reader.GetDateTime(3));Assert.Equal(-123.4567m,reader.GetDecimal(5));Assert.Equal(12.3456m,reader.GetDecimal(7));Assert.Equal(43,reader.GetInt32(8));Assert.False(await reader.ReadAsync());
                }
            }else{
            foreach(var (name,type,nullable) in new[]{("DateValue","datetime",false),("DateNullable","datetime",true),("SmallDateValue","smalldatetime",false),("SmallDateNullable","smalldatetime",true),("MoneyValue","money",false),("MoneyNullable","money",true),("SmallMoneyValue","smallmoney",false),("SmallMoneyNullable","smallmoney",true)}){
                command.CommandText="SELECT "+name+", Marker FROM dbo.FixedValues";
                await using(var reader=await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly)){var schema=reader.GetColumnSchema();Assert.Equal(2,schema.Count);Assert.Equal(type,schema[0].DataTypeName);Assert.Equal(nullable,schema[0].AllowDBNull);}
                command.CommandText="SELECT DB_NAME()";Assert.Equal("fixture",await command.ExecuteScalarAsync());
            }
            }
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Integration")]
    public async Task NullableDateAndMoneyNonNullValues(){
        var adapter=new FixedAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        try{
            await using var connection=await Open(server.Port);using var command=connection.CreateCommand();
            command.CommandText="SELECT DateNullable, SmallDateNullable, MoneyNullable, SmallMoneyNullable, Marker FROM dbo.FixedValues WHERE Marker=43";
            await using(var reader=await command.ExecuteReaderAsync()){
                Assert.True(await reader.ReadAsync());Assert.Equal(new DateTime(2022,1,2),reader.GetDateTime(0));Assert.Equal(new DateTime(2022,2,3,4,5,0),reader.GetDateTime(1));Assert.Equal(-123.4567m,reader.GetDecimal(2));Assert.Equal(12.3456m,reader.GetDecimal(3));Assert.Equal(43,reader.GetInt32(4));Assert.False(await reader.ReadAsync());
            }
        }finally{await server.StopAsync();}
    }
    private static async Task<SqlConnection>Open(int port){
        var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();return connection;
    }
    private sealed class FixedAdapter:IDatabaseAdapter{
        private readonly TableMetadata[] tables=[
            new("dbo","Department",[new("Id",typeof(short),"smallint",false,false,0),new("Name",typeof(string),"nvarchar",false,false,1,50),new("GroupName",typeof(string),"nvarchar",false,false,2,50),new("Modified",typeof(DateTime),"datetime",false,false,3,scale:3)]),
            new("dbo","FixedValues",[new("DateValue",typeof(DateTime),"datetime",false,false,0,scale:3),new("DateNullable",typeof(DateTime),"datetime",true,false,1,scale:3),new("SmallDateValue",typeof(DateTime),"smalldatetime",false,false,2),new("SmallDateNullable",typeof(DateTime),"smalldatetime",true,false,3),new("MoneyValue",typeof(decimal),"money",false,false,4),new("MoneyNullable",typeof(decimal),"money",true,false,5),new("SmallMoneyValue",typeof(decimal),"smallmoney",false,false,6),new("SmallMoneyNullable",typeof(decimal),"smallmoney",true,false,7),new("Marker",typeof(int),"int",false,false,8)])
        ];
        public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata>GetTables()=>this.tables;
        public TableMetadata? FindTable(string name)=>this.tables.FirstOrDefault(t=>string.Equals(t.FullName,name,StringComparison.OrdinalIgnoreCase));
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session){
            var table=this.FindTable(tableName)??throw new InvalidOperationException("Unknown relation.");
            if(table.TableName=="Department"){return new(table.Columns,[(object?[])[(short)1,"name0","group0",new DateTime(2020,1,1)],[(short)2,"name1","group1",new DateTime(2020,1,2)],[(short)3,"name2","group2",new DateTime(2020,1,3)]],3);}
            return new(table.Columns,[(object?[])[new DateTime(2020,1,1),null,new DateTime(2021,2,3,4,5,0),null,123.4567m,null,-12.3456m,null,42],[new DateTime(2020,1,1),new DateTime(2022,1,2),new DateTime(2021,2,3,4,5,0),new DateTime(2022,2,3,4,5,0),123.4567m,-123.4567m,-12.3456m,12.3456m,43]],2);
        }
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
