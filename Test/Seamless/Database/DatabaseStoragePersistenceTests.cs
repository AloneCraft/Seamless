namespace Test.Seamless.Database;

public sealed class DatabaseStoragePersistenceTests {
    [Fact,Trait("Category","Integration")]
    public void StorageMode_Reopen_RestoresTableAndMaterializedViewRows() {
        var directory=global::Seamless.TestKit.TestPath.Directory<DatabaseStoragePersistenceTests>();
        var options=global::Seamless.Database.DatabaseContainerOptions.OnStorage(directory) with {
            IndexSize=1L<<12,
            MemorySize=1L<<24,
        };

        using(var database=new TestDatabase(options)) {
            for(var index=0;index<5000;index++) {
                Assert.True(database.Items.Add(new(index,index.ToString())));
            }
            using(var transaction=database.BeginTransaction()) {
                Assert.True(database.AddView(new(1,100)));
                Assert.True(database.AddView(new(2,200)));
                transaction.Commit();
            }
        }

        using(var database=new TestDatabase(options)) {
            Assert.Equal(5000,database.Items.Count);
            Assert.Contains(new global::Test.Seamless.Database.Spec.Records.Item(0,0.ToString()),database.Items);
            Assert.Contains(new global::Test.Seamless.Database.Spec.Records.Item(4999,4999.ToString()),database.Items);
            Assert.Equal(2,database.Rows.Count);
            Assert.Equal(new Row(1,100),database.Rows[new(1)]);
            Assert.Equal(new Row(2,200),database.Rows[new(2)]);
        }
    }

    private readonly record struct Key(int Id);
    private readonly record struct Row(int Id,int Value);

    private sealed class TestDatabase(global::Seamless.Database.DatabaseContainerOptions options)
        :global::Seamless.Database.DatabaseContainer(options) {
        public global::Seamless.Database.Table<global::Test.Seamless.Database.Spec.Records.Item> Items=>
            this.Table<global::Test.Seamless.Database.Spec.Records.Item>();
        public global::Seamless.Database.MaterializedView<Key,Row> Rows=>
            this.MaterializedView<Key,Row>(static row=>new(row.Id));

        public bool AddView(Row row)=>AddMaterializedViewRow(this.Rows,row);
    }
}
