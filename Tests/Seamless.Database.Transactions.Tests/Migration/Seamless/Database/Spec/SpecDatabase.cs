using DatabaseObjectKey = global::Seamless.Database.System.Keys.DatabaseObject;
using DatabaseObjectKind = global::Seamless.Database.System.DatabaseObjectKind;
using DatabaseObjectRecord = global::Seamless.Database.System.Records.DatabaseObject;

namespace Test.Seamless.Database.Spec {
    internal static partial class DatabaseGenerator;

    // ビュー・関数の結果の行型。ビューは Views、テーブル関数は Functions に置く。
    namespace Views {
        public readonly record struct ItemNames(string Name);
    }


    // Seamless.Database.Generator(DatabaseContainerGenerator)の生成物を種別ごとに検証するための最小スペック。
    // Table/View/TableFunction/ScalarFunction/StoredProcedure を宣言し、SGが生成する DatabaseObjects/DatabaseName/
    // 各アクセサのランタイム動作を SpecDatabaseTests で個別に確かめる。
    public sealed partial class SpecDatabase(global::Seamless.Database.DatabaseContainerOptions? options)
        :global::Seamless.Database.DatabaseContainer(options??global::Seamless.Database.DatabaseContainerOptions.InMemory with { IndexSize=1L<<8,MemorySize=1L<<20 }) {
        public SpecDatabase() : this(null) {
        }

        // 表 Items は Tables に宣言し(Tables.Items)、このプロパティとキーの型(Keys.Items)は DB の生成器が作る。

        [global::Seamless.FLinq.LinqIntercept(false)]
        public global::Seamless.Database.StoredProcedure<int> NoOperation()=>this.StoredProcedure(static()=>0);
    }

    namespace Tables {
        public partial struct Items {
            [global::Seamless.Database.Key] public partial int Id { get; }
            public partial string Name { get; }
        }
    }

    public sealed class SpecDatabaseFixture:IDisposable {
        public SpecDatabase Database { get; }=Load();
        public void Dispose()=>this.Database.Dispose();
        internal static SpecDatabase Load() {
            var database = new SpecDatabase();
            using(var tx=database.BeginTransaction()){
                tx.Items.Add(new Tables.Items(new(1),"Banana"));
                tx.Items.Add(new Tables.Items(new(2),"Apple"));
                tx.Items.Add(new Tables.Items(new(3),"Cherry"));
                tx.Commit();
            }
            return database;
        }
    }

    [Collection("SpecDatabaseCollection")]
    public sealed class SpecDatabaseTests(SpecDatabaseFixture fixture):IClassFixture<SpecDatabaseFixture> {
        private SpecDatabase Database=>fixture.Database;

        [Fact,Trait("Category","Unit")]
        public void Table_Items_Generated() {
            {
                //DatabaseObjectsにTable種別で登録されていること
                var declaration = this.Database.DatabaseObjects[new("Items")];
                Assert.Equal(new DatabaseObjectKey("Items"),declaration.Key);
                Assert.Equal(DatabaseObjectKind.Table,declaration.Kind);
                Assert.Equal(typeof(Tables.Items),declaration.RecordType);
                Assert.Equal("Items",this.Database.Items.Name);
            }
            {
                //生成されたTable<T>プロパティが実際に読み書きできること
                Assert.Equal(3,this.Database.Items.Count());
                Assert.Contains(this.Database.Items,item => item.Name=="Apple");
                Assert.Equal("Apple",this.Database.Items[new Keys.Items(2)].Name);
                Assert.True(this.Database.Items.TryGetValue(new(3),out var item));
                Assert.Equal("Cherry",item.Name);
                // 書き込みは取引からだけ。同じキーの行は追加されない(確定せずに取り消す)。
                using var tx=this.Database.BeginTransaction();
                Assert.False(tx.Items.Add(new Tables.Items(new(3),"Duplicate")));
            }
        }

        [Fact,Trait("Category","Unit")]
        public void View_ItemNames_Generated() {
            {
                //DatabaseObjectsにView種別で登録されていること
                var declaration = this.Database.DatabaseObjects[new("ItemNames")];
                Assert.Equal(DatabaseObjectKind.View,declaration.Kind);
                Assert.Equal(typeof(Views.ItemNames),declaration.RecordType);
            }
            {
                //読み込みの入口からビューを読むと、定義どおりの集合になること(集合なので並びは持たない)
                using var reader = this.Database.BeginRead();
                Assert.Equal(["Apple","Banana","Cherry"],reader.ItemNames.Select(row=>row.Name).Order().ToArray());
            }
            {
                //名前で呼ぶ入口(Manager・Gateway 用)からも同じ結果になること
                var query = Assert.Single(this.Database.Queries,item=>item.Name=="ItemNames");
                using var result = query.Execute(this.Database);
                Assert.Equal(["Apple","Banana","Cherry"],((IEnumerable<Views.ItemNames>)result.Value!).Select(row=>row.Name).Order().ToArray());
            }
        }

