namespace Seamless.Application.Schema.Keys{
    public partial struct Item{
        public partial long Id{get;}
    }
}

namespace Seamless.Application.Schema.Records{
    public partial struct Item{
        public partial byte Kind{get;}
        public partial long Id{get;}
        public partial byte State{get;}
    }

    // 値を含む部分とGC参照を含む部分の分類もビルド・往復試験で確認する。
    public partial struct NamedItem{
        public partial byte Kind{get;}
        public partial string Name{get;}
        public partial long Id{get;}
        public partial byte State{get;}
    }

    [MemoryPack.MemoryPackable]
    public readonly partial record struct TextValue(string? Value);

    public partial struct MixedValue{
        public partial TextValue Text{get;}
        public partial byte Kind{get;}
        public partial long Id{get;}
        public partial int? Optional{get;}
        public partial string? Name{get;}
    }

    public partial struct NullableText{
        public partial TextValue? Value{get;}
    }
}
