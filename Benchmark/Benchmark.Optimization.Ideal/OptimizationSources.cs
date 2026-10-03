namespace Shared.Linq.Optimization;

// Shared/Linq.Optimization の EnumerableSource・ListSource の写し(ソースの作り方を FLinq 側と同じにする)。

public abstract class EnumerableSource<T>:global::Common.Base<T> where T : notnull {
    private readonly T[] values;
    protected EnumerableSource(int count = 4_096) : base(count) => this.values=this.srcCount(count);
    protected internal global::System.Collections.Generic.IEnumerable<T> Source() {
        for(var index = 0;index<this.values.Length;index++) {
            yield return this.values[index];
        }
    }
}

public abstract class ListSource<T>:global::Common.Base<T> where T : notnull {
    private readonly global::System.Collections.Generic.List<T> values;
    protected ListSource(int count = 4_096) : base(count) => this.values=[.. this.srcCount(count)];
    protected internal global::System.Collections.Generic.List<T> Source() => this.values;
}

/// <summary>
/// 結合の内側を恒等キーでまとめる探索表。Add で詰め、Build でキーごとの区画(内側の順)を作る。
/// </summary>
internal sealed class IdentityLookup<T>(int capacity):IDisposable where T : notnull {
    private readonly global::Shared.Linq.Ideal.PooledGroups<T,T> groups = new(capacity);
    private readonly T[] sorted = global::System.Buffers.ArrayPool<T>.Shared.Rent(capacity);
    private int count;

    public void Add(T value) {
        this.groups.Keys[this.count]=value;
        this.groups.Elements[this.count]=value;
        this.count++;
    }

    public void Build() => this.groups.Group(this.count,this.sorted);

    public bool TryGet(T key,out int start,out int length) {
        if(this.groups.Map.TryGetValue(key,out var group)) {
            start=this.groups.Starts[group];
            length=this.groups.Counts[group];
            return true;
        }
        start=0;
        length=0;
        return false;
    }

    public T this[int index] => this.sorted[index];

    public void Dispose() {
        this.groups.Dispose();
        global::System.Buffers.ArrayPool<T>.Shared.Return(this.sorted);
    }
}

/// <summary>複合連鎖の理想版で共有する補助。</summary>
internal static class IdealOptimization {
    /// <summary>昇順の安定な並べ替えをして流す。int は同値が区別できないので通常の並べ替えでよい。</summary>
    public static void ConsumeSorted<T>(global::Common.Consumer consumer,ReadOnlySpan<T> values) where T : notnull {
        if(typeof(T)==typeof(int)) {
            var buffer = global::System.Buffers.ArrayPool<int>.Shared.Rent(values.Length);
            try {
                var span = buffer.AsSpan(0,values.Length);
                global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
                    ref global::System.Runtime.CompilerServices.Unsafe.As<T,int>(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(values)),
                    values.Length).CopyTo(span);
                span.Sort();
                foreach(var value in span) {
                    consumer.Consume(value);
                }
            } finally {
                global::System.Buffers.ArrayPool<int>.Shared.Return(buffer);
            }
            return;
        }
        var indexed = global::System.Buffers.ArrayPool<global::Shared.Linq.Ideal.Indexed<T>>.Shared.Rent(values.Length);
        try {
            var items = indexed.AsSpan(0,values.Length);
            for(var index = 0;index<values.Length;index++) {
                items[index]=new global::Shared.Linq.Ideal.Indexed<T>(values[index],index);
            }
            items.Sort(new global::Shared.Linq.Ideal.AscendingStable<T>(Comparer<T>.Default));
            foreach(var item in items) {
                consumer.Consume(item.Value);
            }
        } finally {
            global::System.Buffers.ArrayPool<global::Shared.Linq.Ideal.Indexed<T>>.Shared.Return(indexed);
        }
    }
}
