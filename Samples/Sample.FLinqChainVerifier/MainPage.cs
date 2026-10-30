using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Sample.FLinqChainVerifier.Controls;
using Sample.FLinqChainVerifier.Core;

namespace Sample.FLinqChainVerifier;

/// <summary>
/// FLinq メソッドチェーン理想展開検証ベンチのメイン画面。
/// 左列=4つの検証段階ナビゲーション、中央列=入力クエリ・FLinq展開コード・手書き理想コード(またはペア合成の推定理想特性)・原因と改善案・再現テストコード、
/// 右列=統計メーター・実行時間/割り当て棒グラフ・クリックで詳細を再表示できる蓄積実行ログ。
/// </summary>
public sealed class MainPage:ContentPage{
    private const int 蓄積ログ最大件数=250;
    private static readonly (string 表示,double 速度)[]速度一覧=[
        ("0.5倍",0.5),
        ("1倍",1.0),
        ("2倍",2.0),
        ("4倍",4.0),
        ("最速",0.0),
    ];
    private static readonly 入力ソース種別[]ソース一覧=[
        入力ソース種別.Array,
        入力ソース種別.List,
        入力ソース種別.IEnumerable,
        入力ソース種別.Range,
        入力ソース種別.Persisted,
        入力ソース種別.Tsavorite,
    ];

    private readonly CancellationTokenSource 取消=new();
    private readonly List<(Border 枠,Label 印)>段階行一覧=[];
    private readonly Dictionary<string,(Label 値,ProgressBar 棒)>メーター一覧=[];
    private readonly Dictionary<string,Label>指標一覧=[];
    private readonly 棒グラフ描画 時間グラフ描画=new();
    private readonly 棒グラフ描画 割当グラフ描画=new();
    private readonly GraphicsView 時間グラフ表示;
    private readonly GraphicsView 割当グラフ表示;

    private readonly Label 状態表示=new(){TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center,FontSize=13};
    private readonly Label 段階見出し=new(){FontSize=21,FontAttributes=FontAttributes.Bold,TextColor=配色.文字};
    private readonly Label 段階副題=new(){FontSize=13,TextColor=配色.アクセント};
    private readonly Label 段階説明=new(){FontSize=13,TextColor=配色.文字,LineHeight=1.25};

