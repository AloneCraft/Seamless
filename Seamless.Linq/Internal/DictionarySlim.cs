#pragma warning disable 0436
namespace Seamless.Linq;

// Lightweight dictionary implementation optimized for performance
// with minimal API surface focused on the specific use case
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DictionarySlim<TKey, TValue>:IDisposable // allows TKey null
{
    private const int MinimumSize = 16; // minimum arraypool size(power of 2)
    private const double LoadFactor = 0.72;
    private const int MixAfterProbes=16;
    private readonly IEqualityComparer<TKey> comparer;
    // 既定コンパレータのときは true。値型では typeof(TKey).IsValueType が JIT で畳み込まれ、
    // EqualityComparer<TKey>.Default 経由の静的呼び出しに devirtualize/inline される。
    private readonly bool isDefaultComparer;
    private Entry[] entries;
    private int[] buckets; // bucket is index of entries, 1-based(0 for empty).
    private int bucketsLength; // power of 2
    private int entryIndex;
    private int resizeThreshold;
    private bool useMixedBuckets;

    public int Count => this.entryIndex;

    public DictionarySlim(IEqualityComparer<TKey>? comparer = null) {
        this.comparer=comparer??EqualityComparer<TKey>.Default;
        this.isDefaultComparer=comparer is null||ReferenceEquals(comparer,EqualityComparer<TKey>.Default);
        this.buckets=ArrayPool<int>.Shared.Rent(MinimumSize);
        this.entries=ArrayPool<Entry>.Shared.Rent(MinimumSize);
        this.bucketsLength=MinimumSize;
        this.resizeThreshold=(int)(MinimumSize*LoadFactor);
        this.buckets.AsSpan(0,MinimumSize).Clear(); // 0-clear.
    }

    public DictionarySlim(int capacity,IEqualityComparer<TKey>? comparer = null)
        : this(comparer) {
        // ZLinq と同様に常に MinimumSize (16) から開始し、実グループ数が閾値を超えた場合のみ倍増させる。
    }

    // 追加せずに探す。無ければ null 参照(Unsafe.IsNullRef で調べる)。GroupBy の集約で、先頭の k グループだけが必要なとき(First・Take)に、
    // k 個を超えるグループを作らずに、要素がどのグループのものかだけを調べるために使う。
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue? GetValueRefOrNullRef(TKey key) {
        var hashCode = this.InternalGetHashCode(key);
        var index = this.buckets[this.GetBucketIndex(hashCode)]-1;
        while(index!=-1) {
            ref var entry = ref this.entries[index];
            if(entry.HashCode==hashCode&&this.KeyEquals(entry.Key,key)) {
                return ref entry.Value;
            }
            index=entry.Next;
        }
        return ref Unsafe.NullRef<TValue?>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue? GetValueRefOrAddDefault(TKey key,out bool exists) {
        var hashCode = this.InternalGetHashCode(key);
        ref var bucket = ref this.buckets[this.GetBucketIndex(hashCode)];
        var index = bucket-1;

        // lookup phase
        var probes=0;
        while(index!=-1) {
            ref var entry = ref this.entries[index];
            if(entry.HashCode==hashCode&&this.KeyEquals(entry.Key,key)) {
                exists=true;
                return ref entry.Value;
            }
            index=entry.Next;
            probes++;
        }

        // add phase
        exists=false;
        var switchBuckets=probes>=MixAfterProbes&&!this.useMixedBuckets;
        if(this.entryIndex>=this.resizeThreshold||switchBuckets) {
            return ref this.AddWithResizeOrMix(key,hashCode,switchBuckets);
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ref TValue? AddWithResizeOrMix(TKey key,uint hashCode,bool switchBuckets) {
        if(this.entryIndex>=this.resizeThreshold) {
            this.Resize(switchBuckets);
        } else if(switchBuckets) {
            this.useMixedBuckets=true;
            this.Rehash(this.entries,this.buckets);
        }
        ref var bucket = ref this.buckets[this.GetBucketIndex(hashCode)];
        ref var newEntry = ref this.entries[this.entryIndex];
        newEntry.HashCode=hashCode;
        newEntry.Key=key;
        newEntry.Value=default;
        newEntry.Next=bucket-1;

        bucket=this.entryIndex+1;
        this.entryIndex++;

        return ref newEntry.Value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Resize(bool switchBuckets) {
        var newSize = BitOperations.RoundUpToPowerOf2((uint)this.bucketsLength*2);
        var newEntries = ArrayPool<Entry>.Shared.Rent((int)newSize);
        var newBuckets = ArrayPool<int>.Shared.Rent((int)newSize);
        this.bucketsLength=(int)newSize; // guarantees PowerOf2
        this.resizeThreshold=(int)(this.bucketsLength*LoadFactor);
        this.useMixedBuckets|=switchBuckets;
        Array.Copy(this.entries,newEntries,this.entryIndex);
        this.Rehash(newEntries,newBuckets);

        // return old arrays
        ArrayPool<int>.Shared.Return(this.buckets,clearArray: false);
        ArrayPool<Entry>.Shared.Return(this.entries,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<Entry>());

        // assign new arrays
        this.entries=newEntries;
        this.buckets=newBuckets;
    }

    private void Rehash(Entry[] entries,int[] buckets){
        buckets.AsSpan(0,this.bucketsLength).Clear();
        for(var index=0;index<this.entryIndex;index++){
            ref var entry=ref entries[index];
            ref var bucket=ref buckets[this.GetBucketIndex(entry.HashCode)];
            entry.Next=bucket-1;
            bucket=index+1;
        }
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

    // 下位ビットで衝突が続いた辞書だけ、構築済みのバケットも含めて混合へ切り替える。
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetBucketIndex(uint hashCode) {
        if(this.useMixedBuckets){
            return HashBucket.Index(hashCode,this.bucketsLength);
        }
        return (int)(hashCode&(uint)(this.bucketsLength-1));
    }

    // Since there are no deletions, the order of Entries is completely guaranteed to be in the order of addition.
    public Enumerator GetEnumerator() => new(this.entries,this.entryIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetKeyAndValue(int index,out TKey key,out TValue? value) {
        ref var entry = ref this.entries[index];
        key=entry.Key;
        value=entry.Value;
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
    [DebuggerDisplay("HashCode = {HashCode}, Key = {Key}, Value =  {Value}, Next = {Next}")]
    internal struct Entry {
        public uint HashCode;
        public TKey Key;
        public TValue? Value;
        public int Next; // next is index of entries, -1 is end of chain
    }

    // Since it's only used internally, I won't make it an IEnumerator style.
    public struct Enumerator {
        private readonly Entry[]? entries;
        private readonly int count;
        internal Enumerator(Entry[]? entries,int count) {
            this.entries=entries;
            this.count=count;
            this.index=0;
        }
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
            if(this.entries!=null&&this.index<this.count) {
                ref var entry = ref this.entries[this.index];
                this.index++;
                current=new(entry.Key,entry.Value!);
                return true;
            }
            current=default;
            return false;
        }
    }
}
