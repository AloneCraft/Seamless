using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Seamless.UI.Storage;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace UI.ViewModels;

// 比較行・シェア・改善候補の構築
public sealed partial class 実行ビューモデル:ObservableObject {
    /// <summary>
    /// 改善プロンプト文字列値 フィールドです。
    /// </summary>
    private string 改善プロンプト文字列値 = string.Empty;

    /// <summary>
    /// 改善候補概要文字列値 フィールドです。
    /// </summary>
    private string 改善候補概要文字列値 = string.Empty;

    private string 全体ベンチマークシェア概要文値 = ベンチマーク改善プロンプトビルダー.概要を作成([]).全体表示文字列;

    private string 全体ベンチマークシェア概要短文 = ベンチマーク改善プロンプトビルダー.概要を作成([]).短縮表示文字列;

    /// <summary>
    /// 比較行一覧値 プロパティです。
    /// </summary>
    /// <value>比較行一覧値 の値を取得/設定します。</value>
    private IReadOnlyList<ベンチマーク比較行> 比較行一覧値 {
        get => this.選択ソースタブ表示状態.比較行一覧;
        set => this.選択ソースタブ表示状態.比較行一覧=value;
    }

    /// <summary>
    /// 改善候補選択状態 フィールドです。
    /// </summary>
    private readonly Dictionary<string,bool> 改善候補選択状態 = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// 改善候補選択読込済みか フィールドです。
    /// </summary>
    private bool 改善候補選択読込済みか;

    /// <summary>
    /// 改善候補選択適用中か フィールドです。
    /// </summary>
    private bool 改善候補選択適用中か;

    /// <summary>
    /// 表示結果項目一覧 プロパティです。
    /// </summary>
    /// <value>表示結果項目一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク結果項目> 表示結果項目一覧 { get; } = [];

    /// <summary>
    /// 改善候補一覧 プロパティです。
    /// </summary>
    /// <value>改善候補一覧 の値を取得/設定します。</value>
    public ObservableCollection<ベンチマーク改善候補行> 改善候補一覧 { get; } = [];

