namespace Seamless.Collections.Persisted;

public sealed class Set<T>:Seamless.Collections.ISet<T>, IDisposable {
    // global::Seamless.Collections.Serializers.Auto は固定長の型を global::Seamless.Collections.Serializers.Blittable と同じ生バイトで、それ以外を MemoryPack で書くので、
    // 型ごとにリフレクションで作り分けなくても従来(BlittableSet / MemoryPack の SerializedSet)と同じレコード本体の形式になる。
    private readonly SerializedSet<T,global::Seamless.Collections.Serializers.Auto<T>> list;
    private readonly IReadOnlySet<T> set;

    public Set(string path,long initialCapacity = 16,long initialDataCapacity = 4096,StorageTuning? tuning = null) {
        this.list=new SerializedSet<T,global::Seamless.Collections.Serializers.Auto<T>>(path,initialCapacity,initialDataCapacity,tuning);
        this.set=this.list;
    }

    public string Path => this.list.Path;
    public int Count => checked((int)this.list.Count);
    public long GetLongCount() => this.list.Count;
    public bool IsDisposed => this.list.IsDisposed;

    public void Add(T item) => this.list.Add(item);

    /// <summary>
    /// 重複を除いて並べ替え(ファイルをその場で書き換える)、そのまま保存する。
    /// 共通ストアから作った集合ではストア全体を <see cref="global::Seamless.Execution.Store.Save"/> する(保存番号が 1 つ増える)。
    /// 単独で作った集合ではこの集合だけをコミットする。
    /// 書き換えは保存済みの内容を壊すので、書き換えたあと保存の前に閉じると保存した状態へ戻せない。それを起こさないよう、必ず保存までを 1 回で行う。
    /// この集合の、書き換え前の保存(古い保存番号)は使えなくなる。
    /// </summary>
    public Set<T> CompactSave() {
        this.list.Compact();
        if(this.saveAction is { } save) {
            save();
        } else {
            this.list.Commit();
        }
        return this;
    }

    // 共通ストアに接続されたとき、ストア全体の保存を呼ぶ口が入る。
    private Action? saveAction;
    internal void SetSaveAction(Action? action) => this.saveAction=action;

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
    internal void SetDisposeAction(Action? action) =>
        this.list.SetDisposeAction(action);

    internal IPersistedCommittable Committable => this.list;

    public void Iterate<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, global::Seamless.Collections.Iteration.IStrategy<TStrategy,T> {
        foreach(var item in this) {
            if(!TStrategy.Invoke(ref strategy,item))
                break;
        }
    }
}

public class SerializedSet<T>(
    string         path,
    long           initialCapacity    =16,
    long           initialDataCapacity=4096,
    StorageTuning? tuning             =null)
    :SerializedSet<T,global::Seamless.Collections.Serializers.MemoryPack<T>>(path,initialCapacity,initialDataCapacity,tuning)
where T : struct;

public class SerializedSet<T,TSerializer>(
    string         path,
    long           initialCapacity    =16,
    long           initialDataCapacity=4096,
    StorageTuning? tuning             =null)
    :SerializedList<T,TSerializer>(path,initialCapacity,initialDataCapacity,tuning),IReadOnlySet<T>
where TSerializer : global::Seamless.Collections.Serializers.IStrategy<T> {
    // 要素の serialize バイト列をキーとするファイル上のハッシュ索引を維持し、Contains を O(1) 化する。
    protected override bool UsesKeyIndex => true;

    public SerializedSet<T,TSerializer> Compact() {
        this.DuplicateRemoval();
        return this;
    }

    public bool Contains(T item) {
        var key = TSerializer.Serialize(ref item);
        return this.TryLookupKey(key,out _);
    }

    // 関係演算は自分自身を全件メモリに載せず、キー索引の Contains と逐次走査で求める。
    // Compact するまで重複を含みうるので、件数による近道は使わない。other が集合でなければ BCL と同じく other だけを集合にする。
    public bool IsProperSubsetOf(IEnumerable<T> other) {
        var otherSet = AsReadOnlySet(other);
        if(!this.IsSubsetOfSet(otherSet)) {
            return false;
        }
        foreach(var item in otherSet) {
            if(!this.Contains(item)) {
                return true;
            }
        }
        return false;
    }
    public bool IsProperSupersetOf(IEnumerable<T> other) {
        var otherSet = AsReadOnlySet(other);
        foreach(var item in otherSet) {
            if(!this.Contains(item)) {
                return false;
            }
        }
        foreach(var item in this) {
            if(!otherSet.Contains(item)) {
                return true;
            }
        }
        return false;
    }
    public bool IsSubsetOf(IEnumerable<T> other) => this.IsSubsetOfSet(AsReadOnlySet(other));
    public bool IsSupersetOf(IEnumerable<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) {
            if(!this.Contains(item)) {
                return false;
            }
        }
        return true;
    }
    public bool Overlaps(IEnumerable<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        foreach(var item in other) {
            if(this.Contains(item)) {
                return true;
            }
        }
        return false;
    }
    public bool SetEquals(IEnumerable<T> other) {
        var otherSet = AsReadOnlySet(other);
        if(!this.IsSubsetOfSet(otherSet)) {
            return false;
        }
        foreach(var item in otherSet) {
            if(!this.Contains(item)) {
                return false;
            }
        }
        return true;
    }
    private bool IsSubsetOfSet(IReadOnlySet<T> otherSet) {
        foreach(var item in this) {
            if(!otherSet.Contains(item)) {
                return false;
            }
        }
        return true;
    }
    private static IReadOnlySet<T> AsReadOnlySet(IEnumerable<T> other) {
        ArgumentNullException.ThrowIfNull(other);
        if(other is IReadOnlySet<T> set) {
            return set;
        } else {
            return new global::System.Collections.Generic.HashSet<T>(other);
        }
    }
}

public sealed class BlittableSet<T>(
    string         path,
    long           initialCapacity    =16,
    long           initialDataCapacity=4096,
    StorageTuning? tuning             =null)
    :SerializedSet<T,global::Seamless.Collections.Serializers.Blittable<T>>(path,initialCapacity,initialDataCapacity,tuning)
where T : unmanaged;
