using PersistedStore=global::Seamless.Collections.Persisted.Store;
namespace Test.Seamless.Persisted;
/// <summary>Persisted Set の関係演算が、全件をメモリに載せずに BCL の HashSet と同じ答えを返すことの仕様(重複を含む状態も含む)。</summary>
public class Set{
    // 自分側は重複を含む(Compact していない)。other は配列(集合ではない)・BCL の集合・Persisted の集合で渡す。
    private static readonly int[][] Selves=[[],[1],[1,1,2],[1,2,3,3,3],[5,6,7,8]];
    private static readonly int[][] Others=[[],[1],[1,2],[1,2,3],[1,2,3,4],[3,3,2,1],[9],[5,6,7,8,9]];
    private static void Check(Func<IReadOnlySet<int>,IEnumerable<int>,bool> actual,Func<global::System.Collections.Generic.HashSet<int>,IEnumerable<int>,bool> expected){
        foreach(var self in Selves){
            using var set=PersistedStore.CreateTempSet<int>();
            foreach(var item in self){
                set.Add(item);
            }
            var reference=new global::System.Collections.Generic.HashSet<int>(self);
            foreach(var other in Others){
                using var persistedOther=PersistedStore.CreateTempSet<int>();
                foreach(var item in other){
                    persistedOther.Add(item);
                }
                var label=$"self=[{string.Join(",",self)}] other=[{string.Join(",",other)}]";
                Assert.True(expected(reference,other)==actual(set,other),label+" (array)");
                Assert.True(expected(reference,other)==actual(set,new global::System.Collections.Generic.HashSet<int>(other)),label+" (BCL set)");
                Assert.True(expected(reference,other)==actual(set,persistedOther),label+" (persisted set)");
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void Constructor(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-persisted-set-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try{
            {
                //固定長の要素: 従来の BlittableSet で書いたファイルを、そのまま開いて読める
                var path=Path.Combine(directory,"blittable.dat");
                using(var old=new global::Seamless.Collections.Persisted.BlittableSet<int>(path)){
                    old.Add(1);
                    old.Add(2);
                }
                using var set=new global::Seamless.Collections.Persisted.Set<int>(path);
                Assert.Equal(2,set.GetLongCount());
                Assert.True(set.Contains(2));
                Assert.Equal([1,2],set.Order().ToArray());
            }
            {
                //参照を含む要素: 従来の MemoryPack の SerializedSet で書いたファイルを、そのまま開いて読める
                var path=Path.Combine(directory,"memorypack.dat");
                using(var old=new global::Seamless.Collections.Persisted.SerializedSet<string,global::Seamless.Collections.MemoryPackSerializer<string>>(path)){
                    old.Add("a");
                    old.Add("b");
                }
                using var set=new global::Seamless.Collections.Persisted.Set<string>(path);
                Assert.True(set.Contains("b"));
                Assert.Equal(["a","b"],set.Order(StringComparer.Ordinal).ToArray());
            }
        }finally{
            Directory.Delete(directory,true);
        }
    }
    [Fact,Trait("Category","Unit")]public void Count(){
        //件数は列挙される件数と常に一致する(FLinq・ValueLinq が TryGetNonEnumeratedCount で使う)。
        //作っている間は追記した件数(重複を含む)で、重複除去(CompactSave)のあとは異なる要素の数になる(INV-06)
        using var set=PersistedStore.CreateTempSet<int>();
        foreach(var item in new[]{3,1,3,2,1,3}){
            set.Add(item);
        }
        Assert.Equal(6,set.GetLongCount());
        Assert.Equal(set.GetLongCount(),set.AsEnumerable().LongCount());
        set.CompactSave();
        Assert.Equal(3,set.GetLongCount());
        Assert.Equal([1,2,3],set.Order().ToArray());
    }
    [Fact,Trait("Category","Unit")]public void IsProperSubsetOf()=>Check(static (set,other)=>set.IsProperSubsetOf(other),static (set,other)=>set.IsProperSubsetOf(other));
    [Fact,Trait("Category","Unit")]public void IsProperSupersetOf()=>Check(static (set,other)=>set.IsProperSupersetOf(other),static (set,other)=>set.IsProperSupersetOf(other));
    [Fact,Trait("Category","Unit")]public void IsSubsetOf()=>Check(static (set,other)=>set.IsSubsetOf(other),static (set,other)=>set.IsSubsetOf(other));
    [Fact,Trait("Category","Unit")]public void IsSupersetOf()=>Check(static (set,other)=>set.IsSupersetOf(other),static (set,other)=>set.IsSupersetOf(other));
    [Fact,Trait("Category","Unit")]public void Overlaps()=>Check(static (set,other)=>set.Overlaps(other),static (set,other)=>set.Overlaps(other));
    [Fact,Trait("Category","Unit")]public void SetEquals()=>Check(static (set,other)=>set.SetEquals(other),static (set,other)=>set.SetEquals(other));
}
