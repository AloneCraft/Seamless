namespace Sample.NetShop.Core.Shop;

/// <summary>開店時に登録する商品と顧客。</summary>
public static class 初期データ{
    public static IReadOnlyList<Tables.商品>商品一覧{get;}=[
        new(new(1),"緑茶",480,12),
        new(new(2),"ほうじ茶",520,10),
        new(new(3),"抹茶",1200,6),
        new(new(4),"煎餅",350,15),
        new(new(5),"羊羹",980,8),
        new(new(6),"急須",3200,4),
        new(new(7),"湯呑み",1500,6),
        new(new(8),"茶筒",2100,5),
    ];

    public static IReadOnlyList<Tables.顧客>顧客一覧{get;}=[
        new(new(1),"青木","北海道"),
        new(new(2),"石井","東北"),
        new(new(3),"上田","関東"),
        new(new(4),"遠藤","関東"),
        new(new(5),"大野","中部"),
        new(new(6),"加藤","近畿"),
        new(new(7),"木村","近畿"),
        new(new(8),"工藤","中国"),
        new(new(9),"小林","四国"),
        new(new(10),"佐藤","九州"),
    ];
}
