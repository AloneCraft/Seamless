using global::System.Linq;
using global::Seamless.Linq;

namespace Test.Seamless.Database.Spec;

// ビュー・関数は、生成される入口(SpecDatabase.IReader)への拡張メンバー。ビューはプロパティ、関数はメソッド。
public static class SpecDatabaseQueries {
    extension(SpecDatabase.IReader q) {
        public global::Seamless.Collections.ISet<Views.ItemNames> ItemNames=>q.Items.Select(item=>new Views.ItemNames(item.Name));
        public global::Seamless.Collections.ISet<Tables.Items> ItemsAbove(int minimumId)=>q.Items.Where(item=>item.Id>=minimumId);
        public int ItemCount()=>q.Items.Count();
    }
}
