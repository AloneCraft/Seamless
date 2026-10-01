using Seamless.Execution;
using Seamless.Linq;

namespace Sample.NetShop.Core;

/// <summary>5章: ExecutionScope の Query。メモリ上限を決めて並べ替え・GroupBy・Join し、上限を超えた分はディスクへ退避する。</summary>
public sealed class 繁忙期章:物語の章{
    public override string 題名=>"繁忙期の大量注文";
    public override string 使う機能=>"ExecutionScope / Query / ToStorage";
    public override string 説明=>
        "年末の繁忙期、注文がメモリに収まらないほど届きました。\n"+
        "ExecutionScope でメモリの上限を決め、Query で並べ替え・GroupBy・Join します。\n"+
        "上限を超えた分はディスクへ退避するので結果は変わりません。全件を ToList するとメモリ上限で止まりますが、ToStorage ならディスクに逐次保存できます。";
    public override string コード=>
        """
        await using var スコープ=new ExecutionScope(new(){MemoryLimitBytes=上限,TemporaryDirectory=作業フォルダ});
        var 高額=スコープ.Query(注文).OrderByDescending(x=>x.金額).ThenBy(x=>x.注文ID).Take(5).ToList();
        var 商品別=スコープ.Query(注文)
            .GroupBy(x=>x.商品ID,x=>x.金額)
            .Select(g=>new 商品別売上(g.Key,g.Sum(),g.LongCount()))
            .Join(商品一覧,x=>x.商品ID,商品=>商品.商品ID,(x,商品)=>(商品.名前,x.金額));
        スコープ.Query(注文).ToList();                    // 全件をメモリへ → 上限で失敗
        using var 保存=スコープ.Query(注文).Where(x=>x.数量>=4).ToStorage();   // ディスクへ逐次保存
        """;

    public override async Task 実行Async(章の文脈 文脈){
        var 件数=200_000L;
        if(文脈.設定.低メモリか){
            件数=50_000L;
        }
        var 注文=new 注文の生成器(件数,1_000,文脈.周回);
        var 上限=文脈.資源.クエリメモリ上限バイト数;
        var 作業フォルダ=文脈.作業フォルダを作る("scope");
        文脈.ログ($"繁忙期の注文 {件数:N0} 件を集計します(メモリ上限 {表示.バイト(上限)})。");

        文脈.コードを指す("new ExecutionScope");
        await using(var スコープ=this.スコープを作る(文脈,上限,作業フォルダ)){
            await 文脈.間を置くAsync();
            文脈.コードを指す("OrderByDescending");
            var 高額=スコープ.Query(注文).OrderByDescending(x=>x.金額).ThenBy(x=>x.注文ID).Take(5).ToList();
            foreach(var 一件 in 高額){
                文脈.ログ($"高額注文 #{一件.注文ID}: {表示.金額(一件.金額)}(顧客{一件.顧客ID})");
            }
            this.資源を出す(文脈,スコープ);
            await 文脈.間を置くAsync();

            文脈.コードを指す(".GroupBy(x=>x.商品ID");
            var 商品一覧=Shop.初期データ.商品一覧;
            var 商品別=スコープ.Query(注文)
                .GroupBy(x=>x.商品ID,x=>x.金額)
                .Select(g=>new 商品別売上(g.Key,g.Sum(),g.LongCount()))
                .Join(商品一覧,x=>x.商品ID,商品=>商品.商品ID,(x,商品)=>new 棒(商品.名前,x.金額/10_000.0,棒の色.強調))
                .ToList();
            文脈.棒グラフ("商品別の売上","万円",商品別);
            this.資源を出す(文脈,スコープ);
            await 文脈.間を置くAsync(1.5);
        }

        文脈.コードを指す("ToList();");
        await using(var スコープ=this.スコープを作る(文脈,上限,作業フォルダ)){
            try{
                var 全件=スコープ.Query(注文).ToList();
                文脈.ログ($"全件 ToList は {全件.Count:N0} 件がメモリに収まりました。");
            }catch(ResourceLimitException 例外){
                文脈.ログ($"全件 ToList はメモリ上限で止まりました({例外.Kind}、上限 {表示.バイト(例外.LimitBytes)})。",ログ種別.失敗);
            }
            await 文脈.間を置くAsync(1.5);
        }

        文脈.コードを指す("ToStorage()");
        await using(var スコープ=this.スコープを作る(文脈,上限,作業フォルダ)){
            using var 保存=スコープ.Query(注文).Where(x=>x.数量>=4).ToStorage();
            long 保存件数=0;
            foreach(var _ in 保存){
                保存件数++;
            }
            文脈.ログ($"数量4個以上の注文 {保存件数:N0} 件をディスクへ逐次保存し、読み返しました。",ログ種別.成功);
            this.資源を出す(文脈,スコープ);
            await 文脈.間を置くAsync(1.5);
        }
    }

    private ExecutionScope スコープを作る(章の文脈 文脈,long 上限,string 作業フォルダ)=>new(new ExecutionOptions{
        MemoryLimitBytes=上限,
        TemporaryStorageLimitBytes=文脈.資源.ディスク上限バイト数,
        TemporaryDirectory=作業フォルダ,
        CancellationToken=文脈.取消,
    });

    private void 資源を出す(章の文脈 文脈,ExecutionScope スコープ){
        文脈.メーター("クエリのメモリ(最大)",スコープ.Budget.PeakMemoryBytes,スコープ.Budget.MemoryLimitBytes);
        文脈.指標("ディスクへの退避(最大)",表示.バイト(スコープ.Budget.PeakTemporaryStorageBytes));
    }
}
