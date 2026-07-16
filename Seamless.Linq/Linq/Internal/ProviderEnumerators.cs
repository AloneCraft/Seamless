namespace Seamless.Linq.Internal;

public struct PersistedSetEnumerator<T>(global::Seamless.Persisted.Collections.Set<T> source):IValueEnumerator<T> {
    private global::Seamless.Persisted.Collections.Set<T>? source=source;
    private global::Seamless.Persisted.Collections.PersistedList<T>.Enumerator enumerator;
    private bool initialized;

    public bool TryContains(T value,out bool result) {
        result=this.source!.Contains(value);
        return true;
    }
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

public struct TsavoriteHashSetEnumerator<T>(global::Seamless.Tsavorite.Collections.HashSet<T> source):IValueEnumerator<T> {
    private global::Seamless.Tsavorite.Collections.HashSet<T>? source=source;
    private global::Seamless.Tsavorite.Collections.HashSet<T>.ValueEnumerator enumerator;
    private bool initialized;

    public bool TryContains(T value,out bool result) {
        result=this.source!.Contains(value);
        return true;
    }
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

public struct TsavoriteSetEnumerator<TKey,TRecord>(global::Seamless.Tsavorite.Collections.Set<TKey,TRecord> source):IValueEnumerator<TRecord>
    where TRecord : global::Seamless.Collections.IRecord<TKey> {
    private global::Seamless.Tsavorite.Collections.Set<TKey,TRecord>? source=source;
    private global::Seamless.Tsavorite.Collections.Set<TKey,TRecord>.ValueEnumerator enumerator;
    private bool initialized;

    public bool TryContains(TRecord value,out bool result) {
        result=this.source!.Contains(value);
        return true;
    }
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