// AdventureWorks2008R2 の宣言的データベース定義。
// 本物のバックアップ取り込みは IDatabaseImportSource 経由で後日行うため、
// 現時点ではリポジトリ内のサンプルデータだけで動作する実働サブセットを宣言する。
// Table 実体は Tsavorite ストアで、DatabaseContainerOptions によりメモリ/ストレージを切り替える。
namespace Test.Seamless.AdventureWorks2008R2 {
    // Keys/Records の partial struct を MemoryPack 対応スキーマレコードへ補完する generator のマーカー。
    internal static partial class DatabaseGenerator;

    // 宣言的コンテナ。プロパティ/メソッドが宣言の主形式で、属性は SQL Server 互換名の補助。
    // FLinq は「LINQ を含む通常メソッドの呼び出し箇所」を展開するため、
    // View/TableFunction の LINQ 本体は *Query メソッドに置き、ラムダからそれを呼ぶ。
    [global::Seamless.Database.Database("AdventureWorks2008R2")]
    public sealed partial class AdventureWorks:global::Seamless.Database.DatabaseContainer {
        public AdventureWorks() : this(null) {
        }

        public AdventureWorks(global::Seamless.Database.DatabaseContainerOptions? options)
            : base(options??(global::Seamless.Database.DatabaseContainerOptions.InMemory with { IndexSize=1L<<12,MemorySize=1L<<24 })) {
        }

        [global::Seamless.Database.Table("Sales.Customer")]
        public global::Seamless.Database.Table<Records.Customer> Customers => this.Table<Records.Customer>();
        [global::Seamless.Database.Table("Sales.SalesOrderHeader")]
        public global::Seamless.Database.Table<Records.SalesOrderHeader> SalesOrderHeaders => this.Table<Records.SalesOrderHeader>();
        [global::Seamless.Database.Table("Sales.Store")]
        public global::Seamless.Database.Table<Records.Store> Stores => this.Table<Records.Store>();
        [global::Seamless.Database.Table("Person.StateProvince")]
        public global::Seamless.Database.Table<Records.StateProvince> StateProvinces => this.Table<Records.StateProvince>();
        [global::Seamless.Database.Table("Person.CountryRegion")]
        public global::Seamless.Database.Table<Records.CountryRegion> CountryRegions => this.Table<Records.CountryRegion>();

        // Sales.vActiveCustomer 相当: Store を持つ顧客だけを射影する。
        public IEnumerable<Records.ActiveCustomer> ActiveCustomersQuery() =>
            this.Customers
                .Where(customer => customer.StoreId.HasValue)
                .OrderBy(customer => customer.CustomerId)
                .Select(customer => new Records.ActiveCustomer(customer.CustomerId,customer.StoreId!.Value));

        [global::Seamless.Database.View("Sales.vActiveCustomer")]
        public global::Seamless.Database.View<Records.ActiveCustomer> ActiveCustomers() {
            return this.View(() => this.ActiveCustomersQuery());
        }

        // Person.vStateProvinceCountryRegion 相当: join ビュー。
        public IEnumerable<Records.StateProvinceCountryRegion> StateProvinceCountryRegionsQuery() =>
            this.StateProvinces
                .Join(this.CountryRegions,state => state.CountryRegionCode,region => region.CountryRegionCode,(state,region) => new Records.StateProvinceCountryRegion(state.StateProvinceId,state.Name,region.CountryRegionCode,region.Name))
                .OrderBy(row => row.StateProvinceId);

        [global::Seamless.Database.View("Person.vStateProvinceCountryRegion")]
        public global::Seamless.Database.View<Records.StateProvinceCountryRegion> StateProvinceCountryRegions() {
            return this.View(() => this.StateProvinceCountryRegionsQuery());
        }

        // dbo.ufnOrdersByCustomer 相当: テーブル関数。
        public IEnumerable<Records.SalesOrderSummary> OrdersByCustomerQuery(int customerId) =>
            this.SalesOrderHeaders
                .Where(order => order.CustomerId==customerId)
                .OrderBy(order => order.SalesOrderId)
                .Select(order => new Records.SalesOrderSummary(order.SalesOrderId,order.TotalDue));

