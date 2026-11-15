using System.Linq;
using Seamless.Linq;

namespace Test.Seamless.AdventureWorks2008R2;

// ビュー・関数は、生成される入口(AdventureWorks.IReader)への拡張メンバー。ビューはプロパティ、関数はメソッド。
public static class AdventureWorksQueries {
    extension(AdventureWorks.IReader q) {
        // Sales.vActiveCustomer 相当: Store を持つ顧客だけを射影する。
        public global::Seamless.Collections.ISet<Rows.ActiveCustomer> ActiveCustomers=>
            q.Customers
                .Where(customer => customer.StoreId.HasValue)
                .Select(customer => new Rows.ActiveCustomer(customer.CustomerId,customer.StoreId!.Value));

        // Person.vStateProvinceCountryRegion 相当: join ビュー。
        public global::Seamless.Collections.ISet<Rows.StateProvinceCountryRegion> StateProvinceCountryRegions=>
            q.StateProvinces
                .Join(q.CountryRegions,state => state.CountryRegionCode,region => region.CountryRegionCode,(state,region) => new Rows.StateProvinceCountryRegion(state.StateProvinceId,state.Name,region.CountryRegionCode,region.Name));

        // dbo.ufnOrdersByCustomer 相当: テーブル関数。
        public global::Seamless.Collections.ISet<Rows.SalesOrderSummary> OrdersByCustomer(int customerId)=>
            q.SalesOrderHeaders
                .Where(order => order.CustomerId==customerId)
                .Select(order => new Rows.SalesOrderSummary(order.SalesOrderId,order.TotalDue));
    }
}
