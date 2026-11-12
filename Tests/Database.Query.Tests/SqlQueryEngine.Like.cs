using Seamless.Database.Query;
namespace Database.Query.Tests;
public sealed partial class SqlQueryEngine{
    [Theory]
    [InlineData("N'Server[@Name=x]'","N'Server%'",1)]
    [InlineData("N'abc'","N'a_c'",1)]
    [InlineData("N'abc'","N'a_'",0)]
    // 照合順序が無い文字は = と同じく大文字小文字を区別する(照合順序が付いた列では _CI_ に従う)。
    [InlineData("N'ABC'","N'abc'",0)]
    [InlineData("N'a1'","N'a[0-9]'",1)]
    [InlineData("N'ab'","N'a[0-9]'",0)]
    [InlineData("N'ab'","N'a[^0-9]'",1)]
    [InlineData("N'a%b'","N'a!%b' ESCAPE '!'",1)]
    [InlineData("N'axb'","N'a!%b' ESCAPE '!'",0)]
    [InlineData("N''","N'%'",1)]
    [InlineData("N'abc'","N'%b%'",1)]
    [InlineData("N'abc'","N'%d%'",0)]
    [InlineData("N'abc  '","N'abc'",1)]
    public void Like(string value,string pattern,int expected){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new Catalog());
        Assert.Equal(expected,Assert.Single(engine.Prepare($"SELECT CASE WHEN {value} LIKE {pattern} THEN 1 ELSE 0 END","Original DB").Execute().Rows)[0]);
    }
    [Fact]public void LikeWithNullIsUnknownAndNotLikeNegates(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new Catalog());
        Assert.Equal(0,Assert.Single(engine.Prepare("SELECT CASE WHEN NULL LIKE N'a%' THEN 1 ELSE 0 END","Original DB").Execute().Rows)[0]);
        Assert.Equal(0,Assert.Single(engine.Prepare("SELECT CASE WHEN N'abc' NOT LIKE NULL THEN 1 ELSE 0 END","Original DB").Execute().Rows)[0]);
        Assert.Equal(1,Assert.Single(engine.Prepare("SELECT CASE WHEN N'abc' NOT LIKE N'x%' THEN 1 ELSE 0 END","Original DB").Execute().Rows)[0]);
    }
}
