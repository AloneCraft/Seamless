using Seamless.FLinq;

namespace Sample.NetShop.Core;

public readonly record struct 商品別件数(int 商品ID,int 件数);

/// <summary>
/// 同じ LINQ を2通りに書いたもの。FLinq は、LINQ を含むメソッドの呼び出しをコンパイル時に展開した処理へ置き換える。
/// LinqIntercept(false) を付けたメソッドは置き換えず、通常の System.Linq のまま動く。
/// </summary>
public sealed class FLinqの比較{
    [LinqIntercept(false)]
    public long 通常LINQで条件付き合計(注文イベント[]注文)=>注文.Where(x=>x.数量>=3).Select(x=>x.金額).Sum();

    public long FLinqで条件付き合計(注文イベント[]注文)=>注文.Where(x=>x.数量>=3).Select(x=>x.金額).Sum();

    [LinqIntercept(false)]
    public 商品別件数[]通常LINQで商品別件数(注文イベント[]注文)=>
        注文.GroupBy(x=>x.商品ID).Select(g=>new 商品別件数(g.Key,g.Count())).OrderBy(x=>x.商品ID).ToArray();

    public 商品別件数[]FLinqで商品別件数(注文イベント[]注文)=>
        注文.GroupBy(x=>x.商品ID).Select(g=>new 商品別件数(g.Key,g.Count())).OrderBy(x=>x.商品ID).ToArray();
}
