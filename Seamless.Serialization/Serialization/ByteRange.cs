namespace Seamless.Serialization;

/// <summary>
/// シリアライズ済みバイト範囲に対する共通のハッシュ/完全一致比較。
/// 技法(c)ビットバイナリ範囲ハッシュ・(d)完全一致透過の単一実装で、
/// Persisted のハッシュ索引・Tsavorite の値比較・ランタイムの unmanaged 経路が共有する。
/// 型別 GetHashCode/IEqualityComparer を介さず、生バイトだけで完結する(deserialize 不要)。
/// </summary>
public static class ByteRange {
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>バイト範囲の 64bit FNV-1a ハッシュ。8byte 単位で畳んで末尾を byte 単位で処理する。</summary>
    public static ulong Hash(ReadOnlySpan<byte> bytes) {
        var hash = FnvOffsetBasis;
        var index = 0;
        var blockEnd = bytes.Length-(bytes.Length&7);
        while(index<blockEnd) {
            var chunk = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetReference(bytes),index));
            hash^=chunk;
            hash*=FnvPrime;
            index+=8;
        }
        while(index<bytes.Length) {
            hash^=bytes[index];
            hash*=FnvPrime;
            index++;
        }
        return hash;
    }

    /// <summary>索引バケット向けの 32bit 版(上位 32bit と下位 32bit を混ぜる)。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash32(ReadOnlySpan<byte> bytes) {
        var hash = Hash(bytes);
        return (uint)(hash^(hash>>32));
    }

    /// <summary>2つのバイト範囲が完全一致するか(deserialize なしの透過比較)。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(ReadOnlySpan<byte> left,ReadOnlySpan<byte> right) =>
        left.SequenceEqual(right);
}
