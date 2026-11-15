// 表の行。サーバー(ServerSchema)とクライアント(ClientSchema)が同じ型を使う。
// [Key] を付けた列がキーになり、キーの型 Keys.<表> は DB の生成器が作る。
namespace Seamless.Application.Schema.Tables{
    public partial struct Item{
        public partial byte Kind{get;}
        [Seamless.Database.Key] public partial long Id{get;}
        public partial byte State{get;}
    }

    // 値を含む列と GC 参照を含む列の分類も、ビルド・往復試験で確認する。
    public partial struct NamedItem{
        public partial byte Kind{get;}
        public partial string Name{get;}
        [Seamless.Database.Key] public partial long Id{get;}
        public partial byte State{get;}
    }

    public partial struct MixedValue{
        public partial TextValue Text{get;}
        public partial byte Kind{get;}
        [Seamless.Database.Key] public partial long Id{get;}
        public partial int? Optional{get;}
        public partial string? Name{get;}
    }

    public partial struct NullableText{
        [Seamless.Database.Key] public partial int Id{get;}
        public partial TextValue? Value{get;}
    }
}

namespace Seamless.Application.Schema{
    // 列の型として使う値。表ではないので Tables の外に置く。
    [MemoryPack.MemoryPackable]
    public readonly partial record struct TextValue(string? Value);
}
