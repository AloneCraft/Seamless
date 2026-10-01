using Seamless.Collections.Tsavorite;
using Seamless.Execution;
// 旧 API にも同じ名前の Store があるため、共通ストアを明示する。
using Store=Seamless.Execution.Store;

namespace Sample.NetShop.Core;

/// <summary>2章: 永続ストアの保存(Save)と、保存した時点への巻き戻し(Load)。</summary>
public sealed class 保存と巻き戻し章:物語の章{
    public override string 題名=>"棚卸しと巻き戻し";
    public override string 使う機能=>"Store.Open / Save / Load";
    public override string 説明=>
        "棚卸しの在庫表を永続ストアに置きます。永続ストアは Save() した時点だけが確定し、保存ごとに番号が付きます。\n"+
        "うっかり在庫表を消しても、Load() で最後の保存へ、Load(番号) でもっと前の保存へ戻せます。";
    public override string コード=>
        """
        using var ストア=Store.Open(棚卸しフォルダ);
        using var 在庫=ストア.CreateTsavorite<int,int>("在庫");
        using(var 表=在庫.Create()){ 表[商品ID]=在庫数; }
        var 開店時=ストア.Save();          // 保存1
        using(var 表=在庫.Create()){ 表[商品ID]-=売れた数; }
        var 昼=ストア.Save();              // 保存2
        using(var 表=在庫.Create()){ 表.Remove(商品ID); }  // うっかり全部消す
        ストア.Load();                     // 最後の保存(昼)へ戻る
        ストア.Load(開店時);               // さらに開店時へ戻る
        """;

    public override async Task 実行Async(章の文脈 文脈){
        文脈.コードを指す("Store.Open");
        var フォルダ=文脈.作業フォルダを作る("inventory");
        using var ストア=Store.Open(フォルダ,new StoreLimits{MemoryBytes=文脈.資源.ストアメモリ上限バイト数,DiskBytes=文脈.資源.ディスク上限バイト数});
        文脈.コードを指す("CreateTsavorite<int,int>");
        using var 在庫=ストア.CreateTsavorite<int,int>("在庫");
        文脈.ログ("永続ストアを開き、在庫表を作りました。");
        await 文脈.間を置くAsync();

        文脈.コードを指す("表[商品ID]=在庫数");
        using(var 表=在庫.Create()){
            foreach(var 商品 in Shop.初期データ.商品一覧){
                表[商品.商品ID]=商品.在庫数;
            }
        }
        this.在庫を描く(文脈,在庫,"開店時の在庫");
        await 文脈.間を置くAsync();
        文脈.コードを指す("var 開店時=ストア.Save()");
        var 開店時=ストア.Save();
        文脈.ログ("保存1: 開店時の在庫を確定しました。",ログ種別.成功);
        文脈.指標("保存の数",ストア.Checkpoints.Count.ToString());
        await 文脈.間を置くAsync();

        文脈.コードを指す("-=売れた数");
        using(var 表=在庫.Create()){
            foreach(var 商品 in Shop.初期データ.商品一覧){
                var 売れた数=文脈.乱数.Next(0,表[商品.商品ID]+1);
                表[商品.商品ID]-=売れた数;
            }
        }
        this.在庫を描く(文脈,在庫,"昼の在庫");
        await 文脈.間を置くAsync();
        文脈.コードを指す("var 昼=ストア.Save()");
        ストア.Save();
        文脈.ログ("保存2: 昼の在庫を確定しました。",ログ種別.成功);
        文脈.指標("保存の数",ストア.Checkpoints.Count.ToString());
        await 文脈.間を置くAsync();

        文脈.コードを指す("表.Remove");
        using(var 表=在庫.Create()){
            foreach(var 商品 in Shop.初期データ.商品一覧){
                表.Remove(商品.商品ID);
            }
        }
        this.在庫を描く(文脈,在庫,"うっかり消した在庫");
        文脈.ログ("しまった!在庫表を全部消してしまいました(まだ保存していません)。",ログ種別.失敗);
        await 文脈.間を置くAsync(1.5);

        文脈.コードを指す("ストア.Load();");
        ストア.Load();
        this.在庫を描く(文脈,在庫,"Load() で戻した在庫");
        文脈.ログ("Load() で最後の保存(昼)へ戻しました。",ログ種別.成功);
        await 文脈.間を置くAsync(1.5);

        文脈.コードを指す("ストア.Load(開店時)");
        ストア.Load(開店時);
        this.在庫を描く(文脈,在庫,"Load(開店時) で戻した在庫");
        文脈.ログ("Load(開店時) で開店時の保存へ戻しました。それより新しい保存は使えなくなります。",ログ種別.成功);
        文脈.指標("保存の数",ストア.Checkpoints.Count.ToString());
        await 文脈.間を置くAsync(1.5);
    }

    private void 在庫を描く(章の文脈 文脈,DictionaryStore<int,int>在庫,string 題名){
        var 棒一覧=new List<棒>();
        using(var 表=在庫.Create()){
            foreach(var 商品 in Shop.初期データ.商品一覧){
                var 数=0;
                if(表.TryGetValue(商品.商品ID,out var 値)){
                    数=値;
                }
                棒一覧.Add(new 棒(商品.名前,数,棒の色.標準));
            }
        }
        文脈.棒グラフ("在庫表","個",棒一覧);
        文脈.指標("表示中",題名);
    }
}
