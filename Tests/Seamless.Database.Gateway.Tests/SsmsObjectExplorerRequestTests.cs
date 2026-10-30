using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Tests;

public sealed class SsmsObjectExplorerRequestTests{
    private static string Resource(string name){using var stream=typeof(SqlExecutionEngine).Assembly.GetManifestResourceStream("Seamless.Database.Gateway.Sql.Compatibility.SsmsObjectExplorer"+name+".sql")??throw new InvalidOperationException(name);using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    private static DatabaseRegistry Registry(){
        var registry=new DatabaseRegistry();var procedure=new RoutineCatalog("dbo","Run",RoutineKind.StoredProcedure,null,[],[],"CREATE PROCEDURE dbo.Run AS SELECT 1",false,0,0){SourceObjectType="P"};
        var function=new RoutineCatalog("dbo","Value",RoutineKind.ScalarFunction,new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),[],[],"CREATE FUNCTION dbo.Value() RETURNS int AS BEGIN RETURN 1; END",false,0,0){SourceObjectType="FN"};
        registry.Register("AdventureWorks2016",new Adapter(),true,views:[new("dbo","Bound","CREATE VIEW dbo.Bound WITH SCHEMABINDING AS SELECT 1 AS Value",[])],routines:[new("dbo","Run",procedure.Definition,procedure),new("dbo","Value",function.Definition,function)]);
        registry.Register("scratch",new Adapter());return registry;
    }
    [Theory,Trait("Category","Integration")]
    [InlineData("Database",2)]
    [InlineData("Table160",2)]
    [InlineData("View160",1)]
    [InlineData("Procedure120",1)]
    [InlineData("Functions",1)]
    public async Task HierarchyPropertiesFromInstalledSsmsExecuteThroughNtext(string name,int expectedRows){
        var registry=Registry();using var server=new TdsServer(0,registry.Resolve("AdventureWorks2016").Adapter,new SqlExecutionEngine(registry));server.Start();
        try{
            await using var connection=new SqlConnection(new SqlConnectionStringBuilder{DataSource=$"127.0.0.1,{server.Port}",InitialCatalog="master",UserID="sa",Password="test",Encrypt=false,Pooling=false,ConnectTimeout=5}.ConnectionString);await connection.OpenAsync();
            using var command=connection.CreateCommand();command.CommandType=CommandType.StoredProcedure;command.CommandText="sp_executesql";
            command.Parameters.Add("@stmt",SqlDbType.NText).Value=Resource(name);command.Parameters.Add("@params",SqlDbType.NVarChar,-1).Value="@_msparam_0 int,@ui_0 int,@ui_2 int,@ui_V nvarchar(2),@ui_P nvarchar(2),@ui_RF nvarchar(2),@ui_PC nvarchar(2)";
            command.Parameters.AddWithValue("@_msparam_0",0);command.Parameters.AddWithValue("@ui_0",0);command.Parameters.AddWithValue("@ui_2",2);command.Parameters.AddWithValue("@ui_V","V");command.Parameters.AddWithValue("@ui_P","P");command.Parameters.AddWithValue("@ui_RF","RF");command.Parameters.AddWithValue("@ui_PC","PC");
            await using var reader=await command.ExecuteReaderAsync();var count=0;var nameColumn="Name";var urnColumn="Urn";if(name=="Database"){nameColumn="Database_Name";urnColumn="Database_Urn";}
            while(await reader.ReadAsync()){
                count++;var objectName=reader.GetString(reader.GetOrdinal(nameColumn));
                Assert.StartsWith("Server[@Name='127.0.0.1,14333']/Database[@Name='",reader.GetString(reader.GetOrdinal(urnColumn)));
                if(name=="Database"){Assert.Equal(1,reader.GetInt32(reader.GetOrdinal("Database_Status")));Assert.True(reader.IsDBNull(reader.GetOrdinal("Database_AvailabilityDatabaseSynchronizationState")));Assert.Equal(objectName=="AdventureWorks2016",reader.GetBoolean(reader.GetOrdinal("ReadOnly")));}
                if(name=="Table160"){Assert.False(reader.GetBoolean(reader.GetOrdinal("IsMemoryOptimized")));Assert.Equal(objectName=="Heap",reader.GetBoolean(reader.GetOrdinal("HasXmlData")));}
                if(name=="View160"){Assert.True(reader.GetBoolean(reader.GetOrdinal("IsSchemaBound")));}
                if(name=="Functions"){Assert.Equal(1,reader.GetInt32(reader.GetOrdinal("FunctionType")));}
            }
            Assert.Equal(expectedRows,count);Assert.False(await reader.NextResultAsync());
        }finally{await server.StopAsync();}
    }
    [Fact,Trait("Category","Unit")]
    public void AvailabilityProfileDoesNotLeakAcrossSessionsOrAcceptChangedSetup(){
        var engine=new SqlExecutionEngine(Registry());using var session=new SessionContext(1,"master");var sql=Resource("Database");var script=(TSqlScript)new TSql160Parser(true).Parse(new StringReader(sql),out var errors);Assert.Empty(errors);
        var statements=script.Batches.Single().Statements;var selectIndex=statements.Count-5;string Text(TSqlStatement s)=>sql.Substring(s.StartOffset,s.FragmentLength);
        var prefix=string.Join(";",statements.Take(selectIndex).Select(Text));var select=Text(statements[selectIndex]);var cleanup=statements.Skip(selectIndex+1).Select(Text).ToArray();
        var parameters=new[]{new Seamless.Database.Query.SqlParameter("@_msparam_0",SqlType.Int)};var values=new Dictionary<string,object?>{{"@_msparam_0",0}};
        void Execute(string text,SessionContext target){foreach(var result in engine.ExecuteBatch(text,target,parameters,values)){_=result.Columns;_=result.Rows.ToArray();}}
        Execute(prefix,session);using(var other=new SessionContext(2,"master")){Assert.Throws<SqlQueryException>(()=>Execute(select,other));}
        Execute(select,session);foreach(var text in cleanup){Execute(text,session);}Assert.Throws<SqlQueryException>(()=>Execute(select,session));
        Execute(prefix,session);session.Reset();Assert.Throws<SqlQueryException>(()=>Execute(select,session));
        Assert.Throws<SqlQueryException>(()=>Execute(sql.Replace("'VIEW SERVER STATE'","'CONTROL SERVER'",StringComparison.Ordinal),session));
        Assert.Throws<SqlQueryException>(()=>Execute(sql+"; DELETE FROM dbo.Protected;",session));
    }
    private sealed class Adapter:IDatabaseAdapter{
        public string DatabaseName=>"AdventureWorks2016";
        public IReadOnlyList<TableMetadata>GetTables()=>[new("dbo","Heap",[new("Document",typeof(string),"xml",true,false,0)]),new("dbo","Keyed",[new("Id",typeof(int),"int",false,true,0)],new("PK_Keyed",[new("Id",false)],1))];
        public TableMetadata?FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string t,IReadOnlyList<string>?c,Func<object,bool>?p,SessionContext s)=>throw new NotSupportedException();
        public int ExecuteInsert(string t,IReadOnlyDictionary<string,object?>v,SessionContext s)=>throw new NotSupportedException();
        public int ExecuteUpdate(string t,IReadOnlyDictionary<string,object?>v,Func<object,bool>?p,SessionContext s)=>throw new NotSupportedException();
        public int ExecuteDelete(string t,Func<object,bool>?p,SessionContext s)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext s)=>throw new NotSupportedException();public void CommitTransaction(SessionContext s)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext s)=>throw new NotSupportedException();
    }
}