    /// <summary>
    /// ベンチマークシェア概要文プロパティです。
    /// </summary>
    /// <value>ベンチマークシェア概要文の値を取得/設定します。</value>
    public string ベンチマークシェア概要文 {
        get => this.選択ソースタブ表示状態.ベンチマークシェア概要文;
        /// <summary>
        /// set プロパティを取得/設定します。
        /// </summary>
        /// <value>set の値です。</value>
        private set {
            if(this.選択ソースタブ表示状態.ベンチマークシェア概要文!=value) {
                this.選択ソースタブ表示状態.ベンチマークシェア概要文=value;
                this.OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 改善プロンプト文字列 プロパティです。
    /// </summary>
    /// <value>改善プロンプト文字列 の値を取得/設定します。</value>
    public string 改善プロンプト文字列 {
        get => this.改善プロンプト文字列値;
        set {
            if(this.SetProperty(ref this.改善プロンプト文字列値,value)) {
                this.OnPropertyChanged(nameof(this.改善プロンプトをコピー可能か));
                this.改善プロンプトコピーコマンド.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// 改善候補概要文字列 プロパティです。
    /// </summary>
    /// <value>改善候補概要文字列 の値を取得/設定します。</value>
    public string 改善候補概要文字列 {
        get => this.改善候補概要文字列値;
        private set => this.SetProperty(ref this.改善候補概要文字列値,value);
    }

    /// <summary>
    /// 改善プロンプトをコピー可能か プロパティです。
    /// </summary>
    /// <value>改善プロンプトをコピー可能か の値を取得/設定します。</value>
    public bool 改善プロンプトをコピー可能か => !string.IsNullOrWhiteSpace(this.改善プロンプト文字列);

    /// <summary>
    /// 改善プロンプト下段タブ表示中か プロパティです。
    /// </summary>
    /// <value>改善プロンプト下段タブ表示中か の値を取得/設定します。</value>
    public bool 改善プロンプト下段タブ表示中か => this.選択下段タブ==ベンチマーク下段タブ.改善プロンプト;

    public string 全体ベンチマークシェア概要文 {
        get => this.全体ベンチマークシェア概要文値;
        private set {
            if(this.SetProperty(ref this.全体ベンチマークシェア概要文値,value)) {
                this.OnPropertyChanged(nameof(this.全体ソース進行概要文字列));
            }
        }
    }

    /// <summary>
    /// 表示結果から比較行を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 表示結果から比較行を更新() {
        var 選択値要素数 = this.選択結果要素数?.値;
        if(this.表示に今回実行メモリを使うか(this.選択ソース定義)) {
            this.比較行一覧値=選択値要素数 is { } 現在要素数
                ? this.要素数の現在実行比較行を構築(現在要素数)
                : this.現在実行比較行を構築();
            return;
        }

        if(this.結果項目一覧.Count>0) {
            this.比較行一覧値=選択値要素数 is { } 表示済み要素数
                ? this.要素数の表示行を構築(this.選択ソース定義,表示済み要素数)
                : this.表示行を構築(this.選択ソース定義);
            return;
        }

        this.比較行一覧値= [];
    }

    /// <summary>
    /// 比較行要求トークン フィールドです。
    /// </summary>
    private int 比較行要求トークン;

    /// <summary>
    /// 比較行更新中か フィールドです。
    /// </summary>
    private bool 比較行更新中か;

    /// <summary>
    /// 比較行更新保留中か フィールドです。
    /// </summary>
    private bool 比較行更新保留中か;

    /// <summary>
    /// 比較行タブ入力 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    /// <param name="現在実行結果一覧">現在実行結果一覧 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private readonly record struct 比較行タブ入力(
        ベンチマークソースタブ表示状態 状態,
        IReadOnlyList<ベンチマーク結果項目> 現在実行結果一覧);

    // 全タブ分の比較行再構築は結果項目一覧全体のLINQ走査とDBの前回結果参照を伴う。
    // を伴い重い。現在実行セッション/ResultItemsに触れる部分だけをUIスレッドで同期的にスナップショット
    // し、残りの純粋計算(フィルタ・整形・DB参照)はバックグラウンドスレッドで行い、
    // state.比較行一覧(ObservableCollectionではない単純プロパティ)への代入だけをUIスレッドに戻す。
    /// <summary>
    /// 現 在 の 状 態 を 再 計 算 し て 内 容 を 最 新 化 し ま す 。
    /// 処 理 を 実 行 し 、 関 連 す る 状 態 や 表 示 を 更 新 し ま す 。
    /// </summary>
    private void 全比較行を表示結果から更新() {
        if(this.比較行更新中か) {
            this.比較行更新保留中か=true;
            return;
        }

        var 選択値要素数 = this.選択結果要素数?.値;
        var タブ入力一覧 = this.ソースタブ表示状態一覧値
            .Select(状態 => new 比較行タブ入力(状態,this.現在実行結果項目一覧を取得(状態.定義.タブ)))
            .ToArray();
        var 結果項目一覧スナップショット = this.結果項目一覧.ToArray();
        var トークン = ++this.比較行要求トークン;
        var 世代 = this.実行結果世代;
        this.比較行更新中か=true;
        var ストップウォッチ = Stopwatch.StartNew();
        Task.Run(() => タブ入力一覧
                .Select(入力 => this.タブの比較行を計算(入力,結果項目一覧スナップショット,選択値要素数))
                .ToArray())
            .ContinueWith(タスク => {
                UIスレッド予算.記録("bg:comparison",ストップウォッチ.Elapsed.TotalMilliseconds);
                this.UIディスパッチャー.呼び出し開始(() => {
                    this.比較行更新中か=false;
                    if(トークン==this.比較行要求トークン&&世代==this.実行結果世代&&!タスク.IsFaulted) {
                        var 計算結果 = タスク.Result;
                        UIスレッド予算.計測("comparison-apply",() => {
                            for(var インデックス = 0;インデックス<タブ入力一覧.Length;インデックス++) {
                                タブ入力一覧[インデックス].状態.比較行一覧=計算結果[インデックス];
                                this.ベンチマークシェア概要を更新(タブ入力一覧[インデックス].状態);
                            }
                            // 新しいComparisonRowsを行/スキップセル/完了状態へ反映する。もう次のtickが
                            // 来ない（停止中実行の閲覧時など）場合でもここで確定させないと表示が更新されないまま残る。
                            this.比較行を全表へ適用();
                            if(選択値要素数 is { } スキップ済み要素数) {
                                this.スキップセルを適用(スキップ済み要素数);
                            }
                            this.最終完了状態を更新();
                        });
                    } else if(タスク.IsFaulted) {
                        起動診断.書き込む("バックグラウンドでの表示結果からの全比較行更新に失敗しました。",タスク.Exception);
                    }

                    if(this.比較行更新保留中か) {
                        this.比較行更新保留中か=false;
                        this.全比較行を表示結果から更新();
                    }
                });
            },TaskScheduler.Default);
    }

    /// <summary>
    /// タブの比較行を計算 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="入力">入力 を指定して実行条件を制御します。</param>
    /// <param name="結果項目一覧スナップショット">結果項目一覧スナップショット を指定して実行条件を制御します。</param>
    /// <param name="選択値要素数">選択値要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> タブの比較行を計算(
        比較行タブ入力 入力,
        IReadOnlyList<ベンチマーク結果項目> 結果項目一覧スナップショット,
        int? 選択値要素数) {
        if(入力.現在実行結果一覧.Count>0&&!保存DB実行選択肢か(this.選択実行選択肢)) {
            return 選択値要素数 is { } 現在要素数
                ? this.実行投影サービス.要素数の比較行を構築(入力.現在実行結果一覧,入力.状態.定義,現在要素数,this.比較用報告行を作成)
                : this.実行投影サービス.最新比較行を構築(入力.現在実行結果一覧,入力.状態.定義,this.比較用報告行を作成);
        }

        if(結果項目一覧スナップショット.Count>0) {
            return this.実行投影サービス.選択要素数の表示行を構築(結果項目一覧スナップショット,入力.状態.定義,選択値要素数,this.比較用報告行を作成);
        }

        return [];
    }

    /// <summary>
    /// 表示結果を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 表示結果を更新() {
        this.表示結果項目一覧.Clear();
        var 選択値要素数 = this.選択結果要素数?.値;
        foreach(var 項目 in this.実行投影サービス.表示結果を取得(this.結果項目一覧.Where(this.結果が表示選択中か),選択値要素数)) {
            this.表示結果項目一覧.Add(項目);
        }
    }

    /// <summary>
    /// 表示結果があるか プロパティです。
    /// </summary>
    /// <value>表示結果があるか の値を取得/設定します。</value>
    private bool 表示結果があるか =>
        this.結果項目一覧.Count>0
        ||this.選択タブに現在実行結果があるか
        ||this.現在実行セッション.結果ありか;

    private void 表示結果を現在実行セッションへ昇格() {
        foreach(var 結果 in this.結果項目一覧) {
            this.現在実行結果項目を追加または更新(結果);
        }
    }

    /// <summary>
    /// 比較行を表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 比較行を表へ適用() {
        this.比較行を表へ適用(this.選択ソースタブ表示状態);
    }

    /// <summary>
    /// 比較行を全表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 比較行を全表へ適用() {
        foreach(var 状態 in this.ソースタブ表示状態一覧値) {
            this.比較行を表へ適用(状態);
        }
    }

