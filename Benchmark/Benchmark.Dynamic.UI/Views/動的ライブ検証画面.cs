using Benchmark.Dynamic.Core;
using Benchmark.Infrastructure;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;

namespace Benchmark.Dynamic.UI;

/// <summary>
/// 「ライブ検証」タブ。ペア全組み合わせ→3段全組み合わせ→ランダム探索の順に、
/// System.Linq と FLinq(最適化なし・あり)・ZLinq の結果が一致するかを流しながら見る。
/// 左=段階、中央=式・判定・診断・生成コード・FLinq展開後のコード・再現テスト、右=達成率・グラフ・ログ。
/// 検証の合否は Worker が決めたものをそのまま出す(画面は独自のしきい値を持たない)。
/// 実行中は CPU を使うので、タブを開いただけでは始めず「開始」を押したときだけ動く。
/// </summary>
public sealed class 動的ライブ検証画面:ContentView{
    public アプリ操作排他? 実行排他{get;set;}
    public Func<(int 最適化,WorkerStorageSettings 設定)>? 測定設定を取得{get;set;}
    private const int ログ最大件数=250;
    private static readonly (string 表示,int 待ちミリ秒)[] 速度一覧=[
        ("ゆっくり",1200),
        ("ふつう",300),
        ("はやい",60),
        ("最速",0),
    ];
    private static readonly SourceTarget[] 対象一覧=[
        SourceTarget.SystemArray,
        SourceTarget.SystemList,
        SourceTarget.SystemIEnumerable,
        SourceTarget.SystemRange,
        SourceTarget.SystemPersisted,
        SourceTarget.SeamlessPersisted,
        SourceTarget.SystemTsavorite,
        SourceTarget.SeamlessTsavorite,
    ];
    private static readonly (動的ライブ段階 段階,string 題名,string 副題)[] 段階定義=[
        (動的ライブ段階.ペア全組み合わせ,"ペア全組み合わせ","中間演算子1つ+終端の全並び"),
        (動的ライブ段階.三段全組み合わせ,"3段全組み合わせ","中間演算子2つ+終端の全並び"),
        (動的ライブ段階.ランダム探索,"ランダム探索","シードを進めながら無限に探索"),
    ];

    private readonly 動的ライブ検証サービス サービス;
    private readonly List<(Border 枠,Label 印)> 段階行一覧=[];
    private readonly Dictionary<string,(Label 値,ProgressBar 棒)> メーター一覧=[];
    private readonly 動的ライブ棒グラフ 時間描画=new();
    private readonly 動的ライブ棒グラフ 割当描画=new();
    private readonly GraphicsView 時間グラフ;
    private readonly GraphicsView 割当グラフ;

