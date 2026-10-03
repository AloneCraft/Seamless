#pragma warning disable
namespace Seamless.Linq {
    partial class Enumerable {
        public static ILookup<TKey,TSource> ToLookup<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            return ToLookup(source,keySelector,null);
        }

        public static ILookup<TKey,TSource> ToLookup<TEnumerator, TSource, TKey>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(keySelector);

            var lookupBuilder = new LookupBuilder<TKey,TSource>(comparer??EqualityComparer<TKey>.Default);
            using(var enumerator = source.Enumerator) {
                if(enumerator.TryGetSpan(out var span)) {
                    foreach(var item in span) {
                        lookupBuilder.AddUnpooled(keySelector(item),item);
                    }
                } else {
                    while(enumerator.TryGetNext(out var item)) {
                        lookupBuilder.AddUnpooled(keySelector(item),item);
                    }
                }

                return lookupBuilder.BuildAndClear();
            }
        }

        public static ILookup<TKey,TElement> ToLookup<TEnumerator, TSource, TKey, TElement>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            return ToLookup(source,keySelector,elementSelector,null!);
        }

        public static ILookup<TKey,TElement> ToLookup<TEnumerator, TSource, TKey, TElement>(this ValueEnumerable<TEnumerator,TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            ArgumentNullException.ThrowIfNull(keySelector);
            ArgumentNullException.ThrowIfNull(elementSelector);

            var lookupBuilder = new LookupBuilder<TKey,TElement>(comparer??EqualityComparer<TKey>.Default);
            using(var enumerator = source.Enumerator) {
                if(enumerator.TryGetSpan(out var span)) {
                    foreach(var item in span) {
                        lookupBuilder.AddUnpooled(keySelector(item),elementSelector(item));
                    }
                } else {
                    while(enumerator.TryGetNext(out var item)) {
                        lookupBuilder.AddUnpooled(keySelector(item),elementSelector(item));
                    }
                }

                return lookupBuilder.BuildAndClear();
            }
        }
    }
}

