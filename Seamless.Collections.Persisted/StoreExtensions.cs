using System.Text.Json;
using Seamless.Execution;
namespace Seamless.Collections.Persisted;
/// <summary>
/// 共通ストア(<see cref="global::Seamless.Execution.Store"/>)から Persisted のコレクションを作る。
/// ファイルはストアのフォルダの中(名前ごとのフォルダ)に置かれ、ファイルが伸びるたびにストアのディスク上限で確認する。
/// 永続ストアでは <see cref="global::Seamless.Execution.Store.Save"/> した時点だけが確定し、開き直すとその時点へ戻る。
/// Persisted は書き込みを 1 スレッドで行う前提で、件数は呼び出し側が管理する(決定9)。
/// メモリストアでは、既定でメモリ上の仮想ファイルに置き(メモリの予算で確認する。保存はできない)、<see cref="PersistedMemoryBacking.TemporaryFolder"/> を選ぶと一時フォルダのファイルに置く(ディスクの予算で確認する)。
/// </summary>
public static partial class StoreExtensions{
    public static Set<T> CreatePersistedSet<T>(this global::Seamless.Execution.Store store,string name,long? initialCapacity=null,long? initialDataCapacity=null,StorageTuning? tuning=null,PersistedMemoryBacking memoryBacking=PersistedMemoryBacking.InMemory,long expectedCount=0){
        var profile=store.ResolveProfile(name,"Persisted",typeof(T).FullName??typeof(T).Name,expectedCount,explicitSettings:new(){InitialCapacity=initialCapacity,InitialDataBytes=initialDataCapacity},io:ProfileIO.Describe(tuning));
        var place=PreparePlace(store,name,memoryBacking);
        return Attach(store,name,place,()=>new Set<T>(Path.Combine(place.Directory,"items.dat"),profile.Settings.InitialCapacity!.Value,profile.Settings.InitialDataBytes!.Value,tuning),static set=>set.Committable,static (set,action)=>set.SetDisposeAction(action),static (set,action)=>set.SetSaveAction(action));
    }
    public static PersistedList<T> CreatePersistedList<T>(this global::Seamless.Execution.Store store,string name,long? initialCapacity=null,long? initialDataCapacity=null,StorageTuning? tuning=null,PersistedMemoryBacking memoryBacking=PersistedMemoryBacking.InMemory,long expectedCount=0){
        var profile=store.ResolveProfile(name,"Persisted",typeof(T).FullName??typeof(T).Name,expectedCount,explicitSettings:new(){InitialCapacity=initialCapacity,InitialDataBytes=initialDataCapacity},io:ProfileIO.Describe(tuning));
        var place=PreparePlace(store,name,memoryBacking);
        return Attach(store,name,place,()=>Store.CreateList<T>(Path.Combine(place.Directory,"items.dat"),profile.Settings.InitialCapacity!.Value,profile.Settings.InitialDataBytes!.Value,tuning),static list=>list,static (list,action)=>list.SetDisposeAction(action),static (list,action)=>{});
    }
    public static Dictionary<TKey,TValue> CreatePersistedDictionary<TKey,TValue>(this global::Seamless.Execution.Store store,string name,long? initialCapacity=null,long? initialDataCapacity=null,StorageTuning? tuning=null,PersistedMemoryBacking memoryBacking=PersistedMemoryBacking.InMemory,long expectedCount=0)
        where TKey:notnull{
        var profile=store.ResolveProfile(name,"Persisted",$"{typeof(TKey).FullName}|{typeof(TValue).FullName}",expectedCount,explicitSettings:new(){InitialCapacity=initialCapacity,InitialDataBytes=initialDataCapacity},io:ProfileIO.Describe(tuning));
        var place=PreparePlace(store,name,memoryBacking);
        return Attach(store,name,place,()=>new Dictionary<TKey,TValue>(Path.Combine(place.Directory,"items.dat"),profile.Settings.InitialCapacity!.Value,profile.Settings.InitialDataBytes!.Value,tuning),static dictionary=>dictionary.Committable,static (dictionary,action)=>dictionary.SetDisposeAction(action),static (dictionary,action)=>dictionary.SetSaveAction(action));
    }
    /// <summary>ファイルの置き場(ディレクトリまたは仮想パスの根)と、使う予算の種類、破棄時の後始末。</summary>
    private sealed record Place(string Directory,bool UsesMemoryBudget,Action? Cleanup);
    private static Place PreparePlace(global::Seamless.Execution.Store store,string name,PersistedMemoryBacking memoryBacking){
        ArgumentNullException.ThrowIfNull(store);
        if(store.Kind!=StoreKind.Memory){
            return new(PrepareDirectory(store,name),false,null);
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if(memoryBacking==PersistedMemoryBacking.InMemory){
            var root=PersistedVirtualFiles.CreateRoot();
            return new(root,true,()=>PersistedVirtualFiles.DeleteRoot(root));
        }
        var temporary=Path.Combine(Path.GetTempPath(),"Seamless","PersistedMemoryStore","persisted-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        return new(temporary,false,()=>{
            try{
                Directory.Delete(temporary,true);
            }catch(IOException){
            }catch(UnauthorizedAccessException){
            }
        });
    }
    private static string PrepareDirectory(global::Seamless.Execution.Store store,string name){
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
    private static TCollection Attach<TCollection>(global::Seamless.Execution.Store store,string name,Place place,Func<TCollection> create,Func<TCollection,IPersistedCommittable> committable,Action<TCollection,Action?> setDisposeAction,Action<TCollection,Action?> setSaveAction)
        where TCollection:IDisposable{
        ResourceReservation disk;
        if(place.UsesMemoryBudget){
            disk=store.Budget.ReserveMemory(0);
        }else{
            disk=store.Budget.ReserveTemporaryStorage(0);
        }
        TCollection? collection=default;
        try{
            collection=create();
            var component=new PersistedComponent(store,name,collection,committable(collection),disk,place.Cleanup);
            setDisposeAction(collection,component.Released);
            setSaveAction(collection,component.SaveStore);
            component.Attach();
            component.EnsureTotalBytes(committable(collection).StorageBytes);
            store.Register(component);
            return collection;
        }catch{
            collection?.Dispose();
            disk.Dispose();
            place.Cleanup?.Invoke();
            throw;
        }
    }
}
/// <summary>Persisted のコレクション 1 つを、共通ストアの子資源として保存・復元・ディスク予算へつなぐ。</summary>
internal sealed class PersistedComponent(global::Seamless.Execution.Store owner,string name,IDisposable collection,IPersistedCommittable committable,ResourceReservation disk,Action? cleanup):IStoreComponent,IPersistedStorageQuota{
    public string Name=>name;
    public string Save()=>JsonSerializer.Serialize(committable.Commit());
    // CompactSave が、書き換えたファイルの状態をストアの保存として確定するために呼ぶ。
    internal void SaveStore(){
        owner.Save();
    }
    // Commit が既にディスクへ確実に書き出しているので、ここですることは無い。
    public void Flush(){}
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
        cleanup?.Invoke();
    }
    internal void Attach()=>committable.StorageQuota=this;
}
