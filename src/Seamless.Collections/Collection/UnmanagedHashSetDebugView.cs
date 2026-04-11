using System.Diagnostics;
namespace Seamless.Collections;
internal sealed class UnmanagedHashSetDebugView<TKey> where TKey : unmanaged, IEquatable<TKey> {
	private readonly UnmanagedHashSet<TKey> HashSet;
	public UnmanagedHashSetDebugView(UnmanagedHashSet<TKey> set) {
		ArgumentNullException.ThrowIfNull(set);
		this.HashSet=set;
	}
	[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
	public TKey[] Items {
		get {
			var Items=new TKey[this.HashSet.Count];
			this.HashSet.CopyTo(Items,0);
			return Items;
		}
	}
}
