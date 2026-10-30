using System.Text;
using Seamless.Database.Query;
using BatchSplitter=Seamless.Database.Query.SqlScriptBatches;
using Xunit;

namespace Database.Query.Tests;

public sealed class SqlScriptBatches{
    [Fact]public void SplitSqlServer(){
        {
            const string sql="SELECT 1;\r\nGO -- comment\r\n\r\nSELECT N'漢😀';\nGO\nSELECT 3;";
            var batches=BatchSplitter.SplitSqlServer(sql);
            Assert.Equal(3,batches.Count);
            Assert.Equal("SELECT 1;\r\n",batches[0].Text);
            Assert.Equal(0,batches[0].StartOffset);
            Assert.Equal(1,batches[0].StartLine);
            Assert.Equal("\r\nSELECT N'漢😀';\n",batches[1].Text);
            Assert.Equal(sql.IndexOf("\r\nSELECT N",StringComparison.Ordinal),batches[1].StartOffset);
            Assert.Equal(3,batches[1].StartLine);
            Assert.Equal("SELECT 3;",batches[2].Text);
            Assert.Equal(sql.IndexOf("SELECT 3",StringComparison.Ordinal),batches[2].StartOffset);
            Assert.Equal(6,batches[2].StartLine);
            foreach(var batch in batches){Assert.Equal(batch.Text,sql.Substring(batch.StartOffset,batch.Text.Length));}
        }
        {
            const string sql="SELECT N'a\nGO\n''b', [a\nGO\n]]b], \"a\nGO\n\"\"b\"; -- GO\n/* outer\n/* GO */\nGO\n*/ SELECT 1;\nGO\nSELECT 2;";
            var batches=BatchSplitter.SplitSqlServer(sql);
            Assert.Equal(2,batches.Count);
            Assert.EndsWith("SELECT 2;",batches[1].Text);
            foreach(var batch in batches){Assert.Equal(batch.Text,sql.Substring(batch.StartOffset,batch.Text.Length));}
        }
        {
            Assert.Empty(BatchSplitter.SplitSqlServer("\r\nGO\r\n  \nGO --x\n"));
            Assert.Single(BatchSplitter.SplitSqlServer("SELECT 1; -- GO\n/* GO */"));
            Assert.Equal(2,BatchSplitter.SplitSqlServer("SELECT 1;\rGO\rSELECT 2;").Count);
        }
        {
            foreach(var sql in new[]{"GO 2","SELECT 1;\nGO 2 -- repeat\nSELECT 2;","SELECT 'unterminated","SELECT [unterminated","SELECT \"unterminated","/* outer /* inner */"}){
                Assert.Throws<SqlQueryException>(()=>BatchSplitter.SplitSqlServer(sql));
            }
            Assert.Throws<ArgumentNullException>(()=>BatchSplitter.SplitSqlServer(null!));
            Assert.Single(BatchSplitter.SplitSqlServer(new string('x',1024*1024)));
            Assert.Throws<SqlQueryException>(()=>BatchSplitter.SplitSqlServer(new string('x',1024*1024+1)));
            Assert.Throws<SqlQueryException>(()=>BatchSplitter.SplitSqlServer(new string('漢',1024*1024/3+1)));
        }
    }
}
