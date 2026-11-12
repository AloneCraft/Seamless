using Seamless.FLinq;
using Seamless.Linq;

namespace Test.Seamless.Tsavorite.Linq.Enumerable;

// Contains の provider 特殊化オーバーロード（Seamless.Linq/Linq/Contains.cs）の回帰テスト。
// membership test は IValueEnumerator の default interface method ではなく型特殊化で表現している。
// DIM にすると値型が constrained. callvirt で box されるため（実測 24 バイト/呼び出し）。
// FLinq に傍受されると検証対象が生成コード側へ移ってしまうので、傍受を止めて素の
// Seamless.Linq 経路だけを見る。
[LinqIntercept(false)]
public sealed class ProviderContainsTests {
    public readonly record struct Row(int Key,int Value):global::Seamless.Collections.IRecord<int>;

    [Fact, Trait("Category","Unit")]
    public void Contains_value() {
        {
            //HashSet の根が provider のとき、存在・非存在を集合自身の lookup で返すテスト
            using var store = Store.Create().CreateHashSet<int>();
            using var set = store.Create();
            set.Add(1);
            set.Add(2);
            set.Add(3);

            Assert.True(set.AsValueEnumerable().Contains(2));
            Assert.False(set.AsValueEnumerable().Contains(99));
        }
        {
            //HashSet に中間段が挟まると汎用の全走査へ落ちるが、結果は同じテスト
            using var store = Store.Create().CreateHashSet<int>();
            using var set = store.Create();
            set.Add(1);
            set.Add(2);
            set.Add(3);

            Assert.True(set.AsValueEnumerable().Where(static 値 => 値>1).Contains(3));
            Assert.False(set.AsValueEnumerable().Where(static 値 => 値>1).Contains(1));
        }
        {
            //キー付き Set の根が provider のとき、存在・非存在をレコード単位で返すテスト
            using var store = Store.Create().CreateSet<int,Row>();
            using var set = store.Create();
            set.Add(new(1,10));
            set.Add(new(2,20));

            Assert.True(set.AsValueEnumerable().Contains(new Row(2,20)));
            Assert.False(set.AsValueEnumerable().Contains(new Row(9,90)));
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_value_comparer() {
        using var store = Store.Create().CreateHashSet<int>();
        using var set = store.Create();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        {
            //comparer 指定時は集合自身の等価ではなく comparer が使われるテスト
            Assert.True(set.AsValueEnumerable().Contains(92,new 下一桁比較器()));
            Assert.False(set.AsValueEnumerable().Contains(99,new 下一桁比較器()));
        }
        {
            //comparer に null を渡すと既定の等価比較になるテスト
            Assert.True(set.AsValueEnumerable().Contains(2,null));
            Assert.False(set.AsValueEnumerable().Contains(92,null));
        }
    }

    // 下一桁だけを見る比較器。集合自身の等価（値そのもの）と結果が食い違う入力を作るために使う。
    private sealed class 下一桁比較器:IEqualityComparer<int> {
        public bool Equals(int x,int y) => x%10==y%10;
        public int GetHashCode(int obj) => (obj%10).GetHashCode();
    }
}
