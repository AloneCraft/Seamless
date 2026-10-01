namespace Sample.NetShop.Core.Shop;

/// <summary>開店時に登録する商品と顧客。</summary>
public static class 初期データ{
    public static IReadOnlyList<Records.商品>商品一覧{get;}=[
        new(1,"緑茶",480,12),
        new(2,"ほうじ茶",520,10),
        new(3,"抹茶",1200,6),
        new(4,"煎餅",350,15),
        new(5,"羊羹",980,8),
        new(6,"急須",3200,4),
        new(7,"湯呑み",1500,6),
        new(8,"茶筒",2100,5),
    ];

    public static IReadOnlyList<Records.顧客>顧客一覧{get;}=[
        new(1,"青木","北海道"),
        new(2,"石井","東北"),
        new(3,"上田","関東"),
        new(4,"遠藤","関東"),
        new(5,"大野","中部"),
        new(6,"加藤","近畿"),
        new(7,"木村","近畿"),
        new(8,"工藤","中国"),
        new(9,"小林","四国"),
        new(10,"佐藤","九州"),
    ];
}
