#pragma warning disable 0436
namespace Seamless.Linq;

// Most of the time we use class instead of struct because objects are kept in the heap.
// Since we're only using it for duplicate checking in set operations, having `bool Add` is sufficient.
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HashSetSlim<T>:IDisposable {
    private const int MinimumSize = 16; // minimum arraypool size(power of 2)
    private const double LoadFactor = 0.72;
    private readonly IEqualityComparer<T> comparer;
    // 既定コンパレータのときは true。値型では typeof(T).IsValueType が JIT で畳み込まれ、
    // EqualityComparer<T>.Default 経由の静的呼び出しに devirtualize/inline される(フィールド仮想呼び出しを回避)。
    private readonly bool isDefaultComparer;
    private Entry[] entries;
    private int[] buckets; // bucket is index of entries, 1-based(0 for empty).
    private int bucketsLength; // power of 2
    private int entryIndex;
    private int resizeThreshold;

    public HashSetSlim(IEqualityComparer<T>? comparer)
        : this(MinimumSize,comparer) {
    }

    public HashSetSlim(int capacity,IEqualityComparer<T>? comparer) {
        var requestedCapacity = Math.Max(capacity,0);
        var bucketCapacity = requestedCapacity==0
            ? MinimumSize
            : (int)Math.Ceiling((requestedCapacity+1)/LoadFactor);
        capacity=Math.Max((int)BitOperations.RoundUpToPowerOf2((uint)bucketCapacity),MinimumSize);

        this.comparer=comparer??EqualityComparer<T>.Default;
        this.isDefaultComparer=comparer is null||ReferenceEquals(comparer,EqualityComparer<T>.Default);
        this.buckets=ArrayPool<int>.Shared.Rent(capacity);
        this.entries=ArrayPool<Entry>.Shared.Rent(capacity);
        this.bucketsLength=capacity;
        this.resizeThreshold=(int)(this.bucketsLength*LoadFactor);
        this.buckets.AsSpan().Clear(); // 0-clear.
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Add(T item) {
        var hashCode = this.InternalGetHashCode(item);
        ref var bucket = ref this.buckets[this.GetBucketIndex(hashCode)];
        var index = bucket-1;

        // lookup phase
        while(index!=-1) {
            ref var entry = ref this.entries[index];
            if(entry.HashCode==hashCode&&this.KeyEquals(entry.Value,item)) {
                return false;
            }
            index=entry.Next;
        }

        // add phase
        if(this.entryIndex>=this.resizeThreshold) {
            this.Resize();
            // Need to recalculate bucket after resize
            bucket=ref this.buckets[this.GetBucketIndex(hashCode)];
        }

        ref var newEntry = ref this.entries[this.entryIndex];
        newEntry.HashCode=hashCode;
        newEntry.Value=item;
        newEntry.Next=bucket-1;

        bucket=this.entryIndex+1;
        this.entryIndex++;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Contains(T item) {
        var hashCode = this.InternalGetHashCode(item);
        var index = this.buckets[this.GetBucketIndex(hashCode)]-1;

        while(index!=-1) {
            ref var entry = ref this.entries[index];
            if(entry.HashCode==hashCode&&this.KeyEquals(entry.Value,item)) {
                return true;
            }
            index=entry.Next;
        }

        return false;
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

    // special use case for Intercept, only call after completely constructed(don't call Add after Remove called).
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Remove(T item) {
        var hashCode = this.InternalGetHashCode(item);
        ref var bucket = ref this.buckets[this.GetBucketIndex(hashCode)];
        var index = bucket-1;
        var lastIndex = -1;

        // lookup phase
        while(index!=-1) {
            ref var entry = ref this.entries[index];
            if(entry.HashCode==hashCode&&this.KeyEquals(entry.Value,item)) {
                if(lastIndex==-1) {
                    // This is the first entry in the bucket
                    bucket=entry.Next+1;
                } else {
                    // Link the previous entry to the next one, skipping this entry
                    this.entries[lastIndex].Next=entry.Next;
                }

                // Clear the removed entry's value
                if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
                    entry.Value=default!;
                }

                return true;
            }

            lastIndex=index;
            index=entry.Next;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool KeyEquals(T x,T y) {
        if(typeof(T).IsValueType&&this.isDefaultComparer) {
            return EqualityComparer<T>.Default.Equals(x,y);
        }
        return this.comparer.Equals(x,y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint InternalGetHashCode(T key) {
        if(typeof(T).IsValueType&&this.isDefaultComparer) {
            // 値型では key は null になり得ず(Nullable<U> の空は Default.GetHashCode が 0 を返す)、
            // Default.GetHashCode はプリミティブでビットハッシュに inline される。
            return (uint)(EqualityComparer<T>.Default.GetHashCode(key!)&0x7FFFFFFF);
        }
        // allows null.
        return (uint)((key is null) ? 0 : this.comparer.GetHashCode(key)&0x7FFFFFFF);
    }

    // バケット番号の計算は HashBucket へ集約している（劣化条件の説明もそちら）。
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(uint hashCode) {
        return HashBucket.Index(hashCode,this.bucketsLength);
    }

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
    [DebuggerDisplay("HashCode = {HashCode}, Value = {Value}, Next = {Next}")]
    private struct Entry {
        public uint HashCode;
        public T Value;
        public int Next; // next is index of entries, -1 is end of chain
    }
}
