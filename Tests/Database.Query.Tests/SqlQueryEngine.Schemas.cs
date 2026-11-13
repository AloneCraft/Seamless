using Seamless.Database.Query;

namespace Database.Query.Tests;

public sealed partial class SqlQueryEngine{
    [Fact]public void UnqualifiedNamesBindToTheRootWithoutDboFallback(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new MixedSchemaCatalog(SqlSchemaResolution.RootOnly));
        foreach(var (name,value) in new[]{("Items",10),("dbo.Items",20),("dbo0.Items",30),("dbo1.Items",40)}){
            Assert.Equal(value,Assert.Single(engine.Prepare("SELECT Id FROM "+name,"MixedDb").Execute().Rows)[0]);
            Assert.Equal(value,Assert.Single(engine.PrepareBasic("SELECT Id FROM "+name,"MixedDb",SqlDialect.SqlServer).Execute().Rows)[0]);
        }
        Assert.Equal(208,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM OnlyDbo","MixedDb")).Number);
        Assert.Equal(208,Assert.Throws<SqlQueryException>(()=>engine.PrepareBasic("SELECT Id FROM OnlyDbo","MixedDb",SqlDialect.SqlServer)).Number);
        Assert.Equal(20,Assert.Single(engine.Prepare("SELECT Id FROM dbo.OnlyDbo","MixedDb").Execute().Rows)[0]);
    }
    // SQL Server と同じ探し方(既定): スキーマを書かなければ dbo を先に探し、無ければ根を探す。書いたスキーマはそのまま使う。
    [Fact]public void UnqualifiedNamesBindToDboThenRootLikeSqlServer(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new MixedSchemaCatalog(SqlSchemaResolution.SqlServer));
        foreach(var (name,value) in new[]{("Items",20),("dbo.Items",20),("dbo0.Items",30),("OnlyDbo",20),("OnlyRoot",10)}){
            Assert.Equal(value,Assert.Single(engine.Prepare("SELECT Id FROM "+name,"MixedDb").Execute().Rows)[0]);
            Assert.Equal(value,Assert.Single(engine.PrepareBasic("SELECT Id FROM "+name,"MixedDb",SqlDialect.SqlServer).Execute().Rows)[0]);
        }
        Assert.Equal(208,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM dbo.OnlyRoot","MixedDb")).Number);
        Assert.Equal(208,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM Missing","MixedDb")).Number);
    }
    private sealed class MixedSchemaCatalog(SqlSchemaResolution resolution):ISqlCatalog{
        public long Generation=>1;
        public SqlSchemaResolution SchemaResolution=>resolution;
        public SqlTable? FindTable(SqlObjectName name){
            if(name.Database!="MixedDb"||name.Name is not("Items" or "OnlyDbo" or "OnlyRoot")){return null;}
            if(name.Name=="OnlyDbo"&&name.Schema!="dbo"){return null;}
            if(name.Name=="OnlyRoot"&&name.Schema!=""){return null;}
            var value=name.Schema switch{""=>10,"dbo"=>20,"dbo0"=>30,"dbo1"=>40,_=>0};
            if(value==0){return null;}
            return new(name,[new("Id",SqlType.Int,false,name)],_=>new object?[][]{[value]});
        }
    }
}