        [global::Seamless.Database.Function("dbo.ufnOrdersByCustomer")]
        public global::Seamless.Database.TableFunction<Records.SalesOrderSummary> OrdersByCustomer(int customerId) {
            return this.TableFunction(() => this.OrdersByCustomerQuery(customerId));
        }

        // dbo.ufnCustomerTotalDue 相当: スカラー関数。
        [global::Seamless.Database.Function("dbo.ufnCustomerTotalDue")]
        public decimal CustomerTotalDue(int customerId) =>
            this.SalesOrderHeaders
                .Where(order => order.CustomerId==customerId)
                .Sum(order => order.TotalDue);
    }

    // スキーマレコード。partial struct の宣言から generator が MemoryPack 対応の完全なレコードを生成する。
    namespace Records {
        public partial struct Customer {
            public partial int CustomerId { get; }
            public partial int? PersonId { get; }
            public partial int? StoreId { get; }
            public partial int? TerritoryId { get; }
            public partial string AccountNumber { get; }
        }
        public partial struct SalesOrderHeader {
            public partial int SalesOrderId { get; }
            public partial int CustomerId { get; }
            public partial DateTime OrderDate { get; }
            public partial byte Status { get; }
            public partial decimal TotalDue { get; }
        }
        public partial struct Store {
            public partial int BusinessEntityId { get; }
            public partial string Name { get; }
        }
        public partial struct StateProvince {
            public partial int StateProvinceId { get; }
            public partial string StateProvinceCode { get; }
            public partial string CountryRegionCode { get; }
            public partial string Name { get; }
        }
        public partial struct CountryRegion {
            public partial string CountryRegionCode { get; }
            public partial string Name { get; }
        }
        public partial struct ActiveCustomer {
            public partial int CustomerId { get; }
            public partial int StoreId { get; }
        }
        public partial struct StateProvinceCountryRegion {
            public partial int StateProvinceId { get; }
            public partial string StateProvinceName { get; }
            public partial string CountryRegionCode { get; }
            public partial string CountryRegionName { get; }
        }
        public partial struct SalesOrderSummary {
            public partial int SalesOrderId { get; }
            public partial decimal TotalDue { get; }
        }
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
            database.CountryRegions.Add(new Records.CountryRegion("JP","Japan"));
            database.CountryRegions.Add(new Records.CountryRegion("US","United States"));
            database.StateProvinces.Add(new Records.StateProvince(1,"CA","US","California"));
            database.StateProvinces.Add(new Records.StateProvince(2,"WA","US","Washington"));
            database.StateProvinces.Add(new Records.StateProvince(3,"13","JP","Tokyo"));
            database.Stores.Add(new Records.Store(10,"Bike World"));
            database.Stores.Add(new Records.Store(11,"Trail Cycles"));
            database.Customers.Add(new Records.Customer(1,100,10,1,"AW00000001"));
            database.Customers.Add(new Records.Customer(2,null,11,2,"AW00000002"));
            database.Customers.Add(new Records.Customer(3,101,null,3,"AW00000003"));
            database.Customers.Add(new Records.Customer(4,null,null,null,"AW00000004"));
            database.SalesOrderHeaders.Add(new Records.SalesOrderHeader(1000,1,new DateTime(2008,6,1),5,125.50m));
            database.SalesOrderHeaders.Add(new Records.SalesOrderHeader(1001,1,new DateTime(2008,6,15),5,300.00m));
            database.SalesOrderHeaders.Add(new Records.SalesOrderHeader(1002,2,new DateTime(2008,7,1),5,42.75m));
            database.SalesOrderHeaders.Add(new Records.SalesOrderHeader(1003,3,new DateTime(2008,7,2),1,999.99m));
        }
    }

    [Collection("AdventureWorksCollection")]
    public sealed class AdventureWorksTests(AdventureWorksFixture fixture):IClassFixture<AdventureWorksFixture> {
        private AdventureWorks Database => fixture.Database;

        [Fact]
        public void ActiveCustomers_View_MatchesBaseline() {
            var expected = Baseline.ActiveCustomers(this.Database);
            var view = this.Database.ActiveCustomers();
            Assert.Equal("ActiveCustomers",view.Name);
            Assert.Equal("Sales.vActiveCustomer",view.SqlName);
            Assert.Equal(expected,view.Select(row => (row.CustomerId,row.StoreId)).ToArray());
            Assert.Same(view,this.Database.ActiveCustomers());
        }

        [Fact]
        public void StateProvinceCountryRegions_View_MatchesBaseline() {
            var expected = Baseline.StateProvinceCountryRegions(this.Database);
            var view = this.Database.StateProvinceCountryRegions();
            Assert.Equal("Person.vStateProvinceCountryRegion",view.SqlName);
            Assert.Equal(expected,view.Select(row => (row.StateProvinceId,row.StateProvinceName,row.CountryRegionCode,row.CountryRegionName)).ToArray());
        }

        [Fact]
        public void OrdersByCustomer_TableFunction_MatchesBaseline() {
            var expected = Baseline.OrdersByCustomer(this.Database,1);
            var function = this.Database.OrdersByCustomer(1);
            Assert.Equal("dbo.ufnOrdersByCustomer",function.SqlName);
            Assert.Equal(expected,function.Select(row => (row.SalesOrderId,row.TotalDue)).ToArray());
            {
                //一致しない顧客IDは空列挙になる
                Assert.Empty(this.Database.OrdersByCustomer(999).ToArray());
            }
        }

        [Fact]
        public void CustomerTotalDue_ScalarFunction_MatchesBaseline() {
            Assert.Equal(Baseline.CustomerTotalDue(this.Database,1),this.Database.CustomerTotalDue(1));
            Assert.Equal(0m,this.Database.CustomerTotalDue(999));
        }

        [Fact]
        public void DatabaseMetadata_FromGenerator() {
            Assert.Equal("AdventureWorks2008R2",this.Database.DatabaseName);
            var objects = this.Database.DatabaseObjects;
            Assert.Contains(objects,declaration => declaration is { Kind: global::Seamless.Database.DatabaseObjectKind.Table, MemberName: "Customers", SqlName: "Sales.Customer" });
            Assert.Contains(objects,declaration => declaration is { Kind: global::Seamless.Database.DatabaseObjectKind.View, MemberName: "ActiveCustomers", SqlName: "Sales.vActiveCustomer" });
            Assert.Contains(objects,declaration => declaration is { Kind: global::Seamless.Database.DatabaseObjectKind.TableFunction, MemberName: "OrdersByCustomer", SqlName: "dbo.ufnOrdersByCustomer" });
            Assert.Contains(objects,declaration => declaration is { Kind: global::Seamless.Database.DatabaseObjectKind.ScalarFunction, MemberName: "CustomerTotalDue", SqlName: "dbo.ufnCustomerTotalDue" });
            Assert.Equal(5,objects.Count(declaration => declaration.Kind==global::Seamless.Database.DatabaseObjectKind.Table));
        }

        [Fact]
        public void StorageMode_PersistsUnderStorageDirectory() {
            var directory = global::Seamless.Collections.TestPath.Directory<AdventureWorksTests>("storage");
            using(var database = new AdventureWorks(global::Seamless.Database.DatabaseContainerOptions.OnStorage(directory) with { IndexSize=1L<<12,MemorySize=1L<<24 })) {
                AdventureWorksFixture.Seed(database);
                var expected = Baseline.ActiveCustomers(database);
                Assert.Equal(expected,database.ActiveCustomers().Select(row => (row.CustomerId,row.StoreId)).ToArray());
                Assert.True(Directory.Exists(Path.Combine(directory,"Sales.Customer")));
            }
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
            public IEnumerable<TRecord> ReadTable<TRecord>(string sqlName) {
                if(sqlName=="Sales.Customer") {
                    return (IEnumerable<TRecord>)(object)new[] {
                        new Records.Customer(21,null,10,1,"AW00000021"),
                        new Records.Customer(22,102,null,2,"AW00000022"),
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
