using Seamless.Linq;

namespace Sample.NetShop.Core.Shop;

/// <summary>
/// 商店DBのビューと関数。生成される入口(商店DB.IReader)への拡張メンバーとして書く。
/// 読み込み専用の入口(BeginRead)からも取引(BeginTransaction)からも同じ書き方で呼べ、呼んだ入口の内容を読む。
/// FLinq が本体を呼び出し側へ取り込んで展開する。
/// </summary>
public static class 商店DBの問い合わせ{
    extension(商店DB.IReader 読み){
        // ---- ビュー: 在庫が0の商品 ----
        public Seamless.Collections.ISet<Views.在庫切れ商品>在庫切れ商品=>
            読み.商品.Where(行=>行.在庫数==0).Select(行=>new Views.在庫切れ商品(行.商品ID,行.名前,行.在庫数));

        // ---- テーブル関数: 引数を取るビュー ----
        public Seamless.Collections.ISet<Records.注文>顧客の注文履歴(int 顧客ID)=>
            読み.注文.Where(行=>行.顧客ID==顧客ID);

        // ---- スカラー関数: 値を返す ----
        public long 売上合計()=>読み.注文.Sum(行=>行.合計金額);
    }
}
