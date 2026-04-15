using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ Dictionary。
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
[DebuggerTypeProxy(typeof(DebugView<,>))]
public partial class UnmanagedDictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>:UnmanagedCollection<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>,
ISeamlessDictionary<TKey,TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public bool IsReadOnly => false;
    //public TryAddResult TryAdd(TKey key,TValue value) => this.PrivateTryAdd(key,value);

    //public void Add(TKey key,TValue value) {
    //    var result=this.TryAdd(key,value);
    //    switch(result) {
    //        case TryAddResult.LogFull:
    //            throw this.ThrowLogFull();
    //        case TryAddResult.KeyAlreadyExists:
    //            throw new ArgumentException($"An item with the same key has already been added. Key: {key}");
    //    }
    //}

    public void Add(KeyValuePair<TKey,TValue> item) => this.Add(item.Key,item.Value);

    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
        var status = this.Read(item.Key,out var output);
        if(status.NotFound) return false;
        return output.Equals(item.Value);
    }

    //public bool ContainsKey2(TKey Key) =>base.ContainsKey() this.PrivateProtectedContainsKey(TKeySerializer.Serialize(ref Key));
    //public bool ContainsKey(TKey key)=>this.Contains(key);

    public partial bool Remove(KeyValuePair<TKey,TValue> item);
    public partial bool Remove(TKey key,out TValue value);

    public override bool IsDisposed => this.disposed;
    private bool disposed;

    protected override void Dispose(bool disposing) {
        if(this.disposed) {
            return;
        }
        this.disposed=true;
        if(disposing) {
            this.Remove_TKey_TValue_ClientSession.Dispose();
            this.Remove_KeyValuePair_ClientSession.Dispose();
        }
        base.Dispose(disposing);
    }

    public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if(array.Length-arrayIndex<this.GetLongCount()) {
            throw new ArgumentException("The destination array is too small.");
        }
        foreach(var kvp in this) {
            array[arrayIndex++]=kvp;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    IEnumerator<KeyValuePair<TKey,TValue>> IEnumerable<KeyValuePair<TKey,TValue>>.GetEnumerator() => this.GetEnumerator();


    public ICollection<TKey> Keys => this.CreateCollection<KeyIterator,TKey>(new());
    public ICollection<TValue> Values => this.CreateCollection<ValueIterator,TValue>(new());
    IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys => this.Enumerate<KeyIterator,TKey>(new());
    IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values => this.Enumerate<ValueIterator,TValue>(new());
    private partial void SetValue(TKey key,TValue value);
    public TValue this[TKey key] {
        get {
            if(this.TryGetValue(key,out var value)) {
                return value;
            }
            throw new KeyNotFoundException();
        }
        set => this.SetValue(key,value);
    }
}
