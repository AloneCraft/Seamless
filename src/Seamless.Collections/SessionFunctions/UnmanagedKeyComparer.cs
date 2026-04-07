#if TSAVORITE1_1_1
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tsavorite.core;

namespace Seamless.SessionFunctions;

/// <summary>unmanaged 固定長キー用の IKeyComparer 実装。</summary>
public struct UnmanagedKeyComparer<TKey> : IKeyComparer<TKey>
    where TKey : unmanaged, IEquatable<TKey>
{
    public bool Equals(ref TKey k1, ref TKey k2) => k1.Equals(k2);

    public long GetHashCode64(ref TKey k)
    {
        var span = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<TKey, byte>(ref k),
            Unsafe.SizeOf<TKey>());
        long hash = 0;
        int i = 0;
        for (; i + 8 <= span.Length; i += 8)
            hash ^= MemoryMarshal.Read<long>(span.Slice(i));
        for (; i < span.Length; i++)
            hash ^= (long)span[i] << ((i % 8) * 8);
        return Utility.GetHashCode(hash);
    }
}
#endif
