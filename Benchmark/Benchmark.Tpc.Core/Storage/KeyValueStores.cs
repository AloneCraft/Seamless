using PersistedCollections=Seamless.Collections.Persisted;
using TsavoriteCollections=Seamless.Collections.Tsavorite;
namespace Benchmark.Tpc.Core;
/// <summary>索引表を置くキーと値の入れ物。保存先ごとに実装する(LINQ は使わない)。</summary>
public interface IKeyValueStore<TKey,TValue>:IDisposable
    where TKey:unmanaged
    where TValue:unmanaged{
    string Name{get;}
    bool TryGet(TKey key,out TValue value);
    /// <summary>追加または上書き。</summary>
    void Put(TKey key,TValue value);
    /// <summary>消す。無ければ false。</summary>
    bool Remove(TKey key);
    /// <summary>生きている項目を順不同で列挙する。</summary>
    IEnumerable<KeyValuePair<TKey,TValue>> Enumerate();
}
/// <summary>Tsavorite の辞書に置いた入れ物。</summary>
public sealed class TsavoriteKeyValueStore<TKey,TValue>:IKeyValueStore<TKey,TValue>,IHandleOwner
    where TKey:unmanaged
    where TValue:unmanaged{
    private readonly TsavoriteCollections.DictionaryStore<TKey,TValue> data;
    private TsavoriteCollections.IDictionary<TKey,TValue>? handle;
    public TsavoriteKeyValueStore(TsavoriteCollections.DictionaryStore<TKey,TValue> data,string name){
        this.data=data;
        this.Name=name;
        this.handle=data.Create();
    }
    public string Name{get;}
    private TsavoriteCollections.IDictionary<TKey,TValue> Handle=>this.handle??throw new InvalidOperationException($"{this.Name}: the handle is released.");
    public bool TryGet(TKey key,out TValue value)=>this.Handle.TryGetValue(key,out value);
    public void Put(TKey key,TValue value){
        this.Handle[key]=value;
    }
    public bool Remove(TKey key)=>this.Handle.Remove(key);
    public IEnumerable<KeyValuePair<TKey,TValue>> Enumerate(){
        foreach(var pair in this.Handle){
            yield return pair;
        }
    }
    public void ReleaseHandle(){
        this.handle?.Dispose();
        this.handle=null;
    }
    public void AcquireHandle(){
        this.handle??=this.data.Create();
    }
    public void Dispose()=>this.ReleaseHandle();
}
/// <summary>Persisted の辞書に置いた入れ物。削除は墓標を追記する。</summary>
public sealed class PersistedKeyValueStore<TKey,TValue>(PersistedCollections.Dictionary<TKey,Slot<TValue>> data,string name):IKeyValueStore<TKey,TValue>
    where TKey:unmanaged
    where TValue:unmanaged{
    public string Name{get;}=name;
    public bool TryGet(TKey key,out TValue value){
        if(data.TryGetValue(key,out var slot)&&slot.Live){
            value=slot.Row;
            return true;
        }
        value=default;
        return false;
    }
    public void Put(TKey key,TValue value){
        data.Add(key,new(true,value));
    }
    public bool Remove(TKey key){
        if(!this.TryGet(key,out _)){
            return false;
        }
        data.Add(key,new(false,default));
        return true;
    }
    public IEnumerable<KeyValuePair<TKey,TValue>> Enumerate(){
        foreach(var pair in data){
            if(pair.Value.Live){
                yield return new(pair.Key,pair.Value.Row);
            }
        }
    }
    public void Dispose(){
        // 実体は共通ストアが破棄する。
    }
}