        [Fact,Trait("Category","Unit")]
        public void TableFunction_ItemsAbove_Generated() {
            {
                //DatabaseObjectsにTableFunction種別で登録されていること
                var declaration = this.Database.DatabaseObjects[new("ItemsAbove")];
                Assert.Equal(DatabaseObjectKind.TableFunction,declaration.Kind);
                Assert.Equal(typeof(Tables.Items),declaration.RecordType);
            }
            {
                //引数を受け取り、呼んだ入口の内容を読むこと
                using var reader = this.Database.BeginRead();
                Assert.Equal([2,3],reader.ItemsAbove(2).Select(item => item.Id).Order().ToArray());
            }
            {
                //取引から呼ぶと、その取引のまだ確定していない書き込みも読むこと
                using var tx = this.Database.BeginTransaction();
                tx.Items.Add(new Tables.Items(new(4),"Date"));
                Assert.Equal([2,3,4],tx.ItemsAbove(2).Select(item => item.Id).Order().ToArray());
            }
            {
                //名前で呼ぶ入口は引数の数と型を持つこと
                var query = Assert.Single(this.Database.Queries,item=>item.Name=="ItemsAbove");
                Assert.Equal([new("minimumId",typeof(int))],query.Parameters);
                using var result = query.Execute(this.Database,2);
                Assert.Equal([2,3],((IEnumerable<Tables.Items>)result.Value!).Select(item=>item.Id).Order().ToArray());
            }
        }

        [Fact,Trait("Category","Unit")]
        public void ScalarFunction_ItemCount_Generated() {
            var declaration=this.Database.DatabaseObjects[new("ItemCount")];
            Assert.Equal(DatabaseObjectKind.ScalarFunction,declaration.Kind);
            Assert.Equal(typeof(int),declaration.RecordType);
            using var reader=this.Database.BeginRead();
            Assert.Equal(3,reader.ItemCount());
        }

        [Fact,Trait("Category","Unit")]
        public void StoredProcedure_NoOperation_Generated() {
            var declaration=this.Database.DatabaseObjects[new("NoOperation")];
            Assert.Equal(DatabaseObjectKind.StoredProcedure,declaration.Kind);
            Assert.Equal(typeof(int),declaration.RecordType);
            Assert.Equal(0,this.Database.NoOperation().Result);
        }

        [Fact,Trait("Category","Unit")]
        public void DatabaseName_Generated() {
            Assert.Equal("SpecDatabase",this.Database.DatabaseName);
        }

        [Fact,Trait("Category","Unit")]
        public void DatabaseObjects_IsMaterializedKeyedSystemTable() {
            var catalog = this.Database.DatabaseObjects;

            Assert.Equal(6,catalog.Count);
            Assert.Equal(6,catalog.Keys.Count);
            Assert.Contains(new DatabaseObjectKey("Items"),catalog.Keys);
            Assert.True(catalog.ContainsKey(new("DatabaseObjects")));
            Assert.True(catalog.TryGetValue(new("DatabaseObjects"),out var self));
            Assert.Equal(DatabaseObjectKind.SystemTable,self.Kind);
            Assert.Equal(typeof(DatabaseObjectRecord),self.RecordType);
            Assert.False(catalog.ContainsKey(new("Missing")));
            Assert.False(catalog.TryGetValue(new("Missing"),out _));
            Assert.Throws<KeyNotFoundException>(()=>catalog[new("Missing")]);
            Assert.Contains(catalog,row => row.Key.Name=="Items");

            using var another = new SpecDatabase();
            Assert.Same(catalog,another.DatabaseObjects);
        }

        [Fact,Trait("Category","Unit")]
        public void SystemTable_HasNoPublicMutationOrConstructionApi() {
            var type = this.Database.DatabaseObjects.GetType();
            Assert.Empty(type.GetConstructors());
            Assert.Null(type.GetMethod("Add"));
            Assert.Null(type.GetMethod("Remove"));
            Assert.Null(type.GetMethod("Clear"));
        }

    }
}
