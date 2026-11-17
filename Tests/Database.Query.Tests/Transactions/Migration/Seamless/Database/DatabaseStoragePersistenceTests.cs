using Test.Seamless.Database.StoragePersistence;

namespace Test.Seamless.Database {
    public sealed class DatabaseStoragePersistenceTests {
        private sealed class ViewSource(params PersistedRow[] rows):global::Seamless.Database.IDatabaseImportSource {
            public IEnumerable<TRecord> ReadTable<TRecord>(global::Seamless.Database.System.Keys.DatabaseObject key)=>
                key.Name=="Rows" ? (IEnumerable<TRecord>)(object)rows : [];
        }

        [Fact,Trait("Category","Integration")]
        public async Task StorageMode_Reopen_RestoresTableAndMaterializedViewRows() {
            var directory=global::Seamless.TestKit.TestPath.Directory<DatabaseStoragePersistenceTests>();
            var options=global::Seamless.Database.DatabaseContainerOptions.OnStorage(directory) with {
                IndexSize=1L<<12,
                MemorySize=1L<<24,
            };

            using(var database=new PersistenceDatabase(options)) {
                using(var tx=database.BeginTransaction()) {
                    for(var index=0;index<5000;index++) {
                        Assert.True(tx.Items.Add(new(new(index),index.ToString())));
                    }
                    tx.Commit();
                }
                // 実体化ビューの行は取込で入れる。
                await using(var tx=await database.BeginTransactionAsync()) {
                    await tx.ImportTablesAsync(new ViewSource(new PersistedRow(1,100),new PersistedRow(2,200)));
                    await tx.CommitAsync();
                }
            }

            using(var database=new PersistenceDatabase(options)) {
                using var reader=database.BeginRead();
                Assert.Equal(5000,reader.Items.Count);
                Assert.Contains(new global::Test.Seamless.Database.Spec.Tables.Items(new(0),0.ToString()),reader.Items);
                Assert.Contains(new global::Test.Seamless.Database.Spec.Tables.Items(new(4999),4999.ToString()),reader.Items);
                Assert.Equal(2,reader.Rows.Count);
                Assert.Equal(new PersistedRow(1,100),reader.Rows[new(1)]);
                Assert.Equal(new PersistedRow(2,200),reader.Rows[new(2)]);
            }
        }
    }
}

namespace Test.Seamless.Database.StoragePersistence {
    // 生成器の目印。この名前空間のコンテナに取引・読み込みの入口を生成させる。
    internal static partial class DatabaseGenerator;

    public readonly record struct PersistedKey(int Id);
    public readonly record struct PersistedRow(int Id,int Value);

    public sealed partial class PersistenceDatabase(global::Seamless.Database.DatabaseContainerOptions options)
        :global::Seamless.Database.DatabaseContainer(options) {
        public global::Seamless.Database.Table<global::Test.Seamless.Database.Spec.Tables.Items> Items=>
            this.Table<global::Test.Seamless.Database.Spec.Tables.Items>();
        public global::Seamless.Database.MaterializedView<PersistedKey,PersistedRow> Rows=>
            this.MaterializedView<PersistedKey,PersistedRow>(static row=>new(row.Id));
    }
}
