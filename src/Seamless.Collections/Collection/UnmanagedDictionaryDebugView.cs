using System.Diagnostics;
namespace Seamless.Collections;
internal sealed class UnmanagedDictionaryDebugView<TKey,TValue,TKeySerializer,TValueSerializer>
	where TKey:unmanaged,IEquatable<TKey> where TValue:unmanaged,IEquatable<TValue>
	where TKeySerializer:ISerializer<TKey> where TValueSerializer:ISerializer<TValue> {
	private readonly UnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> _dictionary;
	public UnmanagedDictionaryDebugView(UnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> dictionary) {
		ArgumentNullException.ThrowIfNull(dictionary);
		this._dictionary=dictionary;
	}
	[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
	public KeyValuePair<TKey,TValue>[] Items {
		get {
			var items=new KeyValuePair<TKey,TValue>[this._dictionary.Count];
			this._dictionary.CopyTo(items,0);
			return items;
		}
	}
}
