using System.Runtime.CompilerServices;
using Tsavorite.Carrier;

namespace BenchmarkExe.Benchmarks;

// 構造最適化（不変式ホイスト/SelectMany 融合+述語プッシュダウン/ハッシュ結合）系ベンチの共有部品。
// LinqSequenceFusionContainer と同じく virtual メソッド内の LINQ をソースジェネレータが intercept する。
public class OptimizationBenchmarkContainer
{
    public virtual int SelectInvariantSum(int[] outer)
        => outer.Select(static value => value + HoistInvariant.Value()).Sum();

    public virtual int SelectManyWhereSum(int[] outer, int[] inner, int threshold)
        => outer.SelectMany(_ => inner).Where(value => value > threshold).Sum();

    public virtual int JoinSum(int[] outer, int[] inner)
        => outer.Join(inner, static o => o % 1000, static i => i, static (o, i) => o + i).Sum();
}

// ループ内で評価される不変式の代役。NoInlining で定数畳み込みを防ぎ、1 回呼ぶか N 回呼ぶかの差を可視化する。
internal static class HoistInvariant
{
    private static int seed = 3;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Value() => (seed * 7) + 1;
}
