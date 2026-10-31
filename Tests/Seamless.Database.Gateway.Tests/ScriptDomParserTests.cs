using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;

namespace Seamless.Database.Gateway.Tests;

public sealed class ScriptDomParserTests{
    private sealed class MockAdapter:IDatabaseAdapter{
        public string DatabaseName=>"MockDb";
        public IReadOnlyList<TableMetadata>GetTables()=>new[]{
            new TableMetadata("dbo","商品",new[]{
                new ColumnMetadata("商品ID",typeof(int),"int",false,true,0),
                new ColumnMetadata("名前",typeof(string),"nvarchar",false,false,1),
                new ColumnMetadata("単価",typeof(int),"int",false,false,2),
                new ColumnMetadata("在庫数",typeof(int),"int",false,false,3)
            })
        };
        public TableMetadata? FindTable(string name)=>this.GetTables()[0];
        public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columnNames,Func<object,bool>? predicate,SessionContext session)=>QueryResult.Empty;
        public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>1;
        public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>1;
        public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>1;
        public void BeginTransaction(SessionContext session){}
        public void CommitTransaction(SessionContext session){}
        public void RollbackTransaction(SessionContext session){}
    }

    [Fact,Trait("Category","Unit")]
    public void Parse(){
        var adapter=new MockAdapter();

        {
            // SELECT文のパース
            var cmd=ScriptDomParser.Parse("SELECT * FROM 商品 WHERE 在庫数 > 10;",adapter);
            Assert.Equal(SqlCommandKind.Select,cmd.Kind);
            Assert.Equal("dbo.商品",cmd.TableName);
            Assert.NotNull(cmd.Columns);
            Assert.Contains("*",cmd.Columns);
            Assert.NotNull(cmd.Predicate);
        }

        {
            // INSERT文のパース
            var cmd=ScriptDomParser.Parse("INSERT INTO 商品 (商品ID, 名前, 単価, 在庫数) VALUES (100, 'リンゴ', 150, 50);",adapter);
            Assert.Equal(SqlCommandKind.Insert,cmd.Kind);
            Assert.Equal("dbo.商品",cmd.TableName);
            Assert.NotNull(cmd.Values);
            Assert.Equal(100,cmd.Values["商品ID"]);
            Assert.Equal("リンゴ",cmd.Values["名前"]);
        }

        {
            // UPDATE文のパース
            var cmd=ScriptDomParser.Parse("UPDATE 商品 SET 在庫数 = 20 WHERE 商品ID = 100;",adapter);
            Assert.Equal(SqlCommandKind.Update,cmd.Kind);
            Assert.Equal("dbo.商品",cmd.TableName);
            Assert.NotEmpty(cmd.UpdateValues);
            Assert.Equal(20,cmd.UpdateValues["在庫数"](new object()));
            Assert.NotNull(cmd.Predicate);
        }

        {
            // DELETE文のパース
            var cmd=ScriptDomParser.Parse("DELETE FROM 商品 WHERE 商品ID = 100;",adapter);
            Assert.Equal(SqlCommandKind.Delete,cmd.Kind);
            Assert.Equal("dbo.商品",cmd.TableName);
            Assert.NotNull(cmd.Predicate);
        }

        {
            // 未対応の条件を無条件削除にしてはならない。
            Assert.Throws<Seamless.Database.Query.SqlQueryException>(()=>ScriptDomParser.Parse("DELETE FROM 商品 WHERE 名前 LIKE N'a%';",adapter));
            Assert.Throws<Seamless.Database.Query.SqlQueryException>(()=>ScriptDomParser.Parse("DELETE FROM 商品 WHERE 不存在 = 1;",adapter));
            var cmd=ScriptDomParser.Parse("DELETE FROM 商品 WHERE 名前 = NULL;",adapter);
            Assert.False(cmd.Predicate!(new {名前=(string?)null}));
        }

        {
            // トランザクション文のパース
            var b=ScriptDomParser.Parse("BEGIN TRANSACTION;",adapter);
            Assert.Equal(SqlCommandKind.BeginTransaction,b.Kind);

            var c=ScriptDomParser.Parse("COMMIT;",adapter);
            Assert.Equal(SqlCommandKind.CommitTransaction,c.Kind);

            var r=ScriptDomParser.Parse("ROLLBACK TRANSACTION;",adapter);
            Assert.Equal(SqlCommandKind.RollbackTransaction,r.Kind);
        }
    }
}
