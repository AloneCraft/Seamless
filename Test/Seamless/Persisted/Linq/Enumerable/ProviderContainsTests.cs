using Seamless.FLinq;
using Seamless.Linq;

namespace Test.Seamless.Persisted.Linq.Enumerable;

// Contains の provider 特殊化オーバーロード（Seamless.Linq/Linq/Contains.cs）の回帰テスト。
// membership test は IValueEnumerator の default interface method ではなく型特殊化で表現している。
// DIM にすると値型が constrained. callvirt で box されるため（実測 24 バイト/呼び出し）。
// FLinq に傍受されると検証対象が生成コード側へ移ってしまうので、傍受を止めて素の
// Seamless.Linq 経路だけを見る。
[LinqIntercept(false)]
public sealed class ProviderContainsTests {
    [Fact, Trait("Category","Unit")]
    public void Contains_value() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Persisted.Collections.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 }) {
                set.Add(value);
            }
            set.Compact();

            {
                //根が provider のとき、集合自身の O(1) lookup で存在を返すテスト
                Assert.True(set.AsValueEnumerable().Contains(3));
            }
            {
                //根が provider のとき、非存在を返すテスト
                Assert.False(set.AsValueEnumerable().Contains(99));
            }
            {
                //中間段が挟まると provider 特殊化は選ばれず汎用の全走査へ落ちるが、結果は同じテスト
                Assert.True(set.AsValueEnumerable().Where(static 値 => 値>2).Contains(3));
                Assert.False(set.AsValueEnumerable().Where(static 値 => 値>2).Contains(1));
            }
        } finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Contains_value_comparer() {
        var path = CreatePath();
        try {
            using var set = global::Seamless.Persisted.Collections.Store.CreateSet<int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
            foreach(var value in new[] { 1,2,3,4 }) {
                set.Add(value);
            }
            set.Compact();

            {
                //comparer 指定時は集合自身の等価ではなく comparer が使われるテスト
                //93 は集合に無いが下一桁 3 が一致するので真、95 は下一桁 5 が一致しないので偽
                Assert.True(set.AsValueEnumerable().Contains(93,new 下一桁比較器()));
                Assert.False(set.AsValueEnumerable().Contains(95,new 下一桁比較器()));
            }
            {
                //comparer に null を渡すと既定の等価比較になるテスト
                Assert.True(set.AsValueEnumerable().Contains(3,null));
                Assert.False(set.AsValueEnumerable().Contains(99,null));
            }
        } finally {
            TryDeleteDirectory(path);
        }
    }

    // 下一桁だけを見る比較器。集合自身の等価（値そのもの）と結果が食い違う入力を作るために使う。
    private sealed class 下一桁比較器:IEqualityComparer<int> {
        public bool Equals(int x,int y) => x%10==y%10;
        public int GetHashCode(int obj) => (obj%10).GetHashCode();
    }

    private static string CreatePath() {
        var directoryPath = TestPath.Directory<ProviderContainsTests>("provider-contains");
        return Path.Combine(directoryPath,"items.dat");
    }

    private static void TryDeleteDirectory(string path) {
        var directoryPath = Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath)) {
            return;
        }
        try {
            Directory.Delete(directoryPath,recursive: true);
        } catch(IOException) {
        } catch(UnauthorizedAccessException) {
        }
    }
}
