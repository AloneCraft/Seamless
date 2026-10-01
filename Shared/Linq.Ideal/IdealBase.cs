namespace Shared.Linq.Ideal;

/// <summary>
/// 手書きの理想ループ版ベンチの基底。定数・補助関数・ベンチの枠は Shared/Linq.Enumerable/Base.cs の写しで、
/// FLinq/ZLinq と同じ仕事量になるようにする(写しのずれは Benchmark.Audit.Tests で検査する)。
/// 理想版の書き方の規則は docs/flinq-ideal/理想版の記述規則.md。
/// </summary>
public abstract partial class IdealBase<T>:global::Common.Base<T> where T : notnull {
    private static void ExecuteOrderDependent(Action action) {
        action();
    }
#if BENCH_AS_TEST
    private const int Any_iterationCount = 100;
    private const int Empty_iterationCount = 100;
    private const int First_iterationCount = 100;
    private const int FirstOrDefault_iterationCount = 100;
    private const int Single_iterationCount = 100;
    private const int SingleOrDefault_iterationCount = 100;
    private const int TryGetNonEnumeratedCount_iterationCount = 100;
    private const int ElementAt_index_iterationCount = 1;
#else
    private const int Any_iterationCount = 800_000;
    private const int Empty_iterationCount = 25_000;
    private const int First_iterationCount = 5_000;
    private const int FirstOrDefault_iterationCount = 4_000;
    private const int Single_iterationCount = 6_000;
    private const int SingleOrDefault_iterationCount = 4_500;
    private const int TryGetNonEnumeratedCount_iterationCount = 2_000;
    private const int ElementAt_index_iterationCount = 3;
#endif
    private const int Contains_value_iterationCount = 3;
    private const int ImmediateShortCircuitCount = 96_000_000;
    private const int StandardBenchmarkElementCount = 4_096;
    private const int ImmediateFullScanCount = StandardBenchmarkElementCount;
    private const int ImmediatePredicateScanCount = StandardBenchmarkElementCount;
    private const int StreamOneToOneCount = StandardBenchmarkElementCount;
    private const int StreamFilterCount = StandardBenchmarkElementCount;
    private const int StreamPartitionCount = StandardBenchmarkElementCount;
    private const int StreamShapeCount = StandardBenchmarkElementCount;
    private const int MaterializeLinearCount = StandardBenchmarkElementCount;
    private const int MaterializeHashCount = StandardBenchmarkElementCount;
    private const int MaterializeLookupCount = StandardBenchmarkElementCount;
    private const int SetCount = StandardBenchmarkElementCount;
    private const int GroupingCount = StandardBenchmarkElementCount;
    private const int JoinCount = StandardBenchmarkElementCount;
    private const int SortCount = StandardBenchmarkElementCount;
    private const int NestedCount = StandardBenchmarkElementCount;
    private const int GeneratedCount = StandardBenchmarkElementCount;
    private const int SourceShapeCount = StandardBenchmarkElementCount;
    private const int SelectManyOuterCount = 64;
    private const int SelectManyInnerCount = StandardBenchmarkElementCount/SelectManyOuterCount;

    internal static int IterationCount(int defaultCount) {
        if(global::Common.BenchmarkExecutionMode.IsElementCountScoped) {
            return 1;
        }
        return defaultCount;
    }

    internal int MidpointIndex(int elementCount) =>
        global::System.Math.Max(0,this.EffectiveSourceCount(elementCount)/2);

    internal int MidpointValue(int elementCount) =>
        global::System.Math.Max(1,this.EffectiveSourceCount(elementCount)/2);

    internal int QuarterCount(int elementCount) =>
        global::System.Math.Max(0,this.EffectiveSourceCount(elementCount)/4);

    internal int ThreeQuarterCount(int elementCount) =>
        global::System.Math.Max(0,this.EffectiveSourceCount(elementCount)*3/4);

    internal static int Number(T value) =>
        ToInt(value);

    internal static T Value(int value) =>
        FromInt(value);

    internal static T Add(T left,T right) =>
        Value(Number(left)+Number(right));

    internal static T Add(T left,int right) =>
        Value(Number(left)+right);

    internal static bool EqualNumber(T value,int number) =>
        Number(value)==number;

    internal static bool GreaterThan(T value,int number) =>
        Number(value)>number;

    internal static bool GreaterThanOrEqual(T value,int number) =>
        Number(value)>=number;

    internal static bool LessThan(T value,int number) =>
        Number(value)<number;

    internal static bool IsEven(T value) =>
        (Number(value)&1)==0;

    internal static int Mod(T value,int divisor) =>
        Number(value)%divisor;

    internal static long LongNumber(T value) =>
        Number(value);

    internal int[] outer = [];
    internal int[] inner = [];
    internal int sourceCount;

    protected IdealBase(int count) : base(count) {
        this.Initialize();
    }

    private void Initialize() {
        var selectManyOuterCount = this.CountForComparison(SelectManyOuterCount);
        var selectManyInnerCount = this.CountForComparison(SelectManyInnerCount);
        if(global::Common.BenchmarkExecutionMode.IsElementCountScoped) {
            var totalCount = global::System.Math.Max(1,this.EffectiveSourceCount(NestedCount));
            selectManyOuterCount=global::System.Math.Max(1,(int)global::System.Math.Sqrt(totalCount));
            selectManyInnerCount=global::System.Math.Max(1,totalCount/selectManyOuterCount);
        }
        this.outer=CreateSequentialArray(selectManyOuterCount);
        this.inner=CreateSequentialArray(selectManyInnerCount);
        this.sourceCount=this.CountForComparison(this.CountForTestTiming(SourceShapeCount));
    }

    private static int[] CreateSequentialArray(int count) {
        var values = new int[count];
        for(var index = 0;index<count;index++) {
            values[index]=index;
        }
        return values;
    }

    internal static void ConsumeResult(global::Common.Consumer consumer,bool result,int count) {
        consumer.Consume(result);
        if(result) {
            consumer.Consume(count);
        } else {
            consumer.Consume(-1);
        }
    }
}
