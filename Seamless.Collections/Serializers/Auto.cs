namespace Seamless.Collections.Serializers;

/// <summary>
/// 型ごとの静的特殊化ディスパッチャ。
/// unmanaged 型は blittable ゼロコピー経路(<see cref="Blittable{T}"/> と同一実装)、
/// 参照を含む型はシリアライズ版(<see cref="MemoryPackSerializer{T}"/>)へ振り分ける。
/// 判定 <see cref="RuntimeHelpers.IsReferenceOrContainsReferences{T}"/> は JIT intrinsic で
/// 具体型ごとに定数畳み込みされ、分岐は消えて片方の経路だけが機械語に残る。
/// したがって値型キーは実行時分岐なしの blittable 経路にコンパイルされる(技法a/b)。
/// </summary>
public readonly struct Auto<T>:IStrategy<T> {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> Serialize(ref T value) {
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            return MemoryPack<T>.Serialize(ref value);
        // blittable 経路: Blittable<T> と同一(T:unmanaged 制約が無いためここでは直接展開)。
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T,byte>(ref value),Unsafe.SizeOf<T>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Deserialize(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty)
            return default!;
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            return MemoryPack<T>.Deserialize(bytes);
        if(bytes.Length<Unsafe.SizeOf<T>())
            throw new ArgumentException("The serialized value is shorter than the target type size.",nameof(bytes));
        return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(bytes));
    }
}
