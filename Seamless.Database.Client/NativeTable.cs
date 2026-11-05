using System.Collections;

namespace Seamless.Database.Client;

/// <summary>公開DB表現から参照する型付き操作。サーバー内部のコンテナー型を公開しない。</summary>
public abstract class NativeTable<TKey,TRecord>:IEnumerable<TRecord> where TKey:struct where TRecord:struct{
    public abstract bool Add(TRecord record);
    public abstract bool Remove(TKey key);
    public abstract bool Replace(TKey key,TRecord record);
    public abstract TRecord this[TKey key]{get;set;}
    public abstract IEnumerator<TRecord> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
