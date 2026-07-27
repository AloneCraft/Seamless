#if NET8_0_OR_GREATER
#endif

namespace Seamless.Linq {
    public static partial class Enumerable {
        public static ValueEnumerable<FromNonGenericEnumerable<object>,object> AsValueEnumerable(this IEnumerable source) {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromNonGenericEnumerable<T>,T> AsValueEnumerable<T>(this IEnumerable source) {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromEnumerable<T>,T> AsValueEnumerable<T>(this IEnumerable<T> source) {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromArray<T>,T> AsValueEnumerable<T>(this T[] source) {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromList<T>,T> AsValueEnumerable<T>(this List<T> source) {
            return new(new(Throws.IfNull(source)));
        }

#if NET9_0_OR_GREATER
        public static ValueEnumerable<FromMemory<T>,T> AsValueEnumerable<T>(this ArraySegment<T> source) {
            return new(new(source));
        }

        public static ValueEnumerable<FromMemory<T>,T> AsValueEnumerable<T>(this Memory<T> source) {
            return new(new(source));
        }

        public static ValueEnumerable<FromMemory<T>,T> AsValueEnumerable<T>(this ReadOnlyMemory<T> source) {
            return new(new(source));
        }

        public static ValueEnumerable<FromReadOnlySequence<T>,T> AsValueEnumerable<T>(this ReadOnlySequence<T> source) {
            return new(new(source));
        }
#endif

        // for global::System.Collections.Generic

        public static ValueEnumerable<FromDictionary<TKey,TValue>,KeyValuePair<TKey,TValue>> AsValueEnumerable<TKey, TValue>(this Dictionary<TKey,TValue> source)
            where TKey : notnull {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromSortedDictionary<TKey,TValue>,KeyValuePair<TKey,TValue>> AsValueEnumerable<TKey, TValue>(this SortedDictionary<TKey,TValue> source)
            where TKey : notnull {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromQueue<T>,T> AsValueEnumerable<T>(this Queue<T> source) {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromStack<T>,T> AsValueEnumerable<T>(this Stack<T> source) {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromLinkedList<T>,T> AsValueEnumerable<T>(this LinkedList<T> source) {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromHashSet<T>,T> AsValueEnumerable<T>(this global::System.Collections.Generic.HashSet<T> source) {
            return new(new(Throws.IfNull(source)));
        }

        public static ValueEnumerable<FromSortedSet<T>,T> AsValueEnumerable<T>(this SortedSet<T> source) {
            return new(new(Throws.IfNull(source)));
        }

#if NET8_0_OR_GREATER

        public static ValueEnumerable<FromImmutableArray<T>,T> AsValueEnumerable<T>(this ImmutableArray<T> source) {
            return new(new(source));
        }

        public static ValueEnumerable<FromImmutableHashSet<T>,T> AsValueEnumerable<T>(this ImmutableHashSet<T> source) {
            return new(new(Throws.IfNull(source)));
        }

#endif


    }
}

namespace Seamless.Linq {
    internal struct FromEnumerableContent(object source) {
        public object Source = source;
        public int Index;
        [MethodImpl]
        public void ThrowIfNoEnumerable() {
            if(this.Index>=0) return;
            Throw();
            return;

            void Throw() {
                throw new InvalidOperationException("The enumerable is no longer available.");
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromEnumerable<T>:IValueEnumerator<T> {
        private readonly CollectionIterator<T> iterator;
        private FromEnumerableContent content;

        public FromEnumerable(IEnumerable<T> source) {
            if(source.GetType()==typeof(T[])) {
                this.iterator=ArrayIterator<T>.Instance;
            } else if(source.GetType()==typeof(List<T>)) {
                this.iterator=ListIterator<T>.Instance;
            } else if(source is IReadOnlyList<T>) {
                this.iterator=IReadOnlyListIterator<T>.Instance;
            } else if(source is IList<T>) {
                this.iterator=IListIterator<T>.Instance;
            } else {
                this.iterator=EnumerableIterator<T>.Instance;
            }

            this.content=new(source);
        }

        // for Contains, need to check ICollection of IEqualityComparer due to compatibility
        internal IEnumerable<T> GetSource() {
            this.content.ThrowIfNoEnumerable();
            return Unsafe.As<IEnumerable<T>>(this.content.Source);
        }

        public bool TryGetNonEnumeratedCount(out int count) {
            this.content.ThrowIfNoEnumerable();
            return this.iterator.TryGetNonEnumeratedCount(Unsafe.As<IEnumerable<T>>(this.content.Source),out count);
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            this.content.ThrowIfNoEnumerable();
            return this.iterator.TryGetSpan(Unsafe.As<IEnumerable<T>>(this.content.Source),out span);
        }

        public bool TryCopyTo(Span<T> destination,Index offset) {
            this.content.ThrowIfNoEnumerable();
            return this.iterator.TryCopyTo(Unsafe.As<IEnumerable<T>>(this.content.Source),destination,offset);
        }

        public bool TryGetNext(out T current) => this.iterator.TryGetNext(ref this.content,out current);

        public void Dispose() {
            if(this.content.Index>=0) return;
            Unsafe.As<IEnumerator<T>>(this.content.Source).Dispose();
        }
    }

    // I've tried multiple implementations(such as switch direct call and managed function pointer),
    // but using virtual methods showed the best performance.
    internal abstract class CollectionIterator<T> {
        public abstract bool TryGetNonEnumeratedCount(IEnumerable<T> source,out int count);

        public virtual bool TryGetSpan(IEnumerable<T> source,out ReadOnlySpan<T> span) {
            span=default;
            return false;
        }

        public virtual bool TryCopyTo(IEnumerable<T> source,Span<T> destination,Index offset) {
            return false;
        }

        public abstract bool TryGetNext(ref FromEnumerableContent content,out T current);
    }

    internal sealed class ArrayIterator<T>:CollectionIterator<T> {
        public static readonly ArrayIterator<T> Instance = new();
        private ArrayIterator() { }

        public override bool TryGetNonEnumeratedCount(IEnumerable<T> source,out int count) {
            count=Unsafe.As<IEnumerable<T>,T[]>(ref source).Length;
            return true;
        }

        public override bool TryGetSpan(IEnumerable<T> source,out ReadOnlySpan<T> span) {
            span=Unsafe.As<IEnumerable<T>,T[]>(ref source);
            return true;
        }

        public override bool TryCopyTo(IEnumerable<T> source,Span<T> destination,Index offset) {
            if(EnumeratorHelper.TryGetSlice<T>(Unsafe.As<IEnumerable<T>,T[]>(ref source),offset,destination.Length,out var slice)) {
                slice.CopyTo(destination);
                return true;
            }
            return false;
        }

        public override bool TryGetNext(ref FromEnumerableContent content,out T current) {
            var index = content.Index;
            var src = Unsafe.As<T[]>(content.Source);
            if((uint)index<(uint)src.Length) {
                current=src[index];
                content.Index=index+1;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }
    }

    internal sealed class ListIterator<T>:CollectionIterator<T> {
        public static readonly ListIterator<T> Instance = new();
        private ListIterator() { }

        public override bool TryGetNonEnumeratedCount(IEnumerable<T> source,out int count) {
            count=Unsafe.As<IEnumerable<T>,List<T>>(ref source).Count;
            return true;
        }

        public override bool TryGetSpan(IEnumerable<T> source,out ReadOnlySpan<T> span) {
            span=CollectionsMarshal.AsSpan(Unsafe.As<IEnumerable<T>,List<T>>(ref source));
            return true;
        }

        public override bool TryCopyTo(IEnumerable<T> source,Span<T> destination,Index offset) {
            if(EnumeratorHelper.TryGetSlice<T>(CollectionsMarshal.AsSpan(Unsafe.As<IEnumerable<T>,List<T>>(ref source)),offset,destination.Length,out var slice)) {
                slice.CopyTo(destination);
                return true;
            }
            return false;
        }

        public override bool TryGetNext(ref FromEnumerableContent content,out T current) {
            var index = content.Index;
            var src = Unsafe.As<List<T>>(content.Source);
            if((uint)index<(uint)src.Count) {
                current=src[index];
                content.Index=index+1;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }
    }

    internal sealed class IListIterator<T>:CollectionIterator<T> {
        public static readonly IListIterator<T> Instance = new();
        private IListIterator() { }

        public override bool TryGetNonEnumeratedCount(IEnumerable<T> source,out int count) {
            count=Unsafe.As<IEnumerable<T>,IList<T>>(ref source).Count;
            return true;
        }

        public override bool TryGetNext(ref FromEnumerableContent content,out T current) {
            var index = content.Index;
            var src = Unsafe.As<IList<T>>(content.Source);
            if((uint)index<(uint)src.Count) {
                current=src[index];
                content.Index=index+1;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }
    }

    internal sealed class IReadOnlyListIterator<T>:CollectionIterator<T> {
        public static readonly IReadOnlyListIterator<T> Instance = new();
        private IReadOnlyListIterator() { }

        public override bool TryGetNonEnumeratedCount(IEnumerable<T> source,out int count) {
            count=Unsafe.As<IEnumerable<T>,IReadOnlyList<T>>(ref source).Count;
            return true;
        }

        public override bool TryGetNext(ref FromEnumerableContent content,out T current) {
            var index = content.Index;
            var src = Unsafe.As<IReadOnlyList<T>>(content.Source);
            if((uint)index<(uint)src.Count) {
                current=src[index];
                content.Index=index+1;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }
    }

    internal sealed class EnumerableIterator<T>:CollectionIterator<T> {
        public static readonly EnumerableIterator<T> Instance = new();
        private EnumerableIterator() { }

        public override bool TryGetNonEnumeratedCount(IEnumerable<T> source,out int count) {
#if NET8_0_OR_GREATER
            if(source.TryGetNonEnumeratedCount(out count)) // SEAMLESS-LINQ-FORWARD: System.Linq.Enumerable.TryGetNonEnumeratedCountへ意図的に委譲する。
            {
                return true;
            }
#else
            if (source is ICollection<T> c)
            {
                count = c.Count;
                return true;
            }
#endif
            else if(source is IReadOnlyCollection<T> rc) // Enumerable.TryGetNonEnumeratedCount does not check IReadOnlyCollection
            {
                count=rc.Count;
                return true;
            }
            count=0;
            return false;
        }

        public override bool TryGetNext(ref FromEnumerableContent content,out T current) {
            var enumerator = Unsafe.As<IEnumerator<T>>(content.Source);
            if(content.Index==0) {
                enumerator=Initialize(ref content);
            }

            if(enumerator.MoveNext()) {
                current=enumerator.Current;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;

            [MethodImpl(MethodImplOptions.NoInlining)]
            static IEnumerator<T> Initialize(ref FromEnumerableContent content) {
                var enumerator = Unsafe.As<IEnumerable<T>>(content.Source).GetEnumerator();
                content.Source=enumerator;
                content.Index=-1;

                // ReSharper disable once NotDisposedResourceIsReturned
                return enumerator;
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromNonGenericEnumerable<T>(IEnumerable source):IValueEnumerator<T> {
        private IEnumerator? enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            if(source is ICollection c) {
                count=c.Count;
                return true;
            }
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) {
            return false;
        }

        public bool TryGetNext(out T current) {
            if(this.enumerator==null) {
                this.enumerator=source.GetEnumerator();
            }

            if(this.enumerator.MoveNext()) {
                var value = this.enumerator.Current;
                current=(T)value;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.enumerator is IDisposable d) {
                d.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromArray<T>(T[] source):IValueEnumerator<T> {
        private int index;

        // becareful, don't call AsSpan
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T[] GetSource() => source;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Length;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            // AsSpan is failed by array variance
            if(source.GetType()!=typeof(T[])) {
                span=default;
                return false;
            }

            span=source.AsSpan();
            return true;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) {
            if(EnumeratorHelper.TryGetSlice<T>(source,offset,destination.Length,out var slice)) {
                slice.CopyTo(destination);
                return true;
            }
            return false;
        }

        public bool TryGetNext(out T current) {
            if((uint)this.index<(uint)source.Length) {
                current=source[this.index];
                this.index++;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }
    }

#if NET9_0_OR_GREATER
    [StructLayout(LayoutKind.Auto)]
    public struct FromMemory<T>(ReadOnlyMemory<T> source):IValueEnumerator<T> {
        private ReadOnlyMemory<T> source = source;
        private int index;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=this.source.Length;
            return true;
        }

        public bool TryCopyTo(scoped Span<T> destination,Index offset) {
            var span = this.source.Span;
            if(EnumeratorHelper.TryGetSlice<T>(span,offset,destination.Length,out var slice)) {
                slice.CopyTo(destination);
                return true;
            }
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=this.source.Span;
            return true;
        }

        public bool TryGetNext(out T current) {
            if((uint)this.index<(uint)this.source.Length) {
                current=this.source.Span[this.index];
                this.index++;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }
    }
#endif

    [StructLayout(LayoutKind.Auto)]
    public struct FromList<T>(List<T> source):IValueEnumerator<T> {
        private int index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal List<T> GetSource() => source;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=CollectionsMarshal.AsSpan(source);
            return true;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) {
            var span = CollectionsMarshal.AsSpan(source);
            if(EnumeratorHelper.TryGetSlice<T>(span,offset,destination.Length,out var slice)) {
                slice.CopyTo(destination);
                return true;
            }
            return false;
        }

        public bool TryGetNext(out T current) {
            if((uint)this.index<(uint)source.Count) {
                current=source[this.index];
                this.index++;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromDictionary<TKey, TValue>(Dictionary<TKey,TValue> source):IValueEnumerator<KeyValuePair<TKey,TValue>>
        where TKey : notnull {
        private bool isInit = false;
        private Dictionary<TKey,TValue>.Enumerator enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<KeyValuePair<TKey,TValue>> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(Span<KeyValuePair<TKey,TValue>> destination,Index offset) => false;

        public bool TryGetNext(out KeyValuePair<TKey,TValue> current) {
            if(!this.isInit) {
                this.isInit=true;
                this.enumerator=source.GetEnumerator();
            }

            if(this.enumerator.MoveNext()) {
                current=this.enumerator.Current;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.isInit) {
                this.enumerator.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromSortedDictionary<TKey, TValue>(SortedDictionary<TKey,TValue> source):IValueEnumerator<KeyValuePair<TKey,TValue>>
        where TKey : notnull {
        private bool isInit = false;
        private SortedDictionary<TKey,TValue>.Enumerator enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<KeyValuePair<TKey,TValue>> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(Span<KeyValuePair<TKey,TValue>> destination,Index offset) => false;

        public bool TryGetNext(out KeyValuePair<TKey,TValue> current) {
            if(!this.isInit) {
                this.isInit=true;
                this.enumerator=source.GetEnumerator();
            }

            if(this.enumerator.MoveNext()) {
                current=this.enumerator.Current;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.isInit) {
                this.enumerator.Dispose();
            }
        }
    }

#if NET9_0_OR_GREATER
    [StructLayout(LayoutKind.Auto)]
    public struct FromReadOnlySequence<T>(ReadOnlySequence<T> source):IValueEnumerator<T> {
        private bool isInit = false;
        private ReadOnlySequence<T>.Enumerator sequenceEnumerator;
        private FromMemory<T> enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            const int ArrayMaxLength = 0X7FFFFFC7;
            if(source.Length<=ArrayMaxLength) {
                count=checked((int)source.Length);
                return true;
            }
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            if(source.IsSingleSegment) {
                span=source.First.Span;
                return true;
            }

            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<T> destination,Index offset) {
            if(source.IsSingleSegment) {
                var span = source.First.Span;
                if(EnumeratorHelper.TryGetSlice<T>(span,offset,destination.Length,out var slice)) {
                    slice.CopyTo(destination);
                    return true;
                }
            } else {
                if(EnumeratorHelper.TryGetSliceRange(checked((int)source.Length),offset,destination.Length,out var start,out var count)) {
                    source.Slice(start,count).CopyTo(destination);
                    return true;
                }
            }

            return false;
        }

        public bool TryGetNext(out T current) {
            if(!this.isInit) {
                this.isInit=true;
                this.sequenceEnumerator=source.GetEnumerator();
            }

        MOVE_NEXT:
            if(this.enumerator.TryGetNext(out current)) {
                return true;
            }

            if(this.sequenceEnumerator.MoveNext()) {
                this.enumerator=this.sequenceEnumerator.Current.AsValueEnumerable().Enumerator;
                goto MOVE_NEXT;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            // no needs FromMemory<T>.Dispose
        }
    }
#endif

    [StructLayout(LayoutKind.Auto)]
    public struct FromQueue<T>(Queue<T> source):IValueEnumerator<T> {
        private bool isInit;
        private Queue<T>.Enumerator enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) => false;

        public bool TryGetNext(out T current) {
            if(!this.isInit) {
                this.isInit=true;
                this.enumerator=source.GetEnumerator();
            }

            if(this.enumerator.MoveNext()) {
                current=this.enumerator.Current;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.isInit) {
                this.enumerator.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromStack<T>(Stack<T> source):IValueEnumerator<T> {
        private bool isInit;
        private Stack<T>.Enumerator enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) => false;

        public bool TryGetNext(out T current) {
            if(!this.isInit) {
                this.isInit=true;
                this.enumerator=source.GetEnumerator();
            }

            if(this.enumerator.MoveNext()) {
                current=this.enumerator.Current;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.isInit) {
                this.enumerator.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromLinkedList<T>(LinkedList<T> source):IValueEnumerator<T> {
        private bool isInit;
        private LinkedList<T>.Enumerator enumerator;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) => false;

        public bool TryGetNext(out T current) {
            if(!this.isInit) {
                this.isInit=true;
                this.enumerator=source.GetEnumerator();
            }

            if(this.enumerator.MoveNext()) {
                current=this.enumerator.Current;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.isInit) {
                this.enumerator.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromHashSet<T>(global::System.Collections.Generic.HashSet<T> source):IValueEnumerator<T> {
        private bool isInit;
        private global::System.Collections.Generic.HashSet<T>.Enumerator enumerator;

        // for Contains, need to check ICollection of IEqualityComparer due to compatibility
        internal global::System.Collections.Generic.HashSet<T> GetSource() => source;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) => false;

        public bool TryGetNext(out T current) {
            if(!this.isInit) {
                this.isInit=true;
                this.enumerator=source.GetEnumerator();
            }

            if(this.enumerator.MoveNext()) {
                current=this.enumerator.Current;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.isInit) {
                this.enumerator.Dispose();
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromSortedSet<T>(SortedSet<T> source):IValueEnumerator<T> {
        private bool isInit;
        private SortedSet<T>.Enumerator enumerator;

        // for Contains, need to check ICollection of IEqualityComparer due to compatibility
        internal SortedSet<T> GetSource() => source;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) => false;

        public bool TryGetNext(out T current) {
            if(!this.isInit) {
                this.isInit=true;
                this.enumerator=source.GetEnumerator();
            }

            if(this.enumerator.MoveNext()) {
                current=this.enumerator.Current;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.isInit) {
                this.enumerator.Dispose();
            }
        }
    }

#if NET8_0_OR_GREATER
    [StructLayout(LayoutKind.Auto)]
    public struct FromImmutableArray<T>(ImmutableArray<T> source):IValueEnumerator<T> {
        private int index;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Length;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=source.AsSpan();
            return true;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) {
            if(EnumeratorHelper.TryGetSlice<T>(source.AsSpan(),offset,destination.Length,out var slice)) {
                slice.CopyTo(destination);
                return true;
            }
            return false;
        }

        public bool TryGetNext(out T current) {
            if((uint)this.index<(uint)source.Length) {
                current=source[this.index];
                this.index++;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }
    }

    [StructLayout(LayoutKind.Auto)]
    public struct FromImmutableHashSet<T>(ImmutableHashSet<T> source):IValueEnumerator<T> {
        private bool isInit;
        private ImmutableHashSet<T>.Enumerator enumerator;

        internal ImmutableHashSet<T> GetSource() => source;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=source.Count;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) => false;

        public bool TryGetNext(out T current) {
            if(!this.isInit) {
                this.isInit=true;
                this.enumerator=source.GetEnumerator();
            }

            if(this.enumerator.MoveNext()) {
                current=this.enumerator.Current;
                return true;
            }

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.isInit) {
                this.enumerator.Dispose();
            }
        }
    }

#endif


}
