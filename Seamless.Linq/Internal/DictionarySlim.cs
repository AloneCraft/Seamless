#pragma warning disable 0436
namespace Seamless.Linq;

// Lightweight dictionary implementation optimized for performance
// with minimal API surface focused on the specific use case
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DictionarySlim<TKey, TValue>:IDisposable // allows TKey null
{
    private const int MinimumSize = 16; // minimum arraypool size(power of 2)
    private const double LoadFactor = 0.72;
    private readonly IEqualityComparer<TKey> comparer;
    // 既定コンパレータのときは true。値型では typeof(TKey).IsValueType が JIT で畳み込まれ、
    // EqualityComparer<TKey>.Default 経由の静的呼び出しに devirtualize/inline される。
    private readonly bool isDefaultComparer;
    private Entry[] entries;
    private int[] buckets; // bucket is index of entries, 1-based(0 for empty).
    private int bucketsLength; // power of 2
    private int entryIndex;
    private int resizeThreshold;

    public int Count => this.entryIndex;

    public DictionarySlim(IEqualityComparer<TKey>? comparer = null) {
        this.comparer=comparer??EqualityComparer<TKey>.Default;
        this.isDefaultComparer=comparer is null||ReferenceEquals(comparer,EqualityComparer<TKey>.Default);
        this.buckets=ArrayPool<int>.Shared.Rent(MinimumSize);
        this.entries=ArrayPool<Entry>.Shared.Rent(MinimumSize);
        this.bucketsLength=MinimumSize;
        this.resizeThreshold=(int)(this.bucketsLength*LoadFactor);
        this.buckets.AsSpan().Clear(); // 0-clear.
    }

    public DictionarySlim(int capacity,IEqualityComparer<TKey>? comparer = null) {
        this.comparer=comparer??EqualityComparer<TKey>.Default;
        this.isDefaultComparer=comparer is null||ReferenceEquals(comparer,EqualityComparer<TKey>.Default);
        var requestedCapacity = Math.Max(capacity,0);
        var bucketCapacity = requestedCapacity==0
            ? MinimumSize
            : (int)Math.Ceiling((requestedCapacity+1)/LoadFactor);
        var actualCapacity = Math.Max((int)BitOperations.RoundUpToPowerOf2((uint)bucketCapacity),MinimumSize);
        this.buckets=ArrayPool<int>.Shared.Rent(actualCapacity);
        this.entries=ArrayPool<Entry>.Shared.Rent(actualCapacity);
        this.bucketsLength=actualCapacity;
        this.resizeThreshold=(int)(this.bucketsLength*LoadFactor);
        this.buckets.AsSpan().Clear(); // 0-clear.
    }

    public ref TValue? GetValueRefOrAddDefault(TKey key,out bool exists) {
        var hashCode = this.InternalGetHashCode(key);
        ref var bucket = ref this.buckets[this.GetBucketIndex(hashCode)];
        var index = bucket-1;

        // lookup phase
        while(index!=-1) {
            ref var entry = ref this.entries[index];
            if(entry.HashCode==hashCode&&this.KeyEquals(entry.Key,key)) {
                exists=true;
                return ref entry.Value;
            }
            index=entry.Next;
        }

        // add phase
        exists=false;
        if(this.entryIndex>=this.resizeThreshold) {
            this.Resize();
            // Need to recalculate bucket after resize
            bucket=ref this.buckets[this.GetBucketIndex(hashCode)];
        }

        ref var newEntry = ref this.entries[this.entryIndex];
        newEntry.HashCode=hashCode;
        newEntry.Key=key;
        newEntry.Value=default;
        newEntry.Next=bucket-1;

        bucket=this.entryIndex+1;
        this.entryIndex++;

        return ref newEntry.Value;
    }
    private void Resize() {
        var newSize = BitOperations.RoundUpToPowerOf2((uint)this.entries.Length*2);
        var newEntries = ArrayPool<Entry>.Shared.Rent((int)newSize);
        var newBuckets = ArrayPool<int>.Shared.Rent((int)newSize);
        this.bucketsLength=(int)newSize; // guarantees PowerOf2
        this.resizeThreshold=(int)(this.bucketsLength*LoadFactor);
        newBuckets.AsSpan().Clear(); // 0-clear.

        // Copy entries
        Array.Copy(this.entries,newEntries,this.entryIndex);

        for(int i = 0;i<this.entryIndex;i++) {
            ref var entry = ref newEntries[i];
            var bucketIndex = this.GetBucketIndex(entry.HashCode);

            ref var bucket = ref newBuckets[bucketIndex];
            entry.Next=bucket-1;
            bucket=i+1;
        }

        // return old arrays
        ArrayPool<int>.Shared.Return(this.buckets,clearArray: false);
        ArrayPool<Entry>.Shared.Return(this.entries,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<Entry>());

        // assign new arrays
        this.entries=newEntries;
        this.buckets=newBuckets;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool KeyEquals(TKey x,TKey y) {
        if(typeof(TKey).IsValueType&&this.isDefaultComparer) {
            return EqualityComparer<TKey>.Default.Equals(x,y);
        }
        return this.comparer.Equals(x,y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint InternalGetHashCode(TKey key) {
        if(typeof(TKey).IsValueType&&this.isDefaultComparer) {
            return (uint)(EqualityComparer<TKey>.Default.GetHashCode(key!)&0x7FFFFFFF);
        }
        // allows null.
        return (uint)((key is null) ? 0 : this.comparer.GetHashCode(key)&0x7FFFFFFF);
    }

    // バケット番号の計算は HashBucket へ集約している（劣化条件の説明もそちら）。
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(uint hashCode) {
        return (int)(hashCode&(uint)(this.bucketsLength-1));
    }

    // Since there are no deletions, the order of Entries is completely guaranteed to be in the order of addition.
    public Enumerator GetEnumerator() => new(this);

    // return to pool
    public void Dispose() {
        if(this.buckets!=null) {
            ArrayPool<int>.Shared.Return(this.buckets,clearArray: false);
            this.buckets=null!;
        }
        if(this.entries!=null) {
            ArrayPool<Entry>.Shared.Return(this.entries,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<Entry>());
            this.entries=null!;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("HashCode = {HashCode}, Key = {Key}, Value =  {Value}, Next = {Next}")]
    private struct Entry {
        public uint HashCode;
        public TKey Key;
        public TValue? Value;
        public int Next; // next is index of entries, -1 is end of chain
    }

    // Since it's only used internally, I won't make it an IEnumerator style.
    public struct Enumerator(DictionarySlim<TKey,TValue> dictionary) {
        private int index;

        // no needs ref style in this repository

        //public bool TryGetNextKeyRef(ref readonly TKey keyRef)
        //{
        //    if (index < dictionary.entryIndex)
        //    {
        //        ref var entry = ref dictionary.entries[index];
        //        index++;
        //        keyRef = ref entry.Key;
        //        return true;
        //    }
        //    return false;
        //}

        //public bool TryGetNextValueRef(ref readonly TValue valueRef)
        //{
        //    if (index < dictionary.entryIndex)
        //    {
        //        ref var entry = ref dictionary.entries[index];
        //        index++;
        //        valueRef = ref entry.Value;
        //        return true;
        //    }
        //    return false;
        //}

        //public bool TryGetNextRef(ref readonly TKey keyRef, ref readonly TValue valueRef)
        //{
        //    if (index < dictionary.entryIndex)
        //    {
        //        ref var entry = ref dictionary.entries[index];
        //        index++;
        //        keyRef = ref entry.Key;
        //        valueRef = ref entry.Value!;
        //        return true;
        //    }
        //    return false;
        //}

        public bool TryGetNext(out KeyValuePair<TKey,TValue> current) {
            if(this.index<dictionary.entryIndex) {
                ref var entry = ref dictionary.entries[this.index];
                this.index++;
                current=new(entry.Key,entry.Value!);
                return true;
            }
            current=default;
            return false;
        }
    }
}
