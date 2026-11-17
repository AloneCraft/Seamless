using F=Seamless.Database.SqlServer.Functions.SqlFunctionCatalog;
namespace Database.SqlServer.Functions.Tests;
public sealed class SqlFunctionCatalog{
    [Fact,Trait("Category","Unit")]public void Find(){
        {
            //サンプルDB(AdventureWorks・Northwind・pubs・WideWorldImporters)のビューと関数の定義に出てくる組み込み関数が、すべて登録されている。
            //CAST・CONVERT・COALESCE・ISNULL・COUNT・MIN・MAX は型の規則で決まるので翻訳器が扱い、ここには無い。
            string[] used=["REPLACE","GETDATE","MONTH","YEAR","DAY","DATEDIFF","DATEADD","RIGHT","LEFT","LTRIM","RTRIM","LEN","REPLICATE","DATALENGTH","DATENAME","SUBSTRING","IS_MEMBER","IS_ROLEMEMBER","CONCAT","USER_NAME","ORIGINAL_LOGIN","SESSION_CONTEXT","ROUND","DECOMPRESS","DATEPART","SUM","AVG"];
            var missing=used.Where(static name=>F.Find(name).Count==0).ToArray();
            Assert.True(missing.Length==0,"登録されていない関数: "+string.Join(",",missing));
        }
        {
            //名前は大文字小文字を区別しない
            Assert.NotEmpty(F.Find("datediff"));
        }
        {
            //無い名前は空
            Assert.Empty(F.Find("NO_SUCH_FUNCTION"));
        }
    }
}