    private readonly Label チェーン式見出し=new(){FontFamily="Consolas",FontSize=15,FontAttributes=FontAttributes.Bold,TextColor=配色.文字};
    private readonly Label 判定バッジ=new(){FontSize=13,FontAttributes=FontAttributes.Bold,Padding=new Thickness(10,4),TextColor=Colors.White};
    private readonly Border 判定バッジ枠=new(){StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=6},VerticalOptions=LayoutOptions.Center};
    private readonly FlexLayout ペア遷移並び=new(){Wrap=Microsoft.Maui.Layouts.FlexWrap.Wrap,JustifyContent=Microsoft.Maui.Layouts.FlexJustify.Start};

    private readonly Label 入力クエリ表示=new(){FontFamily="Consolas",FontSize=13,TextColor=配色.コード文字,Padding=new Thickness(12,8)};
    private readonly Label 展開コード表示=new(){FontFamily="Consolas",FontSize=12,TextColor=配色.コード文字,Padding=new Thickness(12,8)};
    private readonly Label 理想見出しラベル=new(){Text="手書き理想コード / 2メソッドペア合成の推定理想特性",FontSize=13,FontAttributes=FontAttributes.Bold,TextColor=配色.薄い文字};
    private readonly Label 理想コード表示=new(){FontFamily="Consolas",FontSize=12,TextColor=配色.理想文字,Padding=new Thickness(12,8)};

    private readonly VerticalStackLayout 診断領域=new(){Spacing=6};
    private readonly Label 再現テスト表示=new(){FontFamily="Consolas",FontSize=12,TextColor=配色.コード文字,Padding=new Thickness(12,8)};

    private readonly VerticalStackLayout メーター領域=new(){Spacing=8};
    private readonly VerticalStackLayout 指標領域=new(){Spacing=3};
    private readonly VerticalStackLayout ログ領域=new(){Spacing=3};

    private readonly Picker ソース選択=new(){WidthRequest=130};
    private readonly Entry シード入力=new(){Text="0x1018_2026",WidthRequest=135,FontFamily="Consolas",FontSize=13};
    private readonly Entry 試行番号入力=new(){Text="0",WidthRequest=65,FontFamily="Consolas",FontSize=13};
    private readonly Button シード実行ボタン=new(){Text="シード再現",HeightRequest=36};
    private readonly Picker 速度選択=new(){WidthRequest=95};
    private readonly Button 一時停止ボタン=new(){Text="⏸ 一時停止",WidthRequest=115};
    private readonly Button テストコピーボタン=new(){Text="テストコードをコピー",HeightRequest=32,FontSize=12};

    private readonly 検証再生器 再生器;
    private Task? 再生タスク;
    private チェーン検証結果? 表示中の結果;

    public MainPage(){
        this.BackgroundColor=配色.背景;
        this.時間グラフ表示=new GraphicsView{Drawable=this.時間グラフ描画,HeightRequest=175};
        this.割当グラフ表示=new GraphicsView{Drawable=this.割当グラフ描画,HeightRequest=175};

        var 保存先=System.IO.Path.Combine(AppContext.BaseDirectory,"FailedQueries");
        this.再生器=new 検証再生器(保存先);
        this.Content=this.画面を作る();

        this.再生器.段階変化時+=段階=>MainThread.BeginInvokeOnMainThread(()=>this.段階を選択表示する(段階));
        this.再生器.結果報告時+=(結果,統計)=>MainThread.BeginInvokeOnMainThread(()=>this.検証結果を表示する(結果,統計,true));
        this.Loaded+=this.読み込み時;
    }

    /// <summary>ウィンドウ終了時にバックグラウンド検証ループを停止する。</summary>
    public void 終了する(){
        this.取消.Cancel();
        try{
            this.再生タスク?.Wait(TimeSpan.FromSeconds(5));
        }catch(AggregateException){
        }
    }

    private void 読み込み時(object? 送り手,EventArgs 引数){
        if(this.再生タスク is not null){
            return;
        }
        this.段階を選択表示する(検証段階.ペア網羅);
        this.再生タスク=Task.Run(async()=>{
            try{
                await this.再生器.再生Async(this.取消.Token);
            }catch(OperationCanceledException) when(this.取消.IsCancellationRequested){
            }catch(Exception 例外){
                MainThread.BeginInvokeOnMainThread(()=>{
                    this.状態表示.Text=$"検証エラー: {例外.Message}";
                    this.状態表示.TextColor=配色.失敗;
                });
            }
        });
    }

    // ---- 画面レイアウト構築 ----

    private View 画面を作る(){
        var 全体=new Grid{
            RowDefinitions=[new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Star)],
            ColumnDefinitions=[
                new ColumnDefinition(new GridLength(255)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(460)),
            ],
            Padding=12,
            RowSpacing=10,
            ColumnSpacing=12,
        };
        var 上部=this.上部バーを作る();
        全体.Add(上部,0,0);
        Grid.SetColumnSpan(上部,3);
        全体.Add(this.左列を作る(),0,1);
        全体.Add(this.中央列を作る(),1,1);
        全体.Add(this.右列を作る(),2,1);
        return 全体;
    }

    private View 上部バーを作る(){
        foreach(var ソース in ソース一覧){
            this.ソース選択.Items.Add(ソース.ToString());
        }
        this.ソース選択.SelectedIndex=0;
        this.ソース選択.SelectedIndexChanged+=this.ソース変更時;

        foreach(var 項目 in 速度一覧){
            this.速度選択.Items.Add(項目.表示);
        }
        this.速度選択.SelectedIndex=1;
        this.速度選択.SelectedIndexChanged+=this.速度変更時;

        this.シード実行ボタン.Clicked+=this.シード再現押下時;
        this.一時停止ボタン.Clicked+=this.一時停止押下時;
        this.テストコピーボタン.Clicked+=this.テストコピー押下時;

        var 題名=new Label{
            Text="FLinq メソッドチェーン理想展開検証ベンチ",
            FontSize=20,
            FontAttributes=FontAttributes.Bold,
            TextColor=配色.文字,
            VerticalOptions=LayoutOptions.Center,
        };
        var 左並び=new HorizontalStackLayout{Spacing=12,VerticalOptions=LayoutOptions.Center};
        左並び.Add(題名);
        左並び.Add(this.状態表示);

        var 右操作=new HorizontalStackLayout{Spacing=6,HorizontalOptions=LayoutOptions.End,VerticalOptions=LayoutOptions.Center};
        右操作.Add(new Label{Text="ソース",TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center,FontSize=12});
        右操作.Add(this.ソース選択);
        右操作.Add(new Label{Text="シード",TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center,FontSize=12});
        右操作.Add(this.シード入力);
        右操作.Add(new Label{Text="試行#",TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center,FontSize=12});
        右操作.Add(this.試行番号入力);
        右操作.Add(this.シード実行ボタン);
        右操作.Add(new Label{Text="速度",TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center,FontSize=12});
        右操作.Add(this.速度選択);
        右操作.Add(this.一時停止ボタン);

        var 行=new Grid{
            ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)],
        };
        行.Add(左並び,0,0);
        行.Add(右操作,1,0);
        return 行;
    }

    private View 左列を作る(){
        var 並び=new VerticalStackLayout{Spacing=8};
        並び.Add(new Label{Text="検証ステージ",FontSize=16,FontAttributes=FontAttributes.Bold,TextColor=配色.文字});

        for(var i=0;i<this.再生器.段階一覧.Count;i++){
            var 段階定義=this.再生器.段階一覧[i];
            var 印=new Label{Text="○",FontSize=16,WidthRequest=22,TextColor=配色.薄い文字,VerticalOptions=LayoutOptions.Center};
            var 文字塊=new VerticalStackLayout{Spacing=2};
            文字塊.Add(new Label{Text=$"{i+1}. {段階定義.題名}",FontSize=14,FontAttributes=FontAttributes.Bold,TextColor=配色.文字});
            文字塊.Add(new Label{Text=段階定義.副題,FontSize=11,TextColor=配色.薄い文字,LineBreakMode=LineBreakMode.WordWrap,MaximumWidthRequest=195});

            var 中身=new HorizontalStackLayout{Spacing=6};
            中身.Add(印);
            中身.Add(文字塊);

            var 枠=new Border{
                Content=中身,
                Padding=new Thickness(8,8),
                StrokeThickness=1,
                Stroke=配色.枠,
                BackgroundColor=配色.面,
                StrokeShape=new RoundRectangle{CornerRadius=6},
            };
            var 対象段階=段階定義.段階;
            var 押下=new TapGestureRecognizer();
            押下.Tapped+=(_,_)=>this.再生器.段階へ移動(対象段階);
            枠.GestureRecognizers.Add(押下);
            this.段階行一覧.Add((枠,印));
            並び.Add(枠);
        }

        並び.Add(new Label{
            Text="ステージを押すとその検証段階へ即座に切り替わります。段階4はシードを変えながら無限に探索を続けます。",
            FontSize=11,
            TextColor=配色.薄い文字,
            Margin=new Thickness(0,4,0,0),
        });

        var 凡例枠=new VerticalStackLayout{Spacing=4,Padding=10};
        凡例枠.Add(new Label{Text="ペア結合(A.B → B.C → C.D)の判定基準",FontSize=12,FontAttributes=FontAttributes.Bold,TextColor=配色.文字});
        凡例枠.Add(new Label{Text="• 展開確認: FLinqが全段をインターセプトしフォールバックが0であること",FontSize=11,TextColor=配色.薄い文字});
        凡例枠.Add(new Label{Text="• 結果一致: 空・1件・16件・256件でSystem.Linqと結果/例外が完全一致",FontSize=11,TextColor=配色.薄い文字});
        凡例枠.Add(new Label{Text="• 理想特性: 2段ペア遷移の合成から推定したループ上限・割り当て・実行時間と非劣化比較",FontSize=11,TextColor=配色.薄い文字});
        並び.Add(面を作る(凡例枠));

        return new ScrollView{Content=並び};
    }

    private View 中央列を作る(){
        var 並び=new VerticalStackLayout{Spacing=10,Padding=14};

        // ステージ説明ヘッダ
        並び.Add(this.段階見出し);
        並び.Add(this.段階副題);
        並び.Add(this.段階説明);

        // 現在実行中のチェーン見出しと合否バッジ
        this.判定バッジ枠.Content=this.判定バッジ;
        var チェーン行=new Grid{
            ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing=8,
        };
        チェーン行.Add(this.チェーン式見出し,0,0);
        チェーン行.Add(this.判定バッジ枠,1,0);
        並び.Add(チェーン行);

        // 隣接2メソッドペア遷移バッジ列
        並び.Add(this.ペア遷移並び);

        // 1. 入力クエリ
        並び.Add(見出し("1. 入力クエリ (Input Query)"));
        並び.Add(new Border{
            Content=this.入力クエリ表示,
            BackgroundColor=配色.コード背景,
            StrokeThickness=0,
            StrokeShape=new RoundRectangle{CornerRadius=6},
        });

        // 2. FLinq展開コード vs 手書き理想コード / 推定理想特性 (2列比較)
        var コード比較グリッド=new Grid{
            ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)],
            ColumnSpacing=10,
            RowDefinitions=[new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Auto)],
            RowSpacing=4,
        };
        コード比較グリッド.Add(見出し("2A. FLinq 展開コード (Generator Output)"),0,0);
        コード比較グリッド.Add(this.理想見出しラベル,1,0);

        var 展開枠=new Border{
            Content=new ScrollView{Content=this.展開コード表示,MaximumHeightRequest=290},
            BackgroundColor=配色.コード背景,
            StrokeThickness=0,
            StrokeShape=new RoundRectangle{CornerRadius=6},
        };
        var 理想枠=new Border{
            Content=new ScrollView{Content=this.理想コード表示,MaximumHeightRequest=290},
            BackgroundColor=配色.理想背景,
            StrokeThickness=0,
            StrokeShape=new RoundRectangle{CornerRadius=6},
        };
        コード比較グリッド.Add(展開枠,0,1);
        コード比較グリッド.Add(理想枠,1,1);
        並び.Add(コード比較グリッド);

        // 3. 原因と改善案の診断パネル
        並び.Add(見出し("3. 2メソッドペア合成に基づく原因と改善案の自動診断"));
        並び.Add(new Border{
            Content=this.診断領域,
            Padding=10,
            BackgroundColor=配色.診断背景,
            Stroke=配色.診断枠,
            StrokeThickness=1,
            StrokeShape=new RoundRectangle{CornerRadius=6},
        });

        // 4. シードから再現できる単体テストコード
        var テスト見出し行=new Grid{
            ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)],
        };
        テスト見出し行.Add(見出し("4. シード値・データ再現用 xUnit テストコード ([Fact])"),0,0);
        テスト見出し行.Add(this.テストコピーボタン,1,0);
        並び.Add(テスト見出し行);
        並び.Add(new Border{
            Content=new ScrollView{Content=this.再現テスト表示,MaximumHeightRequest=210},
            BackgroundColor=配色.コード背景,
            StrokeThickness=0,
            StrokeShape=new RoundRectangle{CornerRadius=6},
        });

        return 面を作る(new ScrollView{Content=並び});
    }

    private View 右列を作る(){
        var 並び=new VerticalStackLayout{Spacing=10,Padding=12};
        並び.Add(見出し("検証達成率メーター"));
        並び.Add(this.メーター領域);

        並び.Add(見出し("現在のチェーン計測指標"));
        並び.Add(this.指標領域);

        並び.Add(見出し("パフォーマンス・メモリ割り当て比較 (N=512)"));
        並び.Add(this.時間グラフ表示);
        並び.Add(this.割当グラフ表示);

        並び.Add(見出し("蓄積実行ログ (クリックでそのチェーンの展開コード・診断を表示)"));
        var ログスクロール=new ScrollView{
            Content=this.ログ領域,
            HeightRequest=230,
        };
        並び.Add(new Border{
            Content=ログスクロール,
            Padding=6,
            BackgroundColor=配色.背景,
            Stroke=配色.枠,
            StrokeThickness=1,
            StrokeShape=new RoundRectangle{CornerRadius=6},
        });

        return 面を作る(new ScrollView{Content=並び});
    }

    private static Label 見出し(string 文字)=>new(){
        Text=文字,
        FontSize=13,
        FontAttributes=FontAttributes.Bold,
        TextColor=配色.薄い文字,
    };

    private static Border 面を作る(View 中身)=>new(){
        Content=中身,
        BackgroundColor=配色.面,
        Stroke=配色.枠,
        StrokeThickness=1,
        StrokeShape=new RoundRectangle{CornerRadius=8},
    };

    // ---- イベントハンドラ ----

    private void ソース変更時(object? 送り手,EventArgs 引数){
        if(this.ソース選択.SelectedIndex>=0&&this.ソース選択.SelectedIndex<ソース一覧.Length){
            this.再生器.現在のソース種別=ソース一覧[this.ソース選択.SelectedIndex];
        }
    }

    private void 速度変更時(object? 送り手,EventArgs 引数){
        if(this.速度選択.SelectedIndex>=0&&this.速度選択.SelectedIndex<速度一覧.Length){
            this.再生器.速度=速度一覧[this.速度選択.SelectedIndex].速度;
        }
    }

    private void 一時停止押下時(object? 送り手,EventArgs 引数){
        if(this.再生器.一時停止中か){
            this.再生器.再開();
            this.一時停止ボタン.Text="⏸ 一時停止";
        }else{
            this.再生器.一時停止();
            this.一時停止ボタン.Text="▶ 再開";
        }
    }

    private void シード再現押下時(object? 送り手,EventArgs 引数){
        var シード文字列=(this.シード入力.Text??string.Empty).Trim().Replace("_",string.Empty);
        ulong シード=0x1018_2026UL;
        if(シード文字列.StartsWith("0x",StringComparison.OrdinalIgnoreCase)){
            ulong.TryParse(シード文字列[2..],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out シード);
        }else{
            ulong.TryParse(シード文字列,NumberStyles.Integer,CultureInfo.InvariantCulture,out シード);
        }
        var 試行番号=0;
        int.TryParse((this.試行番号入力.Text??"0").Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out 試行番号);

        this.再生器.基準シード=シード;
        this.再生器.一時停止();
        this.一時停止ボタン.Text="▶ 再開";

        Task.Run(()=>{
            var 結果=this.再生器.シード指定で1件検証(this.再生器.現在のソース種別,シード,試行番号);
            MainThread.BeginInvokeOnMainThread(()=>{
                this.状態表示.Text=$"シード 0x{シード:X8} (試行#{試行番号}) を再現実行しました: {結果.項目.クエリ表示式}";
            });
        });
    }

    private async void テストコピー押下時(object? 送り手,EventArgs 引数){
        if(this.表示中の結果 is null){
            return;
        }
        await Clipboard.Default.SetTextAsync(this.表示中の結果.再現テストコード);
        this.状態表示.Text="再現テストコードをクリップボードへコピーしました。";
    }

    // ---- 画面更新処理 ----

    private void 段階を選択表示する(検証段階 段階){
        var インデックス=(int)段階;
        for(var i=0;i<this.段階行一覧.Count;i++){
            var (枠,印)=this.段階行一覧[i];
            if(i==インデックス){
                印.Text="▶";
                印.TextColor=配色.アクセント;
                枠.BackgroundColor=配色.実行中の段階;
            }else if(i<インデックス){
                印.Text="✔";
                印.TextColor=配色.成功;
                枠.BackgroundColor=配色.面;
            }else{
                印.Text="○";
                印.TextColor=配色.薄い文字;
                枠.BackgroundColor=配色.面;
            }
        }
        var 定義=this.再生器.段階一覧[インデックス];
        this.段階見出し.Text=$"{インデックス+1}. {定義.題名}";
        this.段階副題.Text=定義.副題;
        this.段階説明.Text=定義.説明;
    }

    private void 検証結果を表示する(チェーン検証結果 結果,検証統計? 統計,bool ログへ追加するか){
        this.表示中の結果=結果;
        var 項目=結果.項目;

        this.状態表示.Text=$"[{項目.ソース種別}] 試行#{項目.試行番号} Seed=0x{項目.シード値:X8}";
        this.チェーン式見出し.Text=項目.クエリ表示式;

        if(結果.完全合格か){
            this.判定バッジ.Text="✔ 理想展開・結果一致・性能合格";
            this.判定バッジ枠.BackgroundColor=配色.成功;
        }else if(結果.全メソッド展開済みか&&結果.全データ結果一致か){
            this.判定バッジ.Text="⚠ 展開・結果一致 (ペア理想より改善余地あり)";
            this.判定バッジ枠.BackgroundColor=配色.警告;
        }else{
            this.判定バッジ.Text="✖ 要調査 (未展開または結果不一致)";
            this.判定バッジ枠.BackgroundColor=配色.失敗;
        }

        // ペア遷移バッジの構築
        this.ペア遷移並び.Clear();
        foreach(var 遷移 in 項目.推定特性.ペア遷移一覧){
            var バッジ色=配色.アクセント;
            if(!遷移.完全インライン融合か){
                バッジ色=配色.警告;
            }
            var ラベル=new Label{
                Text=$"{遷移.前段メソッド名}.{遷移.後段メソッド名} [{遷移.融合パターン名}]",
                FontSize=11,
                TextColor=Colors.White,
                Padding=new Thickness(8,3),
            };
            this.ペア遷移並び.Add(new Border{
                Content=ラベル,
                BackgroundColor=バッジ色,
                StrokeThickness=0,
                StrokeShape=new RoundRectangle{CornerRadius=4},
                Margin=new Thickness(0,0,6,4),
            });
        }

        // 1. 入力クエリ
        this.入力クエリ表示.Text=$"// ソース種別: {項目.ソース種別} / シード: 0x{項目.シード値:X8} / 試行#{項目.試行番号}\n// {項目.クエリ表示式}\n{項目.クエリ本体コード}";

        // 2. FLinq展開コード と 手書き理想コード(または推定理想特性)
        this.展開コード表示.Text=結果.展開コード抜粋;
        if(!string.IsNullOrWhiteSpace(項目.手書き理想コード)){
            this.理想見出しラベル.Text="2B. 手書き理想コード (Hand-Written Ideal Loop)";
            this.理想コード表示.Text=項目.手書き理想コード;
        }else{
            this.理想見出しラベル.Text="2B. 2メソッドペア合成(A.B+B.C+...)による推定理想特性";
            this.理想コード表示.Text=項目.推定特性.推定理想構造コード;
        }

        // 3. 原因と改善案の診断
        this.診断領域.Clear();
        var 診断色=配色.成功;
        if(!結果.全メソッド展開済みか||!結果.全データ結果一致か){
            診断色=配色.失敗;
        }else if(!結果.完全合格か){
            診断色=配色.警告;
        }
        this.診断領域.Add(new Label{
            Text=$"【結果要約】 {結果.結果要約}",
            FontSize=13,
            FontAttributes=FontAttributes.Bold,
            TextColor=診断色,
        });
        this.診断領域.Add(new Label{
            Text=$"【原因診断】\n{結果.原因診断}",
            FontSize=12,
            TextColor=配色.文字,
        });
        this.診断領域.Add(new Label{
            Text=$"【改善案】\n{結果.改善案}",
            FontSize=12,
            TextColor=配色.文字,
        });
        if(!string.IsNullOrWhiteSpace(結果.保存先ファイルパス)){
            this.診断領域.Add(new Label{
                Text=$"【自動出力テストファイル】 {結果.保存先ファイルパス}",
                FontSize=11,
                TextColor=配色.アクセント,
            });
        }

        // 4. 再現テストコード
        this.再現テスト表示.Text=結果.再現テストコード;

        // 右列のメーター・指標・グラフ更新
        if(統計 is not null){
            this.メーターを更新する("全メソッド展開率 (Fallback=0)",統計.展開済数,統計.総実行数);
            this.メーターを更新する("全データパターン結果一致率",統計.結果一致数,統計.総実行数);
            this.メーターを更新する("ペア理想特性・非劣化達成率",統計.非劣化数,統計.総実行数);
            this.指標を更新する("総実行チェーン数",$"{統計.総実行数} 件 (合格 {統計.合格数} / 要改善 {統計.警告または失敗数})");
        }
        var 高速化倍率=1.0;
        if(結果.最適化FLinq時間マイクロ秒>0.0){
            高速化倍率=結果.非最適化FLinq時間マイクロ秒/結果.最適化FLinq時間マイクロ秒;
        }
        var 対System倍率=1.0;
        if(結果.最適化FLinq時間マイクロ秒>0.0){
            対System倍率=結果.System時間マイクロ秒/結果.最適化FLinq時間マイクロ秒;
        }
        this.指標を更新する("ループ数 (展開実測 / ペア理想期待)",$"{結果.実測ループ本数} 本 / 理想 {項目.推定特性.期待ループ本数} 本以下");
        this.指標を更新する("フォールバック回数",$"{結果.実行時フォールバック数} 回");
        this.指標を更新する("高速化倍率 (対 FLinq未最適化 / 対 System.Linq)",$"{高速化倍率:0.00}x / {対System倍率:0.00}x");

        this.棒グラフを更新する(結果);

        if(ログへ追加するか){
            this.ログ行を追加する(結果,高速化倍率);
        }
    }

    private void メーターを更新する(string 名前,int 達成数,int 総数){
        var 割合=0.0;
        if(総数>0){
            割合=Math.Clamp((double)達成数/総数,0.0,1.0);
        }
        var 文字=$"{達成数} / {総数} ({割合*100.0:0.0}%)";
        if(this.メーター一覧.TryGetValue(名前,out var 既存)){
            既存.値.Text=文字;
            既存.棒.Progress=割合;
            return;
        }
        var 値ラベル=new Label{Text=文字,FontSize=12,TextColor=配色.文字,HorizontalTextAlignment=TextAlignment.End};
        var 棒=new ProgressBar{Progress=割合,ProgressColor=配色.アクセント,HeightRequest=9};
        var 見出し行=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)]};
        見出し行.Add(new Label{Text=名前,FontSize=12,TextColor=配色.薄い文字},0,0);
        見出し行.Add(値ラベル,1,0);
        var 塊=new VerticalStackLayout{Spacing=2};
        塊.Add(見出し行);
        塊.Add(棒);
        this.メーター一覧.Add(名前,(値ラベル,棒));
        this.メーター領域.Add(塊);
    }

    private void 指標を更新する(string 名前,string 値){
        if(this.指標一覧.TryGetValue(名前,out var 既存ラベル)){
            既存ラベル.Text=値;
            return;
        }
        var 値ラベル=new Label{Text=値,FontSize=13,FontAttributes=FontAttributes.Bold,TextColor=配色.文字,HorizontalTextAlignment=TextAlignment.End};
        var 行=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)]};
        行.Add(new Label{Text=名前,FontSize=12,TextColor=配色.薄い文字},0,0);
        行.Add(値ラベル,1,0);
        this.指標一覧.Add(名前,値ラベル);
        this.指標領域.Add(行);
    }

    private void 棒グラフを更新する(チェーン検証結果 結果){
        var opt色=棒の色.強調;
        if(!結果.パフォーマンス合格か){
            opt色=棒の色.警告;
        }
        var 理想ラベル="推定理想";
        if(!string.IsNullOrWhiteSpace(結果.項目.手書き理想コード)){
            理想ラベル="手書き理想";
        }
        var 時間棒=new List<棒グラフ項目>{
            new("System.Linq",結果.System時間マイクロ秒,棒の色.通常),
            new("FLinq未最適",結果.非最適化FLinq時間マイクロ秒,棒の色.比較),
            new("FLinq最適化",結果.最適化FLinq時間マイクロ秒,opt色),
            new(理想ラベル,結果.理想または推定時間マイクロ秒,棒の色.理想),
        };
        this.時間グラフ描画.更新する(new 棒グラフ報告("実行時間","μs",時間棒));
        this.時間グラフ表示.Invalidate();

        var 割当棒=new List<棒グラフ項目>{
            new("System.Linq",結果.System確保バイト数,棒の色.通常),
            new("FLinq未最適",結果.非最適化FLinq確保バイト数,棒の色.比較),
            new("FLinq最適化",結果.最適化FLinq確保バイト数,opt色),
            new(理想ラベル,結果.理想または推定確保バイト数,棒の色.理想),
        };
        this.割当グラフ描画.更新する(new 棒グラフ報告("メモリ割り当て","B",割当棒));
        this.割当グラフ表示.Invalidate();
    }

    private void ログ行を追加する(チェーン検証結果 結果,double 高速化倍率){
        var 項目=結果.項目;
        var 印="✔";
        var 色=配色.成功;
        if(!結果.全メソッド展開済みか||!結果.全データ結果一致か){
            印="✖";
            色=配色.失敗;
        }else if(!結果.パフォーマンス合格か){
            印="⚠";
            色=配色.警告;
        }

        var 文=$"{印} [{項目.段階}] #{項目.試行番号} (0x{項目.シード値:X6}) {項目.クエリ表示式} | {結果.最適化FLinq時間マイクロ秒:0.1}μs ({高速化倍率:0.00}x) Loop={結果.実測ループ本数}/{項目.推定特性.期待ループ本数}";
        var ラベル=new Label{
            Text=文,
            FontFamily="Consolas",
            FontSize=11,
            TextColor=色,
            Padding=new Thickness(4,2),
        };
        var 行枠=new Border{
            Content=ラベル,
            BackgroundColor=Colors.Transparent,
            StrokeThickness=0,
            StrokeShape=new RoundRectangle{CornerRadius=4},
        };
        var 保持結果=結果;
        var 押下=new TapGestureRecognizer();
        押下.Tapped+=(_,_)=>{
            this.再生器.一時停止();
            this.一時停止ボタン.Text="▶ 再開";
            this.シード入力.Text=$"0x{保持結果.項目.シード値:X8}";
            this.試行番号入力.Text=保持結果.項目.試行番号.ToString(CultureInfo.InvariantCulture);
            this.検証結果を表示する(保持結果,null,false);
        };
        行枠.GestureRecognizers.Add(押下);

        this.ログ領域.Insert(0,行枠);
        while(this.ログ領域.Count>蓄積ログ最大件数){
            this.ログ領域.RemoveAt(this.ログ領域.Count-1);
        }
    }
}
