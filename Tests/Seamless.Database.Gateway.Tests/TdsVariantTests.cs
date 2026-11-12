using System.Data.SqlTypes;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;

public sealed class TdsVariantTests{
    [Fact,Trait("Category","Integration")]
    public async Task VariantBaseTypesAndNullsPreserveFollowingValuesAndPacketBoundaries(){
        var adapter=new VariantAdapter();using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter));server.Start();
        using var connection=new SqlConnection(new SqlConnectionStringBuilder{DataSource="tcp:127.0.0.1,"+server.Port,InitialCatalog="fixture",UserID="sa",Password="local-test",Encrypt=false,Pooling=false,PacketSize=512,ConnectTimeout=5}.ConnectionString);await connection.OpenAsync();using var command=connection.CreateCommand();command.CommandText="SELECT Value,Marker FROM dbo.Variants";
        using var reader=await command.ExecuteReaderAsync();Assert.Equal("sql_variant",reader.GetDataTypeName(0));
        for(var index=0;index<adapter.Values.Length;index++){
            Assert.True(await reader.ReadAsync());var expected=adapter.Values[index];if(expected is null){Assert.True(reader.IsDBNull(0));}
            else if(expected is SqlDecimal numeric){var actual=Assert.IsType<SqlDecimal>(reader.GetSqlValue(0));Assert.Equal(numeric.Precision,actual.Precision);Assert.Equal(numeric.Scale,actual.Scale);Assert.True((numeric==actual).Value);}
            else if(expected is byte[] binary){Assert.Equal(binary,Assert.IsType<byte[]>(reader.GetValue(0)));}
            else if(expected is DateOnly day){Assert.Equal(day.ToDateTime(TimeOnly.MinValue),reader.GetValue(0));}
            else if(expected is TimeOnly time){Assert.Equal(time.ToTimeSpan(),reader.GetValue(0));}
            else{Assert.Equal(expected,reader.GetValue(0));}
            Assert.Equal(index,reader.GetInt32(1));
        }
        Assert.False(await reader.ReadAsync());Assert.False(await reader.NextResultAsync());
    }
    private sealed class VariantAdapter:IDatabaseAdapter{
        private readonly TableMetadata table=new("dbo","Variants",[new("Value",typeof(object),"sql_variant",true,false,0),new("Marker",typeof(int),"int",false,false,1)]);
        internal object?[] Values{get;}=[null,(byte)255,short.MinValue,int.MaxValue,long.MinValue,true,float.MaxValue,double.Epsilon,Guid.Parse("12345678-1234-4321-abcd-123456789012"),new DateOnly(2020,2,3),new TimeOnly(4,5,6).Add(TimeSpan.FromTicks(1234567)),new DateTime(2020,2,3,4,5,6).AddTicks(1234567),new DateTimeOffset(2020,2,3,4,5,6,TimeSpan.FromHours(9)),decimal.MaxValue,SqlDecimal.Parse("99999999999999999999999999999999999999"),new string('日',4000),Enumerable.Range(0,8000).Select(static index=>(byte)index).ToArray(),null];
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata> GetTables()=>[this.table];
        public TableMetadata? FindTable(string name){if(name.Equals("dbo.Variants",StringComparison.OrdinalIgnoreCase)){return this.table;}return null;}
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>new(this.table.Columns,this.Values.Select((value,index)=>new object?[]{value,index}),0);
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?> values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?> values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();
        public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();
        public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
