using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tests;

public sealed class SsmsObjectCatalogMetadataTests{
    [Fact,Trait("Category","Unit")]
    public void CatalogExposesObjectExplorerMetadata(){
        var registry=new DatabaseRegistry();
        registry.Register("fixture",new MetadataAdapter(),views:[
            new("dbo","Bound","CREATE VIEW dbo.Bound WITH SCHEMABINDING AS SELECT 1 AS n",[]),
            new("dbo","Unbound","CREATE VIEW dbo.Unbound AS SELECT 1 AS n",[]),
            new("dbo","Unknown","SELECT 1 AS n",[])]);
        var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"fixture");
        var tables=engine.Execute("SELECT t.name, i.index_id, i.type, i.name, t.is_memory_optimized, t.temporal_type, t.ledger_type, t.is_filetable, t.is_external, t.is_node, t.is_edge, i.is_hypothetical, i.is_unique_constraint FROM sys.tables t JOIN sys.indexes i ON t.object_id=i.object_id",session).Rows.ToArray();
        var heap=Assert.Single(tables,row=>Equals(row[0],"Heap"));Assert.Equal(0,heap[1]);Assert.Equal((byte)0,heap[2]);Assert.Null(heap[3]);
        Assert.Equal(false,heap[4]);Assert.Equal((byte)0,heap[5]);Assert.Equal((byte)0,heap[6]);
        for(var i=7;i<heap.Length;i++){Assert.Equal(false,heap[i]);}
        var key=Assert.Single(tables,row=>Equals(row[0],"Keyed"));Assert.Equal(1,key[1]);Assert.Equal((byte)2,key[2]);Assert.Equal("PK_Keyed",key[3]);
        var data=engine.Execute("SELECT t.name, ty.name, c.is_sparse FROM sys.tables t JOIN sys.all_columns c ON t.object_id=c.object_id JOIN sys.types ty ON c.user_type_id=ty.user_type_id",session).Rows.ToArray();
        Assert.Contains(data,row=>Equals(row[0],"Heap")&&Equals(row[1],"xml")&&Equals(row[2],false));
        Assert.Contains(data,row=>Equals(row[0],"Heap")&&Equals(row[1],"geometry")&&Equals(row[2],false));
        var modules=engine.Execute("SELECT v.name, m.is_schema_bound, m.uses_native_compilation FROM sys.all_views v JOIN sys.all_sql_modules m ON v.object_id=m.object_id",session).Rows.ToArray();
        Assert.Equal(true,Assert.Single(modules,row=>Equals(row[0],"Bound"))[1]);
        Assert.Equal(false,Assert.Single(modules,row=>Equals(row[0],"Unbound"))[1]);
        Assert.Null(Assert.Single(modules,row=>Equals(row[0],"Unknown"))[1]);
        Assert.All(modules,row=>Assert.Equal(false,row[2]));
        foreach(var name in new[]{"extended_properties","system_sql_modules","data_spaces"}){
            Assert.True(SystemCatalogEmulator.TryCatalog(name,new MetadataAdapter(),[],out var result));Assert.Empty(result.Rows);Assert.NotEmpty(result.Columns);
        }
    }
    private sealed class MetadataAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata>GetTables()=>[
            new("dbo","Heap",[new("Document",typeof(string),"xml",true,false,0),new("Shape",typeof(byte[]),"geometry",true,false,1)]),
            new("dbo","Keyed",[new("Id",typeof(int),"int",false,true,0)],new("PK_Keyed",[new("Id",false)],2))];
        public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();
        public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();
        public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
