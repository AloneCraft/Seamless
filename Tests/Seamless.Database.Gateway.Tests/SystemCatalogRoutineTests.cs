using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Tests;

public sealed class SystemCatalogRoutineTests{
    [Fact,Trait("Category","Unit")]
    public void RegisteredRoutinesExposeSourceAndGeneratedMethodSeparately(){
        var catalog=new RoutineCatalog("sales","run",RoutineKind.StoredProcedure,null,[new(1,"@id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false)],[],"CREATE PROCEDURE sales.run AS SELECT 1",true,1,0){SourceObjectType="PC"};
        var binding=new GeneratedRoutineBinding("fixture","sales","run",RoutineKind.StoredProcedure,"fixture.Schemas.sales","run","fixture.cs","public void run() => throw new System.NotSupportedException();",true);
        var missing=new RoutineCatalog("only_routines","hidden",RoutineKind.ScalarFunction,new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),[],[],null,false,0,1){SourceObjectType="FN"};
        var tableFunction=new RoutineCatalog("sales","lookup",RoutineKind.TableFunction,null,[new(1,"@name",new(DatabaseTypeKind.String,false,40,SqlTypeName:"nvarchar"),false)],[new(0,"Id",new(DatabaseTypeKind.Int32,false,SqlTypeName:"int"),false),new(1,"Title",new(DatabaseTypeKind.String,true,40,SqlTypeName:"nvarchar"),false)],null,false,0,0){SourceObjectType="IF"};
        var registry=new DatabaseRegistry();registry.Register("fixture",new EmptyAdapter(),routines:[new("sales","run",catalog.Definition,catalog,binding),new("only_routines","hidden",null,missing),new("sales","lookup",null,tableFunction)]);
        var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"fixture");
        CheckMetadata(engine,session,"SELECT name,type,type_desc,is_ms_shipped FROM sys.objects",[("nvarchar",128,false),("char",2,true),("nvarchar",60,true),("bit",null,false)]);
        CheckMetadata(engine,session,"SELECT name FROM sys.schemas",[("nvarchar",128,false)]);
        CheckMetadata(engine,session,"SELECT name FROM sys.parameters",[("nvarchar",128,true)]);
        CheckMetadata(engine,session,"SELECT definition FROM sys.sql_modules",[("nvarchar",-1,true)]);

        var result=engine.Execute("SELECT o.name, m.definition, g.csharp_source, g.is_stub FROM sys.objects o JOIN sys.sql_modules m ON o.object_id=m.object_id JOIN seamless.generated_methods g ON o.object_id=g.object_id WHERE o.name='run'",session);
        var row=Assert.Single(result.Rows);Assert.Equal("run",row[0]);Assert.Equal(catalog.Definition,row[1]);Assert.Equal(binding.CSharpSource,row[2]);Assert.Equal(true,row[3]);
        var procedure=Assert.Single(engine.Execute("SELECT name, type, object_id FROM sys.procedures WHERE name='run'",session).Rows);Assert.Equal("PC",procedure[1]);
        var parameter=Assert.Single(engine.Execute("SELECT name, parameter_id FROM sys.parameters WHERE object_id=OBJECT_ID('sales.run')",session).Rows);Assert.Equal("@id",parameter[0]);Assert.Equal(1,parameter[1]);
        var empty=Assert.Single(engine.Execute("SELECT m.definition, g.csharp_source, g.is_stub FROM sys.sql_modules m JOIN seamless.generated_methods g ON m.object_id=g.object_id WHERE g.source_name='hidden'",session).Rows);Assert.Null(empty[0]);Assert.Null(empty[1]);Assert.Null(empty[2]);
        var function=Assert.Single(engine.Execute("SELECT object_id, type FROM sys.objects WHERE name='lookup'",session).Rows);Assert.Equal("IF",function[1]);
        var resultColumns=engine.Execute("SELECT name, column_id, system_type_id, max_length, is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('sales.lookup') ORDER BY column_id",session).Rows.ToArray();
        Assert.Equal(2,resultColumns.Length);Assert.Equal("Id",resultColumns[0][0]);Assert.Equal(1,resultColumns[0][1]);Assert.Equal((byte)56,resultColumns[0][2]);Assert.Equal(false,resultColumns[0][4]);Assert.Equal("Title",resultColumns[1][0]);Assert.Equal((short)80,resultColumns[1][3]);Assert.Equal(true,resultColumns[1][4]);
        var argument=Assert.Single(engine.Execute("SELECT object_id, name FROM sys.parameters WHERE object_id=OBJECT_ID('sales.lookup')",session).Rows);Assert.Equal(function[0],argument[0]);Assert.Equal("@name",argument[1]);
        var definition=Assert.Single(engine.Execute("SELECT definition FROM sys.sql_modules WHERE object_id=OBJECT_ID('sales.lookup')",session).Rows);Assert.Null(definition[0]);
        Assert.Equal("only_routines",Assert.Single(engine.Execute("SELECT name FROM sys.schemas WHERE name='only_routines'",session).Rows)[0]);
        Assert.Throws<SqlQueryException>(()=>registry.FindRoutine(new("fixture","only_routines","hidden")));
    }
    private static void CheckMetadata(SqlExecutionEngine engine,SessionContext session,string sql,(string Type,int? Length,bool Nullable)[] expected){
        var columns=engine.Execute(sql,session).Columns;Assert.Equal(expected.Length,columns.Count);
        for(var i=0;i<expected.Length;i++){Assert.Equal(expected[i].Type,columns[i].SqlTypeName);Assert.Equal(expected[i].Length,columns[i].MaximumLength);Assert.Equal(expected[i].Nullable,columns[i].IsNullable);}
    }
    private sealed class EmptyAdapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata>GetTables()=>[];public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
