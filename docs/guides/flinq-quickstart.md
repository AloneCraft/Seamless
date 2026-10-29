# ゼロアロケーション LINQ (FLinq Quickstart)

`Seamless.FLinq` は、.NET 標準の `System.Linq.Enumerable` に対する呼び出しをコンパイル時に検出し、中間オブジェクトのアロケーションや仮想呼び出しを一切行わない直接ループコードへと置き換える Roslyn Source Generator / Interceptor です。

---

## なぜ FLinq は標準 LINQ より速いのか？

通常の `System.Linq` は、各クエリ演算子（`Where`, `Select`, `Take` など）を呼び出すたびに、以下のオーバーヘッドを発生させます：

1. **反復子オブジェクト（Iterator）のヒープアロケーション**:
   各ステージごとに `WhereSelectEnumerableIterator` などの参照型インスタンスがヒープ上に確保されます。
2. **デリゲート呼び出し（Delegate Call）のオーバーヘッド**:
   ラムダ式（`x => x > 10`）を呼び出すたびに、デリゲートを経由した間接呼び出しコストが生じます。
3. **多段の `MoveNext()` / `Current` 仮想呼び出し**:
   データが下流に流れるたびに、各反復子のインターフェースまたは仮想メソッドを経由します。

### FLinq の解決策

FLinq は、**C# 12+ の Interceptor 機能** を利用して、ユーザーのコードを変更することなく、コンパイル時に呼び出し元をフックします。

```text
【ユーザーが書いたコード】
source.Where(x => x > 0).Select(x => x * 2).Sum()

【FLinq がコンパイル時に生成・置換するコード】
long sum = 0;
foreach (var item in source)
{
    if (item > 0)
    {
        sum += item * 2;
    }
}
return sum;
```

このように、多段のイテレータを **たった1つの平坦なループ** へと自動的に折り畳むため、GC 負荷が完全にゼロ（0バイト）となり、手書きの命令型ループと同等の最高速度を達成します。

---

## 有効化と属性による制御

FLinq は、アセンブリまたはクラス単位で有効化できます。

```csharp
using Seamless.Linq;

// アセンブリ全体で FLinq 最適化を有効化
[assembly: LinqIntercept(true)]

namespace MyApp
{
    public class Processor
    {
        public int ProcessData(int[] data)
        {
            // この LINQ 呼び出しは自動的にコンパイル時最適化されます
            return data.Where(x => x % 2 == 0).Select(x => x * 10).Count();
        }
    }
}
```

特定のメソッドだけ最適化を無効化したり、詳細な展開トレースを出力したりすることも可能です。

```csharp
// 特定のメソッドで最適化トレースを出力
[LinqIntercept(true, EmitOptimizationTrace = true)]
public void HighPerformanceQuery(List<int> list)
{
    var result = list.Where(x => x > 100).Take(10).ToArray();
}
```

---

## 主な対応演算子

FLinq は、.NET 10 で追加された最新の LINQ 演算子を含め、広範な API をカバーしています：

- **フィルタ・変換**: `Where`, `Select`, `SelectMany`, `Cast`, `OfType`
- **集計・縮約**: `Sum`, `Average`, `Min`, `Max`, `Count`, `Aggregate`, `AggregateBy`
- **切り出し・スキップ**: `Take`, `Skip`, `TakeWhile`, `SkipWhile`
- **集合演算**: `Distinct`, `DistinctBy`, `Union`, `Intersect`, `Except`
- **結合・グループ**: `Join`, `GroupJoin`, `GroupBy`, `CountBy`, `LeftJoin`, `RightJoin`
- **ターミナル**: `ToArray`, `ToList`, `ToDictionary`, `ToHashSet`, `First`, `FirstOrDefault`, `Single`

---

## 関連項目

- [先行の構造体LINQを超えた理由](../performance/beyond-struct-linq.md) : なぜ構造体イテレータの連鎖では不十分なのか
- [FLinq 内部設計](../architecture/flinq-internals.md) : Fragment パイプラインと構文木書き換えの詳細
- [C# の性能限界を引き出す](../performance/csharp-to-the-metal.md) : Unsafe, Span, SIMD との連携
