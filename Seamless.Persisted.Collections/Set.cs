namespace Seamless.Persisted.Collections;

public sealed class Set<T>:Seamless.Collections.ISet<T>, IDisposable {
    private readonly PersistedList<T> list;
    private readonly IReadOnlySet<T> set;

    public Set(string path,long initialCapacity = 16,long initialDataCapacity = 4096) {
        this.list=CreateCore(path,initialCapacity,initialDataCapacity);
        this.set=(IReadOnlySet<T>)this.list;
    }

    public int Count => checked((int)this.list.Count);
    public long GetLongCount() => this.list.Count;
    public bool IsDisposed => this.list.IsDisposed;

    public void Add(T item) => this.list.Add(item);

    public Set<T> Compact() {
        this.list.GetType().GetMethod(nameof(Compact),Type.EmptyTypes)!.Invoke(this.list,null);
        return this;
    }

    public bool Contains(T item) => this.set.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<T> other) => this.set.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => this.set.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<T> other) => this.set.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => this.set.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<T> other) => this.set.Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => this.set.SetEquals(other);
    public PersistedList<T>.Enumerator GetEnumerator() => this.list.GetEnumerator();
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    public void Dispose() => this.list.Dispose();

    internal void ScanUnsafeDelegate(Func<object?,bool> body) {
        ArgumentNullException.ThrowIfNull(body);
        foreach(var item in this) {
            if(!body(item))
                break;
        }
    }

    private static PersistedList<T> CreateCore(string path,long initialCapacity,long initialDataCapacity) {
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            return new SerializedSet<T,MemoryPackSerializer<T>>(path,initialCapacity,initialDataCapacity);

        var type = typeof(BlittableSet<>).MakeGenericType(typeof(T));
        return (PersistedList<T>)Activator.CreateInstance(type,path,initialCapacity,initialDataCapacity)!;
    }
}

internal static class PersistedSetScanUnsafe {
    internal static void Scan<T>(Set<T> source,Func<object?,bool> body) =>
        source.ScanUnsafeDelegate(body);
}

public class SerializedSet<T>:SerializedSet<T,MemoryPackSerializer<T>>
    where T : struct {
    public SerializedSet(string path,long initialCapacity = 16,long initialDataCapacity = 4096)
        : base(path,initialCapacity,initialDataCapacity) {
    }
}

public class SerializedSet<T, TSerializer>:SerializedList<T,TSerializer>, IReadOnlySet<T>
    where TSerializer : ISerializer<T> {
    public SerializedSet(string path,long initialCapacity = 16,long initialDataCapacity = 4096)
        : base(path,initialCapacity,initialDataCapacity) {
    }

    public SerializedSet<T,TSerializer> Compact() {
        this.DuplicateRemoval();
        return this;
    }

    public bool Contains(T item) {
        var comparer = EqualityComparer<T>.Default;
        foreach(var current in this) {
            if(comparer.Equals(current,item)) {
                return true;
            }
        }
        return false;
    }

    public bool IsProperSubsetOf(IEnumerable<T> other) => new HashSet<T>(this).IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => new HashSet<T>(this).IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<T> other) => new HashSet<T>(this).IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => new HashSet<T>(this).IsSupersetOf(other);
    public bool Overlaps(IEnumerable<T> other) => new HashSet<T>(this).Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => new HashSet<T>(this).SetEquals(other);
}

public sealed class BlittableSet<T>:SerializedSet<T,BlittableSerializer<T>>
    where T : unmanaged {
    public BlittableSet(string path,long initialCapacity = 16,long initialDataCapacity = 4096)
        : base(path,initialCapacity,initialDataCapacity) {
    }
}
