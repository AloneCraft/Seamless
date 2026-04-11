namespace Seamless.Collections;

/// <summary>
/// A no-op <see cref="IRecordDisposer{TKey,TValue}"/> for blittable unmanaged key/value types.
/// Equivalent to <see cref="DefaultRecordDisposer{TKey,TValue}"/> but explicitly named for unmanaged stores.
/// </summary>
public readonly struct UnmanagedRecordDisposer<TKey, TValue> : IRecordDisposer<TKey, TValue>
    where TKey : unmanaged
    where TValue : unmanaged {

    public static readonly UnmanagedRecordDisposer<TKey, TValue> Instance = new();

    public bool DisposeOnPageEviction => false;

    public void DisposeRecord(ref TKey key, ref TValue value, DisposeReason reason, int newKeySize) { }
}
