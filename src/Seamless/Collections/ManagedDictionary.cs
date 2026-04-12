using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ Dictionary。
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public abstract partial class ManagedDictionary<
    TKey,
    TValue,
    TKeySerializer,
    TValueSerializer
>
    :ManagedCollection<
        TKey,
        TValue,
        TKeySerializer,
        TValueSerializer
    >,
    ISeamlessDictionary<TKey,TValue>
	where TKey :IEquatable<TKey>
	where TValue : IEquatable<TValue>
	where TKeySerializer : ISerializer<TKey>
	where TValueSerializer : ISerializer<TValue>
{
	public bool IsReadOnly => false;
    public override bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        var Status = this.Read(Key,out Value);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        return true;
    }
	public TryAddResult TryAdd(TKey Key,TValue Value) =>
		this.PrivateTryAdd(Key,Value);
	public void Add(TKey Key,TValue Value) {
        var Result = this.TryAdd(Key,Value);
		switch(Result) {
			case TryAddResult.LogFull:
				throw this.ThrowLogFull();
			case TryAddResult.KeyAlreadyExists: throw new ArgumentException($"An item with the same key has already been added. Key: {Key}");
		}
	}
	public void Add(KeyValuePair<TKey,TValue> item) => this.Add(item.Key,item.Value);
    bool ICollection<KeyValuePair<TKey,TValue>>.Contains(KeyValuePair<TKey,TValue> item) {
        var Status = this.Read(item.Key,out _);
        Debug.Assert(Status.Found==!Status.NotFound);
        return Status.Found;
    }

	public bool ContainsKey(TKey Key)=>
		this.Contains(Key);
	public void CopyTo(KeyValuePair<TKey,TValue>[] array,int arrayIndex){
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
	public List<KeyValuePair<TKey,TValue>> ToList(){
		using var Enumerator=this.GetEnumerator();
		var List=new List<KeyValuePair<TKey,TValue>>();
		while(Enumerator.MoveNext()){
			List.Add(Enumerator.Current);
		}
		return List;
	}
	public ICollection<TKey> Keys=>this.CreateCollection<KeyIterator<TKey,TKeySerializer>,TKey>(new());
	public ICollection<TValue> Values=>this.CreateCollection<ValueIterator<TValue,TValueSerializer>,TValue>(new());
	IEnumerable<TKey> IReadOnlyDictionary<TKey,TValue>.Keys=>this.Enumerate<KeyIterator<TKey,TKeySerializer>,TKey>(new());
	IEnumerable<TValue> IReadOnlyDictionary<TKey,TValue>.Values=>this.Enumerate<ValueIterator<TValue,TValueSerializer>,TValue>(new());
	public override bool IsDisposed=>this.disposed;
	private bool disposed;
	protected override void Dispose(bool disposing) {
		if(this.disposed) return;
		this.disposed=true;
		if(disposing) {
			this.Remove_TKey_TValue_ClientSession.Dispose();
			this.Remove_KeyValuePair_ClientSession.Dispose();
		}
		base.Dispose(disposing);
	}
}
