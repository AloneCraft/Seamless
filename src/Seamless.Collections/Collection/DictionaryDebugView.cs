namespace Seamless.Collections;
using System.Diagnostics;
internal sealed class DictionaryDebugView<TKey,TValue>where TKey:IEquatable<TKey>where TValue:IEquatable<TValue>{
	private readonly VariableDictionary<TKey,TValue> _dictionary;
	public DictionaryDebugView(VariableDictionary<TKey,TValue> dictionary){
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
