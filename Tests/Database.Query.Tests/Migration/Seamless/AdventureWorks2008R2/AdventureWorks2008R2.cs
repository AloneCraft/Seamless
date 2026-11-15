// AdventureWorks2008R2 の宣言的データベース定義。
// 本物のバックアップ取り込みは IDatabaseImportSource 経由で後日行うため、
// 現時点ではリポジトリ内のサンプルデータだけで動作する実働サブセットを宣言する。
// Table 実体は Tsavorite ストアで、DatabaseContainerOptions によりメモリ/ストレージを切り替える。
namespace Test.Seamless.AdventureWorks2008R2 {
    // Keys/Records の partial struct を MemoryPack 対応スキーマレコードへ補完する generator のマーカー。
    internal static partial class DatabaseGenerator;

    // 宣言的コンテナ。プロパティ/メソッドが宣言の主形式で、属性は SQL Server 互換名の補助。
    // ビュー・関数は AdventureWorksQueries(IReader への拡張メンバー)に書く。
    public sealed partial class AdventureWorks(global::Seamless.Database.DatabaseContainerOptions? options)
        :global::Seamless.Database.DatabaseContainer(
            options??(global::Seamless.Database.DatabaseContainerOptions.InMemory with {
                IndexSize=1L<<12,MemorySize=1L<<24
            })) {
        public AdventureWorks() : this(null) {
        }
        public global::Seamless.Database.Table<Rows.Customer> Customers => this.Table<Rows.Customer>();
        public global::Seamless.Database.Table<Rows.SalesOrderHeader> SalesOrderHeaders => this.Table<Rows.SalesOrderHeader>();
        public global::Seamless.Database.Table<Rows.Store> Stores => this.Table<Rows.Store>();
        public global::Seamless.Database.Table<Rows.StateProvince> StateProvinces => this.Table<Rows.StateProvince>();
        public global::Seamless.Database.Table<Rows.CountryRegion> CountryRegions => this.Table<Rows.CountryRegion>();

        // dbo.ufnCustomerTotalDue 相当の計算。コンテナの普通のメソッドなので目録には載らない。
        public decimal CustomerTotalDue(int customerId) =>
            this.SalesOrderHeaders
                .Where(order => order.CustomerId==customerId)
                .Sum(order => order.TotalDue);
    }

    // スキーマレコード。partial struct の宣言から generator が MemoryPack 対応の完全なレコードを生成する。
    namespace Rows {
        // 行の型。旧方式(Keys と Records の二重書き)の生成をやめ、MemoryPack の印を付けた位置引数のレコードで書く(列の順と名前は以前と同じ)。
        [global::MemoryPack.MemoryPackable]
        public readonly partial record struct Customer(int CustomerId,int? PersonId,int? StoreId,int? TerritoryId,string AccountNumber);
        [global::MemoryPack.MemoryPackable]
        public readonly partial record struct SalesOrderHeader(int SalesOrderId,int CustomerId,DateTime OrderDate,byte Status,decimal TotalDue);
        [global::MemoryPack.MemoryPackable]
        public readonly partial record struct Store(int BusinessEntityId,string Name);
        [global::MemoryPack.MemoryPackable]
        public readonly partial record struct StateProvince(int StateProvinceId,string StateProvinceCode,string CountryRegionCode,string Name);
        [global::MemoryPack.MemoryPackable]
        public readonly partial record struct CountryRegion(string CountryRegionCode,string Name);
        [global::MemoryPack.MemoryPackable]
        public readonly partial record struct ActiveCustomer(int CustomerId,int StoreId);
        [global::MemoryPack.MemoryPackable]
        public readonly partial record struct StateProvinceCountryRegion(int StateProvinceId,string StateProvinceName,string CountryRegionCode,string CountryRegionName);
        [global::MemoryPack.MemoryPackable]
        public readonly partial record struct SalesOrderSummary(int SalesOrderId,decimal TotalDue);
    }

    public sealed class AdventureWorksFixture:IDisposable {
        public AdventureWorks Database { get; } = Load();
        public void Dispose() => this.Database.Dispose();

        internal static AdventureWorks Load() {
            var database = new AdventureWorks();
            Seed(database);
            return database;
        }

