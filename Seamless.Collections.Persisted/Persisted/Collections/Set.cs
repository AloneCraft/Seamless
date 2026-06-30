namespace Seamless.Persisted.Collections;

public interface ISetIterateStrategy<TSelf, T>
    where TSelf : struct, ISetIterateStrategy<TSelf,T> {
    static abstract bool Invoke(ref TSelf self,T item);
}

public sealed class Set<T>:Seamless.Collections.ISet<T>, IDisposable {
    private readonly PersistedList<T> list;
    private readonly IReadOnlySet<T> set;

    public Set(string path,long initialCapacity = 16,long initialDataCapacity = 4096,StorageTuning? tuning = null) {
        this.list=CreateCore(path,initialCapacity,initialDataCapacity,tuning);
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

    public void Iterate<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, ISetIterateStrategy<TStrategy,T> {
        foreach(var item in this) {
            if(!TStrategy.Invoke(ref strategy,item))
                break;
        }
    }

    private static PersistedList<T> CreateCore(string path,long initialCapacity,long initialDataCapacity,StorageTuning? tuning) {
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            return new SerializedSet<T,MemoryPackSerializer<T>>(path,initialCapacity,initialDataCapacity,tuning);

        var type = typeof(BlittableSet<>).MakeGenericType(typeof(T));
        return (PersistedList<T>)Activator.CreateInstance(type,path,initialCapacity,initialDataCapacity,tuning)!;
    }
}

public class SerializedSet<T>:SerializedSet<T,MemoryPackSerializer<T>>
    where T : struct {
    public SerializedSet(string path,long initialCapacity = 16,long initialDataCapacity = 4096,StorageTuning? tuning = null)
        : base(path,initialCapacity,initialDataCapacity,tuning) {
    }
}

public class SerializedSet<T, TSerializer>:SerializedList<T,TSerializer>, IReadOnlySet<T>
    where TSerializer : ISerializer<T> {
    public SerializedSet(string path,long initialCapacity = 16,long initialDataCapacity = 4096,StorageTuning? tuning = null)
        : base(path,initialCapacity,initialDataCapacity,tuning) {
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

    public bool IsProperSubsetOf(IEnumerable<T> other) => new global::System.Collections.Generic.HashSet<T>(this).IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => new global::System.Collections.Generic.HashSet<T>(this).IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<T> other) => new global::System.Collections.Generic.HashSet<T>(this).IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => new global::System.Collections.Generic.HashSet<T>(this).IsSupersetOf(other);
    public bool Overlaps(IEnumerable<T> other) => new global::System.Collections.Generic.HashSet<T>(this).Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => new global::System.Collections.Generic.HashSet<T>(this).SetEquals(other);
}

public sealed class BlittableSet<T>:SerializedSet<T,BlittableSerializer<T>>
    where T : unmanaged {
    public BlittableSet(string path,long initialCapacity = 16,long initialDataCapacity = 4096,StorageTuning? tuning = null)
        : base(path,initialCapacity,initialDataCapacity,tuning) {
    }
}
