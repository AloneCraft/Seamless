using Tsavorite.core;
using global::System.Collections;
using global::System.Diagnostics.CodeAnalysis;
using global::System.Threading;
using global::Seamless.Collections.Tsavorite;

namespace Seamless.Database;

// 取引を通さない表(db.X)。データ本体に並列区間を常に1つ開いておき、スレッドごとの置き場のマルチ用で読み書きする。
// 複数のスレッド(Manager・業務アプリ・複数のクライアント)から同時に使える。取引・WAL・版の管理は通らない(I6)。
// 設計：Document/refactoring-designs/20261115-取引を通さない表の作り直し.md

/// <summary>キーで探索できる取引を通さない表。複数のスレッドから同時に使える。書き込みは取り消せず、障害後の復旧は保証しない。</summary>
public sealed class Set<TKey,TRecord>:IReadOnlyCollection<TRecord>,IDisposable where TRecord:global::Seamless.Collections.IRecord<TKey> {
    private readonly DirectSetCore<ParallelSet<TKey,TRecord>> core;
    internal Set(SetStore<TKey,TRecord> store) {
        this.core=new(store.BeginParallel,() => { using var single = store.Create(); return single.GetLongCount(); });
    }
    /// <summary>件数。区間を開いたときの件数に各スレッドの増減を足したその時点の値(他のスレッドが書いている途中なら前後する)。</summary>
    public long LongCount => this.core.Count;
    public int Count => checked((int)this.core.Count);
    /// <summary>表の版。成功した書き込みの回数の合計。値が同じなら、その間に書き込みが無かった。DB を開き直すと 0 から数える。</summary>
    public long Version => this.core.Version;
    internal int MaintenanceCount => this.core.MaintenanceCount;
    /// <summary>追加する。同じキーがすでにあれば false。</summary>
    public bool Add(TRecord item) {
        // 広げてもログが空かない(予算が足りず広げられなかった)ときに繰り返さないよう、やり直しは3回まで。
        for(var attempt = 0; ; attempt++) {
            var slot = this.core.Enter();
            bool added;
            try { added=slot.Accessor.Add(item); }
            catch(TsavoriteLogFullException) when(slot.Depth==1&&attempt<3) { this.core.GrowAfterLogFull(slot); continue; }
            catch { this.core.Leave(slot); throw; }
            if(added) { this.core.Inserted(slot); }
            this.core.LeaveAndCheck(slot);
            return added;
        }
    }
    public bool Contains(TRecord item) { var slot = this.core.Enter(); try { return slot.Accessor.Contains(item); } finally { this.core.Leave(slot); } }
    public bool ContainsKey(TKey key) { var slot = this.core.Enter(); try { return slot.Accessor.ContainsKey(key); } finally { this.core.Leave(slot); } }
    public bool TryGetValue(TKey key,[MaybeNullWhen(false)] out TRecord value) { var slot = this.core.Enter(); try { return slot.Accessor.TryGetValue(key,out value); } finally { this.core.Leave(slot); } }
    /// <summary>キーと値が同じ行を消す。</summary>
    public bool Remove(TRecord item) { var slot = this.core.Enter(); try { var removed = slot.Accessor.Remove(item); if(removed) { this.core.Removed(slot); } return removed; } finally { this.core.Leave(slot); } }
    public bool RemoveKey(TKey key) { var slot = this.core.Enter(); try { var removed = slot.Accessor.RemoveKey(key); if(removed) { this.core.Removed(slot); } return removed; } finally { this.core.Leave(slot); } }
    /// <summary>行を1つずつ渡す。FLinq の生成コードが呼ぶ。渡している間、このスレッドは表を使用中のままにする。</summary>
    public void Iterate<TStrategy>(ref TStrategy strategy) where TStrategy:struct,global::Seamless.Collections.Iteration.IStrategy<TStrategy,TRecord> {
        var slot = this.core.Enter();
        try { slot.Accessor.Iterate(ref strategy); } finally { this.core.Leave(slot); }
    }
    /// <summary>行を列挙する。列挙を破棄するまで、このスレッドは表を使用中のままにする(保守が待つので必ず破棄する)。</summary>
    public IEnumerator<TRecord> GetEnumerator() => this.core.Enumerate(static accessor => accessor.GetEnumerator());
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    /// <summary>DB が閉じるときに呼ぶ。全部の操作が終わるのを待ち、並列区間を閉じる。</summary>
    public void Dispose() => this.core.Dispose();
}

