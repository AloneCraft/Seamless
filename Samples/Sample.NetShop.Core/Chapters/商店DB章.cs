using Sample.NetShop.Core.Shop;
using Seamless.Database.System;

namespace Sample.NetShop.Core;

/// <summary>3章: 宣言的DB。表・ビュー・関数を C# で宣言し、生成器が目録を作る。</summary>
public sealed class 商店DB章:物語の章{
    public override string 題名=>"商店DBを開く";
    public override string 使う機能=>"DatabaseContainer / Table / ForeignKey / IReader への拡張メンバー(ビュー・関数) / BeginRead";
    public override string 説明=>
        "商店のデータは宣言的DBに置きます。表は C# のプロパティで書き、表どうしの関係は [ForeignKey] で宣言します。\n"+
        "ビューと関数は生成される入口(商店DB.IReader)への拡張メンバーとして書きます。\n"+
        "Seamless.Database.Generator が目録(DatabaseObjects)と保存用の読み書きを生成します。\n"+
        "DB はディスクに保存されるので、アプリを閉じても次の起動で続きから営業できます。";
    public override string コード=>
        """
        public Table<Keys.商品,Tables.商品>商品=>this.Table<Keys.商品,Tables.商品>(行=>行.Key);
        [ForeignKey("明細の注文","注文",new[]{"注文ID"},new[]{"注文ID"})]   // 親の行が無ければ Commit で拒否
        [ForeignKey("明細の商品","商品",new[]{"商品ID"},new[]{"商品ID"})]
        public Table<Keys.注文明細,Tables.注文明細>注文明細=>this.Table<Keys.注文明細,Tables.注文明細>(行=>行.Key);
        extension(商店DB.IReader 読み){   // ビューはプロパティ、関数はメソッド
            public ISet<Views.在庫切れ商品>在庫切れ商品=>読み.商品.Where(行=>行.在庫数==0).Select(行=>new Views.在庫切れ商品(行.商品ID,行.名前,行.在庫数));
            public ISet<Tables.注文>顧客の注文履歴(int 顧客ID)=>読み.注文.Where(行=>行.顧客ID==顧客ID);
            public long 売上合計()=>読み.注文.Sum(行=>行.合計金額);
        }

        using var 商店=商店DB.開く(データフォルダ,資源);   // ストレージ方式
        商店.初期データを登録する();                       // 空のときだけ
        foreach(var 物 in 商店.DatabaseObjects){ ... }     // 生成された目録
        using var 読み=商店.BeginRead();                   // 開始時点の内容を読む入口
        var 切れ=読み.在庫切れ商品.ToList();
        var 履歴=読み.顧客の注文履歴(3).Count();
        var 売上=読み.売上合計();
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
        文脈.コードを指す("商店.BeginRead()");
        using var 読み=商店.BeginRead();
        文脈.コードを指す("読み.在庫切れ商品");
        // ビューは集合なので並びを持たない。表示は商品IDの順にする。
        var 切れ=読み.在庫切れ商品.AsEnumerable().OrderBy(行=>行.商品ID).ToList();
        if(切れ.Count==0){
            文脈.ログ("ビュー 在庫切れ商品: ありません。");
        }else{
            文脈.ログ($"ビュー 在庫切れ商品: {string.Join("、",切れ.Select(行=>行.名前))}",ログ種別.失敗);
        }
        await 文脈.間を置くAsync();

        文脈.コードを指す("読み.顧客の注文履歴(3)");
        var 履歴=読み.顧客の注文履歴(3).Count();
        文脈.ログ($"テーブル関数 顧客の注文履歴(3): {履歴} 件");
        await 文脈.間を置くAsync();

        文脈.コードを指す("読み.売上合計()");
        var 売上=読み.売上合計();
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
