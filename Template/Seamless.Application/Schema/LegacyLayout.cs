// 未指定の既存スキーマの配置を変えないことを、テンプレートの自己試験で確認する。
namespace Seamless.Application.Schema.Legacy{
    internal static class DatabaseGenerator;
}

namespace Seamless.Application.Schema.Legacy.Records{
    public partial struct Item{
        public partial byte Kind{get;}
        public partial long Id{get;}
        public partial byte State{get;}
    }

    public partial struct NamedItem{
        public partial byte Kind{get;}
        public partial string Name{get;}
        public partial long Id{get;}
        public partial byte State{get;}
    }
}