        internal static void Seed(AdventureWorks database) {
            // 初期データは1つの取引で登録する(取引の外からは表へ書けない)。
            using var tx = database.BeginTransaction();
            tx.CountryRegions.Add(new Rows.CountryRegion("JP","Japan"));
            tx.CountryRegions.Add(new Rows.CountryRegion("US","United States"));
            tx.StateProvinces.Add(new Rows.StateProvince(1,"CA","US","California"));
            tx.StateProvinces.Add(new Rows.StateProvince(2,"WA","US","Washington"));
            tx.StateProvinces.Add(new Rows.StateProvince(3,"13","JP","Tokyo"));
            tx.Stores.Add(new Rows.Store(10,"Bike World"));
            tx.Stores.Add(new Rows.Store(11,"Trail Cycles"));
            tx.Customers.Add(new Rows.Customer(1,100,10,1,"AW00000001"));
            tx.Customers.Add(new Rows.Customer(2,null,11,2,"AW00000002"));
            tx.Customers.Add(new Rows.Customer(3,101,null,3,"AW00000003"));
            tx.Customers.Add(new Rows.Customer(4,null,null,null,"AW00000004"));
            tx.SalesOrderHeaders.Add(new Rows.SalesOrderHeader(1000,1,new DateTime(2008,6,1),5,125.50m));
            tx.SalesOrderHeaders.Add(new Rows.SalesOrderHeader(1001,1,new DateTime(2008,6,15),5,300.00m));
            tx.SalesOrderHeaders.Add(new Rows.SalesOrderHeader(1002,2,new DateTime(2008,7,1),5,42.75m));
            tx.SalesOrderHeaders.Add(new Rows.SalesOrderHeader(1003,3,new DateTime(2008,7,2),1,999.99m));
            tx.Commit();
        }
    }

    [Collection("AdventureWorksCollection")]
    public sealed class AdventureWorksTests(AdventureWorksFixture fixture):IClassFixture<AdventureWorksFixture> {
        private AdventureWorks Database => fixture.Database;

        [Fact]
        public void ActiveCustomers_View_MatchesBaseline() {
            var expected = Baseline.ActiveCustomers(this.Database);
            using var reader = this.Database.BeginRead();
            // ビューは集合なので並びを持たない。比べる前に並べる。
            Assert.Equal(expected,reader.ActiveCustomers.Select(row => (row.CustomerId,row.StoreId)).Order().ToArray());
        }

        [Fact]
        public void StateProvinceCountryRegions_View_MatchesBaseline() {
            var expected = Baseline.StateProvinceCountryRegions(this.Database);
            using var reader = this.Database.BeginRead();
            Assert.Equal(expected,reader.StateProvinceCountryRegions.Select(row => (row.StateProvinceId,row.StateProvinceName,row.CountryRegionCode,row.CountryRegionName)).Order().ToArray());
        }

        [Fact]
        public void OrdersByCustomer_TableFunction_MatchesBaseline() {
            var expected = Baseline.OrdersByCustomer(this.Database,1);
            using var reader = this.Database.BeginRead();
            Assert.Equal(expected,reader.OrdersByCustomer(1).Select(row => (row.SalesOrderId,row.TotalDue)).Order().ToArray());
            {
                //一致しない顧客IDは空列挙になる
                Assert.Empty(reader.OrdersByCustomer(999).ToArray());
            }
        }

        [Fact]
        public void CustomerTotalDue_ScalarFunction_MatchesBaseline() {
            Assert.Equal(Baseline.CustomerTotalDue(this.Database,1),this.Database.CustomerTotalDue(1));
            Assert.Equal(0m,this.Database.CustomerTotalDue(999));
        }

        [Fact]
        public void DatabaseMetadata_FromGenerator() {
            Assert.Equal("AdventureWorks",this.Database.DatabaseName);
            var objects = this.Database.DatabaseObjects;
            Assert.Equal(global::Seamless.Database.System.DatabaseObjectKind.Table,objects[new("Customers")].Kind);
            Assert.Equal(typeof(Rows.Customer),objects[new("Customers")].RecordType);
            Assert.Equal(global::Seamless.Database.System.DatabaseObjectKind.View,objects[new("ActiveCustomers")].Kind);
            Assert.Equal(global::Seamless.Database.System.DatabaseObjectKind.TableFunction,objects[new("OrdersByCustomer")].Kind);
            Assert.DoesNotContain(objects,declaration => declaration.Key.Name=="CustomerTotalDue");
            Assert.Equal(5,objects.Count(declaration => declaration.Kind==global::Seamless.Database.System.DatabaseObjectKind.Table));
            Assert.Equal(9,objects.Count);
            Assert.Equal(global::Seamless.Database.System.DatabaseObjectKind.SystemTable,objects[new("DatabaseObjects")].Kind);
        }