    private readonly Label 状態表示=new(){TextColor=動的ライブ配色.薄い文字,VerticalOptions=LayoutOptions.Center,FontSize=13,AutomationId="動的ライブ.状態"};
    private readonly Label 式表示=new(){FontFamily="Consolas",FontSize=15,FontAttributes=FontAttributes.Bold,TextColor=動的ライブ配色.文字,AutomationId="動的ライブ.式"};
    private readonly Label 判定バッジ=new(){FontSize=13,FontAttributes=FontAttributes.Bold,Padding=new Thickness(10,4),TextColor=Colors.White,AutomationId="動的ライブ.判定"};
    private readonly Border 判定枠=new(){StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=6},VerticalOptions=LayoutOptions.Center,IsVisible=false};
    private readonly Label 診断表示=new(){FontSize=12,TextColor=動的ライブ配色.文字,AutomationId="動的ライブ.診断"};
    private readonly Label 生成コード表示=new(){FontFamily="Consolas",FontSize=12,TextColor=動的ライブ配色.コード文字,Padding=new Thickness(12,8)};
    private readonly Label 展開コード表示=new(){FontFamily="Consolas",FontSize=12,TextColor=動的ライブ配色.展開文字,Padding=new Thickness(12,8)};
    private readonly Label 再現テスト表示=new(){FontFamily="Consolas",FontSize=12,TextColor=動的ライブ配色.コード文字,Padding=new Thickness(12,8)};
    private readonly VerticalStackLayout メーター領域=new(){Spacing=8};
    private readonly VerticalStackLayout ログ領域=new(){Spacing=3,AutomationId="動的ライブ.ログ"};
    private readonly Label 件数表示=new(){FontSize=13,FontAttributes=FontAttributes.Bold,TextColor=動的ライブ配色.文字};

    private readonly Picker 対象選択=new(){WidthRequest=170,AutomationId="動的ライブ.Source"};
    private readonly Picker 要素型選択=new(){WidthRequest=150,AutomationId="動的ライブ.要素型"};
    private readonly Picker 速度選択=new(){WidthRequest=100,AutomationId="動的ライブ.速度"};
    private readonly Entry シード入力=new(){Text="0x1018_2026",WidthRequest=150,FontFamily="Consolas",FontSize=13,AutomationId="動的ライブ.Seed"};
    private readonly Entry 試行番号入力=new(){Text="0",WidthRequest=65,FontFamily="Consolas",FontSize=13,AutomationId="動的ライブ.試行番号"};
    private readonly CheckBox 特殊値チェック=new(){AutomationId="動的ライブ.特殊値"};
    private readonly Button 開始ボタン=new(){Text="開始",WidthRequest=80,BackgroundColor=動的ライブ配色.アクセント,TextColor=Colors.White,AutomationId="動的ライブ.開始"};
    private readonly Button 停止ボタン=new(){Text="停止",WidthRequest=80,IsEnabled=false,AutomationId="動的ライブ.停止"};
    private readonly Button 一時停止ボタン=new(){Text="一時停止",WidthRequest=100,IsEnabled=false,AutomationId="動的ライブ.一時停止"};
    private readonly Button シード再現ボタン=new(){Text="シード再現",AutomationId="動的ライブ.シード再現"};
    private readonly Button コピーボタン=new(){Text="テストコードをコピー",FontSize=12,AutomationId="動的ライブ.テストコピー"};

    private CancellationTokenSource? 取消;
    private Task? 実行タスク;
    private TaskCompletionSource? 再開待ち;
    private 動的ライブ結果? 表示中の結果;
    private int 総数;
    private int 合格数;
    private int 展開済数;
    private int 速度の待ち=300;
    private bool 一時停止中=false;
    private bool 実行中=false;

    public 動的ライブ検証画面(動的ライブ検証サービス サービス){
        this.サービス=サービス;
        this.AutomationId="動的ライブ.領域";
        this.時間グラフ=new GraphicsView{Drawable=this.時間描画,HeightRequest=175};
        this.割当グラフ=new GraphicsView{Drawable=this.割当描画,HeightRequest=175};
        this.BackgroundColor=動的ライブ配色.背景;
        this.Content=this.画面を作る();
        this.段階を表示する(動的ライブ段階.ペア全組み合わせ,false);
    }

    /// <summary>画面を閉じるとき。</summary>
    public void 終了する(){
        this.取消?.Cancel();
        this.再開待ち?.TrySetResult();
        try{
            this.実行タスク?.Wait(TimeSpan.FromSeconds(5));
        }catch(AggregateException){
        }
    }

    public async Task 終了Async(){
        this.取消?.Cancel();
        this.再開待ち?.TrySetResult();
        if(this.実行タスク is{} 実行){await 実行;}
    }

    // ---- 画面を作る ----

    private View 画面を作る(){
        var 全体=new Grid{
            RowDefinitions=[new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Star)],
            ColumnDefinitions=[
                new ColumnDefinition(new GridLength(240)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(430)),
            ],
            Padding=10,
            RowSpacing=8,
            ColumnSpacing=10,
        };
        var 上部=this.上部を作る();
        全体.Add(上部,0,0);
        Grid.SetColumnSpan(上部,3);
        全体.Add(this.左列を作る(),0,1);
        全体.Add(this.中央列を作る(),1,1);
        全体.Add(this.右列を作る(),2,1);
        return 全体;
    }

    private View 上部を作る(){
        foreach(var 対象 in 対象一覧){
            this.対象選択.Items.Add(対象.ToString());
        }
        this.対象選択.SelectedIndex=0;
        foreach(var 型 in Enum.GetValues<QueryElementType>()){
            this.要素型選択.Items.Add(型.ToString());
        }
        this.要素型選択.SelectedIndex=0;
        foreach(var 項目 in 速度一覧){
            this.速度選択.Items.Add(項目.表示);
        }
        this.速度選択.SelectedIndex=1;
        this.速度選択.SelectedIndexChanged+=(_,_)=>{
            if(this.速度選択.SelectedIndex>=0){
                this.速度の待ち=速度一覧[this.速度選択.SelectedIndex].待ちミリ秒;
            }
        };
        this.開始ボタン.Clicked+=(_,_)=>this.開始する(動的ライブ段階.ペア全組み合わせ,0);
        this.停止ボタン.Clicked+=(_,_)=>this.停止する();
        this.一時停止ボタン.Clicked+=(_,_)=>this.一時停止を切り替える();
        this.シード再現ボタン.Clicked+=(_,_)=>this.シードを再現する();
        this.コピーボタン.Clicked+=async(_,_)=>{
            if(this.表示中の結果 is { }結果){
                await Clipboard.Default.SetTextAsync(結果.再現テスト);
                this.状態表示.Text="再現テストをクリップボードへコピーしました。";
            }
        };

        var 左=new HorizontalStackLayout{Spacing=12,VerticalOptions=LayoutOptions.Center};
        左.Add(new Label{Text="ライブ検証",FontSize=20,FontAttributes=FontAttributes.Bold,TextColor=動的ライブ配色.文字,VerticalOptions=LayoutOptions.Center});
        左.Add(this.状態表示);

        var 右=new HorizontalStackLayout{Spacing=6,HorizontalOptions=LayoutOptions.End,VerticalOptions=LayoutOptions.Center};
        右.Add(ラベル("Source"));
        右.Add(this.対象選択);
        右.Add(ラベル("要素型"));
        右.Add(this.要素型選択);
        右.Add(this.特殊値チェック);
        右.Add(ラベル("特殊な値"));
        右.Add(ラベル("Seed"));
        右.Add(this.シード入力);
        右.Add(ラベル("試行#"));
        右.Add(this.試行番号入力);
        右.Add(this.シード再現ボタン);
        右.Add(ラベル("速度"));
        右.Add(this.速度選択);
        右.Add(this.開始ボタン);
        右.Add(this.一時停止ボタン);
        右.Add(this.停止ボタン);

        var 行=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)]};
        行.Add(左,0,0);
        行.Add(new ScrollView{Content=右,Orientation=ScrollOrientation.Horizontal,HorizontalOptions=LayoutOptions.End},1,0);
        return 行;
    }

    private View 左列を作る(){
        var 並び=new VerticalStackLayout{Spacing=8};
        並び.Add(new Label{Text="検証ステージ",FontSize=16,FontAttributes=FontAttributes.Bold,TextColor=動的ライブ配色.文字});
        for(var i=0;i<段階定義.Length;i++){
            var 定義=段階定義[i];
            var 印=new Label{Text="○",FontSize=16,WidthRequest=22,TextColor=動的ライブ配色.薄い文字,VerticalOptions=LayoutOptions.Center};
            var 文字=new VerticalStackLayout{Spacing=2};
            文字.Add(new Label{Text=$"{i+1}. {定義.題名}",FontSize=14,FontAttributes=FontAttributes.Bold,TextColor=動的ライブ配色.文字});
            文字.Add(new Label{Text=定義.副題,FontSize=11,TextColor=動的ライブ配色.薄い文字,LineBreakMode=LineBreakMode.WordWrap,MaximumWidthRequest=180});
            var 中身=new HorizontalStackLayout{Spacing=6};
            中身.Add(印);
            中身.Add(文字);
            var 枠=new Border{
                Content=中身,
                Padding=new Thickness(8),
                StrokeThickness=1,
                Stroke=動的ライブ配色.枠,
                BackgroundColor=動的ライブ配色.面,
                StrokeShape=new RoundRectangle{CornerRadius=6},
                AutomationId="動的ライブ.段階."+(i+1),
            };
            var 対象段階=定義.段階;
            var タップ=new TapGestureRecognizer();
            タップ.Tapped+=(_,_)=>{
                if(this.実行中){
                    this.開始する(対象段階,0);
                }
            };
            枠.GestureRecognizers.Add(タップ);
            this.段階行一覧.Add((枠,印));
            並び.Add(枠);
        }
        並び.Add(new Label{
            Text="実行中にステージを押すと、その段階の最初から始め直します。ランダム探索は終わりません。",
            FontSize=11,
            TextColor=動的ライブ配色.薄い文字,
        });
        並び.Add(面(new VerticalStackLayout{
            Spacing=4,
            Padding=10,
            Children={
                new Label{Text="判定の見方",FontSize=12,FontAttributes=FontAttributes.Bold,TextColor=動的ライブ配色.文字},
                new Label{Text="• 展開確認: FLinq が全部の呼び出しを展開し、System.Linq への逃げが0",FontSize=11,TextColor=動的ライブ配色.薄い文字},
                new Label{Text="• 結果一致: System / FLinq最適化なし / FLinq / ZLinq の結果・順序・例外の型が一致",FontSize=11,TextColor=動的ライブ配色.薄い文字},
                new Label{Text="• 速度は参考表示です。合否には使いません。",FontSize=11,TextColor=動的ライブ配色.薄い文字},
            },
        }));
        return new ScrollView{Content=並び};
    }

    private View 中央列を作る(){
        var 並び=new VerticalStackLayout{Spacing=10,Padding=14};
        var 式行=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)],ColumnSpacing=8};
        this.判定枠.Content=this.判定バッジ;
        式行.Add(this.式表示,0,0);
        式行.Add(this.判定枠,1,0);
        並び.Add(式行);

        並び.Add(見出し("診断"));
        並び.Add(new Border{
            Content=this.診断表示,
            Padding=10,
            BackgroundColor=Color.FromArgb("#FFFBEB"),
            Stroke=Color.FromArgb("#F59E0B"),
            StrokeThickness=1,
            StrokeShape=new RoundRectangle{CornerRadius=6},
        });

        var コード比較=new Grid{
            ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)],
            ColumnSpacing=10,
            RowDefinitions=[new RowDefinition(GridLength.Auto),new RowDefinition(GridLength.Auto)],
            RowSpacing=4,
        };
        コード比較.Add(見出し("生成した検証コード(入力)"),0,0);
        コード比較.Add(見出し("FLinq が展開した後のコード"),1,0);
        コード比較.Add(コード枠(this.生成コード表示,動的ライブ配色.コード背景,290),0,1);
        コード比較.Add(コード枠(this.展開コード表示,動的ライブ配色.展開背景,290),1,1);
        並び.Add(コード比較);

        var テスト見出し=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)]};
        テスト見出し.Add(見出し("再現用 xUnit テスト"),0,0);
        テスト見出し.Add(this.コピーボタン,1,0);
        並び.Add(テスト見出し);
        並び.Add(コード枠(this.再現テスト表示,動的ライブ配色.コード背景,210));
        return 面(new ScrollView{Content=並び});
    }

    private View 右列を作る(){
        var 並び=new VerticalStackLayout{Spacing=10,Padding=12};
        並び.Add(this.件数表示);
        並び.Add(見出し("達成率"));
        並び.Add(this.メーター領域);
        並び.Add(見出し("実行時間とメモリ割り当て(参考)"));
        並び.Add(this.時間グラフ);
        並び.Add(this.割当グラフ);
        並び.Add(見出し("実行ログ(押すとそのクエリを表示)"));
        並び.Add(new Border{
            Content=new ScrollView{Content=this.ログ領域,HeightRequest=230},
            Padding=6,
            BackgroundColor=動的ライブ配色.背景,
            Stroke=動的ライブ配色.枠,
            StrokeThickness=1,
            StrokeShape=new RoundRectangle{CornerRadius=6},
        });
        return 面(new ScrollView{Content=並び});
    }

    private static Label ラベル(string 文字)=>new(){Text=文字,TextColor=動的ライブ配色.薄い文字,VerticalOptions=LayoutOptions.Center,FontSize=12};

    private static Label 見出し(string 文字)=>new(){Text=文字,FontSize=13,FontAttributes=FontAttributes.Bold,TextColor=動的ライブ配色.薄い文字};

    private static Border 面(View 中身)=>new(){
        Content=中身,
        BackgroundColor=動的ライブ配色.面,
        Stroke=動的ライブ配色.枠,
        StrokeThickness=1,
        StrokeShape=new RoundRectangle{CornerRadius=8},
    };

    private static Border コード枠(View 中身,Color 背景,double 最大高さ)=>new(){
        Content=new ScrollView{Content=中身,MaximumHeightRequest=最大高さ},
        BackgroundColor=背景,
        StrokeThickness=0,
        StrokeShape=new RoundRectangle{CornerRadius=6},
    };

    // ---- 操作 ----

    private void 開始する(動的ライブ段階 段階,int 開始番号){
        if(!Seed256.TryParse((this.シード入力.Text??string.Empty).Trim().Replace("_",string.Empty),out var シード)){
            this.状態表示.Text="Seedは符号なし64bitの10進または0x付き16進で入力してください。";
            this.状態表示.TextColor=動的ライブ配色.失敗;
            return;
        }
        var 対象=対象一覧[Math.Max(0,this.対象選択.SelectedIndex)];
        var 要素型=Enum.GetValues<QueryElementType>()[Math.Max(0,this.要素型選択.SelectedIndex)];
        if(!QueryCapabilityProfiles.SupportsElementType(対象,要素型)){
            this.状態表示.Text=$"要素型 {要素型} は {対象} では使えません(int 以外は Array・List・IEnumerable だけ)。";
            this.状態表示.TextColor=動的ライブ配色.失敗;
            return;
        }
        this.実行を止めて待つ();
        this.総数=0;
        this.合格数=0;
        this.展開済数=0;
        this.ログ領域.Clear();
        this.状態表示.TextColor=動的ライブ配色.薄い文字;
        this.状態表示.Text="開始しています";
        var 追加設定=this.測定設定を取得?.Invoke();
        var 設定=new 動的ライブ設定(対象,要素型,シード,特殊な値を混ぜる:this.特殊値チェック.IsChecked,開始段階:段階,開始番号:開始番号,最適化フラグ:追加設定?.最適化??15,ストレージ設定:追加設定?.設定);
        IDisposable? 予約=null;
        if(this.実行排他 is{} 排他){
            予約=排他.予約する();
            if(予約 is null){this.状態表示.Text="別の操作の実行またはcleanupを待っています。";return;}
        }
        var 取消=new CancellationTokenSource();
        this.取消=取消;
        this.実行中=true;
        this.一時停止中=false;
        this.一時停止ボタン.Text="一時停止";
        this.ボタン状態を更新する();
        this.実行タスク=Task.Run(async()=>{
            try{
                await this.サービス.実行Async(
                    設定,
                    this.結果を受け取るAsync,
                    段階=>MainThread.BeginInvokeOnMainThread(()=>this.段階を表示する(段階,true)),
                    取消.Token).ConfigureAwait(false);
            }catch(OperationCanceledException) when(取消.IsCancellationRequested){
            }catch(Exception 例外){
                MainThread.BeginInvokeOnMainThread(()=>{
                    this.状態表示.Text="検証エラー: "+例外.Message;
                    this.状態表示.TextColor=動的ライブ配色.失敗;
                });
            }finally{
                予約?.Dispose();
                MainThread.BeginInvokeOnMainThread(()=>{
                    if(ReferenceEquals(this.取消,取消)){
                        this.実行中=false;
                        this.ボタン状態を更新する();
                    }
                });
            }
        });
    }

    private void 実行を止めて待つ(){
        if(this.取消 is null){
            return;
        }
        this.取消.Cancel();
        this.再開待ち?.TrySetResult();
        try{
            this.実行タスク?.Wait(TimeSpan.FromSeconds(5));
        }catch(AggregateException){
        }
    }

    private void 停止する(){
        this.実行を止めて待つ();
        this.実行中=false;
        this.一時停止中=false;
        this.ボタン状態を更新する();
        this.状態表示.Text="停止しました";
    }

    private void 一時停止を切り替える(){
        if(this.一時停止中){
            this.一時停止中=false;
            this.一時停止ボタン.Text="一時停止";
            this.再開待ち?.TrySetResult();
        }else{
            this.一時停止中=true;
            this.再開待ち=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this.一時停止ボタン.Text="再開";
        }
    }

    private void シードを再現する(){
        _=int.TryParse((this.試行番号入力.Text??"0").Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var 番号);
        // 試行番号は、ランダム探索の何番目か(Worker の QueryIndex)。
        this.開始する(動的ライブ段階.ランダム探索,Math.Max(0,番号));
    }

    private void ボタン状態を更新する(){
        this.開始ボタン.IsEnabled=!this.実行中;
        this.停止ボタン.IsEnabled=this.実行中;
        this.一時停止ボタン.IsEnabled=this.実行中;
    }

    // ---- 結果の受け取りと表示 ----

    private async Task 結果を受け取るAsync(動的ライブ結果 結果,CancellationToken 取消){
        await MainThread.InvokeOnMainThreadAsync(()=>this.結果を表示する(結果,true));
        var 待ち=this.速度の待ち;
        if(待ち>0){
            await Task.Delay(待ち,取消).ConfigureAwait(false);
        }
        while(this.一時停止中&&this.再開待ち is { }ゲート&&!取消.IsCancellationRequested){
            await ゲート.Task.WaitAsync(取消).ConfigureAwait(false);
        }
    }

    private void 段階を表示する(動的ライブ段階 段階,bool 実行中か){
        var 番号=(int)段階;
        for(var i=0;i<this.段階行一覧.Count;i++){
            var (枠,印)=this.段階行一覧[i];
            if(i==番号&&実行中か){
                印.Text="▶";
                印.TextColor=動的ライブ配色.アクセント;
                枠.BackgroundColor=動的ライブ配色.実行中の段階;
            }else if(i<番号){
                印.Text="✔";
                印.TextColor=動的ライブ配色.成功;
                枠.BackgroundColor=動的ライブ配色.面;
            }else{
                印.Text="○";
                印.TextColor=動的ライブ配色.薄い文字;
                枠.BackgroundColor=動的ライブ配色.面;
            }
        }
    }

    private void 結果を表示する(動的ライブ結果 結果,bool ログへ追加){
        this.表示中の結果=結果;
        this.状態表示.TextColor=動的ライブ配色.薄い文字;
        this.状態表示.Text=$"[{結果.対象} / {結果.要素型}] #{結果.通番} Seed={結果.基準シード}";
        this.式表示.Text=結果.式;

        var 展開済=結果.状態!=WorkerExecutionStatus.ExpansionIncomplete
            &&結果.状態!=WorkerExecutionStatus.CompilationFailed
            &&結果.状態!=WorkerExecutionStatus.InvocationTargetMismatch;
        if(結果.合格か){
            this.判定バッジ.Text="✔ 展開・結果一致";
            this.判定枠.BackgroundColor=動的ライブ配色.成功;
        }else if(結果.状態==WorkerExecutionStatus.Succeeded){
            this.判定バッジ.Text="✖ 結果不一致";
            this.判定枠.BackgroundColor=動的ライブ配色.失敗;
        }else{
            this.判定バッジ.Text="✖ "+結果.状態;
            this.判定枠.BackgroundColor=動的ライブ配色.失敗;
        }
        this.判定枠.IsVisible=true;
        this.診断表示.Text=結果.診断;
        this.診断表示.TextColor=結果.合格か?動的ライブ配色.成功:動的ライブ配色.失敗;
        this.生成コード表示.Text=結果.生成コード;
        this.展開コード表示.Text=結果.展開コード.Length==0?"// 展開後のコードはありません(コンパイルまで進みませんでした)":結果.展開コード;
        this.再現テスト表示.Text=結果.再現テスト;

        if(ログへ追加){
            this.総数++;
            if(結果.合格か){
                this.合格数++;
            }
            if(展開済){
                this.展開済数++;
            }
            this.件数表示.Text=$"検証したクエリ {this.総数} 件(合格 {this.合格数} / 要調査 {this.総数-this.合格数})";
            this.メーターを更新する("全呼び出しの展開率",this.展開済数,this.総数);
            this.メーターを更新する("全経路の結果一致率",this.合格数,this.総数);
            this.ログへ追加する(結果);
        }
        this.グラフを更新する(結果);
    }

    private void グラフを更新する(動的ライブ結果 結果){
        if(結果.計測一覧.Count==0){
            return;
        }
        this.時間描画.更新する("実行時間(中央値)","ns",結果.計測一覧.Select(static 計測=>new 動的ライブ棒(計測.実装,計測.中央値ナノ秒,動的ライブ配色.実装の色(計測.実装))).ToArray());
        this.割当描画.更新する("メモリ割り当て","B/回",結果.計測一覧.Select(static 計測=>new 動的ライブ棒(計測.実装,計測.割当バイト,動的ライブ配色.実装の色(計測.実装))).ToArray());
        this.時間グラフ.Invalidate();
        this.割当グラフ.Invalidate();
    }

    private void メーターを更新する(string 名前,int 達成数,int 総数){
        var 割合=総数>0?Math.Clamp((double)達成数/総数,0.0,1.0):0.0;
        var 文字=$"{達成数} / {総数} ({割合*100.0:0.0}%)";
        if(this.メーター一覧.TryGetValue(名前,out var 既存)){
            既存.値.Text=文字;
            既存.棒.Progress=割合;
            return;
        }
        var 値ラベル=new Label{Text=文字,FontSize=12,TextColor=動的ライブ配色.文字,HorizontalTextAlignment=TextAlignment.End};
        var 棒=new ProgressBar{Progress=割合,ProgressColor=動的ライブ配色.アクセント,HeightRequest=9};
        var 見出し行=new Grid{ColumnDefinitions=[new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)]};
        見出し行.Add(new Label{Text=名前,FontSize=12,TextColor=動的ライブ配色.薄い文字},0,0);
        見出し行.Add(値ラベル,1,0);
        var 塊=new VerticalStackLayout{Spacing=2};
        塊.Add(見出し行);
        塊.Add(棒);
        this.メーター一覧.Add(名前,(値ラベル,棒));
        this.メーター領域.Add(塊);
    }

    private void ログへ追加する(動的ライブ結果 結果){
        var 印=結果.合格か?"✔":"✖";
        var 色=結果.合格か?動的ライブ配色.成功:動的ライブ配色.失敗;
        var ラベル=new Label{
            Text=$"{印} [{結果.段階}] #{結果.通番} {結果.式}",
            FontFamily="Consolas",
            FontSize=11,
            TextColor=色,
            Padding=new Thickness(4,2),
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        var 行枠=new Border{Content=ラベル,BackgroundColor=Colors.Transparent,StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=4}};
        var タップ=new TapGestureRecognizer();
        タップ.Tapped+=(_,_)=>{
            // 見返している間に画面が流れないよう、押したら一時停止する。
            if(this.実行中&&!this.一時停止中){
                this.一時停止を切り替える();
            }
            if(結果.段階==動的ライブ段階.ランダム探索){
                this.試行番号入力.Text=結果.通番.ToString(CultureInfo.InvariantCulture);
            }
            this.結果を表示する(結果,false);
        };
        行枠.GestureRecognizers.Add(タップ);
        this.ログ領域.Insert(0,行枠);
        while(this.ログ領域.Count>ログ最大件数){
            this.ログ領域.RemoveAt(this.ログ領域.Count-1);
        }
    }
}
