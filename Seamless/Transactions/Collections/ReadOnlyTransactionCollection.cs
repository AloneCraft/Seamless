using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Seamless.Transactions.Collections;
/// <summary>実体化ビューと、IReader から見た表の読取専用取引ビュー(読取専用の集合)。操作は同じ取引の所有権・寿命に従う。</summary>
public sealed class ReadOnlyTransactionCollection<TKey,TRecord>(TransactionCollection<TKey,TRecord> collection):global::Seamless.Collections.ISet<TRecord> where TKey:notnull{
    public TransactionRows<TKey,TRecord> Rows=>collection.Rows;
    public TRecord this[TKey key]=>collection[key];
    public bool TryGetValue(TKey key,[global::System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out TRecord row)=>collection.TryGetValue(key,out row);
    public bool ContainsKey(TKey key)=>collection.ContainsKey(key);
    public bool Contains(TRecord row)=>collection.Contains(row);
    // [件数] 走査で数える(O(n))。
    public int Count{get{var count=collection.CountRows();return count>int.MaxValue?throw new OverflowException("行数が Int32 の範囲を超えています。GetLongCount を使ってください。"):(int)count;}}
    public long GetLongCount()=>collection.CountRows();
    public IEnumerator<TRecord> GetEnumerator()=>collection.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
