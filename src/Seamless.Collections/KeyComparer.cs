namespace Seamless.Collections;
using Tsavorite.core;
/// <summary>
/// <see cref="EqualityComparer{T}.Default"/> を使って Tsavorite の
/// <see cref="IKeyComparer"/> を実装する構造体。
/// </summary>
public struct KeyComparer<TKey> : IKeyComparer where TKey : IEquatable<TKey> {
	public long GetHashCode64(ReadOnlySpan<byte> keyBytes){
		const ulong offset=14695981039346656037;
		const ulong prime=1099511628211;
		ulong hash=offset;
		foreach(var b in keyBytes){
			hash^=b;
			hash*=prime;
		}
		return unchecked((long)hash);
	}
	public bool Equals(ReadOnlySpan<byte> k1Bytes,ReadOnlySpan<byte> k2Bytes)=>k1Bytes.SequenceEqual(k2Bytes);
	long IKeyComparer.GetHashCode64<TActualKey>(TActualKey key) where TActualKey:default=>0;
	bool IKeyComparer.Equals<TFirstKey,TSecondKey>(TFirstKey k1,TSecondKey k2) where TFirstKey:default where TSecondKey:default=>false;
}
