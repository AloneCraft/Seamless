using Seamless.Database.Query;

namespace Database.Query.Tests;

public sealed partial class SqlQueryEngine{
    [Fact]public void UnqualifiedNamesBindToTheRootWithoutDboFallback(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new MixedSchemaCatalog());
        foreach(var (name,value) in new[]{("Items",10),("dbo.Items",20),("dbo0.Items",30),("dbo1.Items",40)}){
            Assert.Equal(value,Assert.Single(engine.Prepare("SELECT Id FROM "+name,"MixedDb").Execute().Rows)[0]);
            Assert.Equal(value,Assert.Single(engine.PrepareBasic("SELECT Id FROM "+name,"MixedDb",SqlDialect.SqlServer).Execute().Rows)[0]);
        }
        Assert.Equal(208,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT Id FROM OnlyDbo","MixedDb")).Number);
        Assert.Equal(208,Assert.Throws<SqlQueryException>(()=>engine.PrepareBasic("SELECT Id FROM OnlyDbo","MixedDb",SqlDialect.SqlServer)).Number);
        Assert.Equal(20,Assert.Single(engine.Prepare("SELECT Id FROM dbo.OnlyDbo","MixedDb").Execute().Rows)[0]);
    }
    private sealed class MixedSchemaCatalog:ISqlCatalog{
        public long Generation=>1;
        public SqlTable? FindTable(SqlObjectName name){
            if(name.Database!="MixedDb"||name.Name is not("Items" or "OnlyDbo")){return null;}
            if(name.Name=="OnlyDbo"&&name.Schema!="dbo"){return null;}
            var value=name.Schema switch{""=>10,"dbo"=>20,"dbo0"=>30,"dbo1"=>40,_=>0};
            if(value==0){return null;}
            return new(name,[new("Id",SqlType.Int,false,name)],_=>new object?[][]{[value]});
        }
    }
}