/// <summary>キーの無い取引を通さない表。複数のスレッドから同時に使える。書き込みは取り消せず、障害後の復旧は保証しない。</summary>
public sealed class Set<TRecord>:IReadOnlyCollection<TRecord>,IDisposable where TRecord:notnull {
    private readonly DirectSetCore<ParallelHashSet<TRecord>> core;
    internal Set(HashSetStore<TRecord> store) {
        this.core=new(store.BeginParallel,() => { using var single = store.Create(); return single.Count; });
    }
    public long LongCount => this.core.Count;
    public int Count => checked((int)this.core.Count);
    /// <summary>表の版。成功した書き込みの回数の合計。DB を開き直すと 0 から数える。</summary>
    public long Version => this.core.Version;
    internal int MaintenanceCount => this.core.MaintenanceCount;
    /// <summary>追加する。すでにあれば false。</summary>
    public bool Add(TRecord item) {
        // 広げてもログが空かない(予算が足りず広げられなかった)ときに繰り返さないよう、やり直しは3回まで。
        for(var attempt = 0; ; attempt++) {
            var slot = this.core.Enter();
            bool added;
            try { added=slot.Accessor.Add(item); }
            catch(TsavoriteLogFullException) when(slot.Depth==1&&attempt<3) { this.core.GrowAfterLogFull(slot); continue; }
            catch { this.core.Leave(slot); throw; }
            if(added) { this.core.Inserted(slot); }
            this.core.LeaveAndCheck(slot);
            return added;
        }
    }
    public bool Contains(TRecord item) { var slot = this.core.Enter(); try { return slot.Accessor.ContainsKey(item); } finally { this.core.Leave(slot); } }
    public bool Remove(TRecord item) { var slot = this.core.Enter(); try { var removed = slot.Accessor.Remove(item); if(removed) { this.core.Removed(slot); } return removed; } finally { this.core.Leave(slot); } }
    public void Iterate<TStrategy>(ref TStrategy strategy) where TStrategy:struct,global::Seamless.Collections.Iteration.IStrategy<TStrategy,TRecord> {
        var slot = this.core.Enter();
        try { slot.Accessor.Iterate(ref strategy); } finally { this.core.Leave(slot); }
    }
    public IEnumerator<TRecord> GetEnumerator() => this.core.Enumerate(static accessor => ((IEnumerable<TRecord>)accessor).GetEnumerator());
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    public void Dispose() => this.core.Dispose();
}

/// <summary>
/// 並列区間とスレッドごとの置き場と保守(止めて広げる)の共通部分。
/// 1操作は自分の置き場の使用中の印を立てて下げるだけで、共有の排他を取らない(共有の棚に排他を付けるとスレッドを増やすほど遅くなった、11/15 の計測)。
/// </summary>
internal sealed class DirectSetCore<TAccessor>:IDisposable where TAccessor:class,IDisposable {
    internal sealed class Slot(TAccessor accessor) {
        internal readonly TAccessor Accessor = accessor;
        // 保守が待つ印(0 か 1)。自分のスレッドが書き、保守が読む。
        internal int Busy;
        // 入れ子の深さ。自分のスレッドだけが触る。
        internal int Depth;
        // 増減と成功した書き込みの数。自分のスレッドだけが書き、件数と版を求めるときに他のスレッドが読む。
        internal long Net;
        internal long Writes;
        internal int SinceCheck;
        internal bool CheckPending;
    }
    private sealed class Generation(ParallelRegion<TAccessor> region,long baseCount,long capacity) {
        internal readonly ParallelRegion<TAccessor> Region = region;
        internal readonly long BaseCount = baseCount;
        internal readonly long Capacity = capacity;
        internal readonly List<Slot> Slots = [];
        // 世代を閉じても破棄しない。入れ替えの直前に古い世代を読んだスレッドが触っても例外にならず、
        // 古い世代の保守中の印(閉じた世代は立てたまま)を見て、やり直しで新しい世代へ移る。
        internal readonly ThreadLocal<Slot?> Current = new();
        internal volatile bool Stopping;
    }
    // 件数の見込みを確かめる間隔(置き場ごとの追加の回数)。
    private const int CheckInterval = 4096;
    private const long MinimumCapacity = 1024;
    // 保守が使用中の操作の終わりを待つ上限。超えたら保守をやめて操作を続けさせる(別の表の保守と互いに待ち合って止まらないように)。
    internal static TimeSpan MaintenanceWaitLimit { get; set; } = TimeSpan.FromSeconds(5);
    private readonly Func<long,ParallelRegion<TAccessor>> open;
    private readonly Func<long> countClosed;
    private readonly Lock maintenance = new();
    private Generation generation;
    private long retiredWrites;
    private int disposed;
    // 止めて広げた回数(試験と診断用)。
    internal int MaintenanceCount { get; private set; }

