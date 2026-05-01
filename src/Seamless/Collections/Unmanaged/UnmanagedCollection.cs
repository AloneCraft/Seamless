namespace Seamless.Collections;

public abstract partial class UnmanagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>
    where TKey : unmanaged
    where TValue : unmanaged
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public sealed partial class KeyIterator;
    internal sealed partial class ValueIterator;
    public sealed partial class KeyValueIterator;
}
