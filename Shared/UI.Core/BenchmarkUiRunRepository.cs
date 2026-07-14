using Seamless.UI.Storage;

using System.Text.Json;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class ベンチマークUI実行リポジトリ{
    private readonly UI実行ストア ストア;
    private readonly object キュー排他=new();
    private Task キュー末尾=Task.CompletedTask;
    private readonly System.Collections.Generic.HashSet<long>破棄済み実行ID一覧=[];
    private readonly Action<string,Exception?>ログ;

    public ベンチマークUI実行リポジトリ(UI実行ストア ストア,Action<string,Exception?>?ログ=null){
        this.ストア=ストア;
        this.ログ=ログ??((_,_)=>{
        });
    }

    public async Task<long?>実行を作成Async(
        UI実行アプリ種別 アプリ種別,
        string 表示名,
        string 実行プロファイル,
        ベンチマークソースタブ ソースタブ){
        try{
            return await this.永続化を実行Async(()=>{
                this.ストア.スキーマを確認();
                var 実行=this.ストア.実行を作成(new(
                    アプリ種別,
                    表示名,
                    実行プロファイル,
                    ソースタブ.ToString(),
                    DateTimeOffset.UtcNow));
                return (long?)実行.実行ID;
            });
        }catch(Exception 例外){
            this.ログ("UI DBの実行作成に失敗しました。",例外);
            return null;
        }
    }

    /// <summary>実行状態を直列保存キューへ投入し、UIを待機させずに戻る。</summary>
    public void 実行状態設定を予約(long? 実行ID,UI実行状態 状態,string メッセージ){
        if(!this.書込可能実行IDを取得する(実行ID,out var 値)){
            return;
        }

        this.永続化を予約(
            ()=>{
                if(this.実行を破棄済みか(値)){
                    return;
                }
                this.ストア.実行状態を設定(値,状態,メッセージ);
            },
            "UI DBの実行状態更新に失敗しました。");
    }

    public void 実行設定保存を予約(
        long? 実行ID,
        string 実行ルート,
        IReadOnlyList<int>要素数一覧,
        TimeSpan 適応閾値,
        IReadOnlyList<int>論理CPU一覧){
        if(!this.書込可能実行IDを取得する(実行ID,out var 値)){
            return;
        }

        var 適応閾値ナノ秒=適応閾値.TotalMilliseconds*1_000_000;
        this.永続化を予約(
            ()=>{
                if(this.実行を破棄済みか(値)){
                    return;
                }
                this.ストア.実行設定を保存(値,実行ルート,要素数一覧,適応閾値ナノ秒,論理CPU一覧);
            },
            "UI DBのベンチマーク実行設定保存に失敗しました。");
    }

    public void 結果診断保存を予約(ベンチマーク結果項目 結果,long? 実行ID){
        if(!this.書込可能実行IDを取得する(実行ID,out var 値)
            ||!string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)
            ||結果.診断カウンター一覧.Count==0){
            return;
        }

        var 書込一覧=結果.診断カウンター一覧.Select(カウンター=>new UIフォールバック診断書込(
            結果.プロジェクト,
            結果.メソッド,
            カウンター.カウンター名,
            カウンター.カウンター値){
            タブ名=ベンチマークソースタブ一覧.取得(結果.ソースタブ).ID,
            引数キー=結果.引数キー,
            要素数=結果.要素数,
        }).ToArray();
        this.永続化を予約(
            ()=>{
                if(this.実行を破棄済みか(値)){
                    return;
                }
                this.ストア.フォールバック診断を保存(値,書込一覧,DateTimeOffset.UtcNow);
            },
            "UI DBのベンチマーク診断保存に失敗しました。");
    }

    public void ベンチマークスナップショット項目保存を予約(
        long? 実行ID,
        IReadOnlyList<ベンチマークグリッドスナップショット項目>項目一覧){
        if(!this.書込可能実行IDを取得する(実行ID,out var 値)){
            return;
        }

        var 型名なし項目=項目一覧.FirstOrDefault(static 項目=>string.IsNullOrWhiteSpace(項目.型名));
        if(型名なし項目 is not null){
            throw new InvalidDataException(
                "ベンチマークスナップショットの型名が空です。"
                +" 実行ID="+値
                +" ソースタブ="+型名なし項目.ソースタブID
                +" ベンチマーク="+型名なし項目.ベンチマーク名+"。");
        }

        var 書込一覧=項目一覧.Select(static 項目=>new UIベンチマークスナップショット項目書込(
            項目.ソースタブID,
            項目.型名,
            項目.ベンチマーク名,
            項目.CPU番号,
            項目.選択中か,
            側書込を作成(項目.左),
            側書込を作成(項目.右),
            ケース書込を作成(項目.左.ベンチマーク),
            ケース書込を作成(項目.右.ベンチマーク))).ToArray();
        this.永続化を予約(
            ()=>{
                if(this.実行を破棄済みか(値)){
                    return;
                }
                this.ストア.ベンチマークスナップショット項目を保存(値,書込一覧);
            },
            "UI DBのベンチマークスナップショット保存に失敗しました。");
    }

    public void 実行削除を予約(long? 実行ID){
        if(実行ID is not { } 値){
            return;
        }

        lock(this.キュー排他){
            this.破棄済み実行ID一覧.Add(値);
        }
        this.永続化を予約(
            ()=>this.ストア.実行を削除(値),
            "UI DBの実行削除に失敗しました。");
    }

    public IReadOnlyList<ベンチマーク結果項目>結果項目一覧を読み込む(
        UI実行アプリ種別 アプリ種別,
        ベンチマークソースタブ定義 ソースタブ)=>
        this.ストア.実行一覧を取得(アプリ種別)
            .Where(実行=>string.Equals(実行.タブ名,ソースタブ.タブ.ToString(),StringComparison.OrdinalIgnoreCase))
            .SelectMany(実行=>this.結果項目一覧を読み込む(実行.実行ID,ソースタブ))
            .ToArray();

    public IReadOnlyList<ベンチマーク結果項目>結果項目一覧を読み込む(
        long 実行ID,
        ベンチマークソースタブ定義?ソースタブ=null){
        var スナップショット一覧=this.ベンチマークスナップショット項目一覧を読み込む(実行ID);
        var 項目一覧=new List<ベンチマーク結果項目>();
        foreach(var スナップショット in スナップショット一覧){
            if(ソースタブ is not null
                &&!string.Equals(スナップショット.ソースタブID,ソースタブ.ID,StringComparison.OrdinalIgnoreCase)){
                continue;
            }

            項目一覧.Add(スナップショット.左.結果項目);
            項目一覧.Add(スナップショット.右.結果項目);
        }

        return 項目一覧;
    }

    public ベンチマーク保存結果?前回結果を検索(
        UI実行アプリ種別 アプリ種別,
        ベンチマークソースタブ定義 ソースタブ,
        string メソッド,
        string プロジェクト,
        string 型完全名,
        int 要素数,
        DateTimeOffset?基準日時,
        string 引数キー,
        long? 今回実行ID=null){
        if(アプリ種別==UI実行アプリ種別.スケーラブルベンチマーク&&今回実行ID is { } 実行ID){
            if(this.ストア.前回実行IDを取得(実行ID) is not { } 前回実行ID){
                return null;
            }
            var 型名=スケーラブルベンチマーク名フォーマッター.ジェネリック型表示を作成(型完全名);
            return this.ベンチマークスナップショット項目一覧を読み込む(前回実行ID)
                .Where(項目=>
                    string.Equals(項目.ソースタブID,ソースタブ.ID,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(項目.型名,型名,StringComparison.OrdinalIgnoreCase)
                    &&string.Equals(項目.ベンチマーク名,メソッド,StringComparison.OrdinalIgnoreCase)
                    &&ベンチマーク引数キー一致ロジック.厳密一致か(項目.左.ベンチマーク?.引数キー??string.Empty,引数キー)
                    &&項目.左.結果項目.軸数==要素数
                    &&string.Equals(項目.左.結果項目.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)
                    &&項目.左.結果項目.平均ナノ秒 is not null)
                .Select(項目=>保存結果に変換(項目.左.結果項目))
                .FirstOrDefault();
        }

        return this.結果項目一覧を読み込む(アプリ種別,ソースタブ)
            .Where(結果=>string.Equals(結果.メソッド,メソッド,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(結果.プロジェクト,プロジェクト,StringComparison.OrdinalIgnoreCase)
                &&同じ型か(結果.型完全名,型完全名)
                &&ベンチマーク引数キー一致ロジック.厳密一致か(結果.引数キー,引数キー)
                &&結果.軸数==要素数
                &&string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)
                &&結果.平均ナノ秒 is not null
                &&結果.開始日時 is { } 測定日時
                &&(基準日時 is null||測定日時<基準日時.Value))
            .OrderByDescending(static 結果=>結果.開始日時)
            .Select(保存結果に変換)
            .FirstOrDefault();
    }

    public IReadOnlyList<ベンチマークグリッドスナップショット項目>ベンチマークスナップショット項目一覧を読み込む(long 実行ID){
        var 記録一覧=this.ストア.ベンチマークスナップショット項目一覧を取得(実行ID);
        const int 軸数=ベンチマーク要素数一覧.基本要素数;
        var 項目一覧=new List<ベンチマークグリッドスナップショット項目>(記録一覧.Count);
        for(var インデックス=0;インデックス<記録一覧.Count;インデックス++){
            var 記録=記録一覧[インデックス];
            var ソースタブ=ベンチマークソースタブ一覧.IDから取得を試みる(記録.タブ名);
            if(ソースタブ is null){
                this.ログ("UI DBのベンチマークスナップショットに不明なソースタブがあります: "+記録.タブ名,null);
                continue;
            }

            項目一覧.Add(new(
                記録.実行ID,
                インデックス,
                記録.タブ名,
                記録.型名,
                記録.ベンチマーク名,
                記録.CPU番号,
                記録.選択中か,
                スナップショット側を作成(記録,ソースタブ.左プロジェクト,ソースタブ.左アセンブリ名,記録.左,記録.左ケース,軸数),
                スナップショット側を作成(記録,ソースタブ.右プロジェクト,ソースタブ.右アセンブリ名,記録.右,記録.右ケース,軸数)));
        }

        return 項目一覧;
    }

    /// <summary>予約済みの保存がすべて完了するまで待つ。</summary>
    public Task 保存完了を待機Async(){
        lock(this.キュー排他){
            return this.キュー末尾;
        }
    }

    private static ベンチマーク保存結果 保存結果に変換(ベンチマーク結果項目 結果)=>new(
        結果.プロジェクト,
        結果.メソッド,
        結果.型完全名,
        結果.要素数,
        結果.ループ回数,
        結果.状態,
        結果.平均,
        結果.平均ナノ秒,
        結果.割り当てバイト数値,
        結果.開始日時??DateTimeOffset.MinValue,
        null,
        結果.成果物フォルダー){
        引数キー=結果.引数キー,
        引数一覧=結果.引数一覧,
        割り当て論理CPU=結果.割り当て論理CPU,
        合計ナノ秒=結果.合計ナノ秒,
        実行内測定か=結果.実行内測定か,
    };

    private bool 書込可能実行IDを取得する(long? 実行ID,out long 値){
        if(実行ID is not { } 解決済みID){
            値=0;
            return false;
        }
        値=解決済みID;
        return !this.実行を破棄済みか(解決済みID);
    }

    private bool 実行を破棄済みか(long 実行ID){
        lock(this.キュー排他){
            return this.破棄済み実行ID一覧.Contains(実行ID);
        }
    }

    private static UIベンチマークグリッド側書込 側書込を作成(ベンチマークグリッドスナップショット側 側)=>new(
        側.状態,
        側.失敗理由,
        側.計測日時,
        側.平均ナノ秒,
        側.割り当てバイト数,
        側.結果項目.ループ回数,
        側.結果項目.合計ナノ秒,
        側.結果項目.実行内測定か,
        状態保存値に変換(側.状態));

    private static UIベンチマークスナップショットケース書込?ケース書込を作成(ベンチマークケース?ベンチマーク){
        if(ベンチマーク is null){
            return null;
        }

        return new(
            ベンチマーク.アセンブリ名,
            ベンチマーク.型完全名,
            ベンチマーク.メソッド名,
            ベンチマーク.引数キー,
            JsonSerializer.Serialize(ベンチマーク.引数一覧));
    }

    private static ベンチマークグリッドスナップショット側 スナップショット側を作成(
        UIベンチマークスナップショット項目記録 項目,
        string プロジェクト,
        string アセンブリ名,
        UIベンチマークグリッド側記録 側,
        UIベンチマークスナップショットケース書込?識別情報,
        int 軸数){
        var ベンチマーク=ベンチマークケースを作成(プロジェクト,アセンブリ名,項目.ベンチマーク名,識別情報);
        var 結果項目=結果項目を作成(項目,プロジェクト,側,ベンチマーク,ベンチマーク?.Count引数 is >0 and var Count ? Count : 軸数);
        return new(
            側.状態,
            側.失敗理由,
            側.計測日時,
            側.平均ナノ秒,
            側.割当バイト,
            結果項目,
            ベンチマーク);
    }

    private static ベンチマークケース?ベンチマークケースを作成(
        string プロジェクト,
        string アセンブリ名,
        string ベンチマーク名,
        UIベンチマークスナップショットケース書込?識別情報){
        try{
            var 引数一覧=識別情報 is null
                ? []
                : JsonSerializer.Deserialize<ベンチマーク引数[]>(識別情報.引数JSON)??[];
            return new(
                プロジェクト,
                string.IsNullOrWhiteSpace(識別情報?.アセンブリ名) ? アセンブリ名 : 識別情報.アセンブリ名,
                識別情報?.型完全名??string.Empty,
                string.IsNullOrWhiteSpace(識別情報?.メソッド名) ? ベンチマーク名 : 識別情報.メソッド名,
                識別情報?.引数キー??string.Empty,
                引数一覧);
        }catch(JsonException){
            return null;
        }
    }

    private static ベンチマーク結果項目 結果項目を作成(
        UIベンチマークスナップショット項目記録 項目,
        string プロジェクト,
        UIベンチマークグリッド側記録 側,
        ベンチマークケース?ベンチマーク,
        int 軸数){
        var ソースタブ=ベンチマークソースタブ一覧.IDから取得を試みる(項目.タブ名)?.タブ??ベンチマークソースタブ.System配列;
        var ループ回数=側.ループ回数??ベンチマーク作業軸.ループ回数を解決(ソースタブ,軸数);
        return new ベンチマーク結果項目(new ベンチマーク保存結果(
            プロジェクト,
            ベンチマーク?.メソッド名??項目.ベンチマーク名,
            string.IsNullOrWhiteSpace(ベンチマーク?.型完全名) ? 項目.型名 : ベンチマーク.型完全名,
            ベンチマーク作業軸.要素数を解決(ソースタブ,軸数),
            ループ回数,
            状態保存値に変換(側.状態),
            側.平均ナノ秒?.ToString(System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            側.平均ナノ秒,
            側.割当バイト,
            側.計測日時??DateTimeOffset.MinValue,
            null,
            string.Empty){
            ソースタブID=項目.タブ名,
            引数キー=ベンチマーク?.引数キー??string.Empty,
            引数一覧=ベンチマーク?.引数一覧??[],
            割り当て論理CPU=項目.CPU番号,
            合計ナノ秒=側.合計ナノ秒??(側.平均ナノ秒 is { } 平均 ? 平均*ループ回数 : null),
            実行内測定か=側.実行内計測か,
        });
    }

    private void 永続化を予約(Action 処理,string 失敗メッセージ){
        lock(this.キュー排他){
            this.キュー末尾=this.キュー末尾.ContinueWith(
                _=>{
                    var ストップウォッチ=System.Diagnostics.Stopwatch.StartNew();
                    try{
                        処理();
                        if(ストップウォッチ.Elapsed.TotalMilliseconds>100){
                            System.Diagnostics.Debug.WriteLine($"[DB] 低速処理完了 ({ストップウォッチ.Elapsed.TotalMilliseconds:0.0}ms): {失敗メッセージ}");
                        }
                    }catch(Exception 例外){
                        System.Diagnostics.Debug.WriteLine($"[DB] 失敗 ({ストップウォッチ.Elapsed.TotalMilliseconds:0.0}ms): {失敗メッセージ}: {例外}");
                        this.ログ(失敗メッセージ,例外);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    private Task<T結果>永続化を実行Async<T結果>(Func<T結果> 処理){
        lock(this.キュー排他){
            var タスク=this.キュー末尾.ContinueWith(
                _=>処理(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            this.キュー末尾=タスク;
            return タスク;
        }
    }

    private static string 状態保存値に変換(UI実行状態 状態)=>状態 switch{
        UI実行状態.待機中=>"Queued",
        UI実行状態.実行中=>"Running",
        UI実行状態.一時停止中=>"Paused",
        UI実行状態.停止済み=>"Stopped",
        UI実行状態.完了=>"Completed",
        UI実行状態.失敗=>"Failed",
        _=>throw new ArgumentOutOfRangeException(nameof(状態),状態,null),
    };

    private static bool 同じ型か(string 左,string 右)=>
        string.IsNullOrWhiteSpace(左)
        ||string.IsNullOrWhiteSpace(右)
        ||string.Equals(左,右,StringComparison.OrdinalIgnoreCase);
}

internal sealed record ベンチマークグリッドスナップショット側(
    UI実行状態 状態,
    string 失敗理由,
    DateTimeOffset?計測日時,
    double?平均ナノ秒,
    long?割り当てバイト数,
    ベンチマーク結果項目 結果項目,
    ベンチマークケース?ベンチマーク=null);

internal sealed record ベンチマークグリッドスナップショット項目(
    long 実行ID,
    int 順序,
    string ソースタブID,
    string 型名,
    string ベンチマーク名,
    int?CPU番号,
    bool 選択中か,
    ベンチマークグリッドスナップショット側 左,
    ベンチマークグリッドスナップショット側 右);
