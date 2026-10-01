using Sample.NetShop.Core.Shop;
using Seamless.Database.System;

namespace Sample.NetShop.Core;

/// <summary>3章: 宣言的DB。表・ビュー・関数を C# で宣言し、生成器が目録を作る。</summary>
public sealed class 商店DB章:物語の章{
    public override string 題名=>"商店DBを開く";
    public override string 使う機能=>"DatabaseContainer / Table / View / TableFunction / ScalarFunction";
    public override string 説明=>
        "商店のデータは宣言的DBに置きます。表やビューは C# のプロパティとメソッドで書き、\n"+
        "Seamless.Database.Generator が目録(DatabaseObjects)と保存用の読み書きを生成します。\n"+
        "DB はディスクに保存されるので、アプリを閉じても次の起動で続きから営業できます。";
    public override string コード=>
        """
        public Table<Keys.商品,Records.商品>商品=>this.Table<Keys.商品,Records.商品>(行=>new(行.商品ID));
        public View<Records.商品>在庫切れ商品()=>this.View(()=>this.商品.Where(行=>行.在庫数==0));
        public TableFunction<Records.注文>顧客の注文履歴(int 顧客ID)=>this.TableFunction(...);
        public ScalarFunction<long>売上合計()=>this.ScalarFunction(()=>this.注文.Sum(行=>行.合計金額));

        using var 商店=商店DB.開く(データフォルダ,資源);   // ストレージ方式
        商店.初期データを登録する();                       // 空のときだけ
        foreach(var 物 in 商店.DatabaseObjects){ ... }     // 生成された目録
        var 切れ=商店.在庫切れ商品().ToList();
        var 履歴=商店.顧客の注文履歴(3).Count();
        var 売上=商店.売上合計().Value;
        """;

    public override async Task 実行Async(章の文脈 文脈){
        var 商店=文脈.商店;
        文脈.コードを指す("商店DB.開く");
        文脈.ログ($"商店DBを開きました: {Path.Combine(文脈.設定.データフォルダ,"shop")}");
        await 文脈.間を置くAsync();

        文脈.コードを指す("初期データを登録する");
        if(商店.初期データを登録する()){
            文脈.ログ("初めての開店なので、商品と顧客を登録しました。",ログ種別.成功);
        }else{
            文脈.ログ($"前回の続きから営業します(注文 {商店.注文.LongCount:N0} 件)。",ログ種別.成功);
        }
        this.件数を出す(文脈,商店);
        await 文脈.間を置くAsync();

        文脈.コードを指す("DatabaseObjects");
        foreach(var 物 in 商店.DatabaseObjects){
            if(物.Kind==DatabaseObjectKind.SystemTable){
                continue;
            }
            文脈.ログ($"目録: {種類の名前(物.Kind)} {物.Key.Name}");
            await 文脈.間を置くAsync(0.25);
        }

        在庫を描く(文脈,商店);
        文脈.コードを指す("在庫切れ商品()");
        var 切れ=商店.在庫切れ商品().ToList();
        if(切れ.Count==0){
            文脈.ログ("ビュー 在庫切れ商品: ありません。");
        }else{
            文脈.ログ($"ビュー 在庫切れ商品: {string.Join("、",切れ.Select(行=>行.名前))}",ログ種別.失敗);
        }
        await 文脈.間を置くAsync();

        文脈.コードを指す("顧客の注文履歴(3)");
        var 履歴=商店.顧客の注文履歴(3).ToList();
        文脈.ログ($"テーブル関数 顧客の注文履歴(3): {履歴.Count} 件");
        await 文脈.間を置くAsync();

        文脈.コードを指す("売上合計().Value");
        var 売上=商店.売上合計().Value;
        文脈.指標("売上合計",表示.金額(売上));
        文脈.ログ($"スカラー関数 売上合計: {表示.金額(売上)}");
        var 使用量=商店.ResourceUsage;
        文脈.メーター("DB の管理メモリ",使用量.MemoryBytes,使用量.MemoryLimitBytes);
        await 文脈.間を置くAsync();
    }

    private void 件数を出す(章の文脈 文脈,商店DB 商店){
        文脈.指標("商品",商店.商品.LongCount.ToString("N0"));
        文脈.指標("顧客",商店.顧客.LongCount.ToString("N0"));
        文脈.指標("注文",商店.注文.LongCount.ToString("N0"));
        文脈.指標("注文明細",商店.注文明細.LongCount.ToString("N0"));
    }

    /// <summary>商品ごとの在庫を棒グラフにする。在庫切れは警告色。</summary>
    internal static void 在庫を描く(章の文脈 文脈,商店DB 商店){
        var 棒一覧=new List<棒>();
        // 表の列挙順はキー順ではないので、商品IDの順に引く。
        foreach(var 登録 in 初期データ.商品一覧){
            var 商品=商店.商品[new(登録.商品ID)];
            var 色=棒の色.標準;
            if(商品.在庫数==0){
                色=棒の色.警告;
            }
            棒一覧.Add(new 棒(商品.名前,商品.在庫数,色));
        }
        文脈.棒グラフ("商品の在庫","個",棒一覧);
    }

    private static string 種類の名前(DatabaseObjectKind 種類){
        switch(種類){
            case DatabaseObjectKind.Table:
                return "表";
            case DatabaseObjectKind.View:
                return "ビュー";
            case DatabaseObjectKind.TableFunction:
                return "テーブル関数";
            case DatabaseObjectKind.ScalarFunction:
                return "スカラー関数";
            case DatabaseObjectKind.StoredProcedure:
                return "ストアドプロシージャ";
            default:
                return 種類.ToString();
        }
    }
}