    internal DirectSetCore(Func<long,ParallelRegion<TAccessor>> open,Func<long> countClosed) {
        this.open=open;
        this.countClosed=countClosed;
        this.generation=this.Open(countClosed(),0);
    }
    private Generation Open(long count,long requested) {
        var capacity = Math.Max(MinimumCapacity,Math.Max(requested,checked(count*2)));
        return new(this.open(capacity),count,capacity);
    }

    internal long Count {
        get {
            var generation = Volatile.Read(ref this.generation);
            var count = generation.BaseCount;
            lock(generation.Slots) { foreach(var slot in generation.Slots) { count+=Volatile.Read(ref slot.Net); } }
            return count;
        }
    }
    internal long Version {
        get {
            lock(this.maintenance) {
                var writes = this.retiredWrites;
                var generation = this.generation;
                lock(generation.Slots) { foreach(var slot in generation.Slots) { writes+=Volatile.Read(ref slot.Writes); } }
                return writes;
            }
        }
    }

    /// <summary>自分のスレッドの置き場を使用中にして返す。保守中なら終わるのを待ってやり直す。</summary>
    internal Slot Enter() {
        while(true) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
            var generation = Volatile.Read(ref this.generation);
            var slot = generation.Current.Value??this.CreateSlot(generation);
            if(slot is null) { this.WaitForMaintenance(); continue; }
            // 入れ子の内側は、外側が使用中の印を持っているのでそのまま進む(保守は外側が終わるまで待つ)。
            if(slot.Depth>0) { slot.Depth++; return slot; }
            // 印を立てたあとに保守中かを読む順序を守る(全体の順序を保つ書き込み)。保守は印を立てたあとに使用中を読む。
            Interlocked.Exchange(ref slot.Busy,1);
            if(!generation.Stopping) { slot.Depth=1; return slot; }
            Volatile.Write(ref slot.Busy,0);
            this.WaitForMaintenance();
        }
    }
    internal void Leave(Slot slot) {
        if(--slot.Depth==0) { Volatile.Write(ref slot.Busy,0); }
    }
    /// <summary>追加のあとに使う。外側の操作が終わったところで、件数の見込みを確かめて必要なら広げる。</summary>
    internal void LeaveAndCheck(Slot slot) {
        this.Leave(slot);
        if(slot.Depth==0&&slot.CheckPending) {
            slot.CheckPending=false;
            var generation = Volatile.Read(ref this.generation);
            if(this.Count>generation.Capacity) { this.Grow(generation,0,throwOnTimeout: false); }
        }
    }
    internal void Inserted(Slot slot) {
        Volatile.Write(ref slot.Net,slot.Net+1);
        Volatile.Write(ref slot.Writes,slot.Writes+1);
        if(++slot.SinceCheck>=CheckInterval) { slot.SinceCheck=0; slot.CheckPending=true; }
    }
    internal void Removed(Slot slot) {
        Volatile.Write(ref slot.Net,slot.Net-1);
        Volatile.Write(ref slot.Writes,slot.Writes+1);
    }
    /// <summary>追加でログが満杯になった。外側の操作なので使用中を下ろして広げる(呼び出し側はやり直す)。</summary>
    internal void GrowAfterLogFull(Slot slot) {
        this.Leave(slot);
        var generation = Volatile.Read(ref this.generation);
        this.Grow(generation,checked(Math.Max(generation.Capacity,this.Count)*2),throwOnTimeout: true);
    }
    internal IEnumerator<TRecord> Enumerate<TRecord>(Func<TAccessor,IEnumerator<TRecord>> create) {
        var slot = this.Enter();
        try { return new SlotEnumerator<TRecord>(this,slot,create(slot.Accessor)); }
        catch { this.Leave(slot); throw; }
    }

    private Slot? CreateSlot(Generation generation) {
        lock(generation.Slots) {
            if(generation.Stopping) { return null; }
            var slot = new Slot(generation.Region.Create());
            generation.Slots.Add(slot);
            generation.Current.Value=slot;
            return slot;
        }
    }
    private void WaitForMaintenance() {
        // 保守は maintenance を持ったまま止めて広げる。持てたら保守は終わっている(やめた場合も含む)。
        lock(this.maintenance) { }
    }
    private void Grow(Generation seen,long requested,bool throwOnTimeout) {
        lock(this.maintenance) {
            if(!ReferenceEquals(this.generation,seen)||Volatile.Read(ref this.disposed)!=0) { return; }
            if(!this.StopAll(seen)) {
                if(throwOnTimeout) { throw new TimeoutException("取引を通さない表を広げるために使用中の操作の終わりを待ちましたが、終わりませんでした。走査の中で別の表へ書いている場合は、走査を終えてから書いてください。"); }
                return;
            }
            var count = this.Close(seen);
            this.generation=this.Open(count,Math.Max(requested,checked(count*2)));
            this.MaintenanceCount++;
        }
    }
    // 保守中の印を立て、全部の置き場の使用中が下がるのを待つ。上限を超えたら印を下ろして false。
    private bool StopAll(Generation generation) {
        Slot[] slots;
        lock(generation.Slots) { generation.Stopping=true; slots=generation.Slots.ToArray(); }
        Interlocked.MemoryBarrier();
        var started = Environment.TickCount64;
        foreach(var slot in slots) {
            var spin = new SpinWait();
            while(Volatile.Read(ref slot.Busy)!=0) {
                if(Environment.TickCount64-started>MaintenanceWaitLimit.TotalMilliseconds) {
                    lock(generation.Slots) { generation.Stopping=false; }
                    return false;
                }
                spin.SpinOnce();
            }
        }
        return true;
    }
    // 全部のマルチ用を破棄して区間を閉じる(件数が確定し、索引が混んでいれば広がる)。閉じたあとの件数を返す。
    private long Close(Generation generation) {
        var errors = new List<Exception>();
        long writes = 0;
        foreach(var slot in generation.Slots) {
            writes+=slot.Writes;
            try { slot.Accessor.Dispose(); } catch(Exception error) { errors.Add(error); }
        }
        this.retiredWrites+=writes;
        try { generation.Region.Dispose(); } catch(Exception error) { errors.Add(error); }
        if(errors.Count>0) { throw new AggregateException("取引を通さない表の並列区間を閉じられませんでした。",errors); }
        return this.countClosed();
    }
    public void Dispose() {
        lock(this.maintenance) {
            if(Interlocked.Exchange(ref this.disposed,1)!=0) { return; }
            var generation = this.generation;
            if(!this.StopAll(generation)) {
                throw new InvalidOperationException("取引を通さない表を閉じる前に、使用中の操作(破棄していない列挙など)が終わりませんでした。");
            }
            var errors = new List<Exception>();
            foreach(var slot in generation.Slots) { try { slot.Accessor.Dispose(); } catch(Exception error) { errors.Add(error); } }
            try { generation.Region.Dispose(); } catch(Exception error) { errors.Add(error); }
            if(errors.Count>0) { throw new AggregateException("取引を通さない表の並列区間を閉じられませんでした。",errors); }
        }
    }

    // 列挙を破棄するまで置き場を使用中にしておく列挙。
    private sealed class SlotEnumerator<TRecord>(DirectSetCore<TAccessor> owner,Slot slot,IEnumerator<TRecord> inner):IEnumerator<TRecord> {
        private bool left;
        public TRecord Current => inner.Current;
        object? IEnumerator.Current => inner.Current;
        public bool MoveNext() => inner.MoveNext();
        public void Reset() => inner.Reset();
        public void Dispose() {
            if(this.left) { return; }
            this.left=true;
            try { inner.Dispose(); } finally { owner.Leave(slot); }
        }
    }
}
