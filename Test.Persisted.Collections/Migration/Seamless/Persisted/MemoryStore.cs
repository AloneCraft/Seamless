using CommonStore=global::Seamless.Execution.Store;
using ResourceLimitException=global::Seamless.Execution.ResourceLimitException;
using PersistedExtensions=global::Seamless.Collections.Persisted.StoreExtensions;
namespace Test.Seamless.Persisted;
/// <summary>メモリストア(ディスクを使わない共通ストア)で作る Persisted のコレクションの仕様。メモリ上の仮想ファイルと一時フォルダの両方。</summary>
public class MemoryStore{
    [Fact,Trait("Category","Unit")]public void InMemory(){
        {
            //既定はメモリ上の仮想ファイル。追加・索引の拡大・読み取りができ、メモリの予算で確認し、ディスクの予算は使わない。破棄すると予算も返る
            using var store=CommonStore.CreateMemory();
            var set=PersistedExtensions.CreatePersistedSet<long>(store,"ids");
            for(long i=0;i<50_000;i++){
                set.Add(i);
            }
            Assert.Equal(50_000,set.Count);
            Assert.True(set.Contains(0));
            Assert.True(set.Contains(49_999));
            Assert.False(set.Contains(50_000));
            Assert.True(store.Budget.MemoryBytes>0);
            Assert.Equal(0,store.Budget.TemporaryStorageBytes);
            set.Dispose();
            Assert.Equal(0,store.Budget.MemoryBytes);
        }
        {
            //List と Dictionary も同じ
            using var store=CommonStore.CreateMemory();
            using var list=PersistedExtensions.CreatePersistedList<int>(store,"items");
            using var dictionary=PersistedExtensions.CreatePersistedDictionary<int,string>(store,"names");
            for(var i=0;i<20_000;i++){
                list.Add(i*2);
                dictionary.Add(i,"v"+i);
            }
            Assert.Equal(20_000,list.Count);
            Assert.Equal(19_999*2,list[19_999]);
            Assert.True(dictionary.TryGetValue(12_345,out var value));
            Assert.Equal("v12345",value);
            Assert.False(dictionary.TryGetValue(-1,out _));
        }
        {
            //同じ名前を 2 つ作れない・メモリストアは保存できない
            using var store=CommonStore.CreateMemory();
            using var set=PersistedExtensions.CreatePersistedSet<long>(store,"ids");
            Assert.Throws<ArgumentException>(()=>PersistedExtensions.CreatePersistedSet<long>(store,"ids"));
            Assert.Throws<NotSupportedException>(()=>store.Save());
        }
        {
            //メモリの上限を超える追加は ResourceLimitException
            using var store=CommonStore.CreateMemory(new(){MemoryBytes=1L<<20});
            using var set=PersistedExtensions.CreatePersistedSet<long>(store,"ids");
            var error=Assert.Throws<ResourceLimitException>(()=>{
                for(long i=0;i<100_000_000;i++){
                    set.Add(i);
                }
            });
            Assert.Equal(global::Seamless.Execution.ResourceLimitKind.Memory,error.Kind);
            Assert.True(store.Budget.MemoryBytes<=1L<<20);
        }
    }
    [Fact,Trait("Category","Unit")]public void TemporaryFolder(){
        //一時フォルダを選ぶと、ディスクの予算で確認し、メモリの予算は使わない。破棄すると予算が返る
        using var store=CommonStore.CreateMemory();
        var set=PersistedExtensions.CreatePersistedSet<long>(store,"ids",memoryBacking:global::Seamless.Collections.Persisted.PersistedMemoryBacking.TemporaryFolder);
        for(long i=0;i<50_000;i++){
            set.Add(i);
        }
        Assert.Equal(50_000,set.Count);
        Assert.True(set.Contains(49_999));
        Assert.True(store.Budget.TemporaryStorageBytes>0);
        Assert.Equal(0,store.Budget.MemoryBytes);
        set.Dispose();
        Assert.Equal(0,store.Budget.TemporaryStorageBytes);
    }
    [Fact,Trait("Category","Unit")]public void StoreDispose_ReleasesInMemoryFiles(){
        //ストアを破棄しても(コレクションを破棄していなくても)、メモリ上の仮想ファイルは解放される
        var store=CommonStore.CreateMemory();
        PersistedExtensions.CreatePersistedSet<long>(store,"ids").Add(1);
        store.Dispose();
        Assert.Equal(0,store.Budget.MemoryBytes);
    }
}
