namespace Seamless.Transactions.Collections;
/// <summary>実体化ビューの読取専用取引ビュー。操作は同じ取引の所有権・寿命に従う。</summary>
public sealed class ReadOnlyTransactionCollection<TKey,TRecord>(TransactionCollection<TKey,TRecord> collection):IEnumerable<TRecord> where TKey:notnull{
    public TRecord this[TKey key]=>collection[key];
    public bool TryGetValue(TKey key,out TRecord row)=>collection.TryGetValue(key,out row);
    public bool ContainsKey(TKey key)=>collection.ContainsKey(key);
    public IEnumerator<TRecord> GetEnumerator()=>collection.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
