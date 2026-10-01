using 対象=Sample.NetShop.Core.実行設定;

namespace Sample.NetShop.Tests;

public sealed class 実行設定{
    [Fact,Trait("Category","Unit")]public void 引数から作る(){
        var 既定=Path.Combine(Path.GetTempPath(),"既定");
        {
            //引数なしは既定のデータフォルダ・100万件・標準の資源
            var 設定=対象.引数から作る([],既定);
            Assert.Equal(Path.GetFullPath(既定),設定.データフォルダ);
            Assert.Equal(対象.既定の決算注文件数,設定.決算の注文件数);
            Assert.False(設定.低メモリか);
            Assert.Equal(0,設定.開始章);
            Assert.Same(Core.資源設定.標準,設定.資源);
        }
        {
            //--data・--orders・--chapter・--low-memory を読む。章番号は1始まりで指定し0始まりで持つ
            var 設定=対象.引数から作る(["--data","D:/店","--orders","10000","--chapter","7","--low-memory"],既定);
            Assert.Equal(Path.GetFullPath("D:/店"),設定.データフォルダ);
            Assert.Equal(10_000,設定.決算の注文件数);
            Assert.True(設定.低メモリか);
            Assert.Equal(6,設定.開始章);
            Assert.Same(Core.資源設定.最小,設定.資源);
        }
        {
            //分からない引数・値の無い引数・不正な件数は例外
            Assert.Throws<ArgumentException>(()=>対象.引数から作る(["--unknown"],既定));
            Assert.Throws<ArgumentException>(()=>対象.引数から作る(["--data"],既定));
            Assert.Throws<ArgumentException>(()=>対象.引数から作る(["--orders","0"],既定));
            Assert.Throws<ArgumentException>(()=>対象.引数から作る(["--orders","abc"],既定));
            Assert.Throws<ArgumentException>(()=>対象.引数から作る(["--chapter","0"],既定));
        }
    }
}
