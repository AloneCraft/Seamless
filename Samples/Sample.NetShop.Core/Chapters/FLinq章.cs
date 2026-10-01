namespace Sample.NetShop.Core;

/// <summary>6章: FLinq。同じ LINQ を、通常の System.Linq とコンパイル時に展開した FLinq で比べる。</summary>
public sealed class FLinq章:物語の章{
    public override string 題名=>"FLinq で書き換える";
    public override string 使う機能=>"Seamless.FLinq.Generator / LinqIntercept";
    public override string 説明=>
        "FLinq はソースジェネレータです。LINQ を含むメソッドの呼び出しを、コンパイル時に展開したループへ置き換えます。\n"+
        "書くコードは普通の LINQ のままで、使う側は何も変えません。[LinqIntercept(false)] を付けると置き換えを止められます。\n"+
        "同じ注文データで結果が一致すること、時間と確保メモリがどう変わるかを見てください。\n"+
        "Where/Select/Sum のような流れは確保が減って速くなりやすく、GroupBy のように中間の集まりを作る処理は差が出にくいことがあります。";
    public override string コード=>
        """
        [LinqIntercept(false)]
        public long 通常LINQで条件付き合計(注文イベント[]注文)=>注文.Where(x=>x.数量>=3).Select(x=>x.金額).Sum();
        public long FLinqで条件付き合計(注文イベント[]注文)=>注文.Where(x=>x.数量>=3).Select(x=>x.金額).Sum();

        [LinqIntercept(false)]
        public 商品別件数[]通常LINQで商品別件数(注文イベント[]注文)=>注文.GroupBy(x=>x.商品ID).Select(...).OrderBy(...).ToArray();
        public 商品別件数[]FLinqで商品別件数(注文イベント[]注文)=>注文.GroupBy(x=>x.商品ID).Select(...).OrderBy(...).ToArray();

        var 通常=計測.実行(()=>比較.通常LINQで条件付き合計(注文),3);
        var 展開=計測.実行(()=>比較.FLinqで条件付き合計(注文),3);   // この呼び出しが置き換わる
        """;

    public override async Task 実行Async(章の文脈 文脈){
        var 件数=300_000L;
        if(文脈.設定.低メモリか){
            件数=50_000L;
        }
        var 注文=new 注文の生成器(件数,1_000,文脈.周回).ToArray();
        var 比較=new FLinqの比較();
        文脈.ログ($"注文 {件数:N0} 件を配列に用意しました。");
        await 文脈.間を置くAsync();

        文脈.コードを指す("計測.実行(()=>比較.通常LINQで条件付き合計");
        var 通常の合計=計測.実行(()=>比較.通常LINQで条件付き合計(注文),3);
        文脈.コードを指す("計測.実行(()=>比較.FLinqで条件付き合計");
        var 展開の合計=計測.実行(()=>比較.FLinqで条件付き合計(注文),3);
        this.比べる(文脈,"条件付き合計",通常の合計.時間,展開の合計.時間,通常の合計.確保バイト数,展開の合計.確保バイト数,通常の合計.値==展開の合計.値);
        await 文脈.間を置くAsync(2);

        文脈.コードを指す("FLinqで商品別件数");
        var 通常の件数=計測.実行(()=>比較.通常LINQで商品別件数(注文),3);
        var 展開の件数=計測.実行(()=>比較.FLinqで商品別件数(注文),3);
        this.比べる(文脈,"商品別件数",通常の件数.時間,展開の件数.時間,通常の件数.確保バイト数,展開の件数.確保バイト数,通常の件数.値.AsSpan().SequenceEqual(展開の件数.値));
        await 文脈.間を置くAsync(2);
    }

    private void 比べる(章の文脈 文脈,string 題名,TimeSpan 通常の時間,TimeSpan 展開の時間,long 通常の確保,long 展開の確保,bool 一致か){
        文脈.棒グラフ($"{題名}: 時間","ms",[
            new("System.Linq",通常の時間.TotalMilliseconds,棒の色.比較),
            new("FLinq",展開の時間.TotalMilliseconds,棒の色.強調),
        ]);
        文脈.棒グラフ($"{題名}: 確保メモリ","KB",[
            new("System.Linq",通常の確保/1024.0,棒の色.比較),
            new("FLinq",展開の確保/1024.0,棒の色.強調),
        ]);
        if(一致か){
            文脈.ログ($"{題名}: 結果は一致。System.Linq {表示.ミリ秒(通常の時間)} / FLinq {表示.ミリ秒(展開の時間)}",ログ種別.成功);
        }else{
            throw new InvalidOperationException($"{題名}: System.Linq と FLinq の結果が一致しません。");
        }
    }
}
