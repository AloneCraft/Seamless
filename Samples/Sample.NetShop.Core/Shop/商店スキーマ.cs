namespace Sample.NetShop.Core.Shop.Tables{
    // 表の行。[Key] を付けた列がキーになり、キーの型(Keys.<表>)とコンストラクター・MemoryPack の読み書きは
    // Seamless.Database.Generator が補完する(Shop 名前空間の DatabaseGenerator が目印)。
    public partial struct 商品{
        [Seamless.Database.Key] public partial int 商品ID{get;}
        public partial string 名前{get;}
        public partial int 単価{get;}
        public partial int 在庫数{get;}
    }
    public partial struct 顧客{
        [Seamless.Database.Key] public partial int 顧客ID{get;}
        public partial string 名前{get;}
        public partial string 地域{get;}
    }
    public partial struct 注文{
        [Seamless.Database.Key] public partial long 注文ID{get;}
        public partial int 顧客ID{get;}
        public partial DateTime 注文日時{get;}
        public partial long 合計金額{get;}
    }
    public partial struct 注文明細{
        [Seamless.Database.Key] public partial long 注文ID{get;}
        [Seamless.Database.Key] public partial int 行番号{get;}
        public partial int 商品ID{get;}
        public partial int 数量{get;}
        public partial long 小計{get;}
    }
}
