namespace Test.System.Linq.Intercept;

internal static class LinqCounterDiagnostics {
    public static void EmitExpected(string label,long expectedLogicalDelegate,long expectedPhysicalDelegate,LinqCounter counter) {
        if(expectedLogicalDelegate==0
            &&expectedPhysicalDelegate==0
            &&IsEmpty(counter)) {
            return;
        }

        Console.WriteLine(
            "{0}: delegate expected(log/phys)={1}/{2}, actual(log/phys)={3}/{4}; logical ops={5}, select={6}, where={7}, selectMany(o/i)={8}/{9}, join(o/i)={10}/{11}, groupJoin(o/i)={12}/{13}, leftJoin(o/i)={14}/{15}, rightJoin(o/i)={16}/{17}, moveNext={18}, hash(build/probe)={19}/{20}, bufferAdd={21}, tryGetNonEnumeratedCount={22}",
            label,
            expectedLogicalDelegate,
            expectedPhysicalDelegate,
            counter.LogicalDelegate,
            counter.PhysicalDelegate,
            counter.LogicalOperations,
            counter.LogicalSelect,
            counter.LogicalWhere,
            counter.LogicalSelectManyOuter,
            counter.LogicalSelectManyInner,
            counter.LogicalJoinOuter,
            counter.LogicalJoinInner,
            counter.LogicalGroupJoinOuter,
            counter.LogicalGroupJoinInner,
            counter.LogicalLeftJoinOuter,
            counter.LogicalLeftJoinInner,
            counter.LogicalRightJoinOuter,
            counter.LogicalRightJoinInner,
            counter.PhysicalMoveNext,
            counter.HashBuild,
            counter.HashProbe,
            counter.BufferAdd,
            counter.TryGetNonEnumeratedCount);
    }

    public static void EmitActual(string label,LinqCounter counter) {
        if(IsEmpty(counter)) {
            return;
        }

        Console.WriteLine(
            "{0}: delegate(log/phys)={1}/{2}; logical ops={3}, select={4}, where={5}, selectMany(o/i)={6}/{7}, join(o/i)={8}/{9}, groupJoin(o/i)={10}/{11}, leftJoin(o/i)={12}/{13}, rightJoin(o/i)={14}/{15}, moveNext={16}, hash(build/probe)={17}/{18}, bufferAdd={19}, tryGetNonEnumeratedCount={20}",
            label,
            counter.LogicalDelegate,
            counter.PhysicalDelegate,
            counter.LogicalOperations,
            counter.LogicalSelect,
            counter.LogicalWhere,
            counter.LogicalSelectManyOuter,
            counter.LogicalSelectManyInner,
            counter.LogicalJoinOuter,
            counter.LogicalJoinInner,
            counter.LogicalGroupJoinOuter,
            counter.LogicalGroupJoinInner,
            counter.LogicalLeftJoinOuter,
            counter.LogicalLeftJoinInner,
            counter.LogicalRightJoinOuter,
            counter.LogicalRightJoinInner,
            counter.PhysicalMoveNext,
            counter.HashBuild,
            counter.HashProbe,
            counter.BufferAdd,
            counter.TryGetNonEnumeratedCount);
    }

    private static bool IsEmpty(LinqCounter counter) =>
        counter.PhysicalMoveNext==0
        &&counter.LogicalOperations==0
        &&counter.LogicalWhere==0
        &&counter.LogicalSelect==0
        &&counter.LogicalSelectManyOuter==0
        &&counter.LogicalSelectManyInner==0
        &&counter.LogicalJoinOuter==0
        &&counter.LogicalJoinInner==0
        &&counter.LogicalGroupJoinOuter==0
        &&counter.LogicalGroupJoinInner==0
        &&counter.LogicalLeftJoinOuter==0
        &&counter.LogicalLeftJoinInner==0
        &&counter.LogicalRightJoinOuter==0
        &&counter.LogicalRightJoinInner==0
        &&counter.HashBuild==0
        &&counter.HashProbe==0
        &&counter.BufferAdd==0
        &&counter.TryGetNonEnumeratedCount==0
        &&counter.LogicalDelegate==0
        &&counter.PhysicalDelegate==0;
}