    /// <summary>
    /// 比較行を表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    private void 比較行を表へ適用(ベンチマークソースタブ表示状態 状態) {
        var 現在実行結果ありか = this.現在実行セッション.ソースタブ結果があるか(状態.定義.タブ);
        foreach(var ベンチマーク in 状態.行一覧) {
            var 比較行 = this.ベンチマーク実行有効か||現在実行結果ありか
                ? this.今回実行比較行を検索(状態,ベンチマーク)
                : this.比較行を検索(状態,ベンチマーク);
            ベンチマーク.比較を適用(比較行,this.ベンチマーク実行有効か||現在実行結果ありか);
        }
        this.ベンチマークシェア概要を更新(状態);
    }

    /// <summary>
    /// 選択表示結果を表へ適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 選択表示結果を表へ適用() {
        var 選択値要素数 = this.選択結果要素数?.値;
        var ソース = this.表適用用結果ソースを取得();
        var 結果一覧 = 選択値要素数 is null
            ? ソース
            : ソース.Where(結果 => 結果.軸数==選択値要素数).ToArray();
        if(結果一覧.Count==0&&ソース.Count>0) {
            結果一覧=ソース;
        }
        foreach(var 結果 in 結果一覧) {
            var 行 = this.ベンチマーク行を検索(結果);
            行?.実行結果を適用(結果,this.割当CPUを検索(結果),結果.開始日時??DateTimeOffset.Now,false);
        }
    }

    /// <summary>
    /// 比較行を検索 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク比較行? 比較行を検索(ベンチマーク表行 行) {
        return this.比較行を検索(this.選択ソースタブ表示状態,行);
    }

    /// <summary>
    /// 比較行を検索 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private ベンチマーク比較行? 今回実行比較行を検索(ベンチマークソースタブ表示状態 状態,ベンチマーク表行 行) {
        if(!行.選択中か) {
            return null;
        }

        foreach(var 項目 in 状態.比較行一覧) {
            if(!string.IsNullOrWhiteSpace(項目.型完全名)&&比較行が一致するか(行,項目)) {
                return 項目;
            }
        }

        return null;
    }

    private ベンチマーク比較行? 比較行を検索(ベンチマークソースタブ表示状態 状態,ベンチマーク表行 行) {
        foreach(var 項目 in 状態.比較行一覧) {
            if(比較行が一致するか(行,項目)) {
                return 項目;
            }
        }

        return null;
    }

    /// <summary>
    /// ベンチマークシェア概要を更新します。
    /// </summary>
    private void ベンチマークシェア概要を更新() {
        this.ベンチマークシェア概要を更新(this.選択ソースタブ表示状態);
    }

    /// <summary>
    /// ベンチマークシェア概要を更新します。
    /// </summary>
    /// <param name="状態">状態 を指定して実行条件を制御します。</param>
    private void ベンチマークシェア概要を更新(ベンチマークソースタブ表示状態 状態) {
        var 測定済み行一覧 = this.測定実行比較行を構築(状態.定義);
        var 概要 = ベンチマーク改善プロンプトビルダー.概要を作成(測定済み行一覧);
        状態.ベンチマークシェア概要文=概要.表示文字列;
        this.ソースタブ選択肢を取得(状態.定義.タブ).ベンチマークシェア概要短文を設定(概要.短縮表示文字列);
        this.全体ベンチマークシェア概要を更新();
        if(状態==this.選択ソースタブ表示状態) {
            this.OnPropertyChanged(nameof(this.ベンチマークシェア概要文));
        }
    }

    private IReadOnlyList<ベンチマーク比較行> 測定実行比較行を構築(ベンチマークソースタブ定義 ソースタブ) {
        var 測定済み = this.表示用結果ソースを取得()
            .Where(結果 => 結果.実行内測定か&&ソースタブ.プロジェクトを含むか(結果.プロジェクト))
            .ToArray();
        return 測定済み.Length==0
            ? []
            : this.実行投影サービス.最新比較行を構築(測定済み,ソースタブ,this.比較用報告行を作成);
    }

    private void 全体ベンチマークシェア概要を更新() {
        var 行一覧 = this.ソースタブ表示状態一覧値
            .SelectMany(状態 => this.測定実行比較行を構築(状態.定義))
            .ToArray();
        var 概要 = ベンチマーク改善プロンプトビルダー.概要を作成(行一覧);
        this.全体ベンチマークシェア概要文=概要.全体表示文字列;
        if(this.全体ベンチマークシェア概要短文!=概要.短縮表示文字列) {
            this.全体ベンチマークシェア概要短文=概要.短縮表示文字列;
            this.OnPropertyChanged(nameof(this.全体ソース進行概要文字列));
        }
    }

    /// <summary>
    /// 改善プロンプトを更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 改善プロンプトを更新() {
        var 候補一覧 = this.比較行から改善候補を構築();
        if(候補一覧.Count==0) {
            候補一覧=ベンチマーク改善候補ビルダー.候補を構築(this.改善プロンプト用進行状態一覧を取得());
        }
        this.改善候補を適用(候補一覧);
        this.改善プロンプト文字列を更新();
    }

    private IReadOnlyList<ベンチマーク改善候補> 比較行から改善候補を構築() {
        var 行一覧 = this.ソースタブ表示状態一覧値
            .SelectMany(static 状態 => 状態.比較行一覧)
            .ToArray();
        if(行一覧.Length==0) {
            return [];
        }

        var 要素数 = this.選択結果要素数?.値??0;
        return ベンチマーク改善候補ビルダー.比較行から候補を構築(行一覧,要素数);
    }

    /// <summary>
    /// 改善候補を適用 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="候補一覧">候補一覧 を指定して実行条件を制御します。</param>
    private void 改善候補を適用(IReadOnlyList<ベンチマーク改善候補> 候補一覧) {
        this.現在実行の改善候補選択を読み込む();
        var 保存選択ありか = this.改善候補選択読込済みか&&this.改善候補選択状態.Count>0;
        var 既定選択値 = 保存選択ありか
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : ベンチマーク改善候補ビルダー.既定選択キーを作成(候補一覧);
        foreach(var 行 in this.改善候補一覧) {
            行.選択変更時-=this.改善候補選択変更時;
        }
        this.改善候補一覧.Clear();
        this.改善候補選択適用中か=true;
        try {
            foreach(var 候補 in 候補一覧) {
                var 選択中か = 保存選択ありか
                    ? this.改善候補選択状態.TryGetValue(候補.キー,out var 値か)&&値か
                    : 既定選択値.Contains(候補.キー);
                this.改善候補選択状態[候補.キー]=選択中か;
                var 行 = new ベンチマーク改善候補行(候補,選択中か);
                行.選択変更時+=this.改善候補選択変更時;
                this.改善候補一覧.Add(行);
            }
        } finally {
            this.改善候補選択適用中か=false;
        }
        this.改善候補概要文字列=string.Empty;
    }

    /// <summary>
    /// 改善候補選択変更時 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    /// <param name="送信元">送信元 を指定して実行条件を制御します。</param>
    /// <param name="引数">引数 を指定して実行条件を制御します。</param>
    private void 改善候補選択変更時(object? 送信元,EventArgs 引数) {
        if(送信元 is not ベンチマーク改善候補行 行) {
            return;
        }

        this.改善候補選択状態[行.キー]=行.選択中か;
        if(!this.改善候補選択適用中か) {
            this.改善プロンプト文字列を更新();
            this.改善候補概要文字列=string.Empty;
        }
    }

    /// <summary>
    /// 改善プロンプト文字列を更新 メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 改善プロンプト文字列を更新() {
        this.改善プロンプト文字列=ベンチマーク改善プロンプトビルダー.プロンプトを構築(
            this.改善候補一覧
                .Where(static 行 => 行.選択中か)
                .Select(static 行 => 行.候補));
    }

    /// <summary>
    /// 現在実行の改善候補選択を読み込む メソッドを実行し、副作用として内部状態を更新または描画反映します。
    /// </summary>
    private void 現在実行の改善候補選択を読み込む() {
        var 実行ID = this.現在改善選択実行IDを取得();
        if(this.読込済み改善選択実行ID==実行ID) {
            return;
        }

        this.読込済み改善選択実行ID=実行ID;
        this.改善候補選択状態.Clear();
        this.改善候補選択読込済みか=false;
    }

    /// <summary>
    /// 改善プロンプトをコピーAsync メソッドを実行し、結果を算出して返却します。
    /// </summary>
    private async Task 改善プロンプトをコピーAsync() {
        if(string.IsNullOrWhiteSpace(this.改善プロンプト文字列)) {
            return;
        }

        await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(this.改善プロンプト文字列);
        this.状態文字列="改善プロンプトをコピーしました";
    }

    /// <summary>
    /// 改善プロンプト用進行状態一覧を取得 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<進行状態> 改善プロンプト用進行状態一覧を取得() {
        if(this.現在実行セッション.結果ありか) {
            return this.全タブ結果から進行状態一覧を作成(this.現在未保存実行ルート??string.Empty);
        }

        var 状態 = this.選択実行状態を読み込む();
        return 状態 is null ? [] : [状態];
    }

    /// <summary>
    /// 要素数の現在実行比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の現在実行比較行を構築(int 要素数) {
        return this.要素数の現在実行比較行を構築(this.選択ソース定義,要素数);
    }

    /// <summary>
    /// 要素数の現在実行比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の現在実行比較行を構築(ベンチマークソースタブ定義 ソースタブ,int 要素数) {
        return this.実行投影サービス.要素数の比較行を構築(
            this.現在実行結果項目一覧を取得(ソースタブ.タブ),
            ソースタブ,
            要素数,
            this.比較用報告行を作成);
    }

    /// <summary>
    /// 現在実行比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 現在実行比較行を構築() {
        return this.現在実行比較行を構築(this.選択ソース定義);
    }

    /// <summary>
    /// 現在実行比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 現在実行比較行を構築(ベンチマークソースタブ定義 ソースタブ) {
        return this.実行投影サービス.最新比較行を構築(
            this.現在実行結果項目一覧を取得(ソースタブ.タブ),
            ソースタブ,
            this.比較用報告行を作成);
    }

    /// <summary>
    /// 要素数の比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の比較行を構築(int 要素数) {
        return this.要素数の比較行を構築(this.選択ソース定義,要素数);
    }

    /// <summary>
    /// 要素数の比較行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の比較行を構築(ベンチマークソースタブ定義 ソースタブ,int 要素数) {
        var 現在実行結果一覧 = this.現在実行結果項目一覧を取得(ソースタブ.タブ);
        var ソース結果一覧 = 現在実行結果一覧
            .Where(結果 => 結果.軸数==要素数
                &&結果.軸数>0
                &&ソースタブ.プロジェクトを含むか(結果.プロジェクト)
                &&string.Equals(結果.状態,"Completed",StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var 行一覧 = this.比較用報告行を作成(ソースタブ,ソース結果一覧);
        return 行一覧.Length==0
            ? []
            : ベンチマーク比較フォーマッター.比較行を構築(行一覧,ソースタブ,要素数);
    }

    /// <summary>
    /// 要素数の表示行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <param name="要素数">要素数 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 要素数の表示行を構築(ベンチマークソースタブ定義 ソースタブ,int 要素数) {
        return this.実行投影サービス.選択要素数の表示行を構築(
            this.結果項目一覧.ToArray(),
            ソースタブ,
            要素数,
            this.比較用報告行を作成);
    }

    /// <summary>
    /// 表示行を構築 メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="ソースタブ">ソースタブ を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private IReadOnlyList<ベンチマーク比較行> 表示行を構築(ベンチマークソースタブ定義 ソースタブ) {
        return this.実行投影サービス.表示行を構築(
            this.結果項目一覧,
            ソースタブ,
            this.比較用報告行を作成);
    }

    /// <summary>
    /// 比較行が一致するか メソッドを実行し、結果を算出して返却します。
    /// </summary>
    /// <param name="行">行 を指定して実行条件を制御します。</param>
    /// <param name="項目">項目 を指定して実行条件を制御します。</param>
    /// <returns>処理結果を返却します。</returns>
    private static bool 比較行が一致するか(ベンチマーク表行 行,ベンチマーク比較行 項目) =>
        ベンチマーク比較行一致ロジック.一致か(
            行.メソッド名,
            行.ベンチマーク名,
            行.ソースタブ.タブ,
            行.プロジェクト別ケース.Values.Select(static ベンチマーク => new ベンチマーク比較行一致ケース(ベンチマーク.型完全名,ベンチマーク.引数キー)),
            項目);
}
