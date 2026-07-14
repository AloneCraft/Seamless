#pragma warning disable
namespace Seamless.System.Carrier {
    partial class ValueEnumerableExtensions {
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
                        lookupBuilder.Add(keySelector(item),item);
                    }
                } else {
                    while(enumerator.TryGetNext(out var item)) {
                        lookupBuilder.Add(keySelector(item),item);
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
                        lookupBuilder.Add(keySelector(item),elementSelector(item));
                    }
                } else {
                    while(enumerator.TryGetNext(out var item)) {
                        lookupBuilder.Add(keySelector(item),elementSelector(item));
                    }
                }

                return lookupBuilder.BuildAndClear();
            }
        }
    }
}

namespace Seamless.System.Carrier {
    internal static class Lookup {
        // Enumerable.Join operation ignores null key
        public static Lookup<TKey,TSource> CreateForJoin<TEnumerator, TSource, TKey>(ref TEnumerator source,Func<TSource,TKey> keySelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            var lookupBuilder = new LookupBuilder<TKey,TSource>(comparer??EqualityComparer<TKey>.Default);
            if(source.TryGetSpan(out var span)) {
                foreach(var item in span) {
                    var key = keySelector(item);
                    if(key is not null) {
                        lookupBuilder.Add(key,item);
                    }
                }
            } else {
                while(source.TryGetNext(out var item)) {
                    var key = keySelector(item);
                    if(key is not null) {
                        lookupBuilder.Add(key,item);
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
        private Grouping<TKey,TElement>[]? buckets;
        private int bucketsLength;
        private Grouping<TKey,TElement>? last;
        private int groupCount;
        private readonly bool poolElements;

        public LookupBuilder(IEqualityComparer<TKey>? comparer) {
            last=null;
            groupCount=0;
            poolElements=false;
            this.comparer=comparer??EqualityComparer<TKey>.Default;
        }

        public LookupBuilder(int capacity,IEqualityComparer<TKey>? comparer) {
            last=null;
            groupCount=0;
            poolElements=false;
            this.comparer=comparer??EqualityComparer<TKey>.Default;
            if(capacity<=0) {
                buckets=null;
                bucketsLength=0;
                return;
            }
            var bucketCapacity = (int)Math.Ceiling((capacity+1)/LoadFactor);
            bucketsLength=Math.Max((int)BitOperations.RoundUpToPowerOf2((uint)bucketCapacity),MinimumSize);
            buckets=ArrayPool<Grouping<TKey,TElement>>.Shared.Rent(bucketsLength);
            buckets.AsSpan(0,bucketsLength).Clear();
        }

        public LookupBuilder(int capacity,IEqualityComparer<TKey>? comparer,bool poolElements) : this(capacity,comparer) {
            this.poolElements=poolElements;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucketIndex(uint hashCode) {
            Debug.Assert(buckets is not null);
            return (int)(hashCode&(bucketsLength-1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint InternalGetHashCode(TKey key) {
            // allows null
            return (uint)((key is null) ? 0 : comparer.GetHashCode(key)&0x7FFFFFFF);
        }

        public void Add(TKey key,TElement value) {
            if(buckets==null) {
                buckets=ArrayPool<Grouping<TKey,TElement>>.Shared.Rent(MinimumSize);
                bucketsLength=MinimumSize;
            }

            var hash = InternalGetHashCode(key);
            var index = GetBucketIndex(hash);
            ref var bucket = ref buckets[index];

            if(bucket==null) // new slot
            {
                if(groupCount!=0) {
                    if((double)groupCount/bucketsLength>LoadFactor) {
                        ResizeAndRehash();
                        bucket=ref buckets[GetBucketIndex(hash)]!;
                    }
                }

                bucket=poolElements
                    ? new PooledGrouping<TKey,TElement>(key,hash,value)
                    : new Grouping<TKey,TElement>(key,hash,value);
                groupCount++;
                if(last!=null) {
                    bucket.NextGroupInAddOrder=last.NextGroupInAddOrder;
                    last.NextGroupInAddOrder=bucket;
                } else {
                    bucket.NextGroupInAddOrder=bucket;
                }
                last=bucket;
            } else {
                if(comparer.Equals(bucket.Key,key)) {
                    bucket.Add(value);
                    return; // added existing group
                }

                // conflict, try next chain
                var previous = bucket;
                var next = bucket.NextGroupInSameHashCode;
                while(next!=null) {
                    if(comparer.Equals(next.Key,key)) {
                        next.Add(value);
                        return; // added existing group
                    }
                    previous=next;
                    next=next.NextGroupInSameHashCode;
                }

                // create new and chain
                var newGroup = poolElements
                    ? new PooledGrouping<TKey,TElement>(key,hash,value)
                    : new Grouping<TKey,TElement>(key,hash,value);
                previous.NextGroupInSameHashCode=newGroup;

                groupCount++;
                if(last!=null) {
                    newGroup.NextGroupInAddOrder=last.NextGroupInAddOrder;
                    last.NextGroupInAddOrder=newGroup;
                } else {
                    newGroup.NextGroupInAddOrder=newGroup;
                }
                last=newGroup;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Grouping<TKey,TElement>? GetGroup(TKey key) {
            if(buckets is null) return null;
            var hash = InternalGetHashCode(key);
            var group = buckets[GetBucketIndex(hash)];
            while(group is not null) {
                if(comparer.Equals(group.Key,key)) return group;
                group=group.NextGroupInSameHashCode;
            }
            return null;
        }

        public void Dispose() {
            var pooledBuckets = buckets;
            DisposePooledElements();
            buckets=null;
            bucketsLength=0;
            last=null;
            groupCount=0;
            if(pooledBuckets is not null) {
                ArrayPool<Grouping<TKey,TElement>>.Shared.Return(pooledBuckets,clearArray: true);
            }
        }

        private void DisposePooledElements() {
            var group = last?.NextGroupInAddOrder;
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
            if(groupCount==0||buckets is null) {
                return Lookup<TKey,TElement>.Empty;
            }

            var groups = buckets.AsSpan(0,bucketsLength).ToArray();
            ArrayPool<Grouping<TKey,TElement>>.Shared.Return(buckets,clearArray: true);
            var result = new Lookup<TKey,TElement>(groups,last,groupCount,comparer,groups.Length,false);
            buckets=null;
            bucketsLength=0;
            last=null;
            groupCount=0;
            return result;
        }

        public Lookup<TKey,TElement> BuildPooledAndClear() {
            if(groupCount==0||buckets is null) {
                return Lookup<TKey,TElement>.Empty;
            }

            var groups = buckets;
            var length = bucketsLength;
            var result = new Lookup<TKey,TElement>(groups,last,groupCount,comparer,length,true);
            buckets=null;
            bucketsLength=0;
            last=null;
            groupCount=0;
            return result;
        }

        // GroupBy only needs root group.
        public Grouping<TKey,TElement>? GetRootGroupAndClear() {
            if(groupCount==0||buckets is null) {
                return null;
            }

            ArrayPool<Grouping<TKey,TElement>>.Shared.Return(buckets,clearArray: true);

            return last?.NextGroupInAddOrder;
        }
        private void ResizeAndRehash() {
            Debug.Assert(buckets is not null);
            if(last is null) return;
            var group = last.NextGroupInAddOrder; // as first.
            if(group is null) {
                return;
            }

            var newSize = BitOperations.RoundUpToPowerOf2((uint)(bucketsLength*2));
            ArrayPool<Grouping<TKey,TElement>>.Shared.Return(buckets,clearArray: true);

            var newBuckets = ArrayPool<Grouping<TKey,TElement>>.Shared.Rent((int)newSize);
            buckets=newBuckets;
            bucketsLength=(int)newSize;

            var first = group;
            do {
                group.NextGroupInSameHashCode=null;
                ref var bucket = ref newBuckets[GetBucketIndex(group.HashCode)];
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

            buckets=newBuckets;
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
        private readonly int bucketCount;
        private Lookup() // for empty
        {
            groups=null;
            last=null;
            count=0;
            comparer=null!;
            bucketCount=0;
        }

        internal Lookup(Grouping<TKey,TElement>[]? groupings,Grouping<TKey,TElement>? last,int count,IEqualityComparer<TKey> comparer,int bucketCount,bool pooled) {
            if(groupings==null) {
                groups=null;
                this.last=null;
                this.count=0;
                this.comparer=comparer;
                this.bucketCount=0;
                return;
            }

            groups=groupings;
            this.count=count;
            this.last=last;
            this.comparer=comparer;
            this.bucketCount=pooled ? -bucketCount : bucketCount;
        }

        public IEnumerable<TElement> this[TKey key] {
            get {
                var group = GetGroup(key);
                return (group is null)
                    ? Array.Empty<TElement>()
                    : group;
            }
        }

        public int Count => count;

        // Lookup method

        public IEnumerable<TResult> ApplyResultSelector<TResult>(Func<TKey,IEnumerable<TElement>,TResult> resultSelector) {
            ArgumentNullException.ThrowIfNull(resultSelector);

            if(last is null) yield break;
            var group = last.NextGroupInAddOrder; // as first.
            if(group is null) yield break;

            var first = group;
            do {
                yield return resultSelector(group.Key,group);
                group=group.NextGroupInAddOrder;
            } while(group!=null&&group!=first);
        }

        public bool Contains(TKey key) {
            return GetGroup(key) is not null;
        }

        public Grouping<TKey,TElement>? GetGroup(TKey key) {
            if(groups is null) return null;

            var hashCode = InternalGetHashCode(key);
            var index = GetBucketIndex((uint)hashCode);

            var group = groups[index];
            while(group is not null) {
                if(comparer.Equals(group.Key,key)) {
                    return group;
                } else {
                    group=group.NextGroupInSameHashCode;
                }
            }

            return null;
        }

        public IEnumerator<IGrouping<TKey,TElement>> GetEnumerator() {
            if(last is null) yield break;

            var group = last.NextGroupInAddOrder; // as first.
            if(group is null) yield break;

            var first = group;
            do {
                yield return group;
                group=group.NextGroupInAddOrder;
            } while(group!=null&&group!=first);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucketIndex(uint hashCode) {
            var buckets = groups;
            Debug.Assert(buckets is not null);
            var length = bucketCount<0 ? -bucketCount : bucketCount;
            return (int)(hashCode&(length-1));
        }

        public void Dispose() {
            if(bucketCount>=0) return;
            var pooledGroups = Interlocked.Exchange(ref groups,null);
            if(pooledGroups is not null) {
                var group = last?.NextGroupInAddOrder;
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
            // allows null.
            return (uint)((key is null) ? 0 : comparer.GetHashCode(key)&0x7FFFFFFF);
        }

        // ICollection

        bool ICollection<IGrouping<TKey,TElement>>.IsReadOnly => true;

        void ICollection<IGrouping<TKey,TElement>>.Add(IGrouping<TKey,TElement> item) => throw new NotSupportedException();
        bool ICollection<IGrouping<TKey,TElement>>.Remove(IGrouping<TKey,TElement> item) => throw new NotSupportedException();
        void ICollection<IGrouping<TKey,TElement>>.Clear() => throw new NotSupportedException();

        bool ICollection<IGrouping<TKey,TElement>>.Contains(IGrouping<TKey,TElement> item) {
            var group = GetGroup(item.Key);
            if(group!=null&&group==item) {
                return true;
            }
            return false;
        }

        void ICollection<IGrouping<TKey,TElement>>.CopyTo(IGrouping<TKey,TElement>[] array,int arrayIndex) {
            ArgumentNullException.ThrowIfNull(array);
            if(arrayIndex<0) throw new ArgumentOutOfRangeException(nameof(arrayIndex),"Non-negative number required.");
            if(arrayIndex>array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex),"Index was out of range. Must be non-negative and less than the size of the collection.");
            if(array.Length-arrayIndex<Count) throw new ArgumentOutOfRangeException(nameof(arrayIndex),"Destination array is not long enough to copy all the items in the collection. Check array index and length.");

            if(last is null) return;

            var group = last.NextGroupInAddOrder; // as first.
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

        public Grouping<TKey,TElement>? NextGroupInAddOrder;
        public Grouping<TKey,TElement>? NextGroupInSameHashCode;

        public Grouping(TKey key,uint hashCode,TElement value) {
            this.key=key;
            this.hashCode=hashCode;
            elements= [value];
            count=1;
        }

        protected Grouping(TKey key,uint hashCode) {
            this.key=key;
            this.hashCode=hashCode;
            elements= [];
        }

        public TKey Key => key;
        public uint HashCode => hashCode;
        public virtual int Count => count;

        // mutable in construction, but readonly in public surface

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Add(TElement value) {
            if(elements.Length==count) {
                Array.Resize(ref elements,checked(count*2));
            }

            elements[count]=value;
            count++;
        }

        internal virtual void DisposePooledElements() {
        }

        // we needs IList implementation for System.Linq internal optimization usage

        public bool IsReadOnly => true;

        public TElement this[int index] {
            get => GetElement(index);
            set => throw new NotSupportedException();
        }

        protected virtual TElement GetElement(int index) => elements.AsSpan(0,count)[index];

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<TElement> GetEnumerator() {
            for(int i = 0;i<count;i++) {
                yield return this[i];
            }
        }

        public virtual int IndexOf(TElement item) {
            return Array.IndexOf(elements,item,0,count);
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
            return IndexOf(item)>=0;
        }

        public virtual void CopyTo(TElement[] array,int arrayIndex) {
            elements.AsSpan(0,count).CopyTo(array.AsSpan(arrayIndex));
        }

        public bool Remove(TElement item) {
            throw new NotSupportedException();
        }
    }

    internal sealed class PooledGrouping<TKey, TElement>:Grouping<TKey,TElement> {
        private TElement firstElement;

        public PooledGrouping(TKey key,uint hashCode,TElement value) : base(key,hashCode) {
            firstElement=value;
            count=1;
        }

        public override void Add(TElement value) {
            if(count==1&&elements.Length==0) {
                elements=new TElement[2];
                elements[0]=firstElement;
                elements[1]=value;
                count=2;
                return;
            }
            if(elements.Length==count) {
                if(elements.Length>=8) {
                    var next = ArrayPool<TElement>.Shared.Rent(checked(count*2));
                    elements.AsSpan(0,count).CopyTo(next);
                    if(elements.Length>=16) {
                        ArrayPool<TElement>.Shared.Return(elements,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
                    }
                    elements=next;
                } else {
                    Array.Resize(ref elements,checked(count*2));
                }
            }
            elements[count++]=value;
        }

        protected override TElement GetElement(int index) {
            if((uint)index>=(uint)count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return elements.Length==0 ? firstElement : elements[index];
        }

        public override int IndexOf(TElement item) {
            if(elements.Length==0) {
                return EqualityComparer<TElement>.Default.Equals(firstElement,item) ? 0 : -1;
            }
            return base.IndexOf(item);
        }

        public override void CopyTo(TElement[] array,int arrayIndex) {
            if(elements.Length==0) {
                array[arrayIndex]=firstElement;
            } else {
                base.CopyTo(array,arrayIndex);
            }
        }

        internal override void DisposePooledElements() {
            if(elements.Length<16) {
                return;
            }
            var pooledElements = elements;
            elements= [];
            firstElement=default!;
            count=0;
            ArrayPool<TElement>.Shared.Return(pooledElements,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());
        }
    }

    internal sealed class GroupingDebugView<TKey, TElement> {
        private readonly Grouping<TKey,TElement> _grouping;
        private TElement[]? _cachedValues;

        public GroupingDebugView(Grouping<TKey,TElement> grouping) {
            _grouping=grouping;
        }

        public TKey Key => _grouping.Key;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public TElement[] Values => _cachedValues??=_grouping.ToArray();
    }

    internal sealed class LookupDebugView<TKey, TElement> {
        private readonly ILookup<TKey,TElement> _lookup;
        private IGrouping<TKey,TElement>[]? _cachedGroupings;

        public LookupDebugView(ILookup<TKey,TElement> lookup) {
            _lookup=lookup;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IGrouping<TKey,TElement>[] Groupings => _cachedGroupings??=_lookup.ToArray();
    }
}
