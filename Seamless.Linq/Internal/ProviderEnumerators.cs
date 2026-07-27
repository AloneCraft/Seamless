namespace Seamless.Linq;

public struct PersistedSetEnumerator<T>(global::Seamless.Collections.Persisted.Set<T> source):IValueEnumerator<T> {
    private global::Seamless.Collections.Persisted.Set<T>? source=source;
    private global::Seamless.Collections.PersistedList<T>.Enumerator enumerator;
    private bool initialized;

    // O(1) の membership lookup は Contains.cs の型特殊化オーバーロードから使う。
    // interface メンバにすると値型が constrained. callvirt で box されるため置かない。
    [return: UnorderedEnumerable]
    internal global::Seamless.Collections.Persisted.Set<T> GetSource() => this.source!;

    public bool TryGetNonEnumeratedCount(out int count) {
        if(!this.initialized) {
            count=this.source!.Count;
            return true;
        }
        count=0;
        return false;
    }
    public bool TryGetSpan(out global::System.ReadOnlySpan<T> span) {
        span=default;
        return false;
    }
    public bool TryCopyTo(scoped global::System.Span<T> destination,global::System.Index offset) => false;
    public bool TryGetNext(out T current) {
        if(!this.initialized) {
            this.enumerator=this.source!.GetEnumerator();
            this.initialized=true;
        }
        if(this.enumerator.MoveNext()) {
            current=this.enumerator.Current;
            return true;
        }
        global::System.Runtime.CompilerServices.Unsafe.SkipInit(out current);
        return false;
    }
    public void Dispose() {
        if(this.initialized) this.enumerator.Dispose();
        this.source=null;
    }
}

public struct TsavoriteHashSetEnumerator<T>(global::Seamless.Collections.Tsavorite.HashSet<T> source):IValueEnumerator<T> {
    private global::Seamless.Collections.Tsavorite.HashSet<T>? source=source;
    private global::Seamless.Collections.Tsavorite.HashSet<T>.ValueEnumerator enumerator;
    private bool initialized;

    // O(1) の membership lookup は Contains.cs の型特殊化オーバーロードから使う。
    // interface メンバにすると値型が constrained. callvirt で box されるため置かない。
    [return: UnorderedEnumerable]
    internal global::Seamless.Collections.Tsavorite.HashSet<T> GetSource() => this.source!;

    public bool TryGetNonEnumeratedCount(out int count) {
        if(!this.initialized) {
            count=this.source!.Count;
            return true;
        }
        count=0;
        return false;
    }
    public bool TryGetSpan(out global::System.ReadOnlySpan<T> span) {
        span=default;
        return false;
    }
    public bool TryCopyTo(scoped global::System.Span<T> destination,global::System.Index offset) => false;
    public bool TryGetNext(out T current) {
        if(!this.initialized) {
            this.enumerator=this.source!.GetValueEnumerator();
            this.initialized=true;
        }
        return this.enumerator.TryGetNext(out current);
    }
    public void Dispose() {
        if(this.initialized) this.enumerator.Dispose();
        this.source=null;
    }
}

public struct TsavoriteSetEnumerator<TKey,TRecord>(global::Seamless.Collections.Tsavorite.Set<TKey,TRecord> source):IValueEnumerator<TRecord>
    where TRecord : global::Seamless.Collections.IRecord<TKey> {
    private global::Seamless.Collections.Tsavorite.Set<TKey,TRecord>? source=source;
    private global::Seamless.Collections.Tsavorite.Set<TKey,TRecord>.ValueEnumerator enumerator;
    private bool initialized;

    // O(1) の membership lookup は Contains.cs の型特殊化オーバーロードから使う。
    // interface メンバにすると値型が constrained. callvirt で box されるため置かない。
    [return: UnorderedEnumerable]
    internal global::Seamless.Collections.Tsavorite.Set<TKey,TRecord> GetSource() => this.source!;

    public bool TryGetNonEnumeratedCount(out int count) {
        if(!this.initialized) {
            count=this.source!.Count;
            return true;
        }
        count=0;
        return false;
    }
    public bool TryGetSpan(out global::System.ReadOnlySpan<TRecord> span) {
        span=default;
        return false;
    }
    public bool TryCopyTo(scoped global::System.Span<TRecord> destination,global::System.Index offset) => false;
    public bool TryGetNext(out TRecord current) {
        if(!this.initialized) {
            this.enumerator=this.source!.GetValueEnumerator();
            this.initialized=true;
        }
        return this.enumerator.TryGetNext(out current);
    }
    public void Dispose() {
        if(this.initialized) this.enumerator.Dispose();
        this.source=null;
    }
}