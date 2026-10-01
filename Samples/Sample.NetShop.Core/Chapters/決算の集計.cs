using Seamless.Execution;
using Seamless.FLinq;
using Seamless.Linq;

namespace Sample.NetShop.Core;

public sealed record 決算結果(月別売上[]月別,顧客別売上[]上位顧客){
    // Span の比較は FLinq の展開対象外なので、置き換えを止める(止めないと実行時に例外になる)。
    [LinqIntercept(false)]
    public bool 一致するか(決算結果 相手)=>
        this.月別.AsSpan().SequenceEqual(相手.月別)&&this.上位顧客.AsSpan().SequenceEqual(相手.上位顧客);
}

/// <summary>
/// 決算の集計(月別売上と売上上位10顧客)を3通りで書いたもの。
/// 定数は「決算の集計.上位件数」と型名つきで書く。FLinq はメソッド本体を別の型へ展開するため、
/// 型名なしの定数は展開先で見つからずコンパイルエラーになる(生成器の既知不具合)。
/// </summary>
public sealed class 決算の集計{
    public const int 上位件数=10;

    [LinqIntercept(false)]
    public 決算結果 通常LINQで集計(IEnumerable<注文イベント>注文)=>new(
        注文.GroupBy(x=>x.月,x=>x.金額).Select(g=>new 月別売上(g.Key,g.Sum())).OrderBy(x=>x.月).ToArray(),
        注文.GroupBy(x=>x.顧客ID,x=>x.金額).Select(g=>new 顧客別売上(g.Key,g.Sum()))
            .OrderByDescending(x=>x.金額).ThenBy(x=>x.顧客ID).Take(決算の集計.上位件数).ToArray());

    public 決算結果 FLinqで集計(IEnumerable<注文イベント>注文)=>new(
        注文.GroupBy(x=>x.月,x=>x.金額).Select(g=>new 月別売上(g.Key,g.Sum())).OrderBy(x=>x.月).ToArray(),
        注文.GroupBy(x=>x.顧客ID,x=>x.金額).Select(g=>new 顧客別売上(g.Key,g.Sum()))
            .OrderByDescending(x=>x.金額).ThenBy(x=>x.顧客ID).Take(決算の集計.上位件数).ToArray());

    /// <summary>メモリ上限つきのスコープで集計する。GroupBy と並べ替えは上限を超えるとディスクへ退避する。</summary>
    // Query 以降は Seamless.Linq の ScopedQuery の演算で、FLinq の展開対象ではないので置き換えを止める。
    [LinqIntercept(false)]
    public 決算結果 スコープで集計(ExecutionScope スコープ,IEnumerable<注文イベント>注文)=>new(
        スコープ.Query(注文).GroupBy(x=>x.月,x=>x.金額).Select(g=>new 月別売上(g.Key,g.Sum())).OrderBy(x=>x.月).ToArray(),
        スコープ.Query(注文).GroupBy(x=>x.顧客ID,x=>x.金額).Select(g=>new 顧客別売上(g.Key,g.Sum()))
            .OrderByDescending(x=>x.金額).ThenBy(x=>x.顧客ID).Take(決算の集計.上位件数).ToArray());
}
