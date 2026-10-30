using Sample.NetShop.Core;
using Sample.NetShop.Core.Shop;
using Seamless.Database.Rdbms;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tests;

public sealed class SystemCatalogTests{
    [Fact,Trait("Category","Unit")]
    public void CatalogRowsReflectRelationsAndColumnTypes(){
        var adapter=new MetadataAdapter();
        var views=new[]{new DatabaseViewDefinition("report","Monthly","SELECT 1",[new SqlColumn("Amount",new SqlType("decimal",typeof(decimal),null,12,3),true,null,0)])};
        QueryResult Read(string name){Assert.True(SystemCatalogEmulator.TryCatalog(name,adapter,views,out var result));return result;}
        var schemas=Read("schemas").Rows.ToArray();
        var reportId=(int)Assert.Single(schemas,r=>Equals(r[0],"report"))[1]!;
        var salesId=(int)Assert.Single(schemas,r=>Equals(r[0],"sales"))[1]!;
        Assert.NotEqual(reportId,salesId);
        var table=Assert.Single(Read("tables").Rows,r=>Equals(r[0],"Orders"));
        var view=Assert.Single(Read("views").Rows);
        Assert.Equal(salesId,table[2]);Assert.Equal(reportId,view[2]);Assert.NotEqual(table[1],view[1]);
        var columns=Read("columns").Rows.ToArray();
        var title=Assert.Single(columns,r=>Equals(r[1],"Title"));
        Assert.Equal(table[1],title[0]);Assert.Equal((byte)231,title[3]);Assert.Equal(true,title[7]);Assert.Equal("Japanese_CI_AS",title[9]);Assert.Equal((short)-1,title[4]);Assert.Equal((byte)37,title[5]);Assert.Equal((byte)6,title[6]);
        var amount=Assert.Single(columns,r=>Equals(r[1],"Amount"));
        Assert.Equal(view[1],amount[0]);Assert.Equal((byte)106,amount[3]);Assert.Equal((short)9,amount[4]);Assert.Equal((byte)12,amount[5]);Assert.Equal((byte)3,amount[6]);
        var indexes=Read("indexes").Rows.ToArray();Assert.Single(indexes);Assert.Equal(table[1],indexes[0][0]);Assert.Equal("PK_source",indexes[0][1]);Assert.Equal((byte)2,indexes[0][3]);Assert.Equal("NONCLUSTERED",indexes[0][4]);
        var keys=Read("index_columns").Rows.ToArray();Assert.Equal(2,keys.Length);Assert.Equal(2,keys[0][3]);Assert.Equal(true,keys[0][5]);Assert.Equal(1,keys[1][3]);
        var types=Read("types").Rows.ToArray();Assert.Contains(types,r=>Equals(r[0],"decimal"));Assert.Contains(types,r=>Equals(r[0],"uniqueidentifier"));
        Assert.DoesNotContain(columns,r=>Equals(r[1],"SyntheticRowId"));
    }
    [Fact,Trait("Category","Unit")]
    public void CatalogSelectSupportsProjectionFilterAndJoin(){
        var registry=new DatabaseRegistry();
        registry.Register("fixture",new MetadataAdapter(),views:[new("report","Monthly","SELECT 1",[new SqlColumn("Amount",new SqlType("decimal",typeof(decimal),null,12,3),true,null,0)])]);
        var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"fixture");
        var result=engine.Execute("SELECT s.name, t.name FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'Orders'",session);
        var row=Assert.Single(result.Rows);Assert.Equal("sales",row[0]);Assert.Equal("Orders",row[1]);
        var views=engine.Execute("SELECT name FROM sys.views WHERE name = 'Monthly'",session);
        Assert.Equal("Monthly",Assert.Single(views.Rows)[0]);
        var aliases=engine.Execute("SELECT v.name FROM sys.all_views v JOIN sys.all_objects o ON v.object_id=o.object_id WHERE o.is_ms_shipped=0 AND v.name='Monthly'",session);
        Assert.Single(aliases.Columns);Assert.Equal("Monthly",Assert.Single(aliases.Rows)[0]);
        var excluded=engine.Execute("SELECT name FROM sys.all_objects WHERE is_ms_shipped=1",session);Assert.Empty(excluded.Rows);
        var columns=engine.Execute("SELECT name FROM sys.all_columns WHERE object_id=OBJECT_ID('sales.Orders') AND name='Title'",session);Assert.Single(columns.Columns);Assert.Equal("Title",Assert.Single(columns.Rows)[0]);
        var objects=engine.Execute("SELECT name FROM sys.all_objects WHERE type='U' AND is_ms_shipped=0 AND name='Orders'",session);Assert.Equal("Orders",Assert.Single(objects.Rows)[0]);
        var functions=engine.Execute("SELECT SCHEMA_NAME(t.schema_id), SCHEMA_ID('sales'), OBJECT_NAME(t.object_id), OBJECT_ID('sales.Orders') FROM sys.tables t WHERE t.name = 'Orders'",session);
        var mapped=Assert.Single(functions.Rows);Assert.Equal("sales",mapped[0]);Assert.Equal("Orders",mapped[2]);Assert.Equal(mapped[1],ReadSchemaId(registry,"sales"));Assert.Equal(mapped[3],ReadObjectId(registry,"Orders"));
        static object? ReadSchemaId(DatabaseRegistry registry,string name)=>registry.FindTable(new("fixture","sys","schemas"))!.Rows!(default).Single(r=>Equals(r[0],name))[1];
        static object? ReadObjectId(DatabaseRegistry registry,string name)=>registry.FindTable(new("fixture","sys","tables"))!.Rows!(default).Single(r=>Equals(r[0],name))[1];
    }
    [Fact,Trait("Category","Unit")]
    public void SyntheticKeyIsAbsentFromCatalog(){
        var path=Path.Combine(Path.GetTempPath(),"SeamlessCatalog_"+Guid.NewGuid().ToString("N"));
        using var db=商店DB.開く(path,資源設定.標準);
        var bindings=new[]{new GeneratedTableBinding("商品","sales","Orders",[new("InternalRowId","商品ID",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),true),new("Title","名前",new(DatabaseTypeKind.String,true,40,SqlTypeName:"nvarchar"))])};
        var adapter=new InProcessDatabaseAdapter(db,bindings);
        Assert.True(SystemCatalogEmulator.TryCatalog("tables",adapter,[],out var tables));
        var table=Assert.Single(tables.Rows,r=>Equals(r[0],"Orders"));
        Assert.True(SystemCatalogEmulator.TryCatalog("columns",adapter,[],out var columns));
        var visible=columns.Rows.Where(r=>Equals(r[0],table[1])).ToArray();Assert.Single(visible);Assert.Equal("Title",visible[0][1]);Assert.Equal(true,visible[0][7]);
        Assert.True(SystemCatalogEmulator.TryCatalog("indexes",adapter,[],out var indexes));Assert.DoesNotContain(indexes.Rows,r=>Equals(r[0],table[1]));
        Assert.True(SystemCatalogEmulator.TryCatalog("index_columns",adapter,[],out var keys));Assert.DoesNotContain(keys.Rows,r=>Equals(r[0],table[1]));
        var realBindings=new[]{new GeneratedTableBinding("商品","sales","Orders",[new("Id","商品ID",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int")),new("Title","名前",new(DatabaseTypeKind.String,true,40,SqlTypeName:"nvarchar"))])};
        var source=new DatabaseCatalog("fixture",[new SchemaCatalog("sales",[new TableCatalog("Orders",[],new PrimaryKeyCatalog("PK_original",[new("Title",true),new("Id",false)],2))])]);
        var realAdapter=new InProcessDatabaseAdapter(db,realBindings,source);
        Assert.True(SystemCatalogEmulator.TryCatalog("indexes",realAdapter,[],out var realIndexes));var realIndex=Assert.Single(realIndexes.Rows,r=>Equals(r[1],"PK_original"));Assert.Equal((byte)2,realIndex[3]);
        Assert.True(SystemCatalogEmulator.TryCatalog("index_columns",realAdapter,[],out var realKeys));var ordered=realKeys.Rows.Where(r=>Equals(r[0],realIndex[0])).ToArray();Assert.Equal(2,ordered.Length);Assert.Equal(2,ordered[0][3]);Assert.Equal(true,ordered[0][5]);Assert.Equal(1,ordered[1][3]);
    }
    private sealed class MetadataAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata>GetTables()=>[new("sales","Orders",[new("Id",typeof(int),"int",false,true,0),new("Title",typeof(string),"nvarchar",true,true,1,40,collation:"Japanese_CI_AS",sourceMaximumLength:-1,sourcePrecision:37,sourceScale:6)],new("PK_source",[new("Title",true),new("Id",false)],2)),new("sales","Log",[new("Message",typeof(string),"varchar",true,false,0,-1)])];
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