        [Fact]
        public void StorageMode_PersistsUnderStorageDirectory() {
            var       directory= global::Seamless.TestKit.TestPath.Directory<AdventureWorksTests>("storage");
            using var database = new AdventureWorks(global::Seamless.Database.DatabaseContainerOptions.OnStorage(directory) with { IndexSize=1L<<12,MemorySize=1L<<24 });
            AdventureWorksFixture.Seed(database);
            var expected = Baseline.ActiveCustomers(database);
            using(var reader=database.BeginRead()){Assert.Equal(expected,reader.ActiveCustomers.Select(row => (row.CustomerId,row.StoreId)).Order().ToArray());}
            // 表は版付き方式の共通の保存領域に置かれる(表ごとのディレクトリは作らない)。
            Assert.True(Directory.Exists(Path.Combine(directory,"store-v2")));
        }

        [Fact]
        public void ImportTables_FromImportSource() {
            using var database = new AdventureWorks();
            database.EnsureTablesCreated();
            database.ImportTables(new SampleImportSource());
            var count = 0;
            foreach(var customer in database.Customers) {
                count++;
            }
            Assert.Equal(2,count);
        }

        private sealed class SampleImportSource:global::Seamless.Database.IDatabaseImportSource {
            public IEnumerable<TRecord> ReadTable<TRecord>(global::Seamless.Database.System.Keys.DatabaseObject key) {
                if(key.Name=="Customers") {
                    return (IEnumerable<TRecord>)(object)new[] {
                        new Rows.Customer(21,null,10,1,"AW00000021"),
                        new Rows.Customer(22,102,null,2,"AW00000022"),
                    };
                }
                return [];
            }
        }

        // LINQ を使わない手続き実装のベースライン。FLinq 展開結果との一致検証に使う。
        private static class Baseline {
            public static (int CustomerId,int StoreId)[] ActiveCustomers(AdventureWorks database) {
                var rows = new List<(int CustomerId,int StoreId)>();
                foreach(var customer in database.Customers) {
                    if(customer.StoreId.HasValue) {
                        rows.Add((customer.CustomerId,customer.StoreId.Value));
                    }
                }
                rows.Sort(static (left,right) => left.CustomerId.CompareTo(right.CustomerId));
                return [.. rows];
            }
            public static (int StateProvinceId,string StateProvinceName,string CountryRegionCode,string CountryRegionName)[] StateProvinceCountryRegions(AdventureWorks database) {
                var rows = new List<(int,string,string,string)>();
                foreach(var state in database.StateProvinces) {
                    foreach(var region in database.CountryRegions) {
                        if(state.CountryRegionCode==region.CountryRegionCode) {
                            rows.Add((state.StateProvinceId,state.Name,region.CountryRegionCode,region.Name));
                        }
                    }
                }
                rows.Sort(static (left,right) => left.Item1.CompareTo(right.Item1));
                return [.. rows];
            }
            public static (int SalesOrderId,decimal TotalDue)[] OrdersByCustomer(AdventureWorks database,int customerId) {
                var rows = new List<(int SalesOrderId,decimal TotalDue)>();
                foreach(var order in database.SalesOrderHeaders) {
                    if(order.CustomerId==customerId) {
                        rows.Add((order.SalesOrderId,order.TotalDue));
                    }
                }
                rows.Sort(static (left,right) => left.SalesOrderId.CompareTo(right.SalesOrderId));
                return [.. rows];
            }
            public static decimal CustomerTotalDue(AdventureWorks database,int customerId) {
                var total = 0m;
                foreach(var order in database.SalesOrderHeaders) {
                    if(order.CustomerId==customerId) {
                        total+=order.TotalDue;
                    }
                }
                return total;
            }
        }
    }
}
