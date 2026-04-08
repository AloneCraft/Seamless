namespace Seamless.Collections;
using System.Diagnostics;
internal sealed class DictionaryDebugView<TKey,TValue,TKeySerializer,TValueSerializer>
	where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>
	where TKeySerializer:ISerializer<TKey>where TValueSerializer:ISerializer<TValue>{
	private readonly ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> _dictionary;
	public DictionaryDebugView(ManagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> dictionary){
		ArgumentNullException.ThrowIfNull(dictionary);
		this._dictionary=dictionary;
	}
	[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
	public KeyValuePair<TKey,TValue>[] Items{
		get{
			var items=new KeyValuePair<TKey,TValue>[this._dictionary.Count];
			this._dictionary.CopyTo(items,0);
			return items;
		}
	}
}
