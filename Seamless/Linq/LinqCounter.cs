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

    public long PhysicalMoveNext => Interlocked.Read(ref physicalMoveNext);
    public long LogicalOperations => Interlocked.Read(ref logicalOperations);
    public long LogicalWhere => Interlocked.Read(ref logicalWhere);
    public long LogicalSelect => Interlocked.Read(ref logicalSelect);
    public long LogicalSelectManyOuter => Interlocked.Read(ref logicalSelectManyOuter);
    public long LogicalSelectManyInner => Interlocked.Read(ref logicalSelectManyInner);
    public long LogicalJoinOuter => Interlocked.Read(ref logicalJoinOuter);
    public long LogicalJoinInner => Interlocked.Read(ref logicalJoinInner);
    public long LogicalGroupJoinOuter => Interlocked.Read(ref logicalGroupJoinOuter);
    public long LogicalGroupJoinInner => Interlocked.Read(ref logicalGroupJoinInner);
    public long LogicalLeftJoinOuter => Interlocked.Read(ref logicalLeftJoinOuter);
    public long LogicalLeftJoinInner => Interlocked.Read(ref logicalLeftJoinInner);
    public long LogicalRightJoinOuter => Interlocked.Read(ref logicalRightJoinOuter);
    public long LogicalRightJoinInner => Interlocked.Read(ref logicalRightJoinInner);
    public long HashBuild => Interlocked.Read(ref hashBuild);
    public long HashProbe => Interlocked.Read(ref hashProbe);
    public long BufferAdd => Interlocked.Read(ref bufferAdd);
    public long TryGetNonEnumeratedCount => Interlocked.Read(ref tryGetNonEnumeratedCount);
    public long LogicalDelegate => Interlocked.Read(ref logicalDelegate);
    public long PhysicalDelegate => Interlocked.Read(ref physicalDelegate);

    public void IncrementPhysicalMoveNext() => Interlocked.Increment(ref physicalMoveNext);
    public void IncrementLogicalOperations() => Interlocked.Increment(ref logicalOperations);
    public void IncrementLogicalWhere() {
        Interlocked.Increment(ref logicalWhere);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalSelect() {
        Interlocked.Increment(ref logicalSelect);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalSelectManyOuter() {
        Interlocked.Increment(ref logicalSelectManyOuter);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalSelectManyInner() {
        Interlocked.Increment(ref logicalSelectManyInner);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalJoinOuter() {
        Interlocked.Increment(ref logicalJoinOuter);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalJoinInner() {
        Interlocked.Increment(ref logicalJoinInner);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalGroupJoinOuter() {
        Interlocked.Increment(ref logicalGroupJoinOuter);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalGroupJoinInner() {
        Interlocked.Increment(ref logicalGroupJoinInner);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalLeftJoinOuter() {
        Interlocked.Increment(ref logicalLeftJoinOuter);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalLeftJoinInner() {
        Interlocked.Increment(ref logicalLeftJoinInner);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalRightJoinOuter() {
        Interlocked.Increment(ref logicalRightJoinOuter);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementLogicalRightJoinInner() {
        Interlocked.Increment(ref logicalRightJoinInner);
        Interlocked.Increment(ref logicalOperations);
    }
    public void IncrementHashBuild() => Interlocked.Increment(ref hashBuild);
    public void IncrementHashProbe() => Interlocked.Increment(ref hashProbe);
    public void IncrementBufferAdd() => Interlocked.Increment(ref bufferAdd);
    public void IncrementTryGetNonEnumeratedCount() => Interlocked.Increment(ref tryGetNonEnumeratedCount);
    public void IncrementLogicalDelegate() => Interlocked.Increment(ref logicalDelegate);
    public void IncrementPhysicalDelegate() => Interlocked.Increment(ref physicalDelegate);
    public T RecordLogicalDelegate<T>(T value) {
        IncrementLogicalDelegate();
        return value;
    }
    public T RecordPhysicalDelegate<T>(T value) {
        IncrementLogicalDelegate();
        IncrementPhysicalDelegate();
        return value;
    }

    public void Reset() {
        Interlocked.Exchange(ref physicalMoveNext,0);
        Interlocked.Exchange(ref logicalOperations,0);
        Interlocked.Exchange(ref logicalWhere,0);
        Interlocked.Exchange(ref logicalSelect,0);
        Interlocked.Exchange(ref logicalSelectManyOuter,0);
        Interlocked.Exchange(ref logicalSelectManyInner,0);
        Interlocked.Exchange(ref logicalJoinOuter,0);
        Interlocked.Exchange(ref logicalJoinInner,0);
        Interlocked.Exchange(ref logicalGroupJoinOuter,0);
        Interlocked.Exchange(ref logicalGroupJoinInner,0);
        Interlocked.Exchange(ref logicalLeftJoinOuter,0);
        Interlocked.Exchange(ref logicalLeftJoinInner,0);
        Interlocked.Exchange(ref logicalRightJoinOuter,0);
        Interlocked.Exchange(ref logicalRightJoinInner,0);
        Interlocked.Exchange(ref hashBuild,0);
        Interlocked.Exchange(ref hashProbe,0);
        Interlocked.Exchange(ref bufferAdd,0);
        Interlocked.Exchange(ref tryGetNonEnumeratedCount,0);
        Interlocked.Exchange(ref logicalDelegate,0);
        Interlocked.Exchange(ref physicalDelegate,0);
    }
}

public static class LinqCounterScope {
    private static readonly AsyncLocal<LinqCounter?> CurrentCounter = new();
    public static LinqCounter GlobalCounter { get; } = new();
    public static LinqCounter Counter => CurrentCounter.Value ?? GlobalCounter;
    public static LinqCounter? Current => CurrentCounter.Value;

    public static Scope Start(LinqCounter? counter = null) {
        var previous = CurrentCounter.Value;
        var active = counter ?? new LinqCounter();
        CurrentCounter.Value = active;
        return new Scope(previous, active);
    }

    public readonly struct Scope : IDisposable {
        private readonly LinqCounter? previous;
        public Scope(LinqCounter? previous,LinqCounter counter) {
            this.previous=previous;
            Counter=counter;
        }
        public LinqCounter Counter { get; }
        public void Dispose() {
            CurrentCounter.Value=previous;
        }
    }
}
