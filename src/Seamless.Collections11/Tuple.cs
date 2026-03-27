namespace Seamless.Collections;
public interface ITuple<out TKey>where TKey:IEquatable<TKey>{
	TKey Key{get; }
}

//public readonly struct Tuple<TKey>:ITuple<TKey>,IEquatable<Tuple<TKey>> where TKey:IEquatable<TKey>{
//	public TKey Key { get; }
//	public Tuple(TKey key) => this.Key = key;
//	public bool Equals(Tuple<TKey> other) => EqualityComparer<TKey>.Default.Equals(this.Key, other.Key);
//	public override bool Equals(object? obj) {
//		if (obj is Tuple<TKey> t) return Equals(t);
//		if (obj is ITuple<TKey> it) return EqualityComparer<TKey>.Default.Equals(this.Key, it.Key);
//		return false;
//	}
//	public override int GetHashCode() => EqualityComparer<TKey>.Default.GetHashCode(this.Key!);
//	public static bool operator ==(Tuple<TKey> left, Tuple<TKey> right) => left.Equals(right);
//	public static bool operator !=(Tuple<TKey> left, Tuple<TKey> right) => !left.Equals(right);
//}
