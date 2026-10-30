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

    [Fact,Trait("Category","Unit")]public void FLinqで集計(){
        var 注文=new 注文の生成器(10_000,500,2);
        var 集計=new 対象();
        var linq計測=集計.通常LINQで集計を計測(注文,3);
        var flinq計測=集計.FLinqで集計を計測(注文,3);
        {
            //FLinqの集計結果は通常LINQと完全に一致する
            Assert.True(linq計測.値.一致するか(flinq計測.値));
        }
        {
            //FLinqはDictionarySlimの1パス集計とTop-K部分ソートにより通常LINQよりメモリ確保量が大幅に少ない
            Assert.True(flinq計測.確保バイト数<linq計測.確保バイト数,$"FLinq ({flinq計測.確保バイト数:N0} bytes) は通常LINQ ({linq計測.確保バイト数:N0} bytes) よりメモリ確保が少ないこと");
        }
        {
            //FLinqの実行時間は通常LINQと同等以上である
            Assert.True(flinq計測.時間<=linq計測.時間*2.0,$"FLinq ({flinq計測.時間.TotalMilliseconds:F2} ms) は通常LINQ ({linq計測.時間.TotalMilliseconds:F2} ms) と同等以上の速度であること");
        }
    }

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
