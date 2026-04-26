using System.Collections;
using System.Diagnostics;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ Dictionary。
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
[DebuggerTypeProxy(typeof(DictionaryDebugView<,,,>))]
public abstract partial class ManagedDictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>
    :ManagedManagedCollection<
        TKey,
        TValue,
        TKeySerializer,
        TValueSerializer
    >,
    ISeamlessDictionary<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public bool IsReadOnly => false;
    public void Add(KeyValuePair<TKey,TValue> item) => this.Add(item.Key,item.Value);
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
        var status = this.Read(item.Key,out var output);
        if(status.NotFound)
            return false;
        return output.Equals(item.Value);
    }
    public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount())
            throw new ArgumentException("The destination array is too small.");
        foreach(var kvp in this) {
            array[arrayIndex++]=kvp;
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => this.GetEnumerator();
    public global::System.Collections.Generic.List<KeyValuePair<TKey,TValue>> ToList() {
        using var Enumerator = this.GetEnumerator();
        var List = new global::System.Collections.Generic.List<KeyValuePair<TKey,TValue>>();
        while(Enumerator.MoveNext()) {
            List.Add(Enumerator.Current);
        }
        return List;
    }
    public ICollection<TKey> Keys => this.CreateCollection<KeyIterator,TKey>(new());
    public ICollection<TValue> Values => this.CreateCollection<ValueIterator,TValue>(new());
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.CreateCollection<KeyIterator,TKey>(new());
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.CreateCollection<ValueIterator,TValue>(new());
    public override bool IsDisposed => this.disposed;
    private bool disposed;
    protected override void Dispose(bool disposing) {
        if(this.disposed)
            return;
        this.disposed=true;
        if(disposing) {
            this.Remove_TKey_TValue_ClientSession.Dispose();
            this.Remove_KeyValuePair_ClientSession.Dispose();
        }
        base.Dispose(disposing);
    }
}
