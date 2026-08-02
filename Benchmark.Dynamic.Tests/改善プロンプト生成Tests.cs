using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.UI;

namespace Benchmark.Dynamic.Tests;

public sealed class 改善プロンプト生成Tests {
    [Fact]
    public void 速度プロンプトは共通情報と速度指標を含む() {
        var 行=行を作成();
        var Prompt=改善プロンプト生成.速度(行);
        Assert.Contains("速度改善案",Prompt,StringComparison.Ordinal);
        Assert.Contains("Base seed",Prompt,StringComparison.Ordinal);
        Assert.Contains("median ns/op",Prompt,StringComparison.Ordinal);
        Assert.DoesNotContain("allocated bytes/op",Prompt,StringComparison.Ordinal);
        Assert.Contains(行.IR,Prompt,StringComparison.Ordinal);
        Assert.Contains(行.生成Source,Prompt,StringComparison.Ordinal);
        Assert.Contains("Optimization trace",Prompt,StringComparison.Ordinal);
    }

    [Fact]
    public void メモリプロンプトは割り当てとGC指標を含む() {
        var Prompt=改善プロンプト生成.メモリ(行を作成());
        Assert.Contains("メモリ割り当て削減案",Prompt,StringComparison.Ordinal);
        Assert.Contains("allocated bytes/op",Prompt,StringComparison.Ordinal);
        Assert.Contains("Gen0 collections",Prompt,StringComparison.Ordinal);
        Assert.Contains("raw samples",Prompt,StringComparison.Ordinal);
        Assert.DoesNotContain("median ns/op",Prompt,StringComparison.Ordinal);
    }

    [Fact]
    public void 同一入力では決定的で未取得値を推測しない() {
        var 行=行を作成();
        行.計測詳細="";
        var First=改善プロンプト生成.メモリ(行);
        var Second=改善プロンプト生成.メモリ(行);
        Assert.Equal(First,Second);
        Assert.Contains("未取得",First,StringComparison.Ordinal);
    }

    private static 動的結果行 行を作成() {
        return new 動的結果行 {
            実行ID="run-1",
            QueryID="query-1",
            対象=SourceTarget.SystemArray,
            QueryIndex=0,
            短縮式="Count",
            N=3,
            深度=3,
            BaseSeed="0x1234",
            QuerySeed="0x5678",
            入力要素数="32",
            演算子集合="Common",
            状態="Succeeded",
            意味一致="一致",
            IR="""{"irSchemaVersion":1}""",
            生成Source="class Generated {}",
            最適化Trace="""{"Events":[]}""",
            診断="[]",
            計測詳細="""
                {"measurement":{"measurements":[
                  {"implementation":"System","medianNanoseconds":1000,"meanNanoseconds":1100,"minimumNanoseconds":900,"maximumNanoseconds":1300,"standardDeviationNanoseconds":50,"standardErrorNanoseconds":20,"allocatedBytesPerOperation":16,"gen0Collections":1,"gen1Collections":0,"gen2Collections":0,"operationCount":10,"warmupCount":2,"sampleCount":7}
                ]}}
                """,
        };
    }
}
