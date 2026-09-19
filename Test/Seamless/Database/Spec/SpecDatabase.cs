using DatabaseObjectKey = global::Seamless.Database.System.Keys.DatabaseObject;
using DatabaseObjectKind = global::Seamless.Database.System.DatabaseObjectKind;
using DatabaseObjectRecord = global::Seamless.Database.System.Records.DatabaseObject;

namespace Test.Seamless.Database.Spec {
    internal static partial class DatabaseGenerator;
    // Keys/Records の partial struct を MemoryPack 対応スキーマレコードへ補完する generator のマーカー。

    // Seamless.Database.Generator(DatabaseContainerGenerator)の生成物を種別ごとに検証するための最小スペック。
    // Table/View/TableFunction/ScalarFunction/StoredProcedure を宣言し、SGが生成する DatabaseObjects/DatabaseName/
    // 各アクセサのランタイム動作を SpecDatabaseTests で個別に確かめる。
    public sealed partial class SpecDatabase(global::Seamless.Database.DatabaseContainerOptions? options)
        :global::Seamless.Database.DatabaseContainer(options??global::Seamless.Database.DatabaseContainerOptions.InMemory with { IndexSize=1L<<8,MemorySize=1L<<20 }) {
        public SpecDatabase() : this(null) {
        }

        public global::Seamless.Database.Table<Keys.Item,Records.Item> Items=>
            this.Table<Keys.Item,Records.Item>(static record=>new(record.Id));

        [global::Seamless.FLinq.LinqIntercept(true)]
        public IEnumerable<string> ItemNamesQuery()=>
            this.Items.OrderBy(item=>item.Name).Select(item=>item.Name);

        public global::Seamless.Database.View<string> ItemNames()=>this.View(()=>this.ItemNamesQuery());

        [global::Seamless.FLinq.LinqIntercept(true)]
        public IEnumerable<Records.Item> ItemsAboveQuery(int minimumId)=>
            this.Items.Where(item=>item.Id>=minimumId).OrderBy(item=>item.Id);

        public global::Seamless.Database.TableFunction<Records.Item> ItemsAbove(int minimumId)=>this.TableFunction(()=>this.ItemsAboveQuery(minimumId));

        [global::Seamless.FLinq.LinqIntercept(false)]
        public global::Seamless.Database.ScalarFunction<int> ItemCount()=>this.ScalarFunction(()=>this.Items.Count());

        [global::Seamless.FLinq.LinqIntercept(false)]
        public global::Seamless.Database.StoredProcedure<int> NoOperation()=>this.StoredProcedure(static()=>0);
    }

    namespace Records {
        public partial struct Item {
            public partial int Id { get; }
            public partial string Name { get; }
        }
    }

    namespace Keys {
        public partial struct Item {
            public partial int Id { get; }
        }
    }

    public sealed class SpecDatabaseFixture:IDisposable {
        public SpecDatabase Database { get; }=Load();
        public void Dispose()=>this.Database.Dispose();
        internal static SpecDatabase Load() {
            var database = new SpecDatabase();
            database.Items.Add(new Records.Item(1,"Banana"));
            database.Items.Add(new Records.Item(2,"Apple"));
            database.Items.Add(new Records.Item(3,"Cherry"));
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
                Assert.Equal(typeof(Records.Item),declaration.RecordType);
                Assert.Equal("Items",this.Database.Items.Name);
            }
            {
                //生成されたTable<T>プロパティが実際に読み書きできること
                Assert.Equal(3,this.Database.Items.Count());
                Assert.Contains(this.Database.Items,item => item.Name=="Apple");
                Assert.Equal("Apple",this.Database.Items[new Keys.Item(2)].Name);
                Assert.True(this.Database.Items.TryGetValue(new(3),out var item));
                Assert.Equal("Cherry",item.Name);
                Assert.False(this.Database.Items.Add(new Records.Item(3,"Duplicate")));
            }
        }

        [Fact,Trait("Category","Unit")]
        public void View_ItemNames_Generated() {
            {
                //DatabaseObjectsにView種別で登録されていること
                var declaration = this.Database.DatabaseObjects[new("ItemNames")];
                Assert.Equal(DatabaseObjectKind.View,declaration.Kind);
                Assert.Equal(typeof(string),declaration.RecordType);
                Assert.Equal("ItemNames",this.Database.ItemNames().Name);
            }
            {
                //Viewが定義どおりに列挙され、同一インスタンスにキャッシュされること
                var view = this.Database.ItemNames();
                Assert.Equal(["Apple","Banana","Cherry"],view.ToArray());
                Assert.Same(view,this.Database.ItemNames());
            }
        }

        [Fact,Trait("Category","Unit")]
        public void TableFunction_ItemsAbove_Generated() {
            {
                //DatabaseObjectsにTableFunction種別で登録されていること
                var declaration = this.Database.DatabaseObjects[new("ItemsAbove")];
                Assert.Equal(DatabaseObjectKind.TableFunction,declaration.Kind);
                Assert.Equal(typeof(Records.Item),declaration.RecordType);
                Assert.Equal("ItemsAbove",this.Database.ItemsAbove(2).Name);
            }
            {
                //引数をキャプチャして列挙のたびに定義を評価すること
                var function = this.Database.ItemsAbove(2);
                Assert.Equal([2,3],function.Select(item => item.Id).ToArray());
            }
        }

        [Fact,Trait("Category","Unit")]
        public void ScalarFunction_ItemCount_Generated() {
            var declaration=this.Database.DatabaseObjects[new("ItemCount")];
            Assert.Equal(DatabaseObjectKind.ScalarFunction,declaration.Kind);
            Assert.Equal(typeof(int),declaration.RecordType);
            Assert.Equal(3,this.Database.ItemCount().Value);
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
