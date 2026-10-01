using Seamless.Database;

namespace Sample.NetShop.Core.Shop;

/// <summary>Seamless.Database.Generator の目印。この名前空間の Keys/Records と DB を補完させる。</summary>
internal static partial class DatabaseGenerator;

/// <summary>
/// 小さなネット商店の DB。表・ビュー・テーブル関数・スカラー関数・ストアドプロシージャを
/// C# のプロパティとメソッドで宣言する。ストレージ方式で開くと、閉じて開き直しても内容が残る。
/// </summary>
public sealed partial class 商店DB(DatabaseContainerOptions オプション):DatabaseContainer(オプション){
    private long 最後の注文ID=-1;

    /// <summary>フォルダを指定してストレージ方式で開く。無ければ作る。</summary>
    public static 商店DB 開く(string フォルダ,資源設定 資源){
        var オプション=DatabaseContainerOptions.OnStorage(フォルダ) with{
            IndexSize=資源.DB索引バイト数,
            MemorySize=資源.DBメモリバイト数,
            ManagedMemoryLimitBytes=資源.DB管理メモリ上限バイト数,
            TransactionMemoryLimit=資源.DB取引メモリ上限バイト数,
        };
        return new 商店DB(オプション);
    }

    // ---- 表 ----
    public Table<Keys.商品,Records.商品>商品=>this.Table<Keys.商品,Records.商品>(static 行=>new(行.商品ID));
    public Table<Keys.顧客,Records.顧客>顧客=>this.Table<Keys.顧客,Records.顧客>(static 行=>new(行.顧客ID));
    public Table<Keys.注文,Records.注文>注文=>this.Table<Keys.注文,Records.注文>(static 行=>new(行.注文ID));
    public Table<Keys.注文明細,Records.注文明細>注文明細=>this.Table<Keys.注文明細,Records.注文明細>(static 行=>new(行.注文ID,行.行番号));

    // ---- ビュー: 列挙するたびに定義を評価する ----
    public View<Records.商品>在庫切れ商品()=>this.View(()=>this.在庫切れ商品を探す());
    public IEnumerable<Records.商品>在庫切れ商品を探す()=>
        this.商品.Where(行=>行.在庫数==0).OrderBy(行=>行.商品ID);

    // ---- テーブル関数: 引数を取るビュー ----
    public TableFunction<Records.注文>顧客の注文履歴(int 顧客ID)=>this.TableFunction(()=>this.顧客の注文を探す(顧客ID));
    public IEnumerable<Records.注文>顧客の注文を探す(int 顧客ID)=>
        this.注文.Where(行=>行.顧客ID==顧客ID).OrderBy(行=>行.注文ID);

    // ---- スカラー関数: 呼んだ時点で評価済みの値を返す ----
    public ScalarFunction<long>売上合計()=>this.ScalarFunction(()=>this.売上を合計する());
    public long 売上を合計する()=>this.注文.Sum(行=>行.合計金額);

    // ---- ストアドプロシージャ: 呼んだ時点で実行済みの結果を返す ----
    public StoredProcedure<注文結果>注文確定(int 顧客ID,IReadOnlyList<注文行>行一覧,DateTime 注文日時)=>
        this.StoredProcedure(()=>this.注文を確定する(顧客ID,行一覧,注文日時));

    /// <summary>
    /// 1つの取引の中で在庫を引き当て、注文と明細を登録する。
    /// 途中で在庫が足りなければ Commit せずに抜けるので、それまでに減らした在庫も含めて全部取り消される。
    /// </summary>
    private 注文結果 注文を確定する(int 顧客ID,IReadOnlyList<注文行>行一覧,DateTime 注文日時){
        if(!this.顧客.ContainsKey(new(顧客ID))){
            return 注文結果.失敗($"顧客{顧客ID}は登録されていません");
        }
        var 注文ID=this.次の注文IDを見る();
        using var 取引=this.BeginTransaction();
        long 合計=0;
        for(var 番号=0;番号<行一覧.Count;番号++){
            var 行=行一覧[番号];
            if(!this.商品.TryGetValue(new(行.商品ID),out var 商品)){
                return 注文結果.失敗($"商品{行.商品ID}は登録されていません");
            }
            if(商品.在庫数<行.数量){
                return 注文結果.失敗($"{商品.名前}の在庫が足りません(在庫{商品.在庫数}、注文{行.数量})");
            }
            this.商品[new(商品.商品ID)]=new Records.商品(商品.商品ID,商品.名前,商品.単価,商品.在庫数-行.数量);
            long 小計=(long)商品.単価*行.数量;
            this.注文明細.Add(new Records.注文明細(注文ID,番号+1,商品.商品ID,行.数量,小計));
            合計+=小計;
        }
        this.注文.Add(new Records.注文(注文ID,顧客ID,注文日時,合計));
        取引.Commit();
        this.最後の注文ID=注文ID;
        return 注文結果.成功(注文ID,合計);
    }

    /// <summary>入荷。在庫を増やす。</summary>
    public void 入荷(int 商品ID,int 数量){
        using var 取引=this.BeginTransaction();
        var 商品=this.商品[new(商品ID)];
        this.商品[new(商品ID)]=new Records.商品(商品.商品ID,商品.名前,商品.単価,商品.在庫数+数量);
        取引.Commit();
    }

    /// <summary>空の DB に商品と顧客を登録する。登録済みなら何もしない。</summary>
    /// <returns>登録したら true。</returns>
    public bool 初期データを登録する(){
        if(this.商品.LongCount>0){
            return false;
        }
        using var 取引=this.BeginTransaction();
        foreach(var 商品 in 初期データ.商品一覧){
            this.商品.Add(商品);
        }
        foreach(var 顧客 in 初期データ.顧客一覧){
            this.顧客.Add(顧客);
        }
        取引.Commit();
        return true;
    }

    private long 次の注文IDを見る(){
        if(this.最後の注文ID<0){
            // 開き直した直後だけ、保存済みの最大値から続きを決める。
            this.最後の注文ID=this.保存済みの最大注文ID();
        }
        return this.最後の注文ID+1;
    }

    // LINQ と非公開フィールドの読み書きを同じメソッドに書くと、FLinq の展開がコンパイルエラーになる(生成器の既知不具合)。
    // そのため LINQ はフィールドに触れないこのメソッドへ分けている。
    public long 保存済みの最大注文ID()=>this.注文.Select(行=>行.注文ID).DefaultIfEmpty(0).Max();
}
