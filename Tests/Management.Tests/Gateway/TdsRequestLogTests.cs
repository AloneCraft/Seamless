using System.Text.Json;
using Microsoft.Data.SqlClient;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;

namespace Seamless.Database.Gateway.Tests;
public sealed class TdsRequestLogTests{
    [Fact,Trait("Category","Integration")]
    public async Task RecordsBatchRpcAndFailureWithoutValuesOrSecrets(){
        var root=Environment.GetEnvironmentVariable("SEAMLESS_COMPARISON_TEMP_ROOT");
        if(string.IsNullOrWhiteSpace(root)){root=Directory.Exists(@"D:\")?@"D:\Seamless\Comparison\Temp":Path.GetTempPath();}
        var path=Path.Combine(root,"SeamlessTests_RequestLog_"+Guid.NewGuid().ToString("N"),"requests.jsonl");
        try{
            var adapter=new Adapter();
            using(var log=new TdsRequestLog(path)){
                using var server=new TdsServer(0,adapter,new SqlExecutionEngine(adapter),log);server.Start();
                try{
                    var options=new SqlConnectionStringBuilder{DataSource=$"tcp:127.0.0.1,{server.Port}",InitialCatalog="fixture",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5};
                    await using var connection=new SqlConnection(options.ConnectionString);await connection.OpenAsync();
                    using(var command=connection.CreateCommand()){
                        command.CommandText="SELECT SERVERPROPERTY('EngineEdition') AS DatabaseEngineType";
                        Assert.Equal(3,Convert.ToInt32(await command.ExecuteScalarAsync()));
                    }
                    using(var command=connection.CreateCommand()){
                        command.CommandText="SELECT @value AS Value";command.Parameters.Add(new SqlParameter("@value",global::System.Data.SqlDbType.Int){Value=424242});
                        Assert.Equal(424242,Convert.ToInt32(await command.ExecuteScalarAsync()));
                    }
                    using(var command=connection.CreateCommand()){
                        command.CommandText="ALTER LOGIN sa WITH PASSWORD = N'hunter2-secret'";
                        await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync());
                    }
                }finally{await server.StopAsync();}
            }
            var records=File.ReadAllLines(path).Select(line=>JsonDocument.Parse(line).RootElement).ToArray();
            var text=File.ReadAllText(path);
            var batch=Assert.Single(records,r=>r.GetProperty("kind").GetString()=="batch"&&r.GetProperty("sql").GetString()!.Contains("EngineEdition",StringComparison.Ordinal));
            Assert.True(batch.GetProperty("ok").GetBoolean());Assert.Equal("fixture",batch.GetProperty("database").GetString());Assert.Equal(1,batch.GetProperty("resultSets").GetInt32());
            var rpc=Assert.Single(records,r=>r.GetProperty("procedure").ValueKind==JsonValueKind.String&&r.GetProperty("procedure").GetString()=="sp_executesql");
            Assert.True(rpc.GetProperty("ok").GetBoolean());Assert.Equal("SELECT @value AS Value",rpc.GetProperty("sql").GetString());
            Assert.Contains("@value int",rpc.GetProperty("declarations").GetString(),StringComparison.OrdinalIgnoreCase);
            Assert.Contains(rpc.GetProperty("parameters").EnumerateArray(),p=>p.GetProperty("name").GetString()=="@value"&&p.GetProperty("type").GetString()=="int");
            Assert.DoesNotContain("424242",text,StringComparison.Ordinal);
            var failed=Assert.Single(records,r=>r.GetProperty("kind").GetString()=="batch"&&r.GetProperty("sql").GetString()!.Contains("ALTER LOGIN",StringComparison.Ordinal));
            Assert.False(failed.GetProperty("ok").GetBoolean());Assert.False(string.IsNullOrEmpty(failed.GetProperty("error").GetProperty("message").GetString()));
            Assert.Contains("PASSWORD = '***'",failed.GetProperty("sql").GetString(),StringComparison.Ordinal);
            Assert.DoesNotContain("hunter2",text,StringComparison.Ordinal);
            Assert.Equal(records.Length,records.Select(r=>r.GetProperty("seq").GetInt64()).Distinct().Count());
        }finally{var directory=Path.GetDirectoryName(path)!;if(Directory.Exists(directory)){Directory.Delete(directory,true);}}
    }
    [Theory]
    [InlineData("CREATE LOGIN x WITH PASSWORD = 'a''b'","CREATE LOGIN x WITH PASSWORD = '***'")]
    [InlineData("CREATE CREDENTIAL c WITH IDENTITY='u', SECRET=N'k'","CREATE CREDENTIAL c WITH IDENTITY='u', SECRET='***'")]
    [InlineData("SELECT 'PASSWORD' AS name","SELECT 'PASSWORD' AS name")]
    public void MasksSecretLiterals(string sql,string expected)=>Assert.Equal(expected,TdsRequestLog.Mask(sql));
    private sealed class Adapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata> GetTables()=>[];
        public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?> values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?> values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
