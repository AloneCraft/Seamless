namespace Seamless.Linq;

[Flags]
public enum LinqCounterKind {
    None = 0,
    Counter = 1,
}

public sealed class LinqCounter {
    private long physicalMoveNext;
    private long logicalOperations;
    private long logicalWhere;
    private long logicalSelect;
    private long logicalSelectManyOuter;
    private long logicalSelectManyInner;
    private long logicalJoinOuter;
    private long logicalJoinInner;
    private long logicalGroupJoinOuter;
    private long logicalGroupJoinInner;
    private long logicalLeftJoinOuter;
    private long logicalLeftJoinInner;
    private long logicalRightJoinOuter;
    private long logicalRightJoinInner;
    private long hashBuild;
    private long hashProbe;
    private long bufferAdd;
    private long tryGetNonEnumeratedCount;
    private long logicalDelegate;
    private long physicalDelegate;
    private long enumerableFallback;
    private long directSpanUsed;
    private long tryGetSpanCalled;
    private long tryGetSpanSucceeded;
    private long tryGetSpanFailed;
    private long enumerationLoopUsed;
    private long fastCopyUsed;
    private long hashCapacityHintUsed;
    private readonly global::System.Collections.Concurrent.ConcurrentDictionary<string,long> enumerableFallbackDetails = new(StringComparer.Ordinal);

    public long PhysicalMoveNext => Interlocked.Read(ref this.physicalMoveNext);
    public long LogicalOperations => Interlocked.Read(ref this.logicalOperations);
    public long LogicalWhere => Interlocked.Read(ref this.logicalWhere);
    public long LogicalSelect => Interlocked.Read(ref this.logicalSelect);
    public long LogicalSelectManyOuter => Interlocked.Read(ref this.logicalSelectManyOuter);
    public long LogicalSelectManyInner => Interlocked.Read(ref this.logicalSelectManyInner);
    public long LogicalJoinOuter => Interlocked.Read(ref this.logicalJoinOuter);
    public long LogicalJoinInner => Interlocked.Read(ref this.logicalJoinInner);
    public long LogicalGroupJoinOuter => Interlocked.Read(ref this.logicalGroupJoinOuter);
    public long LogicalGroupJoinInner => Interlocked.Read(ref this.logicalGroupJoinInner);
    public long LogicalLeftJoinOuter => Interlocked.Read(ref this.logicalLeftJoinOuter);
    public long LogicalLeftJoinInner => Interlocked.Read(ref this.logicalLeftJoinInner);
    public long LogicalRightJoinOuter => Interlocked.Read(ref this.logicalRightJoinOuter);
    public long LogicalRightJoinInner => Interlocked.Read(ref this.logicalRightJoinInner);
    public long HashBuild => Interlocked.Read(ref this.hashBuild);
    public long HashProbe => Interlocked.Read(ref this.hashProbe);
    public long BufferAdd => Interlocked.Read(ref this.bufferAdd);
    public long TryGetNonEnumeratedCount => Interlocked.Read(ref this.tryGetNonEnumeratedCount);
    public long LogicalDelegate => Interlocked.Read(ref this.logicalDelegate);
    public long PhysicalDelegate => Interlocked.Read(ref this.physicalDelegate);
    public long DirectSpanUsed => Interlocked.Read(ref this.directSpanUsed);
    public long TryGetSpanCalled => Interlocked.Read(ref this.tryGetSpanCalled);
    public long TryGetSpanSucceeded => Interlocked.Read(ref this.tryGetSpanSucceeded);
    public long TryGetSpanFailed => Interlocked.Read(ref this.tryGetSpanFailed);
    public long EnumerationLoopUsed => Interlocked.Read(ref this.enumerationLoopUsed);
    public long FastCopyUsed => Interlocked.Read(ref this.fastCopyUsed);
    public long HashCapacityHintUsed => Interlocked.Read(ref this.hashCapacityHintUsed);
    /// <summary>FLinq 展開に失敗して System.Linq へフォールバックした(生成インターセプターが失敗例外を構築した)回数。0 が正常。</summary>
    public long EnumerableFallback => Interlocked.Read(ref this.enumerableFallback);
    /// <summary>フォールバック詳細(失敗メッセージ)ごとの回数。</summary>
    public IReadOnlyDictionary<string,long> EnumerableFallbackDetails => this.enumerableFallbackDetails;

