namespace Sample.NetShop.Core.Shop.Views;

/// <summary>ビュー: 在庫が0の商品。列挙するたびに定義を評価する。列は匿名型から生成され、商店DB.在庫切れ商品 プロパティで読む。</summary>
public readonly partial record struct 在庫切れ商品{
    public static IEnumerable<object> Query(商店DB db)=>db.在庫切れ商品を探す().Select(行=>new{行.商品ID,行.名前,行.在庫数});
}
