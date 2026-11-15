using Seamless.Database;

namespace Sample.NetShop.Core.Shop;

/// <summary>Seamless.Database.Generator の目印。この名前空間の Tables からキーの型(Keys)と行の読み書きを補完させる。</summary>
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
    public Table<Keys.商品,Tables.商品>商品=>this.Table<Keys.商品,Tables.商品>(static 行=>new(行.商品ID));
    public Table<Keys.顧客,Tables.顧客>顧客=>this.Table<Keys.顧客,Tables.顧客>(static 行=>new(行.顧客ID));

    // ---- リレーションシップ(外部キー) ----
    // 子の表のプロパティに [ForeignKey(制約名,親の表,子の列,親の主キーの列)] を付ける。
    // 取引の Commit 時に最終状態で検査し、親の行が無ければ ForeignKeyViolationException で全部取り消す。
    // そのため、同じ取引の中なら明細を先に書いて注文を後に書いてもよい。
    [ForeignKey("注文の顧客","顧客",new[]{"顧客ID"},new[]{"顧客ID"})]
    public Table<Keys.注文,Tables.注文>注文=>this.Table<Keys.注文,Tables.注文>(static 行=>new(行.注文ID));
    [ForeignKey("明細の注文","注文",new[]{"注文ID"},new[]{"注文ID"})]
    [ForeignKey("明細の商品","商品",new[]{"商品ID"},new[]{"商品ID"})]
    public Table<Keys.注文明細,Tables.注文明細>注文明細=>this.Table<Keys.注文明細,Tables.注文明細>(static 行=>new(行.注文ID,行.行番号));

    // ---- ビュー・関数: 商店DBの問い合わせ(IReader への拡張メンバー)に書く ----

    /// <summary>今の売上合計。読み込み専用の入口を開いてスカラー関数 売上合計 を呼ぶ。</summary>
    public long 売上合計を読む(){
        using var 読み=this.BeginRead();
        return 読み.売上合計();
    }

    // ---- ストアドプロシージャ: 呼んだ時点で実行済みの結果を返す ----
    public StoredProcedure<注文結果>注文確定(int 顧客ID,IReadOnlyList<注文行>行一覧,DateTime 注文日時)=>
        this.StoredProcedure(()=>this.注文を確定する(顧客ID,行一覧,注文日時));

    /// <summary>
    /// 1つの取引の中で在庫を引き当て、注文と明細を登録する。
    /// 途中で在庫が足りなければ Commit せずに抜けるので、それまでに減らした在庫も含めて全部取り消される。
    /// </summary>
    private 注文結果 注文を確定する(int 顧客ID,IReadOnlyList<注文行>行一覧,DateTime 注文日時){
        using var 取引=this.BeginTransaction();
        var 結果=this.注文を書き込む(取引,顧客ID,行一覧,注文日時);
        if(結果.成功か){
            取引.Commit();
            this.最後の注文ID=結果.注文ID;
        }
        return 結果;
    }

    /// <summary>
    /// 渡された取引の中で在庫を引き当て、注文と明細を書き込む。確定(Commit)は呼び出し側が行う。
    /// 表の読み書きはすべて取引(取引.商品 など)を通す。取引の外(this.商品)からは書けない。
    /// </summary>
    public 注文結果 注文を書き込む(TypedTransaction 取引,int 顧客ID,IReadOnlyList<注文行>行一覧,DateTime 注文日時){
        if(!取引.顧客.ContainsKey(new(顧客ID))){
            return 注文結果.失敗($"顧客{顧客ID}は登録されていません");
        }
        var 注文ID=this.次の注文IDを見る(取引);
        long 合計=0;
        for(var 番号=0;番号<行一覧.Count;番号++){
            var 行=行一覧[番号];
            if(!取引.商品.TryGetValue(new(行.商品ID),out var 商品)){
                return 注文結果.失敗($"商品{行.商品ID}は登録されていません");
            }
            if(商品.在庫数<行.数量){
                return 注文結果.失敗($"{商品.名前}の在庫が足りません(在庫{商品.在庫数}、注文{行.数量})");
            }
            取引.商品[new(商品.商品ID)]=new Tables.商品(商品.Key,商品.名前,商品.単価,商品.在庫数-行.数量);
            long 小計=(long)商品.単価*行.数量;
            取引.注文明細.Add(new Tables.注文明細(new(注文ID,番号+1),商品.商品ID,行.数量,小計));
            合計+=小計;
        }
        取引.注文.Add(new Tables.注文(new(注文ID),顧客ID,注文日時,合計));
        return 注文結果.成功(注文ID,合計);
    }

    /// <summary>入荷。在庫を増やす。</summary>
    public void 入荷(int 商品ID,int 数量){
        using var 取引=this.BeginTransaction();
        var 商品=取引.商品[new(商品ID)];
        取引.商品[new(商品ID)]=new Tables.商品(商品.Key,商品.名前,商品.単価,商品.在庫数+数量);
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
            取引.商品.Add(商品);
        }
        foreach(var 顧客 in 初期データ.顧客一覧){
            取引.顧客.Add(顧客);
        }
        取引.Commit();
        return true;
    }

    private long 次の注文IDを見る(TypedTransaction 取引){
        if(this.最後の注文ID<0){
            // 開き直した直後だけ、保存済みの最大値から続きを決める。
            this.最後の注文ID=this.保存済みの最大注文ID(取引);
        }
        return this.最後の注文ID+1;
    }

    // 保存済みの最大の注文ID。FLinq はこの本体を呼び出し側(次の注文IDを見る)へ取り込んで展開する。
    public long 保存済みの最大注文ID(TypedTransaction 取引)=>取引.注文.Select(行=>行.注文ID).DefaultIfEmpty(0).Max();
}