    public void IncrementPhysicalMoveNext() => Interlocked.Increment(ref this.physicalMoveNext);
    public void IncrementLogicalOperations() => Interlocked.Increment(ref this.logicalOperations);
    public void IncrementLogicalWhere() {
        Interlocked.Increment(ref this.logicalWhere);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalSelect() {
        Interlocked.Increment(ref this.logicalSelect);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalSelectManyOuter() {
        Interlocked.Increment(ref this.logicalSelectManyOuter);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalSelectManyInner() {
        Interlocked.Increment(ref this.logicalSelectManyInner);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalJoinOuter() {
        Interlocked.Increment(ref this.logicalJoinOuter);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalJoinInner() {
        Interlocked.Increment(ref this.logicalJoinInner);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalGroupJoinOuter() {
        Interlocked.Increment(ref this.logicalGroupJoinOuter);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalGroupJoinInner() {
        Interlocked.Increment(ref this.logicalGroupJoinInner);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalLeftJoinOuter() {
        Interlocked.Increment(ref this.logicalLeftJoinOuter);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalLeftJoinInner() {
        Interlocked.Increment(ref this.logicalLeftJoinInner);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalRightJoinOuter() {
        Interlocked.Increment(ref this.logicalRightJoinOuter);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementLogicalRightJoinInner() {
        Interlocked.Increment(ref this.logicalRightJoinInner);
        Interlocked.Increment(ref this.logicalOperations);
    }
    public void IncrementHashBuild() => Interlocked.Increment(ref this.hashBuild);
    public void IncrementHashProbe() => Interlocked.Increment(ref this.hashProbe);
    public void IncrementBufferAdd() => Interlocked.Increment(ref this.bufferAdd);
    public void IncrementTryGetNonEnumeratedCount() => Interlocked.Increment(ref this.tryGetNonEnumeratedCount);
    public void IncrementLogicalDelegate() => Interlocked.Increment(ref this.logicalDelegate);
    public void IncrementPhysicalDelegate() => Interlocked.Increment(ref this.physicalDelegate);
    public void IncrementDirectSpanUsed() => Interlocked.Increment(ref this.directSpanUsed);
    public void IncrementTryGetSpanCalled() => Interlocked.Increment(ref this.tryGetSpanCalled);
    public void IncrementTryGetSpanSucceeded() => Interlocked.Increment(ref this.tryGetSpanSucceeded);
    public void IncrementTryGetSpanFailed() => Interlocked.Increment(ref this.tryGetSpanFailed);
    public void IncrementEnumerationLoopUsed() => Interlocked.Increment(ref this.enumerationLoopUsed);
    public void IncrementFastCopyUsed() => Interlocked.Increment(ref this.fastCopyUsed);
    public void IncrementHashCapacityHintUsed() => Interlocked.Increment(ref this.hashCapacityHintUsed);
    public void IncrementEnumerableFallback(string detail) {
        Interlocked.Increment(ref this.enumerableFallback);
        this.enumerableFallbackDetails.AddOrUpdate(detail,1,static (_,count) => count+1);
    }
    public T RecordLogicalDelegate<T>(T value) {
        this.IncrementLogicalDelegate();
        return value;
    }
    public T RecordPhysicalDelegate<T>(T value) {
        this.IncrementLogicalDelegate();
        this.IncrementPhysicalDelegate();
        return value;
    }

    public void Reset() {
        Interlocked.Exchange(ref this.physicalMoveNext,0);
        Interlocked.Exchange(ref this.logicalOperations,0);
        Interlocked.Exchange(ref this.logicalWhere,0);
        Interlocked.Exchange(ref this.logicalSelect,0);
        Interlocked.Exchange(ref this.logicalSelectManyOuter,0);
        Interlocked.Exchange(ref this.logicalSelectManyInner,0);
        Interlocked.Exchange(ref this.logicalJoinOuter,0);
        Interlocked.Exchange(ref this.logicalJoinInner,0);
        Interlocked.Exchange(ref this.logicalGroupJoinOuter,0);
        Interlocked.Exchange(ref this.logicalGroupJoinInner,0);
        Interlocked.Exchange(ref this.logicalLeftJoinOuter,0);
        Interlocked.Exchange(ref this.logicalLeftJoinInner,0);
        Interlocked.Exchange(ref this.logicalRightJoinOuter,0);
        Interlocked.Exchange(ref this.logicalRightJoinInner,0);
        Interlocked.Exchange(ref this.hashBuild,0);
        Interlocked.Exchange(ref this.hashProbe,0);
        Interlocked.Exchange(ref this.bufferAdd,0);
        Interlocked.Exchange(ref this.tryGetNonEnumeratedCount,0);
        Interlocked.Exchange(ref this.logicalDelegate,0);
        Interlocked.Exchange(ref this.physicalDelegate,0);
        Interlocked.Exchange(ref this.enumerableFallback,0);
        Interlocked.Exchange(ref this.directSpanUsed,0);
        Interlocked.Exchange(ref this.tryGetSpanCalled,0);
        Interlocked.Exchange(ref this.tryGetSpanSucceeded,0);
        Interlocked.Exchange(ref this.tryGetSpanFailed,0);
        Interlocked.Exchange(ref this.enumerationLoopUsed,0);
        Interlocked.Exchange(ref this.fastCopyUsed,0);
        Interlocked.Exchange(ref this.hashCapacityHintUsed,0);
        this.enumerableFallbackDetails.Clear();
    }
}

public static class LinqCounterScope {
    private static readonly AsyncLocal<LinqCounter?> CurrentCounter = new();
    public static LinqCounter GlobalCounter { get; } = new();
    public static LinqCounter Counter => CurrentCounter.Value??GlobalCounter;
    public static LinqCounter? Current => CurrentCounter.Value;

    // 開いている計数の範囲(Start)の数。差し替えたコードの中に無条件に置かれる Increment* は、まずこれだけを見る。
    // AsyncLocal を引く処理はインライン化しない: 大きいので差し替えメソッドのインライン化の予算を使い切り、
    // 要素ごとの Consume などがインライン化されずに呼び出しのまま残っていた(配列の Append で理想の3倍)。
    // 別のスレッドで範囲が開いていても、そのスレッドの AsyncLocal は空なので数え間違えない。
    private static int activeScopes;

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void IncrementDirectSpanUsed() {
        if(activeScopes!=0) {
            IncrementDirectSpanUsedSlow();
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void IncrementDirectSpanUsedSlow() => CurrentCounter.Value?.IncrementDirectSpanUsed();

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void IncrementTryGetSpanCalled() {
        if(activeScopes!=0) {
            IncrementTryGetSpanCalledSlow();
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void IncrementTryGetSpanCalledSlow() => CurrentCounter.Value?.IncrementTryGetSpanCalled();

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void IncrementTryGetSpanSucceeded() {
        if(activeScopes!=0) {
            IncrementTryGetSpanSucceededSlow();
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void IncrementTryGetSpanSucceededSlow() => CurrentCounter.Value?.IncrementTryGetSpanSucceeded();

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void IncrementTryGetSpanFailed() {
        if(activeScopes!=0) {
            IncrementTryGetSpanFailedSlow();
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void IncrementTryGetSpanFailedSlow() => CurrentCounter.Value?.IncrementTryGetSpanFailed();

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void IncrementEnumerationLoopUsed() {
        if(activeScopes!=0) {
            IncrementEnumerationLoopUsedSlow();
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void IncrementEnumerationLoopUsedSlow() => CurrentCounter.Value?.IncrementEnumerationLoopUsed();

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void IncrementFastCopyUsed() {
        if(activeScopes!=0) {
            IncrementFastCopyUsedSlow();
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void IncrementFastCopyUsedSlow() => CurrentCounter.Value?.IncrementFastCopyUsed();

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void IncrementHashCapacityHintUsed() {
        if(activeScopes!=0) {
            IncrementHashCapacityHintUsedSlow();
        }
    }

    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void IncrementHashCapacityHintUsedSlow() => CurrentCounter.Value?.IncrementHashCapacityHintUsed();

    public static Scope Start(LinqCounter? counter = null) {
        var previous = CurrentCounter.Value;
        var active = counter??new LinqCounter();
        CurrentCounter.Value=active;
        Interlocked.Increment(ref activeScopes);
        return new Scope(previous,active);
    }

    public readonly struct Scope(LinqCounter? previous,LinqCounter counter):IDisposable {
        public           LinqCounter  Counter { get; }=counter;
        public void Dispose() {
            CurrentCounter.Value=previous;
            Interlocked.Decrement(ref activeScopes);
        }
    }
}
