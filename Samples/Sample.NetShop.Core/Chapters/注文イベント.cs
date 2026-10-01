namespace Sample.NetShop.Core;

/// <summary>集計用の注文1件。参照を含まない構造体なので、ディスクへ退避するときもそのままのバイト列で書ける。</summary>
public readonly record struct 注文イベント(long 注文ID,int 顧客ID,int 商品ID,int 数量,long 金額,int 月);

public readonly record struct 月別売上(int 月,long 金額);

public readonly record struct 顧客別売上(int 顧客ID,long 金額);

public readonly record struct 商品別売上(int 商品ID,long 金額,long 件数);
