using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using MemoryPack;

namespace Management.Tests;

public sealed partial class MixedSchemaSql{
    [Fact]public void RootAndNamedDboHaveIndependentRowsAndCatalogEntries(){
        var options=DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,MemorySize=1<<20,SegmentSize=1<<20};
        using var database=new MixedDb(options);
        using(var write=database.BeginTransaction()){
            database.Rows.Add(new(1,10));database.dbo.Rows.Add(new(1,20));database.dbo0.Rows.Add(new(1,30));database.dbo1.Rows.Add(new(1,40));database.dbo.OnlyDbo.Add(new(1,50));write.Commit();
        }
        var adapter=new InProcessDatabaseAdapter(database);var registry=new DatabaseRegistry();registry.Register("Mixed",adapter);
        var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"Mixed");
        foreach(var (name,value) in new[]{("Rows",10),("dbo.Rows",20),("dbo0.Rows",30),("dbo1.Rows",40)}){
            Assert.Equal(value,Assert.Single(engine.Execute("SELECT Value FROM "+name,session).Rows)[0]);
        }
        Assert.Equal(208,Assert.Throws<SqlQueryException>(()=>engine.Execute("SELECT Value FROM OnlyDbo",session).Rows.ToArray()).Number);
        Assert.Null(adapter.FindTable("OnlyDbo"));
        Assert.Equal(1,engine.Execute("UPDATE Rows SET Value=11 WHERE Id=1",session).RowsAffected);
        using(var read=database.BeginReadSession()){Assert.Equal(11,database.Rows[new(1)].Value);Assert.Equal(20,database.dbo.Rows[new(1)].Value);}
        Assert.True(SystemCatalogEmulator.TryCatalog("schemas",adapter,[],out var schemas));
        var schemaIds=schemas.Rows.ToDictionary(row=>(string)row[0]!,row=>(int)row[1]!);
        Assert.NotEqual(schemaIds[""],schemaIds["dbo"]);
        Assert.True(SystemCatalogEmulator.TryCatalog("tables",adapter,[],out var tables));
        var owners=tables.Rows.Where(row=>Equals(row[0],"Rows")).Select(row=>(int)row[2]!).Order().ToArray();
        Assert.Equal(new[]{"","dbo","dbo0","dbo1"}.Select(name=>schemaIds[name]).Order(),owners);
    }
    [Fact]public void RuntimeSqlDdlAndMutationKeepTheRootSeparateFromDbo(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"runtime-root-"+Guid.NewGuid().ToString("N"));
        var options=DatabaseContainerOptions.OnStorage(root) with{IndexSize=128,PageSize=4096,MemorySize=1<<20,SegmentSize=1<<20};
        using(var database=new RuntimeDb(options)){
            var adapter=new InProcessDatabaseAdapter(database);var engine=new SqlExecutionEngine(adapter);using var session=new SessionContext(1,adapter.DatabaseName);
            engine.Execute("CREATE TABLE Rows(Id int PRIMARY KEY, Value int)",session);
            engine.Execute("CREATE TABLE dbo.Rows(Id int PRIMARY KEY, Value int)",session);
            engine.Execute("INSERT INTO Rows VALUES(1,10)",session);engine.Execute("INSERT INTO dbo.Rows VALUES(1,20)",session);
            Assert.Equal(10,Assert.Single(engine.Execute("SELECT Value FROM Rows",session).Rows)[0]);
            Assert.Equal(20,Assert.Single(engine.Execute("SELECT Value FROM dbo.Rows",session).Rows)[0]);
            engine.Execute("UPDATE Rows SET Value=11 WHERE Id=1",session);
        }
        using(var reopened=new RuntimeDb(options)){
            var adapter=new InProcessDatabaseAdapter(reopened);var engine=new SqlExecutionEngine(adapter);using var session=new SessionContext(2,adapter.DatabaseName);
            Assert.Equal(11,Assert.Single(engine.Execute("SELECT Value FROM Rows",session).Rows)[0]);
            Assert.Equal(20,Assert.Single(engine.Execute("SELECT Value FROM dbo.Rows",session).Rows)[0]);
        }
    }
    [MemoryPackable]public readonly partial record struct Key(int Id);
    [MemoryPackable]public readonly partial record struct Row(int Id,int Value);
    private sealed class RuntimeDb(DatabaseContainerOptions options):RuntimeSqlDatabaseContainer(options,"mixed-schemas-v1"){
        protected override void EnsureTransactionSchema(){}
    }
    private sealed class MixedDb(DatabaseContainerOptions options):DatabaseContainer(options){
        public Table<Key,Row> Rows=>this.Table<Key,Row>(row=>new(row.Id));
        public Named dbo=>new(this,"dbo");public Named dbo0=>new(this,"dbo0");public Named dbo1=>new(this,"dbo1");
        // 手書きのコンテナは生成器が取引の表の一覧を作らないので、取引の前に全部の表を作っておく。
        protected override void EnsureTransactionSchema(){_=this.Rows;_=this.dbo.Rows;_=this.dbo.OnlyDbo;_=this.dbo0.Rows;_=this.dbo0.OnlyDbo;_=this.dbo1.Rows;_=this.dbo1.OnlyDbo;}
    }
    private sealed class Named(DatabaseContainer database,string name):DatabaseSchema(database,name){
        public Table<Key,Row> Rows=>this.Table<Key,Row>(row=>new(row.Id));
        public Table<Key,Row> OnlyDbo=>this.Table<Key,Row>(row=>new(row.Id));
    }
}