namespace Seamless.Linq {
    internal static class Lookup {
        // Enumerable.Join operation ignores null key
        public static Lookup<TKey,TSource> CreateForJoin<TEnumerator, TSource, TKey>(ref TEnumerator source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            var lookupBuilder = new LookupBuilder<TKey,TSource>(comparer??EqualityComparer<TKey>.Default);
            if(source.TryGetSpan(out var span)) {
                foreach(var item in span) {
                    var key = keySelector(item);
                    if(key is not null) {
                        lookupBuilder.AddUnpooled(key,item);
                    }
                }
            } else {
                while(source.TryGetNext(out var item)) {
                    var key = keySelector(item);
                    if(key is not null) {
                        lookupBuilder.AddUnpooled(key,item);
                    }
                }
            }

            return lookupBuilder.BuildAndClear();
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct LookupBuilder<TKey, TElement>:IDisposable {
        private const int MinimumSize = 16; // minimum arraypool size
        private const double LoadFactor = 0.72;
        private readonly IEqualityComparer<TKey> comparer;
        // 既定コンパレータのときは true。値型では typeof(TKey).IsValueType が JIT で畳み込まれ、
        // EqualityComparer<TKey>.Default 経由の静的呼び出しに devirtualize/inline される。
        private readonly bool isDefaultComparer;
        private Grouping<TKey,TElement>[]? buckets;
        private int bucketsLength;
        // bucketsLength を設定する箇所では必ず同時に更新する（HashSetSlim / DictionarySlim と同じ形）。
        private int resizeThreshold;
        private Grouping<TKey,TElement>? last;
        private int groupCount;
        private readonly bool poolElements;

        public LookupBuilder(IEqualityComparer<TKey>? comparer) {
            this.last      =null;
            this.groupCount=0;
            this.poolElements =false;
            this.comparer  =comparer??EqualityComparer<TKey>.Default;
            this.isDefaultComparer=comparer is null||ReferenceEquals(comparer,EqualityComparer<TKey>.Default);
        }

        public LookupBuilder(int capacity,IEqualityComparer<TKey>? comparer) {
            this.last      =null;
            this.groupCount=0;
            this.poolElements =false;
            this.comparer  =comparer??EqualityComparer<TKey>.Default;
            this.isDefaultComparer=comparer is null||ReferenceEquals(comparer,EqualityComparer<TKey>.Default);
            if(capacity<=0) {
                this.buckets   =null;
                this.bucketsLength=0;
                this.resizeThreshold=0;
                return;
            }
            var bucketCapacity = (int)Math.Ceiling((capacity+1)/LoadFactor);
            this.bucketsLength=Math.Max((int)BitOperations.RoundUpToPowerOf2((uint)bucketCapacity),MinimumSize);
            this.resizeThreshold=(int)(this.bucketsLength*LoadFactor);
            this.buckets         =ArrayPool<Grouping<TKey,TElement>>.Shared.Rent(this.bucketsLength);
            this.buckets.AsSpan(0,this.bucketsLength).Clear();
        }

        public LookupBuilder(int capacity,IEqualityComparer<TKey>? comparer,bool poolElements) : this(capacity,comparer) {
            this.poolElements=poolElements;
        }

        // 【重要】Lookup.GetBucketIndex と必ず同じ式にすること。片方だけ変えると
        // 構築したバケットと参照するバケットがずれ、GetGroup が静かに取りこぼす。
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucketIndex(uint hashCode) {
            Debug.Assert(this.buckets is not null);
            return (int)(hashCode&(uint)(this.bucketsLength-1));
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
            // allows null
            return (uint)((key is null) ? 0 : this.comparer.GetHashCode(key)&0x7FFFFFFF);
        }

        public void Add(TKey key,TElement value) {
            if(this.buckets==null) {
                this.buckets   =ArrayPool<Grouping<TKey,TElement>>.Shared.Rent(MinimumSize);
                this.bucketsLength=MinimumSize;
                this.resizeThreshold=(int)(MinimumSize*LoadFactor);
            }

            var hash=this.InternalGetHashCode(key);
            while(true) {
                var index=this.GetBucketIndex(hash);
                ref var bucket=ref this.buckets![index];

                if(bucket==null) {
                    if(this.groupCount>=this.resizeThreshold) {
                        this.ResizeAndRehash();
                        continue;
                    }

                    bucket=new Grouping<TKey,TElement>(key,hash,value,this.poolElements);
                    this.groupCount++;
                    if(this.last!=null) {
                        bucket.NextGroupInAddOrder=this.last.NextGroupInAddOrder;
                        this.last.NextGroupInAddOrder=bucket;
                    } else {
                        bucket.NextGroupInAddOrder=bucket;
                    }
                    this.last=bucket;
                    return;
                }

                if(this.KeyEquals(bucket.Key,key)) {
                    bucket.AddCore(value);
                    return;
                }

                var previous=bucket;
                var next=bucket.NextGroupInSameHashCode;
                while(next!=null) {
                    if(this.KeyEquals(next.Key,key)) {
                        next.AddCore(value);
                        return;
                    }
                    previous=next;
                    next=next.NextGroupInSameHashCode;
                }

                if(this.groupCount>=this.resizeThreshold) {
                    this.ResizeAndRehash();
                    continue;
                }

                var newGroup=new Grouping<TKey,TElement>(key,hash,value,this.poolElements);
                previous.NextGroupInSameHashCode=newGroup;
                this.groupCount++;
                if(this.last!=null) {
                    newGroup.NextGroupInAddOrder=this.last.NextGroupInAddOrder;
                    this.last.NextGroupInAddOrder  =newGroup;
                } else {
                    newGroup.NextGroupInAddOrder=newGroup;
                }
                this.last=newGroup;
                return;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddUnpooled(TKey key,TElement value) {
            if(this.buckets==null) {
                this.buckets=ArrayPool<Grouping<TKey,TElement>>.Shared.Rent(MinimumSize);
                this.bucketsLength=MinimumSize;
                this.resizeThreshold=(int)(MinimumSize*LoadFactor);
            }

            var hash=this.InternalGetHashCode(key);
        retry:
            var index=this.GetBucketIndex(hash);
            ref var bucket=ref this.buckets![index];
            if(bucket==null) {
                if(this.groupCount>=this.resizeThreshold) {
                    this.ResizeAndRehash();
                    goto retry;
                }

                bucket=new Grouping<TKey,TElement>(key,hash,value);
                this.groupCount++;
                if(this.last!=null) {
                    bucket.NextGroupInAddOrder=this.last.NextGroupInAddOrder;
                    this.last.NextGroupInAddOrder=bucket;
                } else {
                    bucket.NextGroupInAddOrder=bucket;
                }
                this.last=bucket;
                return;
            }

            if(this.KeyEquals(bucket.Key,key)) {
                bucket.AddUnpooledCore(value);
                return;
            }

            var previous=bucket;
            var next=bucket.NextGroupInSameHashCode;
            while(next!=null) {
                if(this.KeyEquals(next.Key,key)) {
                    next.AddUnpooledCore(value);
                    return;
                }
                previous=next;
                next=next.NextGroupInSameHashCode;
            }

            if(this.groupCount>=this.resizeThreshold) {
                this.ResizeAndRehash();
                goto retry;
            }

            var newGroup=new Grouping<TKey,TElement>(key,hash,value);
            previous.NextGroupInSameHashCode=newGroup;
            this.groupCount++;
            if(this.last!=null) {
                newGroup.NextGroupInAddOrder=this.last.NextGroupInAddOrder;
                this.last.NextGroupInAddOrder=newGroup;
            } else {
                newGroup.NextGroupInAddOrder=newGroup;
            }
            this.last=newGroup;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Grouping<TKey,TElement>? GetGroup(TKey key) {
            if(this.buckets is null) return null;
            var hash =this.InternalGetHashCode(key);
            var group=this.buckets[this.GetBucketIndex(hash)];
            while(group is not null) {
                if(this.KeyEquals(group.Key,key)) return group;
                group=group.NextGroupInSameHashCode;
            }
            return null;
        }

        public void Dispose() {
            var pooledBuckets =this.buckets;
            this.DisposePooledElements();
            this.buckets      =null;
            this.bucketsLength=0;
            this.last         =null;
            this.groupCount      =0;
            if(pooledBuckets is not null) {
                ArrayPool<Grouping<TKey,TElement>>.Shared.Return(pooledBuckets,clearArray: true);
            }
        }

        private void DisposePooledElements() {
            var group =this.last?.NextGroupInAddOrder;
            if(group is null) {
                return;
            }
            var first = group;
            do {
                group.DisposePooledElements();
                group=group.NextGroupInAddOrder;
            } while(group is not null&&group!=first);
        }

        public Lookup<TKey,TElement> BuildAndClear() {
            if(this.groupCount==0||this.buckets is null) {
                return Lookup<TKey,TElement>.Empty;
            }

            var groups =this.buckets.AsSpan(0,this.bucketsLength).ToArray();
            ArrayPool<Grouping<TKey,TElement>>.Shared.Return(this.buckets,clearArray: true);
            var result = new Lookup<TKey,TElement>(groups,this.last,this.groupCount,this.comparer,groups.Length,false);
            this.buckets      =null;
            this.bucketsLength=0;
            this.last         =null;
            this.groupCount      =0;
            return result;
        }

        public Lookup<TKey,TElement> BuildPooledAndClear() {
            if(this.groupCount==0||this.buckets is null) {
                return Lookup<TKey,TElement>.Empty;
            }

            var groups=this.buckets;
            var length=this.bucketsLength;
            var result= new Lookup<TKey,TElement>(groups,this.last,this.groupCount,this.comparer,length,true);
            this.buckets      =null;
            this.bucketsLength=0;
            this.last         =null;
            this.groupCount      =0;
            return result;
        }

        // GroupBy only needs root group.
        public Grouping<TKey,TElement>? GetRootGroupAndClear() {
            if(this.groupCount==0||this.buckets is null) {
                return null;
            }

            ArrayPool<Grouping<TKey,TElement>>.Shared.Return(this.buckets,clearArray: true);

            return this.last?.NextGroupInAddOrder;
        }
        private void ResizeAndRehash() {
            Debug.Assert(this.buckets is not null);
            if(this.last is null) return;
            var group =this.last.NextGroupInAddOrder; // as first.
            if(group is null) {
                return;
            }

            var newSize = BitOperations.RoundUpToPowerOf2((uint)(this.bucketsLength*2));
            ArrayPool<Grouping<TKey,TElement>>.Shared.Return(this.buckets,clearArray: true);

            var newBuckets = ArrayPool<Grouping<TKey,TElement>>.Shared.Rent((int)newSize);
            this.buckets   =newBuckets;
            this.bucketsLength=(int)newSize;
            this.resizeThreshold=(int)(this.bucketsLength*LoadFactor);

            var first = group;
            do {
                group.NextGroupInSameHashCode=null;
                ref var bucket = ref newBuckets[this.GetBucketIndex(group.HashCode)];
                if(bucket==null) {
                    bucket=group;
                } else {
                    var next = bucket;
                    while(true) {
                        if(next.NextGroupInSameHashCode==null) {
                            next.NextGroupInSameHashCode=group;
                            break;
                        }
                        next=next.NextGroupInSameHashCode;
                    }
                }

                group=group.NextGroupInAddOrder;
            } while(group!=null&&group!=first);
            this.buckets=newBuckets;
        }
    }

    // .NET ILookup implements ICollection and it is public
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(LookupDebugView<,>))]
    public sealed class Lookup<TKey, TElement>:ILookup<TKey,TElement>, ICollection<IGrouping<TKey,TElement>>, IReadOnlyCollection<IGrouping<TKey,TElement>>, IDisposable {
        internal static readonly Lookup<TKey,TElement> Empty = new Lookup<TKey,TElement>();
        private Grouping<TKey,TElement>?[]? groups;
        private readonly Grouping<TKey,TElement>? last;
        private readonly int count;
        private readonly IEqualityComparer<TKey> comparer;
        // 既定コンパレータのときは true。値型では typeof(TKey).IsValueType が JIT で畳み込まれ、
        // EqualityComparer<TKey>.Default 経由の静的呼び出しに devirtualize/inline される。
        private readonly bool isDefaultComparer;
        private readonly int bucketCount;
        private Lookup() // for empty
        {
            this.groups  =null;
            this.last    =null;
            this.count   =0;
            this.comparer=null!;
            this.isDefaultComparer=true;
            this.bucketCount=0;
        }

        internal Lookup(Grouping<TKey,TElement>[]? groupings,Grouping<TKey,TElement>? last,int count,IEqualityComparer<TKey> comparer,int bucketCount,bool pooled) {
            this.comparer=comparer;
            this.isDefaultComparer=ReferenceEquals(comparer,EqualityComparer<TKey>.Default);
            if(groupings==null) {
                this.groups=null;
                this.last=null;
                this.count=0;
                this.bucketCount=0;
                return;
            }
            this.groups=groupings;
            this.count=count;
            this.last=last;
            this.bucketCount=pooled ? -bucketCount : bucketCount;
        }

        public IEnumerable<TElement> this[TKey key] {
            get {
                var group =this.GetGroup(key);
                return (group is null)
                    ? Array.Empty<TElement>()
                    : group;
            }
        }

        public int Count => this.count;

        // Lookup method

        public IEnumerable<TResult> ApplyResultSelector<TResult>(Func<TKey,IEnumerable<TElement>,TResult> resultSelector) {
            ArgumentNullException.ThrowIfNull(resultSelector);

            if(this.last is null) yield break;
            var group =this.last.NextGroupInAddOrder; // as first.
            if(group is null) yield break;

            var first = group;
            do {
                yield return resultSelector(group.Key,group);
                group=group.NextGroupInAddOrder;
            } while(group!=null&&group!=first);
        }

        public bool Contains(TKey key) {
            return this.GetGroup(key) is not null;
        }

        public Grouping<TKey,TElement>? GetGroup(TKey key) {
            if(this.groups is null) return null;

            var hashCode=this.InternalGetHashCode(key);
            var index   =this.GetBucketIndex((uint)hashCode);

            var group =this.groups[index];
            while(group is not null) {
                if(this.KeyEquals(group.Key,key)) {
                    return group;
                } else {
                    group=group.NextGroupInSameHashCode;
                }
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool KeyEquals(TKey x,TKey y) {
            if(typeof(TKey).IsValueType&&this.isDefaultComparer) {
                return EqualityComparer<TKey>.Default.Equals(x,y);
            }
            return this.comparer.Equals(x,y);
        }

        public IEnumerator<IGrouping<TKey,TElement>> GetEnumerator() {
            if(this.last is null) yield break;

            var group =this.last.NextGroupInAddOrder; // as first.
            if(group is null) yield break;

            var first = group;
            do {
                yield return group;
                group=group.NextGroupInAddOrder;
            } while(group!=null&&group!=first);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }

        // 【重要】LookupBuilder.GetBucketIndex と必ず同じ式にすること。片方だけ変えると
        // 構築したバケットと参照するバケットがずれ、GetGroup が静かに取りこぼす。
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucketIndex(uint hashCode) {
            var buckets =this.groups;
            Debug.Assert(buckets is not null);
            var length =this.bucketCount<0 ? -this.bucketCount : this.bucketCount;
            return (int)(hashCode&(uint)(length-1));
        }

        public void Dispose() {
            if(this.bucketCount>=0) return;
            var pooledGroups = Interlocked.Exchange(ref this.groups,null);
            if(pooledGroups is not null) {
                var group =this.last?.NextGroupInAddOrder;
                if(group is not null) {
                    var first = group;
                    do {
                        group.DisposePooledElements();
                        group=group.NextGroupInAddOrder;
                    } while(group is not null&&group!=first);
                }
                ArrayPool<Grouping<TKey,TElement>>.Shared.Return(pooledGroups,clearArray: true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint InternalGetHashCode(TKey key) {
            if(typeof(TKey).IsValueType&&this.isDefaultComparer) {
                return (uint)(EqualityComparer<TKey>.Default.GetHashCode(key!)&0x7FFFFFFF);
            }
            // allows null.
            return (uint)((key is null) ? 0 : this.comparer.GetHashCode(key)&0x7FFFFFFF);
        }

        // ICollection

        bool ICollection<IGrouping<TKey,TElement>>.IsReadOnly => true;

        void ICollection<IGrouping<TKey,TElement>>.Add(IGrouping<TKey,TElement> item) => throw new NotSupportedException();
        bool ICollection<IGrouping<TKey,TElement>>.Remove(IGrouping<TKey,TElement> item) => throw new NotSupportedException();
        void ICollection<IGrouping<TKey,TElement>>.Clear() => throw new NotSupportedException();

        bool ICollection<IGrouping<TKey,TElement>>.Contains(IGrouping<TKey,TElement> item) {
            var group =this.GetGroup(item.Key);
            if(group!=null&&group==item) {
                return true;
            }
            return false;
        }

        void ICollection<IGrouping<TKey,TElement>>.CopyTo(IGrouping<TKey,TElement>[] array,int arrayIndex) {
            ArgumentNullException.ThrowIfNull(array);
            if(arrayIndex             <0) throw new ArgumentOutOfRangeException(nameof(arrayIndex),           "Non-negative number required.");
            if(arrayIndex             >array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex),"Index was out of range. Must be non-negative and less than the size of the collection.");
            if(array.Length-arrayIndex<this.Count) throw new ArgumentOutOfRangeException(nameof(arrayIndex),     "Destination array is not long enough to copy all the items in the collection. Check array index and length.");

            if(this.last is null) return;

            var group =this.last.NextGroupInAddOrder; // as first.
            if(group is null) return;

            var first = group;
            do {
                array[arrayIndex]=group;
                ++arrayIndex;

                group=group.NextGroupInAddOrder;
            } while(group!=null&&group!=first);
        }
    }

    [DebuggerDisplay("Key = {Key}")]
    [DebuggerTypeProxy(typeof(GroupingDebugView<,>))]
    public class Grouping<TKey, TElement>:IGrouping<TKey,TElement>, IList<TElement>, IReadOnlyList<TElement> {
        private readonly TKey key;
        private readonly uint hashCode;
        protected TElement[] elements;
        protected int count;
        private readonly bool poolElements;
        private TElement firstElement;
        // 要素は elements[start..start+count)。共有配列の区間を指すグループ(ContiguousLookupBuilder が作る)は要素を足せない。
        private readonly int start;
        private readonly bool segment;

        public Grouping<TKey,TElement>? NextGroupInAddOrder;
        public Grouping<TKey,TElement>? NextGroupInSameHashCode;

        public Grouping(TKey key,uint hashCode,TElement value) {
            this.key     =key;
            this.hashCode=hashCode;
            this.elements=[value];
            this.count      =1;
            this.poolElements=false;
            this.firstElement=default!;
        }

        // 要素を詰め終えた配列からグループを作る(生成コードが要素数の分かるグループを直接組み立てるときに使う)。
        // 配列はこのグループが持つので、呼び出し側は以後書き換えない。
        public Grouping(TKey key,TElement[] elements,int count) {
            if((uint)count>(uint)elements.Length) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            this.key     =key;
            this.hashCode=0;
            this.elements=elements;
            this.count   =count;
            this.poolElements=false;
            this.firstElement=default!;
        }

        // 共有配列の区間 elements[start..start+count) を指すグループ。配列は書き換えない前提で、要素は足せない。
        internal Grouping(TKey key,uint hashCode,TElement[] elements,int start,int count) {
            this.key     =key;
            this.hashCode=hashCode;
            this.elements=elements;
            this.start   =start;
            this.count   =count;
            this.poolElements=false;
            this.segment =true;
            this.firstElement=default!;
        }

        internal Grouping(TKey key,uint hashCode,TElement value,bool poolElements) {
            this.key     =key;
            this.hashCode=hashCode;
            this.poolElements=poolElements;
            this.firstElement=value;
            this.elements   =poolElements ? [] : [value];
            this.count      =1;
        }

        public         TKey Key      => this.key;
        public         uint HashCode => this.hashCode;
        public virtual  int  Count    => this.count;

        // mutable in construction, but readonly in public surface

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Add(TElement value) {
            if(this.segment) {
                throw new NotSupportedException();
            }
            this.AddCore(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddCore(TElement value) {
            if(this.elements.Length==this.count) {
                if(this.poolElements&&this.elements.Length>=8) {
                    var next=ArrayPool<TElement>.Shared.Rent(checked(this.count*2));
                    this.elements.AsSpan(0,this.count).CopyTo(next);
                    if(this.elements.Length>=16) {
                        ArrayPool<TElement>.Shared.Return(this.elements,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
                    }
                    this.elements=next;
                } else {
                    Array.Resize(ref this.elements,checked(this.count*2));
                }
            } else if(this.poolElements&&this.count==1&&this.elements.Length==0) {
                this.elements   =new TElement[2];
                this.elements[0]=this.firstElement;
                this.elements[1]=value;
                this.count=2;
                return;
            }
            this.elements[this.count]=value;
            this.count++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddUnpooledCore(TElement value) {
            if(this.elements.Length==this.count) {
                Array.Resize(ref this.elements,checked(this.count*2));
            }
            this.elements[this.count]=value;
            this.count++;
        }
        internal void DisposePooledElements() {
            if(!this.poolElements||this.elements.Length<16) {
                return;
            }
            var pooledElements=this.elements;
            this.elements=[];
            this.firstElement=default!;
            this.count=0;
            ArrayPool<TElement>.Shared.Return(pooledElements,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
        }

        // we needs IList implementation for System.Linq internal optimization usage

        public bool IsReadOnly => true;

        public TElement this[int index] {
            get => this.GetElement(index);
            set => throw new NotSupportedException();
        }

        protected virtual TElement GetElement(int index) {
            if(this.poolElements&&this.elements.Length==0) {
                if((uint)index>=1u) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                return this.firstElement;
            }
            if((uint)index>=(uint)this.count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return this.elements[this.start+index];
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public IEnumerator<TElement> GetEnumerator() {
            for(int i = 0;i<this.count;i++) {
                yield return this[i];
            }
        }

        public virtual int IndexOf(TElement item) {
            if(this.poolElements&&this.elements.Length==0) {
                return EqualityComparer<TElement>.Default.Equals(this.firstElement,item) ? 0 : -1;
            }
            var index = Array.IndexOf(this.elements,item,this.start,this.count);
            return index<0 ? -1 : index-this.start;
        }

        public void Insert(int index,TElement item) {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index) {
            throw new NotSupportedException();
        }

        void ICollection<TElement>.Add(TElement item) {
            throw new NotSupportedException();
        }

        public void Clear() {
            throw new NotSupportedException();
        }

        public bool Contains(TElement item) {
            return this.IndexOf(item)>=0;
        }

        public virtual void CopyTo(TElement[] array,int arrayIndex) {
            if(this.poolElements&&this.elements.Length==0) {
                array[arrayIndex]=this.firstElement;
                return;
            }
            this.elements.AsSpan(this.start,this.count).CopyTo(array.AsSpan(arrayIndex));
        }

        public bool Remove(TElement item) {
            throw new NotSupportedException();
        }
    }

    internal sealed class GroupingDebugView<TKey,TElement>(Grouping<TKey,TElement> grouping) {
        private          TElement[]?             _cachedValues;
        public           TKey                    Key => grouping.Key;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public TElement[] Values => this._cachedValues??=grouping.ToArray();
    }

    internal sealed class LookupDebugView<TKey,TElement>(ILookup<TKey,TElement> lookup) {
        private          IGrouping<TKey,TElement>[]? _cachedGroupings;
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IGrouping<TKey,TElement>[] Groupings => this._cachedGroupings??=lookup.ToArray();
    }
}
