using System.IO;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tests;

public sealed class SqlBatchInterpreterTests{
    private static (SqlExecutionEngine Engine,SessionContext Session) Open(){
        var registry=new DatabaseRegistry();
        // AdventureWorks と同じく読み取り専用。一時表は tempdb 相当なので作れなければならない。
        registry.Register("fixture",new Adapter(),readOnly:true);
        return (new SqlExecutionEngine(registry),new SessionContext(1,"fixture"));
    }
    private static string Corpus(string name){
        using var stream=typeof(SqlBatchInterpreterTests).Assembly.GetManifestResourceStream("Seamless.Database.Gateway.Tests.SsmsCorpus."+name+".sql")??throw new InvalidDataException(name);
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }

    [Fact,Trait("Category","Unit")]
    public void VariablesAndControlFlow(){
        var (engine,session)=Open();
        using(session){
            var results=engine.ExecuteBatch("DECLARE @a int = 1; DECLARE @b nvarchar(10); SET @a += 2; IF @a = 3 SET @b = N'three' ELSE SET @b = N'other'; WHILE @a < 5 BEGIN SET @a = @a + 1; IF @a = 4 CONTINUE; END SELECT @a AS a, @b AS b",session);
            var row=Assert.Single(Assert.Single(results).Rows);
            Assert.Equal(5,row[0]);Assert.Equal("three",row[1]);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void AssignmentSelectTakesLastRowAndKeepsValueWhenEmpty(){
        var (engine,session)=Open();
        using(session){
            var empty=engine.ExecuteBatch("DECLARE @n sysname = N'none'; SELECT @n = name FROM sys.tables WHERE name = N'Missing'; SELECT @n",session);
            Assert.Equal("none",Assert.Single(Assert.Single(empty).Rows)[0]);
            var found=engine.ExecuteBatch("DECLARE @n sysname; SELECT @n = name FROM sys.tables; SELECT @n",session);
            Assert.Equal("Items",Assert.Single(Assert.Single(found).Rows)[0]);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void TryCatchExposesTheErrorAndThrowRethrows(){
        var (engine,session)=Open();
        using(session){
            var caught=engine.ExecuteBatch("BEGIN TRY THROW 50001, N'boom', 1 END TRY BEGIN CATCH SELECT ERROR_NUMBER() AS n, ERROR_MESSAGE() AS m END CATCH",session);
            var row=Assert.Single(Assert.Single(caught).Rows);
            Assert.Equal(50001,row[0]);Assert.Equal("boom",row[1]);
            Assert.Null(Assert.Single(Assert.Single(engine.ExecuteBatch("SELECT ERROR_NUMBER()",session)).Rows)[0]);
            // SSMS のサーバー情報バッチと同じ形: 想定内の番号以外は投げ直す。
            var rethrown=engine.ExecuteBatch("BEGIN TRY THROW 50002, N'again', 1 END TRY BEGIN CATCH IF(ERROR_NUMBER() NOT IN (297,300)) BEGIN THROW END END CATCH",session);
            var error=Assert.Throws<SqlQueryException>(()=>rethrown[^1].Columns);
            Assert.Equal(50002,error.Number);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void RegistryReadsLeaveOutputVariablesNull(){
        var (engine,session)=Open();
        using(session){
            var results=engine.ExecuteBatch("declare @v int = 7\nexec master.dbo.xp_instance_regread N'HKEY_LOCAL_MACHINE', N'SOFTWARE\\Microsoft\\MSSQLServer', N'AuditLevel', @v OUTPUT\nselect ISNULL(@v, -1)",session);
            Assert.Equal(-1,Assert.Single(Assert.Single(results).Rows)[0]);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void TableChangeTrackingBatchUsesTemporaryTablesOnReadOnlyDatabase(){
        // SSMS がテーブルのプロパティを読むときのバッチ。以前は「読み取り専用DB」で一時表の作成が拒否されていた。
        var (engine,session)=Open();
        using(session){
            var text=new SqlType("nvarchar",typeof(string),4000);
            var results=engine.ExecuteBatch(Corpus("TableChangeTracking"),session,[new("@_msparam_0",text),new("@_msparam_1",text),new("@_msparam_2",text)],new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase){["@_msparam_0"]="2",["@_msparam_1"]="Items",["@_msparam_2"]="dbo"});
            var select=Assert.Single(results,r=>r.Columns.Count>0);
            var row=Assert.Single(select.Rows);
            Assert.Equal("Items",row[0]);
            var names=select.Columns.Select(c=>c.Name).ToList();
            Assert.Equal(false,row[names.IndexOf("ChangeTrackingEnabled")]);
            // バッチの最後で消した一時表は、もう見えない。
            Assert.ThrowsAny<Exception>(()=>engine.ExecuteBatch("SELECT * FROM #file_tables",session)[0].Columns);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void PolicyHealthPartsWorkOnTheEmptyMsdb(){
        var (engine,session)=Open();
        using(session){
            Assert.Equal(0,Assert.Single(engine.Execute("SELECT msdb.dbo.fn_syspolicy_is_automation_enabled()",session).Rows)[0]);
            Assert.Empty(engine.Execute("SELECT * FROM msdb.dbo.syspolicy_system_health_state WHERE target_query_expression_with_id LIKE 'Server%'",session).Rows);
            Assert.Equal(0,Assert.Single(engine.Execute("SELECT CASE WHEN 1=msdb.dbo.fn_syspolicy_is_automation_enabled() AND EXISTS (SELECT * FROM msdb.dbo.syspolicy_system_health_state WHERE target_query_expression_with_id LIKE 'Server%') THEN 1 ELSE 0 END",session).Rows)[0]);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void ServerPropertiesBatchReturnsOneRow(){
        // SSMS がクエリ画面を開くとき(上位1000行の選択など)に送るサーバー情報のバッチ。
        var (engine,session)=Open();
        using(session){
            var results=engine.ExecuteBatch(Corpus("ServerProperties"),session);
            var select=Assert.Single(results,r=>r.Columns.Count>0);
            var names=select.Columns.Select(c=>c.Name).ToList();
            var row=Assert.Single(select.Rows);
            Assert.Equal(["AuditLevel","NumberOfLogFiles","LoginMode"],names.Take(3));Assert.Contains("ServiceName",names);Assert.Contains("ClusterQuorumState",names);
            Assert.Equal("17.0.1000.7",row[names.IndexOf("VersionString")]);
            Assert.Equal("MSSQLSERVER",row[names.IndexOf("ServiceName")]);
            Assert.Equal(-1,row[names.IndexOf("NumberOfLogFiles")]);
        }
    }

    private sealed class Adapter:IDatabaseAdapter{
        public string DatabaseName=>"fixture";
        public IReadOnlyList<TableMetadata>GetTables()=>[new TableMetadata("dbo","Items",[new("Id",typeof(int),"int",false,true,0),new("Title",typeof(string),"nvarchar",true,false,1,50)],new("PK_Items",[new("Id",false)],1)){CreateDate=new DateTime(2020,2,3,4,5,6)}];
        public TableMetadata? FindTable(string name)=>null;
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
        public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
    }
}
