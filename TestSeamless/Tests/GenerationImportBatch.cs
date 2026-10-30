using System.Reflection;
using Seamless.Database;
using Seamless.Database.Query;

namespace TestSeamless.Tests;

public sealed class GenerationImportBatch{
    private sealed class Fixture:RuntimeSqlDatabaseContainer{
        public Fixture():base(DatabaseContainerOptions.InMemory with{Transactions=DatabaseTransactionMode.SharedV2},"batch-test"){this.EnsureTransactionSchema();}
        public Table<int,int> Items=>this.Table<int,int>(static value=>value,"items");
        protected override void EnsureTransactionSchema(){_=this.Items;}
    }
    private sealed class Rows(int count,int? failAt=null):IDatabaseImportSource{
        public IEnumerable<T> ReadTable<T>(global::Seamless.Database.System.Keys.DatabaseObject key){
            for(var i=0;i<count;i++){if(i==failAt){throw new InvalidOperationException("source failure");}yield return (T)(object)i;}
        }
    }
    private static IDatabaseImportSource Batch(DatabaseContainer database,IDatabaseImportSource source){
        var type=typeof(GenerationImport).GetNestedType("BatchedImportSource",BindingFlags.NonPublic)!;
        return (IDatabaseImportSource)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[database,source],null)!;
    }
    [Fact]
    public void StreamingBatchesCommitAllRows(){
        using var db=new Fixture();foreach(var row in Batch(db,new Rows(2001)).ReadTable<int>(new("items"))){Assert.True(db.Items.Add(row));}
        Assert.Equal(2001,db.Items.Count);Assert.True(db.Items.ContainsKey(0));Assert.True(db.Items.ContainsKey(2000));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureRollsBackOnlyCurrentBatch(bool sourceFailure){
        using var db=new Fixture();var source=new Rows(2001,sourceFailure?1005:null);
        Assert.Throws<InvalidOperationException>(()=>{foreach(var row in Batch(db,source).ReadTable<int>(new("items"))){if(!sourceFailure&&row==1005){throw new InvalidOperationException("consumer failure");}Assert.True(db.Items.Add(row));}});
        Assert.Equal(1000,db.Items.Count);Assert.True(db.Items.ContainsKey(999));Assert.False(db.Items.ContainsKey(1000));
    }
}
