using System.Diagnostics;
namespace Seamless.Collections;
internal sealed class HashSetDebugView<T> where T : IEquatable<T> {
	private readonly ManagedHashSet<T> HashSet;
	public HashSetDebugView(ManagedHashSet<T> set) {
		ArgumentNullException.ThrowIfNull(set);
		this.HashSet=set;
	}
	[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
	public T[] Items {
		get {
			var Items=new T[this.HashSet.Count];
			this.HashSet.CopyTo(Items,0);
			return Items;
		}
	}
}
