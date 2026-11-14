namespace Sample.NetShop.Core.Shop.Views;

/// <summary>ビュー 在庫切れ商品(商店DBの問い合わせ.在庫切れ商品)の結果の行。</summary>
public readonly record struct 在庫切れ商品(int 商品ID,string 名前,int 在庫数);
