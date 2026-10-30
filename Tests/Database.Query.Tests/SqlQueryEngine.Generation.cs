using Seamless.Database.Query;

namespace Database.Query.Tests;

public sealed partial class SqlQueryEngine{
    [Fact]public void RetainedPlansRejectChangedCatalogGeneration(){
        var catalog=new MutableGenerationCatalog();var engine=new Seamless.Database.Query.SqlQueryEngine(catalog);
        var plan=engine.Prepare("SELECT Id FROM dbo.Items","fixture");
        Assert.Equal(new object?[]{1},plan.Execute().Rows.First());
        var delayed=plan.Execute();
        catalog.Revision++;
        Assert.Equal(3961,Assert.Throws<SqlQueryException>(()=>plan.Execute()).Number);
        Assert.Equal(3961,Assert.Throws<SqlQueryException>(()=>delayed.Rows.First()).Number);
        var current=engine.Prepare("SELECT Id FROM dbo.Items","fixture");Assert.NotSame(plan,current);
        using var iterator=current.Execute().Rows.GetEnumerator();Assert.True(iterator.MoveNext());Assert.Equal(new object?[]{1},iterator.Current);
        catalog.Revision++;
        Assert.Equal(3961,Assert.Throws<SqlQueryException>(()=>iterator.MoveNext()).Number);
        Assert.Equal(new object?[]{1,2},engine.Prepare("SELECT Id FROM dbo.Items","fixture").Execute().Rows.Select(row=>row[0]).ToArray());
    }
    [Fact]public void PlansTrackForeignViewDependenciesAndBypassUncommittedCache(){
        var catalog=new ForeignGenerationCatalog();var engine=new Seamless.Database.Query.SqlQueryEngine(catalog);
        const string sql="SELECT Id FROM dbo.V";var first=engine.Prepare(sql,"front");Assert.Equal(new object?[]{1},Assert.Single(first.Execute().Rows));
        catalog.BackRevision=2;catalog.Value=2;
        Assert.Equal(3961,Assert.Throws<SqlQueryException>(()=>first.Execute()).Number);
        var second=engine.Prepare(sql,"front");Assert.NotSame(first,second);Assert.Equal(new object?[]{2},Assert.Single(second.Execute().Rows));
        catalog.AllowCache=false;catalog.BackRevision=3;catalog.Value=3;var uncommitted=engine.Prepare(sql,"front");Assert.Equal(new object?[]{3},Assert.Single(uncommitted.Execute().Rows));
        catalog.BackRevision=2;catalog.Value=2;catalog.AllowCache=true;
        Assert.Same(second,engine.Prepare(sql,"front"));Assert.Equal(3961,Assert.Throws<SqlQueryException>(()=>uncommitted.Execute()).Number);
    }
    private sealed class ForeignGenerationCatalog:ISqlCatalog{
        internal long BackRevision=1;internal int Value=1;internal bool AllowCache=true;
        public long Generation=>0;
        public long GetGeneration(string database){if(database=="back"){return this.BackRevision;}return 1;}
        public bool CanCachePlans=>this.AllowCache;
        public SqlTable?FindTable(SqlObjectName name){
            if(name==new SqlObjectName("front","dbo","V")){return new(name,[new("Id",SqlType.Int,false,name)],ViewDefinition:"SELECT Id FROM [back].dbo.Items");}
            if(name==new SqlObjectName("back","dbo","Items")){return new(name,[new("Id",SqlType.Int,false,name)],_=>new object?[][]{[this.Value]});}
            return null;
        }
    }
    private sealed class MutableGenerationCatalog:ISqlCatalog{
        internal long Revision=1;
        public long Generation=>this.Revision;
        public SqlTable?FindTable(SqlObjectName name){
            if(name!=new SqlObjectName("fixture","dbo","Items")){return null;}
            return new(name,[new("Id",SqlType.Int,false,name)],_=>Rows());
        }
        private static IEnumerable<object?[]>Rows(){yield return [1];yield return [2];}
    }
}
