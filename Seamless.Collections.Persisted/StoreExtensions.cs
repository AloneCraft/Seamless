using System.Text.Json;
using Seamless.Execution;
namespace Seamless.Collections.Persisted;
/// <summary>
/// 共通ストア(<see cref="global::Seamless.Execution.Store"/>)から Persisted のコレクションを作る。
/// ファイルはストアのフォルダの中(名前ごとのフォルダ)に置かれ、ファイルが伸びるたびにストアのディスク上限で確認する。
/// 永続ストアでは <see cref="global::Seamless.Execution.Store.Save"/> した時点だけが確定し、開き直すとその時点へ戻る。
/// Persisted は書き込みを 1 スレッドで行う前提で、件数は呼び出し側が管理する(決定9)。メモリストアでは使えない。
/// </summary>
public static class StoreExtensions{
    public static Set<T> CreatePersistedSet<T>(this global::Seamless.Execution.Store store,string name,long initialCapacity=16,long initialDataCapacity=4096){
        var directory=PrepareDirectory(store,name);
        return Attach(store,name,()=>new Set<T>(Path.Combine(directory,"items.dat"),initialCapacity,initialDataCapacity),static set=>set.Committable,static (set,action)=>set.SetDisposeAction(action));
    }
    public static PersistedList<T> CreatePersistedList<T>(this global::Seamless.Execution.Store store,string name,long initialCapacity=16,long initialDataCapacity=4096){
        var directory=PrepareDirectory(store,name);
        return Attach(store,name,()=>Store.CreateList<T>(Path.Combine(directory,"items.dat"),initialCapacity,initialDataCapacity),static list=>list,static (list,action)=>list.SetDisposeAction(action));
    }
    public static Dictionary<TKey,TValue> CreatePersistedDictionary<TKey,TValue>(this global::Seamless.Execution.Store store,string name,long initialCapacity=16,long initialDataCapacity=4096)
        where TKey:notnull{
        var directory=PrepareDirectory(store,name);
        return Attach(store,name,()=>new Dictionary<TKey,TValue>(Path.Combine(directory,"items.dat"),initialCapacity,initialDataCapacity),static dictionary=>dictionary.Committable,static (dictionary,action)=>dictionary.SetDisposeAction(action));
    }
    private static string PrepareDirectory(global::Seamless.Execution.Store store,string name){
        ArgumentNullException.ThrowIfNull(store);
        if(store.Kind==StoreKind.Memory){
            throw new NotSupportedException("Persisted collections are file-backed and cannot be created in a memory store.");
        }
        var directory=store.GetComponentDirectory(name);
        if(!store.TryGetSavedState(name,out _)){
            // 保存されていない状態は確定していないので、前回の書きかけは捨てて新しく始める。
            foreach(var child in Directory.EnumerateDirectories(directory)){
                Directory.Delete(child,true);
            }
            foreach(var file in Directory.EnumerateFiles(directory)){
                File.Delete(file);
            }
        }
        return directory;
    }
    private static TCollection Attach<TCollection>(global::Seamless.Execution.Store store,string name,Func<TCollection> create,Func<TCollection,IPersistedCommittable> committable,Action<TCollection,Action?> setDisposeAction)
        where TCollection:IDisposable{
        var disk=store.Budget.ReserveTemporaryStorage(0);
        TCollection? collection=default;
        try{
            collection=create();
            var component=new PersistedComponent(store,name,collection,committable(collection),disk);
            setDisposeAction(collection,component.Released);
            component.Attach();
            component.EnsureTotalBytes(committable(collection).StorageBytes);
            store.Register(component);
            return collection;
        }catch{
            collection?.Dispose();
            disk.Dispose();
            throw;
        }
    }
}
/// <summary>Persisted のコレクション 1 つを、共通ストアの子資源として保存・復元・ディスク予算へつなぐ。</summary>
internal sealed class PersistedComponent(global::Seamless.Execution.Store owner,string name,IDisposable collection,IPersistedCommittable committable,ResourceReservation disk):IStoreComponent,IPersistedStorageQuota{
    public string Name=>name;
    public string Save()=>JsonSerializer.Serialize(committable.Commit());
    public void Load(string state){
        committable.RollbackTo(JsonSerializer.Deserialize<PersistedCommitState>(state));
    }
    public void EnsureTotalBytes(long totalBytes){
        if(totalBytes>disk.Bytes){
            disk.Resize(totalBytes);
        }
    }
    public void Dispose()=>collection.Dispose();
    // コレクションが破棄されたとき(利用者の破棄でもストアの破棄でも)に呼ばれる。
    internal void Released(){
        owner.Unregister(this);
        disk.Dispose();
    }
    internal void Attach()=>committable.StorageQuota=this;
}
