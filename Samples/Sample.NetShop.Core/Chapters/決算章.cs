using Seamless.Execution;

namespace Sample.NetShop.Core;

/// <summary>7章: 決算。1年分の注文を3通り(System.Linq / FLinq / メモリ上限つき Query)で集計して比べる。</summary>
public sealed class 決算章:物語の章{
    public override string 題名=>"決算";
    public override string 使う機能=>"System.Linq / FLinq / ExecutionScope の比較";
    public override string 説明=>
        "1年分の注文から、月別の売上と売上上位10人の顧客を集計します。\n"+
        "A: 通常の System.Linq、B: FLinq で展開、C: メモリ上限つきの Query(上限を超えるとディスクへ退避)。\n"+
        "3つの結果が一致すること、時間・確保メモリ・ディスク退避の違いを比べます。注文は全件をメモリに持たず、生成しながら流します。";
    public override string コード=>
        """
        var 注文=new 注文の生成器(件数,顧客数,種);          // 列挙のたびに同じ注文を生成(全件は持たない)
        var A=計測.実行(()=>集計.通常LINQで集計(注文),3);  // [LinqIntercept(false)]。3回測って最速
        var B=計測.実行(()=>集計.FLinqで集計(注文),3);     // FLinq が展開
        await using var スコープ=new ExecutionScope(new(){MemoryLimitBytes=上限,...});
        var C=計測.実行(()=>集計.スコープで集計(スコープ,注文));
        // 月別: GroupBy(x=>x.月).Select(g=>new 月別売上(g.Key,g.Sum())).OrderBy(x=>x.月)
        // 上位: GroupBy(x=>x.顧客ID)...OrderByDescending(x=>x.金額).ThenBy(x=>x.顧客ID).Take(10)
        A.値.一致するか(B.値) && A.値.一致するか(C.値)
        """;

    public override async Task 実行Async(章の文脈 文脈){
        var 件数=文脈.設定.決算の注文件数;
        var 顧客数=(int)Math.Max(100,件数/20);
        var 注文=new 注文の生成器(件数,顧客数,文脈.周回);
        var 集計=new 決算の集計();
        文脈.ログ($"1年分の注文 {件数:N0} 件(顧客 {顧客数:N0} 人)を集計します。");
        文脈.コードを指す("new 注文の生成器");
        await 文脈.間を置くAsync();

        文脈.コードを指す("var A=");
        文脈.ログ("A: System.Linq で集計中…(初回のコンパイル時間を除くため3回測って最速を採ります)");
        var A=await Task.Run(()=>計測.実行(()=>集計.通常LINQで集計(注文),3),文脈.取消);
        this.時間を描く(文脈,A.時間,null,null);
        await 文脈.間を置くAsync(0.5);

        文脈.コードを指す("var B=");
        文脈.ログ("B: FLinq で集計中…(同じく3回の最速)");
        var B=await Task.Run(()=>計測.実行(()=>集計.FLinqで集計(注文),3),文脈.取消);
        this.時間を描く(文脈,A.時間,B.時間,null);
        await 文脈.間を置くAsync(0.5);

        文脈.コードを指す("new ExecutionScope");
        var 上限=文脈.資源.クエリメモリ上限バイト数;
        文脈.ログ($"C: メモリ上限 {表示.バイト(上限)} の Query で集計中…");
        await using var スコープ=new ExecutionScope(new ExecutionOptions{
            MemoryLimitBytes=上限,
            TemporaryStorageLimitBytes=文脈.資源.ディスク上限バイト数,
            TemporaryDirectory=文脈.作業フォルダを作る("scope"),
            CancellationToken=文脈.取消,
        });
        文脈.コードを指す("var C=");
        var C=await Task.Run(()=>計測.実行(()=>集計.スコープで集計(スコープ,注文)),文脈.取消);
        this.時間を描く(文脈,A.時間,B.時間,C.時間);
        文脈.棒グラフ("確保メモリ","MB",[
            new("A System.Linq",A.確保バイト数/(double)(1<<20),棒の色.比較),
            new("B FLinq",B.確保バイト数/(double)(1<<20),棒の色.強調),
            new("C Query",C.確保バイト数/(double)(1<<20),棒の色.標準),
        ]);
        文脈.メーター("C のクエリメモリ(最大)",スコープ.Budget.PeakMemoryBytes,スコープ.Budget.MemoryLimitBytes);
        文脈.指標("C のディスク退避(最大)",表示.バイト(スコープ.Budget.PeakTemporaryStorageBytes));
        await 文脈.間を置くAsync();

        文脈.コードを指す("一致するか");
        if(!A.値.一致するか(B.値)||!A.値.一致するか(C.値)){
            throw new InvalidOperationException("3通りの集計結果が一致しません。");
        }
        文脈.ログ("A・B・C の集計結果はすべて一致しました。",ログ種別.成功);
        var 月別=new List<棒>();
        foreach(var 月 in A.値.月別){
            月別.Add(new 棒($"{月.月}月",月.金額/10_000.0,棒の色.強調));
        }
        文脈.棒グラフ("月別の売上","万円",月別);
        for(var 順位=0;順位<3&&順位<A.値.上位顧客.Length;順位++){
            var 顧客=A.値.上位顧客[順位];
            文脈.ログ($"売上{順位+1}位: 顧客{顧客.顧客ID} {表示.金額(顧客.金額)}");
        }
        await 文脈.間を置くAsync(2);
    }

    private void 時間を描く(章の文脈 文脈,TimeSpan A,TimeSpan? B,TimeSpan? C){
        var 棒一覧=new List<棒>{new("A System.Linq",A.TotalMilliseconds,棒の色.比較)};
        if(B is TimeSpan B時間){
            棒一覧.Add(new("B FLinq",B時間.TotalMilliseconds,棒の色.強調));
        }
        if(C is TimeSpan C時間){
            棒一覧.Add(new("C Query",C時間.TotalMilliseconds,棒の色.標準));
        }
        文脈.棒グラフ("集計の時間","ms",棒一覧);
    }
}
