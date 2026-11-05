using Seamless.Execution;

namespace Seamless.Database;

/// <summary>メモリ内と退避後で同じ canonical バイト列をキーの同一性に使う。</summary>
internal sealed class CanonicalKeyComparer<TKey>(ExecutionScope resources):IEqualityComparer<TKey> where TKey:struct{
    public bool Equals(TKey first,TKey second){using var left=resources.Encode(first);using var right=resources.Encode(second);return left.Memory.Span.SequenceEqual(right.Memory.Span);}
    public int GetHashCode(TKey value){using var bytes=resources.Encode(value);var hash=new HashCode();hash.AddBytes(bytes.Memory.Span);return hash.ToHashCode();}
}
