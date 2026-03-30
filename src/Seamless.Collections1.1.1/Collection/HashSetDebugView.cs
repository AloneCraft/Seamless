using System.Diagnostics;
namespace Seamless.Collections;
internal sealed class HashSetDebugView<T> where T : IEquatable<T> {
	private readonly HashSet<T> _set;
	public HashSetDebugView(HashSet<T> set) {
		ArgumentNullException.ThrowIfNull(set);
		this._set=set;
	}
	[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
	public T[] Items {
		get {
			var items = new T[this._set.Count];
			this._set.CopyTo(items,0);
			return items;
		}
	}
}
