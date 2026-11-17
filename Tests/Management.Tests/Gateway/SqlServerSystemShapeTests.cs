using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.SqlServer;

namespace Seamless.Database.Gateway.Tests;

public sealed class SqlServerSystemShapeTests{
    private static (SqlExecutionEngine Engine,SessionContext Session) Open(){
        var registry=new DatabaseRegistry();
        registry.Register("fixture",new ShapeAdapter(),views:[new("dbo","V1","CREATE VIEW dbo.V1 AS SELECT 1 AS n",[])]);
        return (new SqlExecutionEngine(registry),new SessionContext(1,"fixture"));
    }

    [Fact,Trait("Category","Unit")]
    public void AllColumnsAnswersTheColumnFiltersSsmsSends(){
        // SSMS 22 の「上位1000行の選択」が送った問い合わせ。graph_type などの列が無くて落ちていた。
        var (engine,session)=Open();
        using(session){
            var rows=engine.Execute("SELECT clmns.name AS [Name] FROM sys.tables AS tbl INNER JOIN sys.all_columns AS clmns ON clmns.object_id=tbl.object_id WHERE (CAST(clmns.is_sparse AS bit)=0 and clmns.generated_always_type=0 and (ISNULL(clmns.graph_type, 0)=0 or ISNULL(clmns.graph_type, 0)=1) and CAST(clmns.is_dropped_ledger_column AS bit)=0)and((tbl.name='Items' and SCHEMA_NAME(tbl.schema_id)='dbo')) ORDER BY clmns.column_id ASC",session).Rows.ToArray();
            Assert.Equal(["Id","Title"],rows.Select(static r=>(string)r[0]!).ToArray());
        }
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("all_columns")]
    [InlineData("all_objects")]
    [InlineData("all_views")]
    [InlineData("tables")]
    [InlineData("types")]
    [InlineData("indexes")]
    public void ExposesEveryColumnOfTheRealViewInOrder(string view){
        var (engine,session)=Open();
        using(session){
            var result=engine.Execute("SELECT * FROM sys."+view,session);
            var expected=SqlServerSystemCatalog.Default.Resolve("fixture","sys",view)!.Columns.Select(static c=>c.Name).ToArray();
            Assert.Equal(expected,result.Columns.Select(static c=>c.Name).ToArray());
            Assert.NotEmpty(result.Rows);
        }
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("fixture","sys","assembly_modules")]
    [InlineData("fixture","sys","all_parameters")]
    [InlineData("master","sys","master_files")]
    [InlineData("msdb","dbo","syspolicy_configuration")]
    [InlineData("master","sys","objects")]
    public void ViewsSeamlessDoesNotFillAreEmptyButHaveTheRealShape(string database,string schema,string name){
        var (engine,session)=Open();
        using(session){
            var result=engine.Execute($"SELECT * FROM {database}.{schema}.{name}",session);
            var expected=SqlServerSystemCatalog.Default.Resolve(database,schema,name)!.Columns.Select(static c=>c.Name).ToArray();
            Assert.Equal(expected,result.Columns.Select(static c=>c.Name).ToArray());
            Assert.Empty(result.Rows);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void ModifyDateStartsFromCreateDateAndNotNullColumnsGetDefaults(){
        var (engine,session)=Open();
        using(session){
            var tables=engine.Execute("SELECT name, create_date, modify_date, lob_data_space_id, is_replicated FROM sys.tables",session).Rows.ToArray();
            var row=Assert.Single(tables);
            Assert.Equal("Items",row[0]);
            Assert.Equal(row[1],row[2]);
            Assert.Equal(0,row[3]);
            Assert.Equal(false,row[4]);
            var views=engine.Execute("SELECT name, modify_date FROM sys.all_views WHERE name='V1'",session).Rows.ToArray();
            Assert.Equal(new DateTime(1900,1,1),Assert.Single(views)[1]);
        }
    }

    private sealed class ShapeAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata>GetTables()=>[new TableMetadata("dbo","Items",[new("Id",typeof(int),"int",false,true,0),new("Title",typeof(string),"nvarchar",true,false,1,50)]){CreateDate=new DateTime(2020,2,3,4,5,6)}];
        public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
