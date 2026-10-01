namespace Sample.NetShop.Core.Shop;

/// <summary>注文1行分の依頼。</summary>
public readonly record struct 注文行(int 商品ID,int 数量);

/// <summary>注文確定の結果。失敗した注文は取引ごと取り消され、在庫も元に戻っている。</summary>
public readonly record struct 注文結果(bool 成功か,long 注文ID,long 合計金額,string 理由){
    public static 注文結果 成功(long 注文ID,long 合計金額)=>new(true,注文ID,合計金額,"");
    public static 注文結果 失敗(string 理由)=>new(false,0,0,理由);
}
