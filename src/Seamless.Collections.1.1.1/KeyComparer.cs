namespace Seamless.Collections;
using Tsavorite.core;

/// <summary>
/// <see cref="EqualityComparer{T}.Default"/> を使って Tsavorite の
/// <see cref="IKeyComparer{T}"/> を実装する構造体。
/// </summary>
public struct KeyComparer<TKey> : IKeyComparer<SpanByte> where TKey : IEquatable<TKey> {
	public long GetHashCode64(ref SpanByte key){
		const ulong offset=14695981039346656037;
		const ulong prime=1099511628211;
		ulong hash=offset;
		foreach(var b in key.AsReadOnlySpan()){
			hash^=b;
			hash*=prime;
		}
		return unchecked((long)hash);
	}
	public bool Equals(ref SpanByte k1, ref SpanByte k2) => k1.AsReadOnlySpan().SequenceEqual(k2.AsReadOnlySpan());
}
