using Sample.NetShop.Core;
using Seamless.Execution;
using 対象=Sample.NetShop.Core.決算の集計;

namespace Sample.NetShop.Tests.Chapters;

public sealed class 決算の集計{
    private static ExecutionScope 最小のスコープ(テスト用フォルダ フォルダ)=>new(new ExecutionOptions{
        MemoryLimitBytes=資源設定.最小.クエリメモリ上限バイト数,
        TemporaryStorageLimitBytes=資源設定.最小.ディスク上限バイト数,
        TemporaryDirectory=フォルダ.下("scope"),
    });

    [Fact,Trait("Category","Unit")]public void 通常LINQで集計(){
        var 注文=new 注文の生成器(1_000,50,1);
        var 結果=new 対象().通常LINQで集計(注文);
        {
            //月別は1〜12月が月順に並び、合計は全注文の金額の和
            Assert.Equal(Enumerable.Range(1,12),結果.月別.Select(x=>x.月));
            Assert.Equal(注文.Sum(x=>x.金額),結果.月別.Sum(x=>x.金額));
        }
        {
            //上位顧客は金額の降順で上位件数だけ
            Assert.Equal(対象.上位件数,結果.上位顧客.Length);
            Assert.Equal(結果.上位顧客.OrderByDescending(x=>x.金額).ThenBy(x=>x.顧客ID),結果.上位顧客);
        }
    }

    // FLinqで集計 は、呼び出し元をコンパイルするプロジェクトで FLinq が置き換える。
    // このテストプロジェクトは FLinq を使わないので、置き換え後の結果は Core から呼ぶ決算章(物語の再生器のテスト)で確かめる。

    [Fact,Trait("Category","Unit")]public void スコープで集計(){
        using var フォルダ=new テスト用フォルダ();
        {
            //メモリ最小のスコープでディスクへ退避しても System.Linq と同じ結果になる
            var 注文=new 注文の生成器(10_000,500,3);
            var 集計=new 対象();
            using var スコープ=最小のスコープ(フォルダ);
            Assert.True(集計.通常LINQで集計(注文).一致するか(集計.スコープで集計(スコープ,注文)));
            Assert.True(スコープ.Budget.PeakMemoryBytes<=スコープ.Budget.MemoryLimitBytes);
        }
    }
}
