using CommonStore=global::Seamless.Execution.Store;
using ResourceLimitException=global::Seamless.Execution.ResourceLimitException;
using PersistedExtensions=global::Seamless.Collections.Persisted.StoreExtensions;
namespace Test.Seamless.Persisted;
/// <summary>共通ストアから Persisted のコレクションを作る拡張メソッドの仕様。</summary>
public class StoreExtensions{
    private static string NewRoot(){
        var root=Path.Combine(Path.GetTempPath(),"seamless-persisted-store-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
    [Fact,Trait("Category","Unit")]public void CreatePersistedSet(){
        var root=NewRoot();
        try{
            {
                //一時ストア: ストアのフォルダの中に作られ、破棄でフォルダごと消える
                var store=CommonStore.CreateTemporary(temporaryDirectory:root);
                var directory=store.DirectoryPath!;
                var set=PersistedExtensions.CreatePersistedSet<long>(store,"ids");
                for(long i=0;i<1000;i++){
                    set.Add(i);
                }
                Assert.True(set.Contains(999));
                Assert.True(store.Budget.TemporaryStorageBytes>0);
                store.Dispose();
                Assert.True(set.IsDisposed);
                Assert.False(Directory.Exists(directory));
            }
            {
                //永続ストア: 保存した時点へ開き直せる。保存後の追加は捨てられ、索引も保存時点に戻る
                var directory=Path.Combine(root,"set");
                using(var store=CommonStore.Open(directory)){
                    var set=PersistedExtensions.CreatePersistedSet<long>(store,"ids");
                    for(long i=0;i<100;i++){
                        set.Add(i);
                    }
                    store.Save();
                    for(long i=100;i<150;i++){
                        set.Add(i);
                    }
                }
                using(var store=CommonStore.Open(directory)){
                    var set=PersistedExtensions.CreatePersistedSet<long>(store,"ids");
                    Assert.Equal(100,set.GetLongCount());
                    Assert.True(set.Contains(99));
                    Assert.False(set.Contains(120));
                    set.Add(120);
                    Assert.True(set.Contains(120));
                }
            }
            {
                //ディスクの上限を超えてファイルを伸ばすと ResourceLimitException。それまでの要素は読める
                using var store=CommonStore.CreateTemporary(new(){MemoryBytes=1L<<20,DiskBytes=1L<<18},root);
                var set=PersistedExtensions.CreatePersistedSet<long>(store,"ids");
                long added=0;
                var error=Assert.Throws<ResourceLimitException>(()=>{
                    for(;added<100_000_000;added++){
                        set.Add(added);
                    }
                });
                Assert.Equal(global::Seamless.Execution.ResourceLimitKind.TemporaryStorage,error.Kind);
                Assert.True(store.Budget.TemporaryStorageBytes<=1L<<18);
                Assert.True(set.Contains(added-1));
            }
            {
                //ファイルを使うので、メモリストアでは作れない
                using var store=CommonStore.CreateMemory();
                Assert.Throws<NotSupportedException>(()=>PersistedExtensions.CreatePersistedSet<long>(store,"ids"));
            }
            {
                //同じストアに Tsavorite と Persisted のコレクションを作り、1 回の保存でまとめて確定できる
                var directory=Path.Combine(root,"both");
                using(var store=CommonStore.Open(directory)){
                    using var tsavorite=store.CreateTsavoriteHashSet<long>("hot");
                    var persisted=PersistedExtensions.CreatePersistedSet<long>(store,"cold");
                    using(var set=tsavorite.Create()){
                        set.Add(1);
                    }
                    persisted.Add(2);
                    store.Save();
                    using(var set=tsavorite.Create()){
                        set.Add(3);
                    }
                    persisted.Add(4);
                }
                using(var store=CommonStore.Open(directory)){
                    using var tsavorite=store.CreateTsavoriteHashSet<long>("hot");
                    var persisted=PersistedExtensions.CreatePersistedSet<long>(store,"cold");
                    using var set=tsavorite.Create();
                    Assert.True(set.Contains(1));
                    Assert.False(set.Contains(3));
                    Assert.True(persisted.Contains(2));
                    Assert.False(persisted.Contains(4));
                }
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void CreatePersistedList(){
        var root=NewRoot();
        try{
            //固定長の要素でも、保存した時点へ開き直せる
            var directory=Path.Combine(root,"list");
            using(var store=CommonStore.Open(directory)){
                var list=PersistedExtensions.CreatePersistedList<long>(store,"values");
                for(long i=0;i<10;i++){
                    list.Add(i*10);
                }
                store.Save();
                list.Add(-1);
            }
            using(var store=CommonStore.Open(directory)){
                var list=PersistedExtensions.CreatePersistedList<long>(store,"values");
                Assert.Equal(10,list.Count);
                Assert.Equal(90,list[9]);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void CreatePersistedDictionary(){
        var root=NewRoot();
        try{
            //辞書も、保存した時点へ開き直せる
            var directory=Path.Combine(root,"dictionary");
            using(var store=CommonStore.Open(directory)){
                var dictionary=PersistedExtensions.CreatePersistedDictionary<string,int>(store,"names");
                dictionary.Add("a",1);
                dictionary.Add("b",2);
                store.Save();
                dictionary.Add("c",3);
            }
            using(var store=CommonStore.Open(directory)){
                var dictionary=PersistedExtensions.CreatePersistedDictionary<string,int>(store,"names");
                Assert.True(dictionary.TryGetValue("b",out var value));
                Assert.Equal(2,value);
                Assert.False(dictionary.ContainsKey("c"));
            }
        }finally{Directory.Delete(root,true);}
    }
}
