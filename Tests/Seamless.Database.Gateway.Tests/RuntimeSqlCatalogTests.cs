using Seamless.Database;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tests;

internal sealed class RuntimeSqlCatalogFixture(DatabaseContainerOptions options,string identity="test-schema-v1"):RuntimeSqlDatabaseContainer(options,identity){
    protected override void EnsureTransactionSchema(){}
}
internal sealed partial class RuntimeSqlGeneratedRoot:RuntimeSqlDatabaseContainer{
    public RuntimeSqlGeneratedRoot(DatabaseContainerOptions options):base(options,"generated-root-v1"){this.EnsureTablesCreated();}
    public Table<RuntimeTypedKey,RuntimeTypedRecord> TypedOrders=>this.Table<RuntimeTypedKey,RuntimeTypedRecord>(static value=>new(value.ObjectId),"TypedOrders");
}


public sealed class RuntimeSqlCatalogTests{
    [Fact,Trait("Category","Integration")]
    public void RuntimeScopeSharesGeneratedTypedTransactionAndRecovery(){
        var options=Options();using(var first=new RuntimeSqlGeneratedRoot(options)){}using(var db=new RuntimeSqlGeneratedRoot(options)){
            var api=db.RuntimeSql;using(var tx=api.BeginTransaction()){Assert.True(db.TypedOrders.Add(new(11,"typed",1)));api.CreateTable("dbo","t",Columns());api.InsertRow("dbo","t",[7,"seven",12.34m]);tx.Commit();Assert.Throws<InvalidOperationException>(()=>api.DropTable("dbo","t"));}
            using(var tx=api.BeginTransaction()){db.TypedOrders[new(11)]=new(11,"rollback",2);api.DropTable("dbo","t");tx.Rollback();}
            using var read=db.BeginReadSession();Assert.Equal("typed",db.TypedOrders[new(11)].Name);Assert.Single(api.ReadRows("dbo","t",api.SchemaRevision));
        }
        using(var reopened=new RuntimeSqlGeneratedRoot(options)){using var read=reopened.BeginReadSession();Assert.Equal("typed",reopened.TypedOrders[new(11)].Name);Assert.Single(reopened.RuntimeSql.ReadRows("dbo","t",reopened.RuntimeSql.SchemaRevision));}
    }
    [Fact,Trait("Category","Integration")]
    public async Task SchemaAndRowsBecomeVisibleTogetherOnCommit(){
        using var db=new RuntimeSqlCatalogFixture(Options());var api=db.RuntimeSql;var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);Task<(long Revision,int Count)> reader;
        using(var tx=api.BeginTransaction()){
            api.CreateTable("dbo","t",Columns());api.InsertRow("dbo","t",[1,"a",1m]);
            using(ExecutionContext.SuppressFlow()){reader=Task.Run(()=>{started.SetResult();var revision=api.SchemaRevision;return(revision,api.ReadRows("dbo","t",revision).Count());});}
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.NotSame(reader,await Task.WhenAny(reader,Task.Delay(200)));tx.Commit();
        }
        var observed=await reader.WaitAsync(TimeSpan.FromSeconds(10));Assert.Equal(1,observed.Revision);Assert.Equal(1,observed.Count);
    }
    [Fact,Trait("Category","Integration")]
    public void CorruptRowPayloadIsRejected(){
        using var db=new RuntimeSqlCatalogFixture(Options());var api=db.RuntimeSql;long id,objectId;
        using(var tx=api.BeginTransaction()){objectId=api.CreateTable("dbo","t",Columns()).ObjectId;id=api.InsertRow("dbo","t",[1,"a",1m]);tx.Commit();}
        var field=typeof(RuntimeSqlCatalog).GetField("rows",global::System.Reflection.BindingFlags.Instance|global::System.Reflection.BindingFlags.NonPublic)!;var table=field.GetValue(api)!;var types=table.GetType().GetGenericArguments();var key=Activator.CreateInstance(types[0],objectId,id)!;var item=table.GetType().GetProperty("Item")!;var record=item.GetValue(table,[key])!;var original=((byte[])types[1].GetProperty("Payload")!.GetValue(record)!).ToArray();
        var wrongVersion=original.ToArray();wrongVersion[0]=2;var wrongTag=original.ToArray();wrongTag[8]=2;
        foreach(var invalid in new[]{wrongVersion,wrongTag,original[..^1],original.Concat(new byte[]{0}).ToArray()}){
            using(var tx=db.BeginTransaction()){item.SetValue(table,Activator.CreateInstance(types[1],key,invalid),[key]);tx.Commit();}
            Assert.Throws<InvalidDataException>(()=>api.ReadRows("dbo","t",api.SchemaRevision).ToArray());
            using(var tx=db.BeginTransaction()){item.SetValue(table,Activator.CreateInstance(types[1],key,original),[key]);tx.Commit();}
        }
        Assert.Equal(new object?[]{1,"a",1m},Assert.Single(api.ReadRows("dbo","t",api.SchemaRevision)).Values);
    }

    private static DatabaseContainerOptions Options(){var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT");if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root)||!(root.StartsWith("D:\\",StringComparison.OrdinalIgnoreCase)||root.StartsWith("F:\\",StringComparison.OrdinalIgnoreCase))){throw new InvalidOperationException("An absolute D: or F: test root is required.");}var path=Path.Combine(root,"runtime-sql-api-"+Guid.NewGuid().ToString("N"));return DatabaseContainerOptions.OnStorage(path) with{Transactions=DatabaseTransactionMode.SharedV2,IndexSize=128,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20,TemporaryDirectory=Path.Combine(path,"scratch")};}
    private static RuntimeSqlColumn[] Columns()=>[new("id","int",false,IsPrimaryKey:true),new("title","nvarchar",true,Length:8,Collation:"Japanese_CI_AS"),new("price","decimal",false,Precision:5,Scale:2)];
    [Fact,Trait("Category","Integration")]
    public void TransactionsRecoveryAlterAndRecreate(){
        var options=Options();long objectId,rowId,revision;
        using(var db=new RuntimeSqlCatalogFixture(options)){
            var api=db.RuntimeSql;Assert.Throws<InvalidOperationException>(()=>api.CreateTable("dbo","t",Columns()));
            using(var legacy=db.BeginTransaction()){Assert.Throws<InvalidOperationException>(()=>api.CreateTable("dbo","t",Columns()));legacy.Rollback();}
            using(var transaction=api.BeginTransaction()){
                objectId=api.CreateTable("dbo","t",Columns()).ObjectId;rowId=api.InsertRow("dbo","t",[7,"seven",12.34m]);Assert.Equal(1,api.SchemaRevision);transaction.Commit();
            }
            revision=api.SchemaRevision;Assert.Equal(1,revision);Assert.Equal("Japanese_CI_AS",api.FindTable("dbo","t")!.Columns[1].Collation);Assert.Equal(objectId,api.FindTable("DBO","T")!.ObjectId);Assert.Single(api.Tables);
            var first=Assert.Single(api.ReadRows("dbo","t",revision));Assert.Equal(rowId,first.RowId);Assert.Equal(new object?[]{7,"seven",12.34m},first.Values);
            using(var tx=api.BeginTransaction()){using var cursor=api.ReadRows("dbo","t",revision).GetEnumerator();Assert.True(cursor.MoveNext());api.AddNullableColumn("dbo","t",new("note","nvarchar",true,Length:5));Assert.Throws<InvalidOperationException>(()=>cursor.MoveNext());api.UpdateRow("dbo","t",rowId,[7,"changed",12.34m,"x"]);tx.Rollback();}
            Assert.Equal(revision,api.SchemaRevision);Assert.Equal(new object?[]{7,"seven",12.34m},Assert.Single(api.ReadRows("dbo","t",revision)).Values);
            using(var tx=api.BeginTransaction()){api.AddNullableColumn("dbo","t",new("note","nvarchar",true,Length:5));tx.Commit();}
            Assert.Throws<InvalidOperationException>(()=>api.ReadRows("dbo","t",revision).ToArray());revision=api.SchemaRevision;Assert.Equal(new object?[]{7,"seven",12.34m,null},Assert.Single(api.ReadRows("dbo","t",revision)).Values);
        }
        using(var db=new RuntimeSqlCatalogFixture(options)){
            var api=db.RuntimeSql;Assert.Equal(revision,api.SchemaRevision);Assert.Equal(objectId,api.FindTable("dbo","t")!.ObjectId);Assert.Equal(new object?[]{7,"seven",12.34m,null},Assert.Single(api.ReadRows("dbo","t",revision)).Values);
            using(var tx=api.BeginTransaction()){api.DropTable("dbo","t");var replacement=api.CreateTable("dbo","t",Columns());Assert.True(replacement.ObjectId>objectId);tx.Commit();}
            Assert.Empty(api.ReadRows("dbo","t",api.SchemaRevision));
        }
        using(var reopened=new RuntimeSqlCatalogFixture(options)){Assert.Empty(reopened.RuntimeSql.ReadRows("dbo","t",reopened.RuntimeSql.SchemaRevision));Assert.True(reopened.RuntimeSql.FindTable("dbo","t")!.ObjectId>objectId);}
    }
    [Fact,Trait("Category","Integration")]
    public void ConstraintsAndNoPrimaryKeyDuplicates(){
        using var db=new RuntimeSqlCatalogFixture(Options());var api=db.RuntimeSql;
        using(var tx=api.BeginTransaction()){
            api.CreateTable("dbo","t",Columns());var id=api.InsertRow("dbo","t",[1,"a",1.23m]);
            Assert.Throws<InvalidOperationException>(()=>api.InsertRow("dbo","t",[1,"b",1.23m]));Assert.Throws<InvalidOperationException>(()=>api.InsertRow("dbo","t",[null,"b",1.23m]));
            Assert.Throws<InvalidOperationException>(()=>api.InsertRow("dbo","t",[2,"123456789",1.23m]));Assert.Throws<InvalidOperationException>(()=>api.InsertRow("dbo","t",[2,"a",1.234m]));Assert.Throws<InvalidOperationException>(()=>api.InsertRow("dbo","t",[2,"a",1000m]));
            Assert.Throws<InvalidOperationException>(()=>api.CreateTable("dbo","T",Columns()));Assert.Throws<InvalidOperationException>(()=>api.AddNullableColumn("dbo","t",new("ID","int",true)));Assert.Throws<InvalidOperationException>(()=>api.AddNullableColumn("dbo","t",new("required","int",false)));
            Assert.Throws<InvalidOperationException>(()=>api.CreateTable("dbo","bad",[new("x","date",false)]));Assert.Throws<InvalidOperationException>(()=>api.CreateTable("dbo","bad",[new("x","nvarchar",true,Length:4001)]));Assert.Throws<InvalidOperationException>(()=>api.CreateTable("dbo","bad",[new("x","varbinary",true,Length:8001)]));Assert.Throws<InvalidOperationException>(()=>api.CreateTable("dbo","bad",[new("x","int",false,Collation:"Japanese_CI_AS")]));Assert.Throws<InvalidOperationException>(()=>api.CreateTable("dbo","bad",[new("x","nvarchar",true,Length:8,Collation:"unsupported")]));Assert.Throws<InvalidOperationException>(()=>api.CreateTable("dbo","bad",[new("x","nvarchar",false,Length:8,IsPrimaryKey:true)]));
            api.CreateTable("dbo","duplicates",[new("x","int",true)]);api.InsertRow("dbo","duplicates",[1]);api.InsertRow("dbo","duplicates",[1]);api.InsertRow("dbo","duplicates",[null]);
            api.UpdateRow("dbo","t",id,[1,"ok",2.00m]);tx.Commit();
        }
        Assert.Equal(3,api.ReadRows("dbo","duplicates",api.SchemaRevision).Count());Assert.Equal("ok",Assert.Single(api.ReadRows("dbo","t",api.SchemaRevision)).Values[1]);
        var deleteId=api.ReadRows("dbo","duplicates",api.SchemaRevision).First().RowId;using(var tx=api.BeginTransaction()){api.DeleteRow("dbo","duplicates",deleteId);tx.Rollback();}Assert.Equal(3,api.ReadRows("dbo","duplicates",api.SchemaRevision).Count());
        using(var tx=api.BeginTransaction()){api.DeleteRow("dbo","duplicates",deleteId);tx.Commit();}Assert.Equal(2,api.ReadRows("dbo","duplicates",api.SchemaRevision).Count());
    }
    [Fact,Trait("Category","Integration")]
    public void PartialBackingRegistrationReleasesStorageAfterFailure(){
        var options=Options();var invalid=options with{TableStorage=new Dictionary<string,DatabaseTableStorageOptions>{["~runtime-sql-v1.rows"]=new(){IndexSize=long.MaxValue,PageSize=4096,SegmentSize=1<<20,MemorySize=1<<20}}};
        var error=Assert.ThrowsAny<Exception>(()=>new RuntimeSqlCatalogFixture(invalid));
        Assert.True(error is OverflowException || error is AggregateException aggregate&&aggregate.InnerExceptions.Any(static e=>e is OverflowException));
        using(var handle=new FileStream(Path.Combine(options.StorageDirectory!,"store-v2","store.lock"),FileMode.Open,FileAccess.Read,FileShare.None)){}
        using(var reopened=new RuntimeSqlCatalogFixture(options)){using var tx=reopened.RuntimeSql.BeginTransaction();reopened.RuntimeSql.CreateTable("dbo","t",Columns());reopened.RuntimeSql.InsertRow("dbo","t",[1,"a",1m]);tx.Commit();}
        using(var reopened=new RuntimeSqlCatalogFixture(options)){Assert.Single(reopened.RuntimeSql.ReadRows("dbo","t",reopened.RuntimeSql.SchemaRevision));}
    }
    [Fact,Trait("Category","Integration")]
    public void GeneratedRootDisposedBeforeFirstTransactionCanReopen(){
        var options=Options();using(var first=new RuntimeGeneratedFixture(options)){}using(var reopened=new RuntimeGeneratedFixture(options)){using(var tx=reopened.BeginTransaction()){Assert.True(reopened.TypedOrders.Add(new(7,"after-reopen",1)));tx.Commit();}using var read=reopened.BeginReadSession();Assert.Equal("after-reopen",reopened.TypedOrders[new(7)].Name);}
    }
    [Fact,Trait("Category","Integration")]
    public void RejectsLegacyAndMismatchedStorageBeforeOpening(){
        var options=Options();Directory.CreateDirectory(options.StorageDirectory!);File.WriteAllText(Path.Combine(options.StorageDirectory!,"existing.bin"),"unchanged");
        Assert.Throws<InvalidDataException>(()=>new RuntimeSqlCatalogFixture(options));Assert.Equal("unchanged",File.ReadAllText(Path.Combine(options.StorageDirectory!,"existing.bin")));Assert.False(File.Exists(Path.Combine(options.StorageDirectory!,"runtime-sql-v1.profile")));
        var fresh=Options();using(var db=new RuntimeSqlCatalogFixture(fresh)){}Assert.Throws<InvalidDataException>(()=>new RuntimeSqlCatalogFixture(fresh,"different-schema"));
        Assert.Throws<InvalidOperationException>(()=>new RuntimeSqlCatalogFixture(DatabaseContainerOptions.InMemory));
    }
}
