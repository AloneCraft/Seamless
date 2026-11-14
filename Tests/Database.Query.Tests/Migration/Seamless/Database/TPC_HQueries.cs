using System.Linq;
using Seamless.Linq;

// ReSharper disable InconsistentNaming
namespace Test.Seamless.Database.TPC_H;

// ビュー・関数は、生成される入口(Container.IReader)への拡張メンバー。ビューはプロパティ、関数はメソッド。
public static class TpchQueries {
    extension(Container.IReader q) {
        public global::Seamless.Collections.ISet<Views.NationNames> NationNames=>
            q.nations.Select(nation => new Views.NationNames(nation.n_name));
        public global::Seamless.Collections.ISet<int> OrdersByCustomer(int customerKey)=>
            q.orders.Where(order => order.o_custkey==customerKey).Select(order => order.o_orderkey);
    }
}
