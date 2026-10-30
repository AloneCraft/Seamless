using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Tests;

public sealed class SsmsCatalogScalarFunctionTests{
    [Fact,Trait("Category","Unit")]
    public void QuoteName(){
        var registry=new DatabaseRegistry();registry.Register("fixture",new MetadataAdapter());
        var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"fixture");
        object? Value(string sql)=>Assert.Single(engine.Execute("SELECT "+sql,session).Rows)[0];
        Assert.Equal("[a]]b]",Value("QUOTENAME(N'a]b')"));
        Assert.Equal("'a''b'",Value("QUOTENAME(N'a''b',N'''')"));
        Assert.Equal("(a))b)",Value("QUOTENAME(N'a)b',N')')"));
        Assert.Equal("{a}}b}",Value("QUOTENAME(N'a}b',N'{')"));
        Assert.Equal("<a>>b>",Value("QUOTENAME(N'a>b',N'>')"));
        Assert.Equal("\"a\"\"b\"",Value("QUOTENAME(N'a\"b',N'\"')"));
        Assert.Equal("`a``b`",Value("QUOTENAME(N'a`b',N'`')"));
        Assert.Equal("["+new string('x',128)+"]",Value("QUOTENAME(N'"+new string('x',128)+"')"));
        Assert.Null(Value("QUOTENAME(N'"+new string('x',129)+"')"));
        Assert.Null(Value("QUOTENAME(NULL)"));Assert.Null(Value("QUOTENAME(N'a',NULL)"));
        Assert.Null(Value("QUOTENAME(N'a',N'xx')"));Assert.Null(Value("QUOTENAME(N'a',N'x')"));
        var result=engine.Execute("SELECT QUOTENAME(N'a')",session);Assert.Equal("nvarchar",result.Columns[0].SqlTypeName);Assert.Equal(258,result.Columns[0].MaximumLength);
    }
    [Fact,Trait("Category","Unit")]
    public void ObjectPropertyEx(){
        var views=new[]{new DatabaseViewDefinition("dbo","Bound","CREATE VIEW dbo.Bound WITH SCHEMABINDING AS SELECT 1 AS value",[]),new DatabaseViewDefinition("dbo","Ordinary","CREATE VIEW dbo.Ordinary AS SELECT 1 AS value",[])};
        var registry=new DatabaseRegistry();registry.Register("fixture",new MetadataAdapter(),views:views);
        var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"fixture");
        object? Value(string sql)=>Assert.Single(engine.Execute("SELECT "+sql,session).Rows)[0];
        Assert.Equal(1,Value("OBJECTPROPERTYEX(OBJECT_ID(N'dbo.Bound'),N'IsSchemaBound')"));
        Assert.Equal(0,Value("OBJECTPROPERTYEX(OBJECT_ID(N'dbo.Ordinary'),N'isschemabound')"));
        Assert.Equal(0,Value("OBJECTPROPERTYEX(OBJECT_ID(N'dbo.Orders'),N'IsSchemaBound')"));
        Assert.Null(Value("OBJECTPROPERTYEX(-1,N'IsSchemaBound')"));
        Assert.Null(Value("OBJECTPROPERTYEX(NULL,N'IsSchemaBound')"));
        Assert.Null(Value("OBJECTPROPERTYEX(OBJECT_ID(N'dbo.Bound'),NULL)"));
        var failure=Assert.Throws<SqlQueryException>(()=>Value("OBJECTPROPERTYEX(OBJECT_ID(N'dbo.Bound'),N'Unknown')"));Assert.Equal(40515,failure.Number);
        var result=engine.Execute("SELECT OBJECTPROPERTYEX(OBJECT_ID(N'dbo.Bound'),N'IsSchemaBound')",session);Assert.Equal("int",result.Columns[0].SqlTypeName);
    }
    private sealed class MetadataAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata>GetTables()=>[new("dbo","Orders",[])];
        public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>?columns,Func<object,bool>?predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>?predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>?predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();
        public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();
        public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
