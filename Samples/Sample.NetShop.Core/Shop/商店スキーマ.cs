namespace Sample.NetShop.Core.Shop{
    // Keys/Records 名前空間の partial struct は、Seamless.Database.Generator が
    // コンストラクタと MemoryPack の読み書きを補完する(同じ名前空間の DatabaseGenerator が目印)。
    namespace Records{
        public partial struct 商品{
            public partial int 商品ID{get;}
            public partial string 名前{get;}
            public partial int 単価{get;}
            public partial int 在庫数{get;}
        }
        public partial struct 顧客{
            public partial int 顧客ID{get;}
            public partial string 名前{get;}
            public partial string 地域{get;}
        }
        public partial struct 注文{
            public partial long 注文ID{get;}
            public partial int 顧客ID{get;}
            public partial DateTime 注文日時{get;}
            public partial long 合計金額{get;}
        }
        public partial struct 注文明細{
            public partial long 注文ID{get;}
            public partial int 行番号{get;}
            public partial int 商品ID{get;}
            public partial int 数量{get;}
            public partial long 小計{get;}
        }
    }
    namespace Keys{
        public partial struct 商品{
            public partial int 商品ID{get;}
        }
        public partial struct 顧客{
            public partial int 顧客ID{get;}
        }
        public partial struct 注文{
            public partial long 注文ID{get;}
        }
        public partial struct 注文明細{
            public partial long 注文ID{get;}
            public partial int 行番号{get;}
        }
    }
}
