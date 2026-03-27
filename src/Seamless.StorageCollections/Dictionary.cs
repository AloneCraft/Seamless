using System.Collections;
using System.Diagnostics.CodeAnalysis;
namespace Seamless.StorageCollections;

public class Dictionary<TKey,TValue>:IDictionary<TKey,TValue>{
    public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator(){
        throw new NotImplementedException();
    }
    IEnumerator IEnumerable.GetEnumerator(){
        return this.GetEnumerator();
    }
    public void Add(KeyValuePair<TKey,TValue> item){
        throw new NotImplementedException();
    }
    public void Clear(){
        throw new NotImplementedException();
    }
    public bool Contains(KeyValuePair<TKey,TValue> item){
        throw new NotImplementedException();
    }
    public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex){
        throw new NotImplementedException();
    }
    public bool Remove(KeyValuePair<TKey,TValue> item){
        throw new NotImplementedException();
    }
    public int Count{
        get;
    }
    public bool IsReadOnly{
        get;
    }
    public void Add(TKey key,TValue value){
        throw new NotImplementedException();
    }
    public bool ContainsKey(TKey key){
        throw new NotImplementedException();
    }
    public bool Remove(TKey key){
        throw new NotImplementedException();
    }
    public bool TryGetValue(TKey key,[MaybeNullWhen(false)]out TValue value){
        throw new NotImplementedException();
    }
    public TValue this[TKey key]{
        get=>
            throw new NotImplementedException();
        set=>
            throw new NotImplementedException();
    }
    public ICollection<TKey> Keys{
        get;
    }
    public ICollection<TValue> Values{
        get;
    }
}
[Flags]
enum  ggg{}
//public static class Factory{
//    public static Dictionary<TKey,TValue> CreateDictionary<TKey,TValue>(
//        StorageConfiguration config,
//        string name
//    ){
//        // 1. Tsavoriteのデバイス・ログ設定の構築
//        // 2. MemoryPackシリアライザのセットアップ
//        // 3. StorageDictionaryの実装クラスを生成して返す
//        return new TsavoriteDictionary<TKey,TValue>(config,name);
//    }
//}
