namespace Seamless.Collections;
//public static class Extensions0{
//    public static bool PrivateTryAdd0<TKey, TValue>(this Collection<TKey,TValue>Collection,TKey Key,TValue Value) where TKey : IEquatable<TKey> where TValue : IEquatable<TValue> {
//        var KeyBytes = MemoryPack.MemoryPackSerializer.Serialize(Key);
//        var ValueBytes = MemoryPack.MemoryPackSerializer.Serialize(Key);
//    }
//}
//public sealed class HashSet<T>(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store,long LogMemorySize=0,double MutableFraction=1.0):HashSet<T,CheckStrategy<T,Empty>>(Store,LogMemorySize,MutableFraction)where T:IEquatable<T>;
public sealed class MemoryHashSet<T>(MemoryHashSetStore<T>CollectionStore):HashSet<T>(CollectionStore)where T:IEquatable<T>{
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>
        this.PrivateProtectedWouldOverflowMutable(KeyLength, ValueLength);
}
