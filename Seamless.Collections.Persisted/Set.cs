namespace Seamless.Collections.Persisted;

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